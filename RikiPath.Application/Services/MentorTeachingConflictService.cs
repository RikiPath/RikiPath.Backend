using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

// Kiểm tra trùng lịch dạy chuẩn duy nhất của cùng một Giáo viên.
// Chỉ đối chiếu với lịch giảng dạy CHÍNH THỨC (StudyCohort đang hoạt động/nháp và khung giờ đã duyệt chưa gắn lớp hủy);
// không bao giờ khóa vô thời hạn khung giờ của đề xuất chưa duyệt và không giới hạn chéo giữa các Giáo viên khác nhau.
public sealed class MentorTeachingConflictService(IUnitOfWork uow, IMentorGroupPolicyService? groupPolicies = null)
    : IMentorTeachingConflictService
{
    public const string ScheduleConflict = "MENTOR_SCHEDULE_CONFLICT";
    public const string RestViolation = "MENTOR_REST_INTERVAL_VIOLATION";
    public const string Duplicate = "COHORT_SESSION_DUPLICATE";

    public const string KindCohort = "CohortSession";
    public const string KindProposed = "ProposedSession";
    public const string KindProposal = "TeachingProposal";

    private const int MaxReportedConflicts = 200;
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
        var rest = await RestMinutesAsync(minStart, options, ct);

        var windowStart = minStart.AddDays(-1);
        var windowEnd = maxEnd.AddDays(1);
        var commitments = await LoadCommitmentsAsync(mentorId, windowStart, windowEnd, options, ct);

        if (options.AdditionalCommittedIntervals is { Count: > 0 })
        {
            foreach (var extra in options.AdditionalCommittedIntervals)
            {
                var extraStart = Utc(extra.StartUtc);
                var extraEnd = Utc(extra.EndUtc);
                if (extraEnd <= windowStart || extraStart >= windowEnd) continue;
                commitments.Add(new Commitment(
                    KindProposal,
                    null,
                    null,
                    "Lịch dự kiến cùng đợt duyệt",
                    extraStart,
                    extraEnd,
                    null,
                    MentorTeachingProposalStatus.PendingReview,
                    null));
            }
            commitments = commitments.OrderBy(x => x.StartUtc).ToList();
        }

        var conflicts = new List<TeachingConflict>();
        void Add(TeachingConflict c) { if (conflicts.Count < MaxReportedConflicts) conflicts.Add(c); }

        foreach (var p in items)
        {
            foreach (var a in commitments)
            {
                if (a.StartUtc < p.EndUtc && p.StartUtc < a.EndUtc)
                    Add(Conflict(ScheduleConflict, p, a, OverlapMessage(a)));
                else if (rest > 0 && Gap(a.StartUtc, a.EndUtc, p.StartUtc, p.EndUtc) < TimeSpan.FromMinutes(rest))
                    Add(Conflict(RestViolation, p, a, RestMessage(p, a, rest)));
            }
        }

        if (options.IncludeOwnIntervals)
        {
            for (var i = 0; i < items.Count; i++)
            for (var j = i + 1; j < items.Count; j++)
            {
                var a = items[i]; var b = items[j];
                if (b.StartUtc >= a.EndUtc.AddMinutes(rest) && b.StartUtc > a.StartUtc) break;
                var own = new Commitment(KindProposed, null, null, null, a.StartUtc, a.EndUtc);
                if (a.StartUtc == b.StartUtc)
                    Add(Conflict(Duplicate, b, own, $"Có hai buổi cùng bắt đầu lúc {Describe(b.StartUtc)}. Vui lòng bỏ buổi trùng."));
                else if (a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc)
                    Add(Conflict(ScheduleConflict, b, own,
                        $"Các buổi được chọn bị trùng giờ: {Range(a.StartUtc, a.EndUtc)} và {Range(b.StartUtc, b.EndUtc)}. Vui lòng chọn khung giờ khác."));
                else if (rest > 0 && Gap(a.StartUtc, a.EndUtc, b.StartUtc, b.EndUtc) < TimeSpan.FromMinutes(rest))
                    Add(Conflict(RestViolation, b, own,
                        $"Các buổi {Range(a.StartUtc, a.EndUtc)} và {Range(b.StartUtc, b.EndUtc)} quá sát nhau; cần nghỉ tối thiểu {rest} phút giữa hai buổi."));
            }
        }

        var ordered = conflicts.OrderBy(c => c.ProposedStartUtc).ThenBy(c => Priority(c.Code)).ToList();
        return new TeachingConflictReport(ordered.Count == 0, ordered);
    }

    private async Task<int> RestMinutesAsync(DateTime atUtc, TeachingConflictOptions o, CancellationToken ct)
    {
        if (o.RestMinutes.HasValue)
            return Math.Max(0, o.RestMinutes.Value);
        var policy = groupPolicies is not null ? await groupPolicies.GetEffectiveAsync(atUtc, ct) : null;
        return Math.Max(0, policy?.RestMinutes ?? MentorSlotPolicy.MinimumRestMinutes);
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

        // (a) Buổi học chính thức của lớp đang mở hoặc nháp do chính Giáo viên này dạy (loại trừ lớp đã hủy).
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

        var cohortAvIds = cohortRows.Select(r => r.AvailabilityId).Distinct().ToList();
        var fullyCancelledCohortAvIds = cohortAvIds.Count == 0
            ? new HashSet<int>()
            : (await uow.StudyCourses.Query().AsNoTracking()
                .Where(c => c.CohortId.HasValue
                    && c.Status != "Cancelled"
                    && c.Status != "Transferred"
                    && c.Status != "PaymentReview"
                    && (c.PaymentStatus == "Paid"
                        || c.PaymentStatus == "PartiallyRefunded"
                        || (c.PaymentStatus == "Pending" && c.PaymentExpiresAt > DateTime.UtcNow)))
                .SelectMany(c => c.Sessions
                    .Where(s => s.AvailabilityId.HasValue && cohortAvIds.Contains(s.AvailabilityId.Value))
                    .Select(s => new { AvailabilityId = s.AvailabilityId!.Value, s.Status }))
                .GroupBy(x => x.AvailabilityId)
                .Select(g => new
                {
                    AvailabilityId = g.Key,
                    HasActiveSession = g.Any(s =>
                        s.Status != "CancelledByMentor"
                        && s.Status != "Cancelled"
                        && s.Status != "CancelledByLearner"
                        && s.Status != "TransferredOut"),
                })
                .Where(x => !x.HasActiveSession)
                .Select(x => x.AvailabilityId)
                .ToListAsync(ct))
                .ToHashSet();

        var seen = new HashSet<int>();
        var result = new List<Commitment>();
        foreach (var r in cohortRows)
        {
            if (fullyCancelledCohortAvIds.Contains(r.AvailabilityId)) continue;
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

        // Khung giờ của lớp đã hủy không còn giữ lịch dạy của Giáo viên.
        var cancelledCohortAvIds = await uow.StudyCohorts.Query().AsNoTracking()
            .Where(g => g.MentorId == mentorId && g.Status == "Cancelled")
            .SelectMany(g => g.Sessions.Select(s => s.AvailabilityId))
            .ToListAsync(ct);
        var cancelledAvSet = cancelledCohortAvIds.ToHashSet();

        // (b) Các buổi đã phê duyệt chính thức (từ đề xuất mở lớp khác hoặc slot độc lập đã duyệt).
        var approvedProposalSlots = await uow.MentorAvailabilities.Query().AsNoTracking()
            .Include(a => a.TeachingProposal).ThenInclude(p => p!.CertificateLevel)
            .Where(a => a.MentorId == mentorId
                && !a.IsDeleted
                && a.RejectedAt == null
                && a.IsApproved
                && a.EndTime > from
                && a.StartTime < to
                && !excludedIds.Contains(a.Id)
                && (a.TeachingProposalId == null
                    || (a.TeachingProposalId != excludeProposalId
                        && a.TeachingProposal != null
                        && (a.TeachingProposal.Status == MentorTeachingProposalStatus.OpenedClass
                            || a.TeachingProposal.Status == MentorTeachingProposalStatus.Approved))))
            .Select(a => new
            {
                a.Id,
                a.TeachingProposalId,
                CourseTitle = a.TeachingProposal != null ? a.TeachingProposal.CourseTitle : null,
                JlptLevel = a.TeachingProposal != null && a.TeachingProposal.CertificateLevel != null
                    ? a.TeachingProposal.CertificateLevel.Code
                    : null,
                a.StartTime,
                a.EndTime,
            })
            .ToListAsync(ct);

        foreach (var av in approvedProposalSlots)
        {
            if (cancelledAvSet.Contains(av.Id) || !seen.Add(av.Id)) continue;
            var label = string.IsNullOrWhiteSpace(av.CourseTitle) ? "Lớp học đã duyệt" : av.CourseTitle;
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

        return result.OrderBy(x => x.StartUtc).ToList();
    }

    private static TeachingConflict Conflict(string code, ProposedTeachingInterval p, Commitment a, string message)
        => new(code, p.SessionNumber, p.StartUtc, p.EndUtc, a.Kind, a.CohortId, a.CohortName, a.StartUtc, a.EndUtc, message, a.JlptLevelCode, a.Status, a.ProposalId);

    private static string Subject(Commitment a)
    {
        var level = string.IsNullOrWhiteSpace(a.JlptLevelCode) ? "" : $" ({a.JlptLevelCode})";
        if (a.Kind == KindCohort)
            return $"lịch dạy lớp {a.CohortName}{level}";
        if (a.Status == MentorTeachingProposalStatus.Approved)
            return $"lịch dạy đã duyệt{level}";
        return $"lịch dạy đã xác nhận{level}";
    }

    private static string OverlapMessage(Commitment a)
        => $"Khung giờ này trùng với {Subject(a)} từ {Time(a.StartUtc)} đến {Time(a.EndUtc)} vào {DayLabel(LocalDate(a.StartUtc))}.";

    private static string RestMessage(ProposedTeachingInterval p, Commitment a, int rest)
        => $"Khung giờ {Range(p.StartUtc, p.EndUtc)} quá sát {Subject(a)} từ {Time(a.StartUtc)} đến {Time(a.EndUtc)} vào {DayLabel(LocalDate(a.StartUtc))}; cần nghỉ tối thiểu {rest} phút giữa hai buổi.";

    private static int Priority(string code) => code switch
    {
        ScheduleConflict => 0, Duplicate => 1, RestViolation => 2, _ => 3
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

    private static string Time(DateTime utc) => Local(utc).ToString("HH:mm", CultureInfo.InvariantCulture);
    private static string Range(DateTime startUtc, DateTime endUtc)
        => $"{Time(startUtc)}–{Time(endUtc)} {DayLabel(LocalDate(startUtc))}";
    private static string Describe(DateTime utc) => $"{Time(utc)} {DayLabel(LocalDate(utc))}";

    internal static string Weekday(DayOfWeek day) => day == DayOfWeek.Sunday ? "Chủ nhật" : $"thứ {(int)day + 1}";
    internal static string DayLabel(DateOnly d) => $"{Weekday(d.DayOfWeek)} ({d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})";
}
