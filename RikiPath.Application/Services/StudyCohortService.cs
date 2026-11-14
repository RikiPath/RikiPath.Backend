using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService
{
    private static bool ReservesSeat(StudyCourse c, DateTime now) => (c.PaymentStatus == "Paid" && c.Status != "PaymentReview")
        || (c.PaymentStatus == "Pending" && c.PaymentExpiresAt > now);

    private async Task<bool> HasLearnerConflictAsync(int learnerId, IReadOnlyList<MentorAvailability> proposed, int excludeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var courses = await uow.StudyCourses.Query().Where(e => e.Id != excludeId && e.LearnerId == learnerId
            && (e.PaymentStatus == "Paid" || (e.PaymentStatus == "Pending" && e.PaymentExpiresAt > now)))
            .Include(e => e.Sessions).Include(e => e.Cohort).ThenInclude(g => g!.Sessions).ThenInclude(s => s.Availability)
            .AsSplitQuery().ToListAsync(ct);
        foreach (var e in courses)
        {
            if (e.Sessions.Any(s => s.Status == "Scheduled" && proposed.Any(p =>
                s.ScheduledAt < p.EndTime && s.ScheduledAt.AddMinutes(e.SessionMinutes) > p.StartTime))) return true;
            if (e.PaymentStatus == "Pending" && e.Cohort is not null && e.Cohort.Sessions.Any(s => proposed.Any(p =>
                s.Availability.StartTime < p.EndTime && s.Availability.EndTime > p.StartTime))) return true;
        }
        var legacy = await uow.MentorBookings.Query().Where(b => !b.IsDeleted && b.UserSubscription.UserId == learnerId && b.MentorAvailability != null
            && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted
                || (b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentExpiresAt > now)))
            .Include(b => b.MentorAvailability).ToListAsync(ct);
        return legacy.Any(b => proposed.Any(p => b.MentorAvailability!.StartTime < p.EndTime && b.MentorAvailability.EndTime > p.StartTime));
    }

    public Task<ApiResponse<object>> CohortsAsync(CancellationToken ct) => Run(async () =>
    {
        var now = DateTime.UtcNow;
        var rows = await uow.StudyCohorts.Query().Include(x => x.Plan).ThenInclude(x => x.CertificateLevel)
            .Include(x => x.Mentor).Include(x => x.Sessions).ThenInclude(x => x.Availability)
            .Include(x => x.Enrollments).AsSplitQuery()
            .Where(x => Actor.Role == Role.Admin || (Actor.Role == Role.Mentor && x.MentorId == Actor.Id)
                || (x.Status == "Published" && x.StartsAt > now && x.Plan.IsPublished))
            .OrderBy(x => x.StartsAt).ToListAsync(ct);
        return rows.Select(x => new { x.Id, x.PlanId, x.Name, x.MentorId,
            MentorName = $"{x.Mentor.FirstName} {x.Mentor.LastName}".Trim(),
            x.StartsAt, x.EndsAt, x.Capacity, x.Status, x.Revision, Level = x.Plan.CertificateLevel.Code,
            x.Plan.Price, SeatsAvailable = Math.Max(0, x.Capacity - x.Enrollments.Count(c => ReservesSeat(c, now))),
            Sessions = x.Sessions.OrderBy(s => s.Number).Select(s => new {
                s.Number, s.AvailabilityId, s.Availability.StartTime, s.Availability.EndTime }) }).ToList();
    });

    public Task<ApiResponse<object>> SaveCohortAsync(int? id, SaveStudyCohort r, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Require(!string.IsNullOrWhiteSpace(r.Name) && r.Name.Length <= 200 && r.Capacity is >= 2 and <= 100,
            "Nhập tên lớp và sĩ số từ 2 đến 100.");
        Require(r.AvailabilityIds is { Count: > 0 and <= 500 } && r.AvailabilityIds.Distinct().Count() == r.AvailabilityIds.Count,
            "Chọn lịch cố định, không trùng buổi.");
        int savedId = 0;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            // Serialize every timetable mutation against 1–1 bookings as well.
            await uow.StudyCourses.LockScheduleAsync(r.MentorId, r.MentorId, token);
            if (id.HasValue) await uow.StudyCourses.LockCohortAsync(id.Value, token);
            var p = await uow.StudyCoursePlans.GetByIdAsync(r.PlanId, token);
            Require(p is { SessionType: "Group", SessionMinutes: not null, TotalSessions: not null }, "Chọn gói học nhóm đã cấu hình đủ buổi.");
            Require(await uow.UserAccounts.AnyAsync(x => x.Id == r.MentorId && x.Role == Role.Mentor && !x.IsDeleted, token), "Mentor không hợp lệ.");
            var c = id.HasValue ? await uow.StudyCohorts.Query().Include(x => x.Sessions).Include(x => x.Enrollments)
                .SingleOrDefaultAsync(x => x.Id == id.Value, token) : new StudyCohort();
            Require(c is not null && (!id.HasValue || c.Revision == r.Revision), "Lớp vừa thay đổi hoặc không tồn tại.");
            Require(!id.HasValue || c!.MentorId == r.MentorId, "Không đổi Mentor trực tiếp; gửi yêu cầu điều chỉnh lịch.");
            Require(!id.HasValue || !c!.Enrollments.Any(x => ReservesSeat(x, DateTime.UtcNow)),
                "Lớp đang có học viên hoặc giữ chỗ. Xử lý qua yêu cầu điều chỉnh, không sửa trực tiếp.");
            var slots = await uow.MentorAvailabilities.Query().Where(x => r.AvailabilityIds.Contains(x.Id)
                && x.MentorId == r.MentorId && !x.IsDeleted && x.IsApproved && x.RejectedAt == null
                && x.StartTime > DateTime.UtcNow && x.ChangeRequestedAt == null).OrderBy(x => x.StartTime).ToListAsync(token);
            Require(slots.Count == r.AvailabilityIds.Count && slots.Count == p!.TotalSessions,
                "Chọn đủ tổng buổi của gói từ lịch rảnh đã duyệt.");
            Require(slots.All(x => x.PolicyVersion == null && (x.EndTime - x.StartTime).TotalMinutes == p!.SessionMinutes && x.BookedCount == 0),
                "Lịch phải đúng thời lượng gói và chưa có người đặt.");
            Require(!await uow.StudyCohorts.Query().AnyAsync(x => x.Id != (id ?? 0)
                && x.Sessions.Any(s => r.AvailabilityIds.Contains(s.AvailabilityId)), token), "Lịch đã thuộc lớp khác.");
            Require(!await uow.StudyCourses.Query().AnyAsync(x => x.PaymentStatus == "Paid"
                && x.Sessions.Any(s => s.AvailabilityId.HasValue && r.AvailabilityIds.Contains(s.AvailabilityId.Value)), token), "Lịch đã có học viên 1–1.");
            for (var i = 1; i < slots.Count; i++) Require(slots[i - 1].EndTime <= slots[i].StartTime, "Các buổi học bị trùng giờ.");
            Require(slots[^1].EndTime <= slots[0].StartTime.AddMonths(p!.DurationMonths), "Lịch lớp vượt thời gian học của gói.");
            Require(!p.TargetExamDate.HasValue || slots[^1].EndTime <= p.TargetExamDate, "Lớp phải kết thúc trước kỳ thi mục tiêu.");
            var weekly = slots.GroupBy(s => { var d = s.StartTime.AddHours(7).Date; return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); });
            Require(weekly.All(g => g.Count() <= p.SessionsPerWeek), "Số buổi trong tuần vượt cấu hình gói.");
            c!.Name = r.Name.Trim(); c.PlanId = r.PlanId; c.MentorId = r.MentorId; c.Capacity = r.Capacity;
            c.StartsAt = slots[0].StartTime; c.EndsAt = slots[^1].EndTime;
            c.Status = r.Publish ? "Published" : "Draft";
            if (r.Publish) Require(p.IsPublished, "Gói cần được mở bán trước khi công bố lớp.");
            var old = c.Sessions.ToDictionary(x => x.AvailabilityId);
            c.Sessions.RemoveAll(x => !r.AvailabilityIds.Contains(x.AvailabilityId));
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i].MaxLearners = r.Capacity;
                if (old.TryGetValue(slots[i].Id, out var existing)) existing.Number = i + 1;
                else c.Sessions.Add(new() { Number = i + 1, AvailabilityId = slots[i].Id });
            }
            if (!id.HasValue) await uow.StudyCohorts.AddAsync(c, token); else c.Revision++;
            await uow.SaveChangesAsync(token); savedId = c.Id;
        }, ct);
        return new { Id = savedId };
    });

    public Task<ApiResponse<object>> AvailableSlotsAsync(int id, CancellationToken ct) => Run(async () =>
    {
        var c = await uow.StudyCourses.GetByIdAsync(id, ct);
        Require(c is not null, "Không tìm thấy gói học."); Learner(c!);
        Require(c!.SessionType == "Individual" && c.PaymentStatus == "Paid", "Chỉ gói 1–1 đã thanh toán được chọn lịch.");
        return await uow.MentorAvailabilities.Query().Where(s => !s.IsDeleted && s.IsApproved && s.RejectedAt == null
            && s.ChangeRequestedAt == null && s.StartTime > DateTime.UtcNow && s.MaxLearners == 1 && s.BookedCount == 0
            && (!c.MentorId.HasValue || s.MentorId == c.MentorId) && (c.Status == "Ready" || (s.StartTime >= c.StartsAt && s.EndTime <= c.EndsAt)) && s.EndTime == s.StartTime.AddMinutes(c.SessionMinutes)
            && (!c.TargetExamDate.HasValue || s.EndTime <= c.TargetExamDate)
            && !uow.StudyCohorts.Query().Any(g => g.Sessions.Any(t => t.AvailabilityId == s.Id))
            && !uow.StudyCourses.Query().Any(e => e.PaymentStatus == "Paid" && e.Sessions.Any(t => t.AvailabilityId == s.Id && t.Status == "Scheduled")))
            .OrderBy(s => s.StartTime).Select(s => new { s.Id, s.MentorId,
                MentorName = s.Mentor.FirstName + " " + s.Mentor.LastName, s.StartTime, s.EndTime }).Take(500).ToListAsync(ct);
    });

    public Task<ApiResponse<object>> BookSlotAsync(int id, BookCourseSlot r, CancellationToken ct) => Change(id, async c =>
    {
        Learner(c);
        Require(c.SessionType == "Individual" && c.PaymentStatus == "Paid" && c.Status is "Ready" or "Active", "Gói không còn được đặt lịch.");
        var slot = await uow.MentorAvailabilities.GetByIdAsync(r.AvailabilityId, ct);
        Require(slot is not null, "Không tìm thấy lịch.");
        await uow.StudyCourses.LockScheduleAsync(slot!.MentorId, c.LearnerId, ct);
        // Requery after acquiring the same lock used by existing booking flows.
        var available = await uow.MentorAvailabilities.Query().AsNoTracking().AnyAsync(s => s.Id == r.AvailabilityId
            && !s.IsDeleted && s.IsApproved && s.RejectedAt == null && s.ChangeRequestedAt == null
            && s.StartTime > DateTime.UtcNow && s.MaxLearners == 1 && s.BookedCount == 0, ct);
        Require(available && (!c.MentorId.HasValue || c.MentorId == slot.MentorId), "Lịch đã có người đặt hoặc không thuộc Mentor của gói.");
        Require(await uow.UserAccounts.AnyAsync(x => x.Id == slot.MentorId && x.Role == Role.Mentor && !x.IsDeleted, ct), "Mentor không còn hoạt động.");
        Require((slot.EndTime - slot.StartTime).TotalMinutes == c.SessionMinutes
            && (!c.TargetExamDate.HasValue || slot.EndTime <= c.TargetExamDate), "Thời lượng/ngày học không phù hợp gói.");
        Require(!await uow.StudyCohorts.AnyAsync(g => g.Sessions.Any(s => s.AvailabilityId == slot.Id), ct), "Lịch dành cho lớp nhóm.");
        var occupied = c.Sessions.Where(s => s.Status != "CancelledByMentor").Select(s => s.Number).Distinct().ToHashSet();
        Require(occupied.Count < c.TotalSessions, "Đã dùng hết số buổi của gói.");
        Require(!await HasLearnerConflictAsync(c.LearnerId, [slot], c.Id, ct), "Bạn đã có lịch hoặc đang giữ chỗ trùng giờ.");
        Require(!await uow.StudyCourses.Query().AnyAsync(e => e.PaymentStatus == "Paid" && e.Status != "Completed"
            && (e.MentorId == slot.MentorId || e.LearnerId == c.LearnerId)
            && e.Sessions.Any(s => s.Status == "Scheduled" && s.ScheduledAt < slot.EndTime
                && s.ScheduledAt.AddMinutes(e.SessionMinutes) > slot.StartTime), ct), "Mentor hoặc học viên đã có lịch trùng giờ.");
        Require(!await uow.MentorBookings.AnyAsync(b => !b.IsDeleted && b.UserSubscription.UserId == c.LearnerId
            && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted) && b.MentorAvailability != null
            && b.MentorAvailability.StartTime < slot.EndTime && b.MentorAvailability.EndTime > slot.StartTime, ct), "Bạn đã có lịch trùng giờ.");
        if (c.Status == "Ready") { c.StartsAt = slot.StartTime; c.EndsAt = slot.StartTime.AddMonths(c.DurationMonths); }
        Require(slot.StartTime >= c.StartsAt && slot.EndTime <= c.EndsAt, "Lịch nằm ngoài thời hạn gói tính từ buổi đầu tiên.");
        slot.BookedCount = 1;
        c.MentorId = slot.MentorId; c.Status = "Active";
        var number = Enumerable.Range(1, c.TotalSessions).First(n => !occupied.Contains(n));
        if (!c.Sessions.Any(s => s.Number == number))
        {
            var local = slot.StartTime.AddHours(7).Date;
            var week = local.AddDays(-(((int)local.DayOfWeek + 6) % 7)).AddHours(-7);
            Require(c.Sessions.Count(s => s.Attempt == 1 && s.Status != "CancelledByMentor" && s.ScheduledAt >= week && s.ScheduledAt < week.AddDays(7)) < c.SessionsPerWeek,
                "Tuần này đã đủ số buổi theo gói.");
        }
        c.Sessions.Add(new() { Number = number, Attempt = c.Sessions.Count(s => s.Number == number) + 1,
            ScheduledAt = slot.StartTime, AvailabilityId = slot.Id, MeetingUrl = $"/meeting-room/{slot.RoomId}" });
        Audit(c, "LearnerBooked", $"Đặt buổi {number}: {slot.StartTime:O}.");
        return new { c.Id };
    }, ct);

    private void AddCohortSessions(StudyCourse c, StudyCohort cohort)
    {
        c.MentorId = cohort.MentorId; c.StartsAt = cohort.StartsAt; c.EndsAt = cohort.EndsAt;
        c.Status = "Active";
        foreach (var s in cohort.Sessions)
            c.Sessions.Add(new() { Number = s.Number, ScheduledAt = s.Availability.StartTime,
                AvailabilityId = s.AvailabilityId, MeetingUrl = $"/meeting-room/{s.Availability.RoomId}" });
    }
}
