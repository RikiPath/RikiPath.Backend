using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

// Kiểm tra trùng lịch dạy chuẩn duy nhất của Mentor. Chỉ đọc dữ liệu; tuần tự hóa do bên gọi giữ advisory lock
// LockScheduleAsync(mentorId, ...) trong cùng giao dịch. Khung giờ rảnh đơn lẻ chưa gắn đề xuất/lớp, lớp đã hủy,
// và đề xuất Draft/Rejected/Withdrawn KHÔNG phải cam kết dạy.
public sealed class MentorTeachingConflictService(IUnitOfWork uow, IMentorGroupPolicyService? groupPolicies = null)
    : IMentorTeachingConflictService
{
    public const string ScheduleConflict = "MENTOR_SCHEDULE_CONFLICT";
    public const string RestViolation = "MENTOR_REST_INTERVAL_VIOLATION";
    public const string Duplicate = "COHORT_SESSION_DUPLICATE";
    public const string DailyLimit = "MENTOR_DAILY_LIMIT_EXCEEDED";
    public const string WeeklyLimit = "MENTOR_WEEKLY_LIMIT_EXCEEDED";

    public const string KindCohort = "CohortSession";
    public const string KindLegacy = "LegacyBooking";
    public const string KindIndividual = "IndividualCourseSession";
    public const string KindProposed = "ProposedSession";
    public const string KindProposal = "TeachingProposal";

    private const int MaxReportedConflicts = 200;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<TimeZoneInfo> BusinessZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));

    private sealed record Commitment(
        string Kind,
        int? AvailabilityId,
        int? CohortId,
        string? CohortName,
        DateTime StartUtc,
        DateTime EndUtc,
        string? JlptLevelCode = null,
        string? Status = null,
        int? ProposalId = null);

    public async Task<TeachingConflictReport> CheckAsync(int mentorId, IReadOnlyList<ProposedTeachingInterval> proposed,
        TeachingConflictOptions? options, CancellationToken ct)
    {
        options ??= new TeachingConflictOptions();
        if (proposed is null || proposed.Count == 0) return TeachingConflictReport.Valid;
        var items = proposed.Select(p => p with { StartUtc = Utc(p.StartUtc), EndUtc = Utc(p.EndUtc) })
            .OrderBy(p => p.StartUtc).ThenBy(p => p.EndUtc).ToList();

        var minStart = items[0].StartUtc;
        var maxEnd = items.Max(p => p.EndUtc);
        var (rest, dailyCap, weeklyCap) = await LimitsAsync(minStart, options, ct);

        // Cửa sổ tải: [min-1 ngày, max+1 ngày], mở rộng thêm cho đủ tuần ISO (giờ Việt Nam) khi có giới hạn tuần.
        var windowStart = minStart.AddDays(-1);
        var windowEnd = maxEnd.AddDays(1);
        if (weeklyCap > 0)
        {
            windowStart = Min(windowStart, WeekStartUtc(minStart));
            windowEnd = Max(windowEnd, WeekStartUtc(maxEnd).AddDays(7));
        }
        var commitments = await LoadCommitmentsAsync(mentorId, windowStart, windowEnd, options, ct);

        var conflicts = new List<TeachingConflict>();
        void Add(TeachingConflict c) { if (conflicts.Count < MaxReportedConflicts) conflicts.Add(c); }

        foreach (var p in items)
        {
            foreach (var a in commitments)
            {
                if (a.StartUtc < p.EndUtc && p.StartUtc < a.EndUtc)
                    Add(Conflict(ScheduleConflict, p, a, OverlapMessage(a)));
                else if (Gap(a.StartUtc, a.EndUtc, p.StartUtc, p.EndUtc) < TimeSpan.FromMinutes(rest))
                    Add(Conflict(RestViolation, p, a, RestMessage(p, a, rest)));
            }
        }

        if (options.IncludeOwnIntervals)
        {
            for (var i = 0; i < items.Count; i++)
            for (var j = i + 1; j < items.Count; j++)
            {
                var a = items[i]; var b = items[j];
                // Đã sắp theo giờ bắt đầu: khi b bắt đầu sau a.End + nghỉ thì các buổi sau cũng vậy.
                if (b.StartUtc >= a.EndUtc.AddMinutes(rest) && b.StartUtc > a.StartUtc) break;
                var own = new Commitment(KindProposed, null, null, null, a.StartUtc, a.EndUtc);
                if (a.StartUtc == b.StartUtc)
                    Add(Conflict(Duplicate, b, own, $"Có hai buổi cùng bắt đầu lúc {Describe(b.StartUtc)}. Vui lòng bỏ buổi trùng."));
                else if (a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc)
                    Add(Conflict(ScheduleConflict, b, own,
                        $"Các buổi được chọn bị trùng giờ: {Range(a.StartUtc, a.EndUtc)} và {Range(b.StartUtc, b.EndUtc)}. Vui lòng chọn khung giờ khác."));
                else if (Gap(a.StartUtc, a.EndUtc, b.StartUtc, b.EndUtc) < TimeSpan.FromMinutes(rest))
                    Add(Conflict(RestViolation, b, own,
                        $"Các buổi {Range(a.StartUtc, a.EndUtc)} và {Range(b.StartUtc, b.EndUtc)} quá sát nhau; cần nghỉ tối thiểu {rest} phút giữa hai buổi."));
            }
        }

        // Giới hạn ngày/tuần theo ngày làm việc giờ Việt Nam; cam kết chỉ được đếm một lần.
        if (dailyCap > 0)
        {
            foreach (var day in items.GroupBy(p => LocalDate(p.StartUtc)))
            {
                var total = day.Count() + commitments.Count(a => LocalDate(a.StartUtc) == day.Key);
                if (total <= dailyCap) continue;
                var p = day.First();
                Add(new TeachingConflict(DailyLimit, p.SessionNumber, p.StartUtc, p.EndUtc, KindProposed, null, null, null, null,
                    $"Lịch dạy vào {DayLabel(day.Key)} sẽ có {total} buổi, vượt giới hạn {dailyCap} buổi mỗi ngày theo chính sách. Vui lòng chọn ngày khác."));
            }
        }
        if (weeklyCap > 0)
        {
            foreach (var week in items.GroupBy(p => LocalWeekStart(LocalDate(p.StartUtc))))
            {
                var weekEnd = week.Key.AddDays(7);
                var total = week.Count() + commitments.Count(a => LocalDate(a.StartUtc) is var d && d >= week.Key && d < weekEnd);
                if (total <= weeklyCap) continue;
                var p = week.First();
                Add(new TeachingConflict(WeeklyLimit, p.SessionNumber, p.StartUtc, p.EndUtc, KindProposed, null, null, null, null,
                    $"Tuần bắt đầu {DayLabel(week.Key)} sẽ có {total} buổi dạy, vượt giới hạn {weeklyCap} buổi mỗi tuần theo chính sách. Vui lòng chọn tuần khác."));
            }
        }

        var ordered = conflicts.OrderBy(c => c.ProposedStartUtc).ThenBy(c => Priority(c.Code)).ToList();
        return new TeachingConflictReport(ordered.Count == 0, ordered);
    }

    private async Task<(int RestMinutes, int DailyCap, int WeeklyCap)> LimitsAsync(DateTime atUtc, TeachingConflictOptions o, CancellationToken ct)
    {
        RikiPath.Domain.Entities.MentorGroupPolicy? policy = null;
        if ((o.RestMinutes is null || o.DailyCap is null || o.WeeklyCap is null) && groupPolicies is not null)
            policy = await groupPolicies.GetEffectiveAsync(atUtc, ct);
        var rest = o.RestMinutes ?? policy?.RestMinutes ?? MentorSlotPolicy.MinimumRestMinutes;
        var daily = o.DailyCap ?? policy?.DailySessionCap ?? MentorSlotPolicy.DefaultDailyBookingCap;
        var weekly = o.WeeklyCap ?? policy?.WeeklySessionCap ?? 0;
        return (Math.Max(0, rest), daily, weekly);
    }

    private async Task<List<Commitment>> LoadCommitmentsAsync(int mentorId, DateTime from, DateTime to,
        TeachingConflictOptions o, CancellationToken ct)
    {
        var excluded = new HashSet<int>(o.ExcludeAvailabilityIds ?? []);
        if (o.ExcludeCohortId is int excludedCohort)
        {
            var own = await uow.StudyCohorts.Query().AsNoTracking().Where(g => g.Id == excludedCohort)
                .SelectMany(g => g.Sessions.Select(s => s.AvailabilityId)).ToListAsync(ct);
            excluded.UnionWith(own);
        }
        var excludedIds = excluded.ToList();
        var excludeCohortId = o.ExcludeCohortId ?? 0;
        var excludeProposalId = o.ExcludeProposalId ?? 0;

        // (a) Buổi của lớp nhóm đang mở hoặc nháp do Giáo viên này dạy (bỏ qua lớp đã hủy).
        var cohortRows = await uow.StudyCohorts.Query().AsNoTracking()
            .Where(g => (g.Status == "Published" || g.Status == "Draft") && g.Id != excludeCohortId)
            .SelectMany(g => g.Sessions
                .Where(s => s.Availability.MentorId == mentorId && !s.Availability.IsDeleted && s.Availability.RejectedAt == null
                    && s.Availability.StartTime < to && s.Availability.EndTime > from && !excludedIds.Contains(s.AvailabilityId))
                .Select(s => new
                {
                    s.AvailabilityId,
                    CohortId = g.Id,
                    g.Name,
                    g.Status,
                    JlptLevel = g.Plan.CertificateLevel != null ? g.Plan.CertificateLevel.Code : null,
                    ProposalId = g.SourceProposalId,
                    s.Availability.StartTime,
                    s.Availability.EndTime,
                }))
            .ToListAsync(ct);

        var seen = new HashSet<int>();
        var result = new List<Commitment>();
        foreach (var r in cohortRows)
        {
            if (seen.Add(r.AvailabilityId))
            {
                result.Add(new Commitment(
                    KindCohort,
                    r.AvailabilityId,
                    r.CohortId,
                    r.Name,
                    Utc(r.StartTime),
                    Utc(r.EndTime),
                    r.JlptLevel,
                    r.Status,
                    r.ProposalId));
            }
        }

        // Các Availability thuộc lớp đã hủy không còn giữ lịch dạy của Mentor.
        var cancelledCohortAvIds = await uow.StudyCohorts.Query().AsNoTracking()
            .Where(g => g.MentorId == mentorId && g.Status == "Cancelled")
            .SelectMany(g => g.Sessions.Select(s => s.AvailabilityId))
            .ToListAsync(ct);
        var cancelledAvSet = cancelledCohortAvIds.ToHashSet();

        // (b) Các buổi đã duyệt từ đề xuất mở lớp nhưng chưa gắn vào StudyCohort.
        var approvedProposalSlots = await uow.MentorAvailabilities.Query().AsNoTracking()
            .Include(a => a.TeachingProposal).ThenInclude(p => p!.CertificateLevel)
            .Where(a => a.MentorId == mentorId
                && !a.IsDeleted
                && a.RejectedAt == null
                && a.IsApproved
                && a.TeachingProposalId != null
                && a.TeachingProposalId != excludeProposalId
                && a.EndTime > from
                && a.StartTime < to
                && !excludedIds.Contains(a.Id)
                && a.TeachingProposal!.Status == MentorTeachingProposalStatus.Approved)
            .Select(a => new
            {
                a.Id,
                a.TeachingProposalId,
                a.TeachingProposal!.CourseTitle,
                JlptLevel = a.TeachingProposal.CertificateLevel != null ? a.TeachingProposal.CertificateLevel.Code : null,
                a.StartTime,
                a.EndTime,
            })
            .ToListAsync(ct);

        foreach (var av in approvedProposalSlots)
        {
            if (cancelledAvSet.Contains(av.Id) || !seen.Add(av.Id)) continue;
            var label = string.IsNullOrWhiteSpace(av.CourseTitle) ? $"Đề xuất đã duyệt #{av.TeachingProposalId}" : av.CourseTitle;
            result.Add(new Commitment(
                KindProposal,
                av.Id,
                null,
                label,
                Utc(av.StartTime),
                Utc(av.EndTime),
                av.JlptLevel,
                MentorTeachingProposalStatus.Approved,
                av.TeachingProposalId));
        }

        // (c) Đề xuất đang chờ duyệt hoặc đang yêu cầu chỉnh sửa (giữ chỗ tạm thời cho lịch 10 tuần).
        var heldProposals = await uow.MentorTeachingProposals.Query().AsNoTracking()
            .Include(p => p.CertificateLevel)
            .Where(p => p.MentorId == mentorId
                && p.Id != excludeProposalId
                && (p.Status == MentorTeachingProposalStatus.PendingReview
                    || p.Status == MentorTeachingProposalStatus.ChangesRequested)
                && p.ExpectedStartDate.HasValue)
            .ToListAsync(ct);

        foreach (var p in heldProposals)
        {
            var sessions = DeserializeWeeklySessions(p.WeeklySessionsJson);
            if (sessions.Count == 0 || !p.ExpectedStartDate.HasValue) continue;
            var durationWeeks = p.CourseDurationWeeks > 0 ? p.CourseDurationWeeks : 10;
            var sessionMinutes = p.SessionDurationMinutes > 0 ? p.SessionDurationMinutes : 90;
            var cutoffUtc = p.SubmittedAtUtc.HasValue ? Utc(p.SubmittedAtUtc.Value) : DateTime.MinValue;
            var title = string.IsNullOrWhiteSpace(p.CourseTitle) ? $"Đề xuất #{p.Id}" : p.CourseTitle;
            var levelCode = p.CertificateLevel?.Code;

            foreach (var (startUtc, endUtc) in ExpandProposalIntervals(p.ExpectedStartDate.Value, sessions, durationWeeks, sessionMinutes, cutoffUtc))
            {
                if (endUtc <= from || startUtc >= to) continue;
                result.Add(new Commitment(
                    KindProposal,
                    null,
                    null,
                    title,
                    startUtc,
                    endUtc,
                    levelCode,
                    p.Status,
                    p.Id));
            }
        }

        return result.OrderBy(x => x.StartUtc).ToList();
    }

    private static List<WeeklySessionSlotDto> DeserializeWeeklySessions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<WeeklySessionSlotDto>>(json, JsonOpts) ?? [])
                .Where(s => s is not null && s.StartMinute >= 0)
                .OrderBy(s => (s.Weekday + 6) % 7)
                .ThenBy(s => s.StartMinute)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<(DateTime StartUtc, DateTime EndUtc)> ExpandProposalIntervals(
        DateOnly expectedStartDate,
        List<WeeklySessionSlotDto> sessions,
        int durationWeeks,
        int sessionMinutes,
        DateTime cutoffUtc)
    {
        _ = cutoffUtc;
        var tuples = sessions.Select(s => (s.Weekday, s.StartMinute)).ToList();
        foreach (var occ in MentorSlotPolicy.ExpandRecurringSchedule(expectedStartDate, tuples, durationWeeks, sessionMinutes, BusinessZone.Value))
        {
            yield return (occ.StartUtc, occ.EndUtc);
        }
    }

    private static TeachingConflict Conflict(string code, ProposedTeachingInterval p, Commitment a, string message)
        => new(code, p.SessionNumber, p.StartUtc, p.EndUtc, a.Kind, a.CohortId, a.CohortName, a.StartUtc, a.EndUtc, message, a.JlptLevelCode, a.Status, a.ProposalId);

    private static string Subject(Commitment a)
    {
        var level = string.IsNullOrWhiteSpace(a.JlptLevelCode) ? "" : $" ({a.JlptLevelCode})";
        if (a.Kind == KindCohort)
            return $"lịch dạy lớp {a.CohortName}{level}";
        if (a.Status == MentorTeachingProposalStatus.PendingReview)
            return $"đề xuất đang chờ duyệt{level}";
        if (a.Status == MentorTeachingProposalStatus.ChangesRequested)
            return $"đề xuất cần bổ sung{level}";
        if (a.Status == MentorTeachingProposalStatus.Approved)
            return $"lịch dạy đã duyệt{level}";
        return $"lịch dạy đã xác nhận{level}";
    }

    private static string OverlapMessage(Commitment a)
        => $"Bạn đã có {Subject(a)} từ {Time(a.StartUtc)} đến {Time(a.EndUtc)} vào {DayLabel(LocalDate(a.StartUtc))}. Vui lòng chọn khung giờ khác.";

    private static string RestMessage(ProposedTeachingInterval p, Commitment a, int rest)
        => $"Khung giờ {Range(p.StartUtc, p.EndUtc)} quá sát {Subject(a)} từ {Time(a.StartUtc)} đến {Time(a.EndUtc)} vào {DayLabel(LocalDate(a.StartUtc))}; cần nghỉ tối thiểu {rest} phút giữa hai buổi. Vui lòng chọn khung giờ khác.";

    private static int Priority(string code) => code switch
    {
        ScheduleConflict => 0, Duplicate => 1, RestViolation => 2, DailyLimit => 3, _ => 4
    };

    private static TimeSpan Gap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        => bStart >= aEnd ? bStart - aEnd : aStart - bEnd;

    internal static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), BusinessZone.Value);
    private static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(Local(utc));
    private static DateOnly LocalWeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    private static DateTime WeekStartUtc(DateTime utc)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(LocalWeekStart(LocalDate(utc)).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified), BusinessZone.Value);
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static string Time(DateTime utc) => Local(utc).ToString("HH:mm", CultureInfo.InvariantCulture);
    private static string Range(DateTime startUtc, DateTime endUtc)
        => $"{Time(startUtc)}–{Time(endUtc)} {DayLabel(LocalDate(startUtc))}";
    private static string Describe(DateTime utc) => $"{Time(utc)} {DayLabel(LocalDate(utc))}";

    internal static string Weekday(DayOfWeek day) => day == DayOfWeek.Sunday ? "Chủ nhật" : $"thứ {(int)day + 1}";
    internal static string DayLabel(DateOnly d) => $"{Weekday(d.DayOfWeek)} ({d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})";
}
