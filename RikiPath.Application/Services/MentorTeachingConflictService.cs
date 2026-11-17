using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

// Kiểm tra trùng lịch dạy chuẩn duy nhất của Mentor. Chỉ đọc dữ liệu; tuần tự hóa do bên gọi giữ advisory lock
// LockScheduleAsync(mentorId, ...) trong cùng giao dịch. Khung giờ rảnh chưa có người đặt, lớp nháp, mục đã xóa/từ chối/hủy
// KHÔNG phải cam kết dạy.
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

    private const int MaxReportedConflicts = 200;
    private static readonly Lazy<TimeZoneInfo> BusinessZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId));

    private sealed record Commitment(string Kind, int? AvailabilityId, int? CohortId, string? CohortName, DateTime StartUtc, DateTime EndUtc);

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
        var now = DateTime.UtcNow;
        var excluded = new HashSet<int>(o.ExcludeAvailabilityIds ?? []);
        if (o.ExcludeCohortId is int excludedCohort)
        {
            var own = await uow.StudyCohorts.Query().AsNoTracking().Where(g => g.Id == excludedCohort)
                .SelectMany(g => g.Sessions.Select(s => s.AvailabilityId)).ToListAsync(ct);
            excluded.UnionWith(own);
        }
        var excludedIds = excluded.ToList();
        var excludeCohortId = o.ExcludeCohortId ?? 0;

        // (a) Buổi của lớp nhóm đã công bố do Mentor này dạy.
        var cohortRows = await uow.StudyCohorts.Query().AsNoTracking()
            .Where(g => g.Status == "Published" && g.Id != excludeCohortId)
            .SelectMany(g => g.Sessions
                .Where(s => s.Availability.MentorId == mentorId && !s.Availability.IsDeleted && s.Availability.RejectedAt == null
                    && s.Availability.StartTime < to && s.Availability.EndTime > from && !excludedIds.Contains(s.AvailabilityId))
                .Select(s => new { s.AvailabilityId, CohortId = g.Id, g.Name, s.Availability.StartTime, s.Availability.EndTime }))
            .ToListAsync(ct);

        // (b) Lịch 1-1 cũ đang có người đặt (đã giao/chấp nhận, hoặc đang chờ thanh toán còn hạn).
        var legacyRows = await uow.MentorBookings.Query().AsNoTracking()
            .Where(b => !b.IsDeleted && b.MentorAvailability != null && b.MentorAvailability.MentorId == mentorId
                && !b.MentorAvailability.IsDeleted && b.MentorAvailability.RejectedAt == null
                && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted
                    || (b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentExpiresAt > now))
                && b.MentorAvailability.StartTime < to && b.MentorAvailability.EndTime > from
                && !excludedIds.Contains(b.MentorAvailabilityId!.Value))
            .Select(b => new { AvailabilityId = b.MentorAvailabilityId!.Value, b.MentorAvailability!.StartTime, b.MentorAvailability.EndTime })
            .ToListAsync(ct);

        // (c) Buổi đã lên lịch của khóa 1-1 đang học, đã thanh toán.
        var individualRows = await uow.StudyCourses.Query().AsNoTracking()
            .Where(c => c.MentorId == mentorId && c.CohortId == null && c.Status == "Active" && c.PaymentStatus == "Paid")
            .SelectMany(c => c.Sessions
                .Where(s => s.Status == "Scheduled" && s.ScheduledAt < to && s.ScheduledAt >= from.AddDays(-1)
                    && (s.AvailabilityId == null || !excludedIds.Contains(s.AvailabilityId.Value)))
                .Select(s => new { s.AvailabilityId, s.ScheduledAt, c.SessionMinutes }))
            .ToListAsync(ct);

        var seen = new HashSet<int>();
        var result = new List<Commitment>();
        foreach (var r in cohortRows)
            if (seen.Add(r.AvailabilityId))
                result.Add(new Commitment(KindCohort, r.AvailabilityId, r.CohortId, r.Name, Utc(r.StartTime), Utc(r.EndTime)));
        foreach (var r in legacyRows)
            if (seen.Add(r.AvailabilityId))
                result.Add(new Commitment(KindLegacy, r.AvailabilityId, null, null, Utc(r.StartTime), Utc(r.EndTime)));
        var individualKeys = new HashSet<DateTime>();
        foreach (var r in individualRows)
        {
            if (r.AvailabilityId is int aid && !seen.Add(aid)) continue;
            var start = Utc(r.ScheduledAt);
            if (r.AvailabilityId is null && !individualKeys.Add(start)) continue;
            var end = start.AddMinutes(r.SessionMinutes);
            if (end <= from || start >= to) continue;
            result.Add(new Commitment(KindIndividual, r.AvailabilityId, null, null, start, end));
        }
        return result.OrderBy(x => x.StartUtc).ToList();
    }

    private static TeachingConflict Conflict(string code, ProposedTeachingInterval p, Commitment a, string message)
        => new(code, p.SessionNumber, p.StartUtc, p.EndUtc, a.Kind, a.CohortId, a.CohortName, a.StartUtc, a.EndUtc, message);

    private static string Subject(Commitment a) => a.Kind == KindCohort
        ? $"lịch dạy lớp {a.CohortName}"
        : "lịch dạy đã xác nhận";

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
