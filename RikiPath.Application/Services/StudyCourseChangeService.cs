using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class StudyCourseService
{
    public Task<ApiResponse<object>> ChangeRequestsAsync(CancellationToken ct) => Run(async () =>
        await uow.CourseScheduleRequests.Query().Where(x => Actor.Role == Role.Admin
            || x.Course.LearnerId == Actor.Id || x.Course.MentorId == Actor.Id)
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.CourseId, x.SessionId, x.Kind, x.Reason,
                x.Status, x.CreatedAt, x.Response, x.RequestedById, CourseName = x.Course.Name, x.Course.MentorId, x.Course.SessionType }).ToListAsync(ct));

    public Task<ApiResponse<object>> RequestChangeAsync(int id, RequestCourseChange r, CancellationToken ct) => Change(id, async c =>
    {
        Active(c);
        Require(r.Kind is "Cancel" or "Move" && !string.IsNullOrWhiteSpace(r.Reason) && r.Reason.Length <= 1000, "Chọn hủy/dời và nhập lý do.");
        var s = c.Sessions.SingleOrDefault(x => x.Id == r.SessionId);
        Require(s is { Status: "Scheduled" } && s.ScheduledAt > DateTime.UtcNow, "Chỉ yêu cầu thay đổi buổi chưa bắt đầu.");
        Require(!await uow.CourseScheduleRequests.AnyAsync(x => x.CourseId == id && x.SessionId == r.SessionId && x.Status == "Pending", ct), "Buổi đã có yêu cầu chờ xử lý.");
        await uow.CourseScheduleRequests.AddAsync(new() { CourseId = id, SessionId = r.SessionId, RequestedById = Actor.Id,
            Kind = r.Kind, Reason = r.Reason.Trim() }, ct);
        Audit(c, "ScheduleChangeRequested", $"Buổi {s!.Number}: {r.Kind}. {r.Reason.Trim()}");
        return new { c.Id };
    }, ct);

    public Task<ApiResponse<object>> ReviewChangeAsync(int id, int requestId, ReviewCourseChange r, CancellationToken ct) => Change(id, async c =>
    {
        Admin(); Active(c);
        Require(!string.IsNullOrWhiteSpace(r.Response) && r.Response.Length <= 1000, "Nhập lý do xử lý.");
        var request = await uow.CourseScheduleRequests.FirstOrDefaultAsync(x => x.Id == requestId && x.CourseId == id, ct);
        Require(request is { Status: "Pending" }, "Yêu cầu đã được xử lý hoặc không tồn tại.");
        var session = c.Sessions.Single(x => x.Id == request!.SessionId);
        Require(session.Status == "Scheduled" && session.ScheduledAt > DateTime.UtcNow, "Buổi không còn được điều chỉnh.");
        if (r.Approve)
        {
            Require(!c.CohortId.HasValue || request!.Kind != "Move" || r.EntireClassConfirmed, "Dời buổi nhóm sẽ ảnh hưởng cả lớp; Admin cần xác nhận phạm vi cả lớp.");
            Require(!c.CohortId.HasValue || request!.Kind != "Cancel",
                "Không thể hủy cả lớp qua yêu cầu của một học viên; cần quy trình duyệt hủy lớp và quyền lợi riêng.");
            await uow.StudyCourses.LockScheduleAsync(c.MentorId!.Value, c.LearnerId, ct);
            if (request!.Kind == "Move")
            {
                Require(r.NewAvailabilityId.HasValue, "Chọn lịch rảnh đã duyệt thay thế.");
                var slot = await uow.MentorAvailabilities.Query()
                    .Include(x => x.GroupPolicy)
                    .FirstOrDefaultAsync(x => x.Id == r.NewAvailabilityId!.Value, ct);
                RequireConflict(slot is not null && slot.MentorId == c.MentorId && slot.IsApproved && !slot.IsDeleted && slot.RejectedAt == null
                    && slot.BookedCount == 0 && (c.CohortId.HasValue || slot.MaxLearners == 1) && slot.StartTime > DateTime.UtcNow && slot.ChangeRequestedAt == null
                    && (slot.EndTime - slot.StartTime).TotalMinutes == c.SessionMinutes, "Lịch thay thế không phù hợp hoặc đã có người đặt.");
                Require((!c.TargetExamDate.HasValue || slot!.EndTime <= c.TargetExamDate)
                    && (c.CohortId.HasValue || (slot!.StartTime >= c.StartsAt && slot.EndTime <= c.EndsAt)), "Lịch thay thế ngoài thời hạn/cấu hình gói.");
                RequireConflict(!await uow.StudyCohorts.Query().AnyAsync(g => g.Sessions.Any(s => s.AvailabilityId == slot!.Id), ct), "Lịch dành cho lớp nhóm.");
                // Phía Mentor: kiểm tra chuẩn (lớp đã công bố, lịch 1-1 đã xác nhận, buổi khóa 1-1) cho khung giờ mới,
                // bỏ qua khung giờ cũ của buổi đang dời. Đã giữ LockScheduleAsync(mentor) ở trên.
                await EnsureNoTeachingConflictAsync(c.MentorId!.Value,
                    [new ProposedTeachingInterval(slot!.StartTime, slot.EndTime, session.Number)],
                    new TeachingConflictOptions
                    {
                        ExcludeAvailabilityIds = session.AvailabilityId.HasValue ? new[] { session.AvailabilityId.Value } : null
                    }, ct);
                var restMinutes = slot.RestMinutesSnapshot ?? slot.GroupPolicy?.RestMinutes ?? 0;
                var slotRestEnd = slot.EndTime.AddMinutes(restMinutes);
                // Phía học viên: giữ nguyên kiểm tra trùng lịch của học viên.
                RequireConflict(!await uow.StudyCourses.Query().AnyAsync(e => e.PaymentStatus == "Paid" && e.Status != "Cancelled" && e.LearnerId == c.LearnerId
                    && e.Sessions.Any(s => s.Id != session.Id && (!c.CohortId.HasValue || e.CohortId != c.CohortId || s.AvailabilityId != session.AvailabilityId) && s.Status == "Scheduled" && s.ScheduledAt < slotRestEnd
                        && s.ScheduledAt.AddMinutes(e.SessionMinutes + (s.Availability != null && s.Availability.RestMinutesSnapshot.HasValue ? s.Availability.RestMinutesSnapshot.Value : 0)) > slot.StartTime), ct), "Lịch thay thế bị trùng.");
                RequireConflict(!await uow.MentorBookings.Query().AnyAsync(b => !b.IsDeleted && b.MentorAvailability != null
                    && b.UserSubscription.UserId == c.LearnerId
                    && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted
                        || (b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentExpiresAt > DateTime.UtcNow))
                    && b.MentorAvailability.StartTime < slotRestEnd
                    && slot.StartTime < b.MentorAvailability.EndTime.AddMinutes(b.MentorAvailability.RestMinutesSnapshot ?? 0), ct),
                    "Lịch thay thế trùng với buổi tư vấn hiện có.");
                if (!c.CohortId.HasValue && session.AvailabilityId.HasValue)
                {
                    var old = await uow.MentorAvailabilities.GetByIdAsync(session.AvailabilityId.Value, ct);
                    if (old is not null) old.BookedCount = Math.Max(0, old.BookedCount - 1);
                }
                if (c.CohortId.HasValue)
                {
                    await uow.StudyCourses.LockCohortAsync(c.CohortId.Value, ct);
                    var cohort = await uow.StudyCohorts.Query().Include(x => x.Sessions).ThenInclude(x => x.Availability).ThenInclude(x => x.GroupPolicy)
                        .Include(x => x.Enrollments).ThenInclude(x => x.Sessions).SingleAsync(x => x.Id == c.CohortId.Value, ct);
                    Require(session.AvailabilityId.HasValue, "Buổi nhóm chưa có lịch nguồn.");
                    var previousId = session.AvailabilityId;
                    var classSession = cohort.Sessions.Single(x => x.AvailabilityId == previousId);
                    var rescheduleLeadHours = classSession.Availability.GroupPolicy?.ReschedulingLeadHours ?? 0;
                    if (rescheduleLeadHours > 0)
                    {
                        RequireConflict((classSession.Availability.StartTime - DateTime.UtcNow).TotalHours >= rescheduleLeadHours,
                            $"Chỉ được dời buổi nhóm trước giờ bắt đầu ít nhất {rescheduleLeadHours} giờ theo chính sách.");
                    }
                    if (classSession.Availability.GroupPolicyId.HasValue)
                    {
                        RequireConflict(slot.MaxLearners == cohort.Capacity
                            && slot.GroupPolicyId == classSession.Availability.GroupPolicyId
                            && slot.SessionMinutesSnapshot == classSession.Availability.SessionMinutesSnapshot,
                            "Lịch thay thế phải giữ nguyên sức chứa và chính sách của buổi nhóm.");
                    }
                    else
                    {
                        RequireConflict(slot.PolicyVersion == null && slot.GroupPolicyId == null
                            && slot.MaxLearners == cohort.Capacity,
                            "Lịch thay thế lớp nhóm phải giữ nguyên sức chứa và cấu hình.");
                    }
                    var learners = cohort.Enrollments.Where(e => e.PaymentStatus == "Paid" && e.Status == "Active").Select(e => e.LearnerId).ToList();
                    RequireConflict(!await uow.StudyCourses.Query().AnyAsync(e => learners.Contains(e.LearnerId) && e.PaymentStatus == "Paid"
                        && e.Status != "Cancelled"
                        && e.Sessions.Any(s => s.AvailabilityId != previousId && s.Status == "Scheduled" && s.ScheduledAt < slot.EndTime
                            && s.ScheduledAt.AddMinutes(e.SessionMinutes) > slot.StartTime), ct), "Một học viên trong lớp bị trùng lịch thay thế.");
                    RequireConflict(!await uow.MentorBookings.Query().AnyAsync(b => !b.IsDeleted && b.MentorAvailability != null
                        && learners.Contains(b.UserSubscription.UserId)
                        && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted
                            || (b.Status == MentorStatus.AwaitingPayment && b.UserSubscription.PaymentExpiresAt > DateTime.UtcNow))
                        && b.MentorAvailability.StartTime < slot.EndTime
                        && slot.StartTime < b.MentorAvailability.EndTime, ct),
                        "Một học viên trong lớp có lịch tư vấn trùng giờ thay thế.");
                    classSession.AvailabilityId = slot.Id; classSession.Availability = slot;
                    foreach (var enrollment in cohort.Enrollments.Where(e => e.PaymentStatus == "Paid" && e.Status == "Active"))
                    {
                        var item = enrollment.Sessions.SingleOrDefault(s => s.AvailabilityId == previousId && s.Status == "Scheduled");
                        if (item is null) continue;
                        item.AvailabilityId = slot.Id; item.ScheduledAt = slot.StartTime; item.MeetingUrl = $"/meeting-room/{slot.RoomId}";
                        Audit(enrollment, "GroupSessionMoved", $"Admin dời buổi nhóm sang {slot.StartTime:O}. {r.Response.Trim()}");
                    }
                    if (cohort.StartsAt > DateTime.UtcNow) cohort.StartsAt = cohort.Sessions.Min(s => s.Availability.StartTime);
                    cohort.EndsAt = cohort.Sessions.Max(s => s.Availability.EndTime); cohort.Revision++;
                }
                else
                {
                    slot!.BookedCount = 1; session.AvailabilityId = slot.Id; session.ScheduledAt = slot.StartTime;
                    session.MeetingUrl = $"/meeting-room/{slot.RoomId}";
                }
            }
            else
            {
                session.Status = r.RestoreSessionCredit ? "CancelledByMentor" : "Cancelled";
                session.Note = r.Response.Trim(); session.ConfirmedById = Actor.Id; session.ConfirmedAt = DateTime.UtcNow;
                if (!c.CohortId.HasValue && session.AvailabilityId.HasValue)
                {
                    var slot = await uow.MentorAvailabilities.GetByIdAsync(session.AvailabilityId.Value, ct);
                    if (slot is not null) slot.BookedCount = Math.Max(0, slot.BookedCount - 1);
                }
            }
        }
        request!.Status = r.Approve ? "Approved" : "Rejected"; request.Response = r.Response.Trim();
        request.ReviewedById = Actor.Id; request.ReviewedAt = DateTime.UtcNow;
        Audit(c, "ScheduleChangeReviewed", $"Yêu cầu {requestId}: {request.Status}. {r.Response.Trim()}");
        return new { request.Id };
    }, ct);
}
