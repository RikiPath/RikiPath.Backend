using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

// Phản hồi khóa/lớp nhóm: đánh giá cố vấn, khiếu nại có minh chứng và quyết định Admin,
// thông báo PaymentReview và tổng kết hoàn thành khóa học (tách biệt hoàn toàn với 1-1 MentorReview/MentorDispute).
public sealed partial class StudyCourseService
{
    public const string InternalCompletionNote = "Đây là báo cáo học tập nội bộ, không phải chứng chỉ JLPT chính thức.";

    private static readonly HashSet<string> AllowedComplaintCategories =
    [
        CohortComplaint.CategoryTeachingQuality,
        CohortComplaint.CategoryMentorConduct,
        CohortComplaint.CategorySchedule,
        CohortComplaint.CategoryPaymentEnrollment,
        CohortComplaint.CategoryOther
    ];

    private static readonly HashSet<string> AllowedComplaintStatuses =
    [
        CohortComplaint.Open,
        CohortComplaint.UnderReview,
        CohortComplaint.Resolved,
        CohortComplaint.Rejected
    ];

    private async Task QueuePaymentReviewNotificationsAsync(StudyCourse course, CancellationToken ct)
    {
        var admins = await uow.UserAccounts.Query()
            .Where(u => u.Role == Role.Admin && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(ct);

        // Không đưa mã tham chiếu nhà cung cấp hay số tiền vào nội dung thông báo cho học viên.
        var items = new List<(int UserId, string Title, string Message, string Type, string? LinkUrl)>
        {
            (
                course.LearnerId,
                "Giao dịch đang chờ xem xét",
                "Giao dịch đã được ghi nhận nhưng chưa thể hoàn tất ghi danh. Quản trị viên sẽ xem xét yêu cầu của bạn.",
                "PaymentReview",
                "/my-courses"
            )
        };

        foreach (var adminId in admins)
        {
            items.Add((
                adminId,
                "Đơn ghi danh cần xem xét thanh toán",
                $"Đơn khóa học #{course.Id} đã ghi nhận thanh toán nhưng chưa thể hoàn tất ghi danh tự động; vui lòng xem xét.",
                "PaymentReview",
                $"/admin/study-courses?courseId={course.Id}"
            ));
        }

        QueueNotifications($"course-payment-review:{course.Id}", "StudyCourse", course.Id.ToString(), items);
    }

    private static void AddComplaintEvent(CohortComplaint complaint, int actorId, Role actorRole, string action, string detail)
        => complaint.History.Add(new CohortComplaintEvent
        {
            Action = action,
            ActorId = actorId,
            ActorRole = actorRole.ToString(),
            Detail = detail.Length > 2000 ? detail[..2000] : detail,
            AtUtc = DateTime.UtcNow
        });

    private static HashSet<int> InvolvedMentorIdsOf(StudyCourse course)
    {
        var ids = new HashSet<int>();
        if (course.MentorId.HasValue) ids.Add(course.MentorId.Value);
        if (course.Cohort is not null) ids.Add(course.Cohort.MentorId);
        foreach (var s in course.Sessions)
        {
            if (s.Availability is not null) ids.Add(s.Availability.MentorId);
        }
        return ids;
    }

    private static object ReviewView(CohortMentorReview x) => new
    {
        x.Id,
        x.CourseId,
        x.CohortId,
        x.LearnerId,
        LearnerName = $"{x.Learner?.FirstName} {x.Learner?.LastName}".Trim(),
        x.MentorId,
        MentorName = $"{x.Mentor?.FirstName} {x.Mentor?.LastName}".Trim(),
        x.Rating,
        x.Comment,
        x.IsHidden,
        x.CreatedAtUtc,
        x.UpdatedAtUtc
    };

    private static object ComplaintView(CohortComplaint x, Role viewerRole)
    {
        var history = x.History.OrderBy(h => h.Id).Select(h => new { h.Action, h.ActorRole, h.Detail, h.AtUtc }).ToList();
        var learnerName = $"{x.Learner?.FirstName} {x.Learner?.LastName}".Trim();
        var mentorName = x.Mentor is null ? null : $"{x.Mentor.FirstName} {x.Mentor.LastName}".Trim();

        // Cố vấn chỉ thấy khiếu nại nhắm tới mình và KHÔNG bao giờ thấy thông tin thanh toán/hoàn tiền.
        if (viewerRole == Role.Mentor)
        {
            return new
            {
                x.Id,
                x.CourseId,
                x.CohortId,
                x.LearnerId,
                LearnerName = learnerName,
                x.MentorId,
                MentorName = mentorName,
                CohortName = x.Cohort?.Name,
                x.Category,
                x.Message,
                HasEvidence = !string.IsNullOrWhiteSpace(x.EvidenceUrl),
                x.Status,
                x.MentorResponse,
                x.MentorRespondedAtUtc,
                x.Decision,
                x.DecidedById,
                x.DecidedAtUtc,
                x.CreatedAtUtc,
                x.Revision,
                History = history
            };
        }

        return new
        {
            x.Id,
            x.CourseId,
            x.CohortId,
            x.LearnerId,
            LearnerName = learnerName,
            x.MentorId,
            MentorName = mentorName,
            CohortName = x.Cohort?.Name,
            x.Category,
            x.Message,
            HasEvidence = !string.IsNullOrWhiteSpace(x.EvidenceUrl),
            x.RefundReviewRequested,
            CourseStatus = x.Course?.Status,
            CoursePaymentStatus = x.Course?.PaymentStatus,
            CoursePaymentReviewReason = x.Course?.PaymentReviewReason,
            x.Status,
            x.MentorResponse,
            x.MentorRespondedAtUtc,
            x.Decision,
            x.DecidedById,
            x.DecidedAtUtc,
            x.CreatedAtUtc,
            x.Revision,
            History = history
        };
    }

    private IQueryable<CohortComplaint> ComplaintDetails => uow.CohortComplaints.Query()
        .Include(x => x.Course)
        .Include(x => x.Cohort)
        .Include(x => x.Learner)
        .Include(x => x.Mentor)
        .Include(x => x.History)
        .AsSplitQuery();

    public Task<ApiResponse<object>> CreateCohortMentorReviewAsync(
        int courseId, CreateCohortMentorReviewRequest request, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        Rule(request.MentorId > 0, "COHORT_REVIEW_INVALID_MENTOR", "Giáo viên không hợp lệ.");
        Rule(request.Rating is >= 1 and <= 5, "COHORT_REVIEW_INVALID_RATING", "Điểm đánh giá phải từ 1 đến 5 sao.");
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        Rule(comment is null || comment.Length <= 2000, "COHORT_REVIEW_COMMENT_TOO_LONG", "Nhận xét tối đa 2000 ký tự.");

        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(courseId, token);
            Rule(locked is not null, "COHORT_REVIEW_COURSE_NOT_FOUND", "Không tìm thấy khóa học.", HttpStatusCode.NotFound);
            Rule(locked!.LearnerId == Actor.Id, "COHORT_REVIEW_FORBIDDEN", "Bạn chỉ được đánh giá khóa học của mình.", HttpStatusCode.Forbidden);

            var course = await uow.StudyCourses.Query().AsNoTracking()
                .Include(x => x.Cohort)
                .Include(x => x.Sessions).ThenInclude(s => s.Availability)
                .AsSplitQuery()
                .SingleAsync(x => x.Id == courseId, token);

            Rule(HasConfirmedCourseAccess(course), "PAYMENT_REQUIRED",
                "Vui lòng hoàn tất thanh toán để đánh giá Giáo viên lớp học.",
                HttpStatusCode.Forbidden);

            var delivered = course.Sessions.Where(s => s.Status == "Present").ToList();
            var eligibleMentors = new HashSet<int>();
            if (delivered.Count > 0 && course.PaymentStatus == "Paid")
            {
                foreach (var s in delivered)
                {
                    var slotMentorId = s.Availability?.MentorId ?? course.MentorId ?? course.Cohort?.MentorId;
                    if (slotMentorId.HasValue) eligibleMentors.Add(slotMentorId.Value);
                }
                if (course.MentorId.HasValue) eligibleMentors.Add(course.MentorId.Value);
                if (course.Cohort is not null) eligibleMentors.Add(course.Cohort.MentorId);
            }

            Rule(eligibleMentors.Contains(request.MentorId), "COHORT_REVIEW_NOT_ELIGIBLE",
                "Bạn chỉ có thể đánh giá Giáo viên đã giảng dạy ít nhất một buổi học bạn có mặt hoặc Giáo viên hiện tại sau khi lớp đã có buổi học.");

            var duplicate = await uow.CohortMentorReviews.AnyAsync(
                x => x.CourseId == courseId && x.MentorId == request.MentorId, token);
            Rule(!duplicate, "COHORT_REVIEW_DUPLICATE",
                "Bạn đã đánh giá Giáo viên này trong khóa học này rồi.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            var review = new CohortMentorReview
            {
                CourseId = courseId,
                CohortId = course.CohortId,
                LearnerId = Actor.Id,
                MentorId = request.MentorId,
                Rating = request.Rating,
                Comment = comment,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            // Đánh giá không bao giờ tự tạo yêu cầu chuyển lớp, đặt lịch 1-1 hay giao dịch thanh toán/hoàn tiền.
            await uow.CohortMentorReviews.AddAsync(review, token);
            await uow.SaveChangesAsync(token);

            var saved = await uow.CohortMentorReviews.Query().AsNoTracking()
                .Include(x => x.Learner).Include(x => x.Mentor)
                .SingleAsync(x => x.Id == review.Id, token);
            result = ReviewView(saved);
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> UpdateCohortMentorReviewAsync(
        int courseId, int reviewId, UpdateCohortMentorReviewRequest request, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        Rule(request.Rating is >= 1 and <= 5, "COHORT_REVIEW_INVALID_RATING", "Điểm đánh giá phải từ 1 đến 5 sao.");
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        Rule(comment is null || comment.Length <= 2000, "COHORT_REVIEW_COMMENT_TOO_LONG", "Nhận xét tối đa 2000 ký tự.");

        var review = await uow.CohortMentorReviews.Query()
            .Include(x => x.Learner).Include(x => x.Mentor)
            .SingleOrDefaultAsync(x => x.Id == reviewId && x.CourseId == courseId, ct);
        Rule(review is not null, "COHORT_REVIEW_NOT_FOUND", "Không tìm thấy đánh giá.", HttpStatusCode.NotFound);
        Rule(review!.LearnerId == Actor.Id, "COHORT_REVIEW_FORBIDDEN", "Bạn chỉ được chỉnh sửa đánh giá của mình.", HttpStatusCode.Forbidden);

        review.Rating = request.Rating;
        review.Comment = comment;
        review.UpdatedAtUtc = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return ReviewView(review);
    });

    public Task<ApiResponse<object>> CohortMentorReviewsAsync(int courseId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Learner or Role.Mentor)) throw new UnauthorizedAccessException();
        var course = await uow.StudyCourses.Query().AsNoTracking()
            .Include(x => x.Cohort)
            .Include(x => x.Sessions).ThenInclude(s => s.Availability)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == courseId, ct);
        Rule(course is not null, "COHORT_REVIEW_COURSE_NOT_FOUND", "Không tìm thấy khóa học.", HttpStatusCode.NotFound);

        if (Actor.Role == Role.Learner && course!.LearnerId != Actor.Id)
            throw new UnauthorizedAccessException();
        if (Actor.Role == Role.Learner)
        {
            Rule(HasConfirmedCourseAccess(course!), "PAYMENT_REQUIRED",
                "Vui lòng hoàn tất thanh toán để xem đánh giá của lớp học.",
                HttpStatusCode.Forbidden);
        }
        if (Actor.Role == Role.Mentor && !InvolvedMentorIdsOf(course!).Contains(Actor.Id))
            throw new UnauthorizedAccessException();

        var query = uow.CohortMentorReviews.Query().AsNoTracking()
            .Include(x => x.Learner).Include(x => x.Mentor)
            .Where(x => x.CourseId == courseId);
        if (Actor.Role == Role.Mentor)
            query = query.Where(x => !x.IsHidden);

        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
        return rows.Select(ReviewView).ToList();
    });

    public Task<ApiResponse<object>> MentorCohortReviewSummaryAsync(int mentorId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Learner or Role.Mentor)) throw new UnauthorizedAccessException();
        Rule(mentorId > 0, "COHORT_REVIEW_INVALID_MENTOR", "Giáo viên không hợp lệ.");

        var ratings = await uow.CohortMentorReviews.Query().AsNoTracking()
            .Where(x => x.MentorId == mentorId && !x.IsHidden)
            .Select(x => x.Rating)
            .ToListAsync(ct);

        var count = ratings.Count;
        var average = count == 0 ? 0.0 : Math.Round(ratings.Average(), 2);

        // Tuyệt đối không trả thông tin định danh học viên trong tổng hợp đánh giá công khai của cố vấn.
        var comments = await uow.CohortMentorReviews.Query().AsNoTracking()
            .Where(x => x.MentorId == mentorId && !x.IsHidden && x.Comment != null && x.Comment != "")
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.CohortId,
                x.Rating,
                x.Comment,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return new
        {
            MentorId = mentorId,
            AverageRating = average,
            ReviewCount = count,
            Comments = comments
        };
    });

    public Task<ApiResponse<object>> CreateCohortComplaintAsync(
        int courseId, string? category, string? message, int? mentorId, bool refundReviewRequested,
        Guid idempotencyKey, IFormFile? evidence, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Learner) throw new UnauthorizedAccessException();
        Rule(idempotencyKey != Guid.Empty, "COMPLAINT_IDEMPOTENCY_KEY_REQUIRED", "Thiếu mã chống gửi trùng.");
        var normalizedCategory = category?.Trim() ?? "";
        Rule(AllowedComplaintCategories.Contains(normalizedCategory), "COMPLAINT_INVALID_CATEGORY",
            "Danh mục khiếu nại không hợp lệ. Vui lòng chọn: Chất lượng giảng dạy, Tác phong Giáo viên, Lịch học, Thanh toán & Ghi danh hoặc Khác.");
        var trimmedMessage = message?.Trim() ?? "";
        Rule(trimmedMessage.Length is >= 10 and <= 2000, "COMPLAINT_MESSAGE_INVALID",
            "Nội dung khiếu nại phải từ 10 đến 2000 ký tự.");
        Rule(!refundReviewRequested || normalizedCategory == CohortComplaint.CategoryPaymentEnrollment,
            "COMPLAINT_REFUND_REVIEW_NOT_ALLOWED",
            "Chỉ khiếu nại về thanh toán hoặc ghi danh mới có thể yêu cầu xem xét hoàn tiền.");
        if (evidence is not null)
            Rule(evidence.Length is > 0 and <= 10 * 1024 * 1024 && AllowedEvidenceTypes.Contains(evidence.ContentType),
                "COMPLAINT_EVIDENCE_INVALID", "Minh chứng phải là PDF/PNG/JPG tối đa 10 MB.");

        object result = new { };
        string? evidenceUrl = null;
        var uploaded = false;
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var locked = await uow.StudyCourses.LockAsync(courseId, token);
            Rule(locked is not null, "COMPLAINT_COURSE_NOT_FOUND", "Không tìm thấy khóa học.", HttpStatusCode.NotFound);
            Rule(locked!.LearnerId == Actor.Id, "COMPLAINT_FORBIDDEN",
                "Bạn chỉ được gửi khiếu nại cho khóa học của mình.", HttpStatusCode.Forbidden);

            var existing = await ComplaintDetails.SingleOrDefaultAsync(
                x => x.LearnerId == Actor.Id && x.IdempotencyKey == idempotencyKey, token);
            if (existing is not null)
            {
                Rule(existing.CourseId == courseId && existing.Category == normalizedCategory
                        && existing.MentorId == mentorId && existing.RefundReviewRequested == refundReviewRequested,
                    "COMPLAINT_IDEMPOTENCY_KEY_REUSED", "Mã chống gửi trùng đã dùng cho khiếu nại khác.", HttpStatusCode.Conflict);
                result = ComplaintView(existing, Actor.Role);
                return;
            }

            var course = await uow.StudyCourses.Query().AsNoTracking()
                .Include(x => x.Cohort)
                .Include(x => x.Sessions).ThenInclude(s => s.Availability)
                .AsSplitQuery()
                .SingleAsync(x => x.Id == courseId, token);

            if (normalizedCategory != CohortComplaint.CategoryPaymentEnrollment)
            {
                Rule(HasConfirmedCourseAccess(course), "PAYMENT_REQUIRED",
                    "Vui lòng hoàn tất thanh toán để gửi khiếu nại nội dung lớp học.",
                    HttpStatusCode.Forbidden);
            }

            if (mentorId.HasValue)
            {
                Rule(InvolvedMentorIdsOf(course).Contains(mentorId.Value), "COMPLAINT_MENTOR_NOT_INVOLVED",
                    "Giáo viên được chọn không thuộc lịch sử giảng dạy của khóa học này.");
            }

            if (evidence is not null && !uploaded)
            {
                await using var stream = evidence.OpenReadStream();
                evidenceUrl = (await storage.UploadPrivateVerificationAsync(stream, evidence.ContentType, Actor.Id, token)).Url;
                uploaded = true;
            }

            var now = DateTime.UtcNow;
            var complaint = new CohortComplaint
            {
                CourseId = courseId,
                CohortId = course.CohortId,
                LearnerId = Actor.Id,
                MentorId = mentorId,
                Category = normalizedCategory,
                Message = trimmedMessage,
                EvidenceUrl = evidenceUrl,
                RefundReviewRequested = refundReviewRequested,
                Status = CohortComplaint.Open,
                IdempotencyKey = idempotencyKey,
                CreatedAtUtc = now,
                Revision = 1
            };
            var categoryLabel = normalizedCategory switch
            {
                CohortComplaint.CategoryTeachingQuality => "Chất lượng giảng dạy",
                CohortComplaint.CategoryMentorConduct => "Tác phong Giáo viên",
                CohortComplaint.CategorySchedule => "Lịch học",
                CohortComplaint.CategoryPaymentEnrollment => "Thanh toán & Ghi danh",
                _ => "Khác"
            };
            AddComplaintEvent(complaint, Actor.Id, Actor.Role, "Created",
                refundReviewRequested
                    ? $"Học viên gửi khiếu nại ({categoryLabel}) và đề nghị Quản trị viên xem xét hoàn tiền/bồi hoàn."
                    : $"Học viên gửi khiếu nại ({categoryLabel}).");

            await uow.CohortComplaints.AddAsync(complaint, token);
            await uow.SaveChangesAsync(token);

            var admins = await uow.UserAccounts.Query()
                .Where(u => u.Role == Role.Admin && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(token);
            QueueNotifications($"cohort-complaint:{complaint.Id}:created", "CohortComplaint", complaint.Id.ToString(),
                admins.Select(id => (id, "Khiếu nại khóa học mới",
                    $"Có khiếu nại mới ({categoryLabel}) đang chờ xem xét.",
                    "CohortComplaint", (string?)$"/admin/study-courses?complaintId={complaint.Id}")));
            await uow.SaveChangesAsync(token);

            var saved = await ComplaintDetails.SingleAsync(x => x.Id == complaint.Id, token);
            result = ComplaintView(saved, Actor.Role);
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> CohortComplaintsAsync(string? status, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Learner or Role.Mentor)) throw new UnauthorizedAccessException();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (normalizedStatus is not null)
            Rule(AllowedComplaintStatuses.Contains(normalizedStatus), "COMPLAINT_INVALID_STATUS",
                "Trạng thái lọc không hợp lệ (chỉ hỗ trợ: Đang mở, Đang xem xét, Đã giải quyết hoặc Bị từ chối).");

        var actor = Actor;
        var query = ComplaintDetails.Where(x =>
            actor.Role == Role.Admin
            || (actor.Role == Role.Learner && x.LearnerId == actor.Id)
            || (actor.Role == Role.Mentor
                && x.MentorId == actor.Id
                && x.Category != CohortComplaint.CategoryPaymentEnrollment
                && !x.RefundReviewRequested));
        if (normalizedStatus is not null)
            query = query.Where(x => x.Status == normalizedStatus);

        var rows = await query.OrderByDescending(x => x.Id).Take(500).ToListAsync(ct);
        return rows.Select(x => ComplaintView(x, actor.Role)).ToList();
    });

    public Task<ApiResponse<object>> CohortComplaintDetailAsync(int complaintId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Learner or Role.Mentor)) throw new UnauthorizedAccessException();
        var complaint = await ComplaintDetails.SingleOrDefaultAsync(x => x.Id == complaintId, ct);
        Rule(complaint is not null, "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", HttpStatusCode.NotFound);

        // Không tiết lộ khiếu nại ngoài phạm vi của Học viên hoặc Cố vấn (bao gồm khiếu nại thanh toán/hoàn tiền của Học viên).
        if (Actor.Role == Role.Learner && complaint!.LearnerId != Actor.Id)
            Rule(false, "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", HttpStatusCode.NotFound);
        if (Actor.Role == Role.Mentor
            && (complaint!.MentorId != Actor.Id
                || complaint.Category == CohortComplaint.CategoryPaymentEnrollment
                || complaint.RefundReviewRequested))
            Rule(false, "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", HttpStatusCode.NotFound);

        return ComplaintView(complaint!, Actor.Role);
    });

    public Task<ApiResponse<object>> RespondCohortComplaintAsync(
        int complaintId, RespondCohortComplaintRequest request, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role != Role.Mentor) throw new UnauthorizedAccessException();
        var response = request.Response?.Trim() ?? "";
        Rule(response.Length is >= 1 and <= 2000, "COMPLAINT_RESPONSE_REQUIRED",
            "Nhập phản hồi từ 1 đến 2000 ký tự.");

        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var complaint = await uow.CohortComplaints.Query().Include(x => x.History)
                .SingleOrDefaultAsync(x => x.Id == complaintId, token);
            Rule(complaint is not null, "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", HttpStatusCode.NotFound);
            Rule(complaint!.MentorId == Actor.Id, "COMPLAINT_FORBIDDEN",
                "Bạn chỉ được phản hồi khiếu nại nhắm tới mình.", HttpStatusCode.Forbidden);
            Rule(complaint.Category != CohortComplaint.CategoryPaymentEnrollment, "COMPLAINT_MENTOR_RESPONSE_NOT_ALLOWED",
                "Giáo viên không phản hồi khiếu nại về thanh toán hoặc ghi danh.");
            Rule(complaint.Status is CohortComplaint.Open or CohortComplaint.UnderReview, "COMPLAINT_ALREADY_DECIDED",
                "Khiếu nại đã có quyết định cuối cùng.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            complaint.MentorResponse = response;
            complaint.MentorRespondedAtUtc = now;
            if (complaint.Status == CohortComplaint.Open)
                complaint.Status = CohortComplaint.UnderReview;
            complaint.Revision++;
            AddComplaintEvent(complaint, Actor.Id, Actor.Role, "MentorResponded", response);
            await uow.SaveChangesAsync(token);

            var admins = await uow.UserAccounts.Query()
                .Where(u => u.Role == Role.Admin && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(token);
            QueueNotifications($"cohort-complaint:{complaint.Id}:mentor-responded:{complaint.Revision}",
                "CohortComplaint", complaint.Id.ToString(),
                admins.Select(id => (id, "Giáo viên đã phản hồi khiếu nại",
                    $"Giáo viên đã gửi phản hồi cho khiếu nại #{complaint.Id}.",
                    "CohortComplaint", (string?)$"/admin/study-courses?complaintId={complaint.Id}")));
            await uow.SaveChangesAsync(token);

            var reloaded = await ComplaintDetails.SingleAsync(x => x.Id == complaint.Id, token);
            result = ComplaintView(reloaded, Actor.Role);
        }, ct);
        return result;
    });

    public Task<ApiResponse<object>> DecideCohortComplaintAsync(
        int complaintId, DecideCohortComplaintRequest request, CancellationToken ct) => Run(async () =>
    {
        Admin();
        Rule(request.Status is CohortComplaint.Resolved or CohortComplaint.Rejected, "COMPLAINT_INVALID_DECISION_STATUS",
            "Trạng thái quyết định phải là Đã giải quyết hoặc Bị từ chối.");
        var decision = request.Decision?.Trim() ?? "";
        Rule(decision.Length is >= 1 and <= 2000, "COMPLAINT_DECISION_REQUIRED",
            "Cần nhập nội dung quyết định (tối đa 2000 ký tự).");

        object result = new { };
        await uow.ExecuteInTransactionAsync(async token =>
        {
            var complaint = await uow.CohortComplaints.Query().Include(x => x.History)
                .SingleOrDefaultAsync(x => x.Id == complaintId, token);
            Rule(complaint is not null, "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", HttpStatusCode.NotFound);
            Rule(complaint!.Status is CohortComplaint.Open or CohortComplaint.UnderReview, "COMPLAINT_ALREADY_DECIDED",
                "Khiếu nại đã có quyết định cuối cùng.", HttpStatusCode.Conflict);
            Rule(!request.ExpectedRevision.HasValue || request.ExpectedRevision == complaint.Revision,
                "COMPLAINT_STALE", "Khiếu nại vừa thay đổi. Hãy tải lại.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            complaint.Status = request.Status;
            complaint.Decision = decision;
            complaint.DecidedById = Actor.Id;
            complaint.DecidedAtUtc = now;
            complaint.Revision++;
            AddComplaintEvent(complaint, Actor.Id, Actor.Role, request.Status, decision);
            await uow.SaveChangesAsync(token);

            var recipients = new List<(int UserId, string Title, string Message, string Type, string? LinkUrl)>
            {
                (
                    complaint.LearnerId,
                    "Kết quả xử lý khiếu nại",
                    $"Khiếu nại #{complaint.Id} đã được xử lý ({(request.Status == CohortComplaint.Resolved ? "Đã giải quyết" : "Bị từ chối")}): {decision}",
                    "CohortComplaintDecision",
                    $"/my-courses/{complaint.CourseId}"
                )
            };
            if (complaint.MentorId.HasValue && complaint.Category != CohortComplaint.CategoryPaymentEnrollment)
            {
                recipients.Add((
                    complaint.MentorId.Value,
                    "Kết quả xử lý khiếu nại",
                    $"Quản trị viên đã đưa ra quyết định cho khiếu nại #{complaint.Id}.",
                    "CohortComplaintDecision",
                    $"/mentor/complaints/{complaint.Id}"
                ));
            }
            QueueNotifications($"cohort-complaint:{complaint.Id}:decided", "CohortComplaint", complaint.Id.ToString(), recipients);
            await uow.SaveChangesAsync(token);

            var reloaded = await ComplaintDetails.SingleAsync(x => x.Id == complaint.Id, token);
            result = ComplaintView(reloaded, Actor.Role);
        }, ct);
        return result;
    });

    public async Task<PrivateVerificationReadResult?> CohortComplaintEvidenceAsync(int complaintId, CancellationToken ct)
    {
        var complaint = await uow.CohortComplaints.Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == complaintId, ct);
        if (complaint is null || string.IsNullOrWhiteSpace(complaint.EvidenceUrl)
            || (Actor.Role != Role.Admin && !(Actor.Role == Role.Learner && complaint.LearnerId == Actor.Id)))
            return null;
        return await storage.OpenPrivateVerificationAsync(complaint.EvidenceUrl, ct);
    }

    public Task<ApiResponse<object>> CourseCompletionSummaryAsync(int courseId, CancellationToken ct) => Run(async () =>
    {
        if (Actor.Role is not (Role.Admin or Role.Learner or Role.Mentor)) throw new UnauthorizedAccessException();
        var course = await uow.StudyCourses.Query().AsNoTracking()
            .Include(x => x.CertificateLevel)
            .Include(x => x.GroupPolicy)
            .Include(x => x.Learner)
            .Include(x => x.Mentor)
            .Include(x => x.Cohort).ThenInclude(c => c!.Mentor)
            .Include(x => x.Sessions).ThenInclude(s => s.Availability).ThenInclude(a => a!.Mentor)
            .Include(x => x.Homework)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == courseId, ct);
        Rule(course is not null, "COURSE_NOT_FOUND", "Không tìm thấy khóa học.", HttpStatusCode.NotFound);

        if (Actor.Role == Role.Learner && course!.LearnerId != Actor.Id)
            throw new UnauthorizedAccessException();
        if (Actor.Role == Role.Learner)
        {
            Rule(HasConfirmedCourseAccess(course!), "PAYMENT_REQUIRED",
                "Vui lòng hoàn tất thanh toán để xem tổng kết khóa học.",
                HttpStatusCode.Forbidden);
        }
        if (Actor.Role == Role.Mentor && !InvolvedMentorIdsOf(course!).Contains(Actor.Id))
            throw new UnauthorizedAccessException();

        var now = DateTime.UtcNow;
        var currentPrimaryMentorId = course!.MentorId ?? course.Cohort?.MentorId;

        var mentorNames = new Dictionary<int, string>();
        var mentorAvatars = new Dictionary<int, string?>();
        void RememberMentor(UserAccount? u)
        {
            if (u is not null && u.Id > 0)
            {
                mentorNames[u.Id] = $"{u.FirstName} {u.LastName}".Trim();
                mentorAvatars[u.Id] = u.AvatarUrl;
            }
        }
        RememberMentor(course.Mentor);
        RememberMentor(course.Cohort?.Mentor);
        foreach (var s in course.Sessions) RememberMentor(s.Availability?.Mentor);

        var orderedSessions = course.Sessions.OrderBy(s => s.Number).ThenBy(s => s.Attempt).ToList();
        var mentorsFromSessions = orderedSessions
            .Select(s => new { Session = s, MentorId = s.Availability?.MentorId ?? currentPrimaryMentorId })
            .Where(x => x.MentorId.HasValue)
            .GroupBy(x => x.MentorId!.Value)
            .Select(g => new
            {
                MentorId = g.Key,
                MentorName = mentorNames.GetValueOrDefault(g.Key, string.Empty),
                MentorAvatarUrl = mentorAvatars.GetValueOrDefault(g.Key),
                IsCurrentPrimary = currentPrimaryMentorId == g.Key,
                IsPrimary = currentPrimaryMentorId == g.Key,
                SessionNumbers = g.Select(x => x.Session.Number).Distinct().OrderBy(n => n).ToList(),
                DeliveredSessionCount = g.Count(x => x.Session.Status == "Present")
            })
            .ToList();

        if (mentorsFromSessions.Count == 0 && currentPrimaryMentorId.HasValue)
        {
            mentorsFromSessions.Add(new
            {
                MentorId = currentPrimaryMentorId.Value,
                MentorName = mentorNames.GetValueOrDefault(currentPrimaryMentorId.Value, string.Empty),
                MentorAvatarUrl = mentorAvatars.GetValueOrDefault(currentPrimaryMentorId.Value),
                IsCurrentPrimary = true,
                IsPrimary = true,
                SessionNumbers = new List<int>(),
                DeliveredSessionCount = 0
            });
        }

        var attendance = orderedSessions.Select(s =>
        {
            var mId = s.Availability?.MentorId ?? currentPrimaryMentorId;
            return new
            {
                SessionId = s.Id,
                s.Number,
                s.Attempt,
                Date = s.ScheduledAt,
                ScheduledAt = s.ScheduledAt,
                EndAt = s.ScheduledAt.AddMinutes(course.SessionMinutes),
                s.Status,
                MentorId = mId,
                MentorName = mId.HasValue ? mentorNames.GetValueOrDefault(mId.Value, string.Empty) : null
            };
        }).ToList();

        var assignments = await uow.HomeworkAssignments.Query().AsNoTracking()
            .Include(h => h.Submission)
            .Where(h => !h.IsDeleted && h.StudyCourseId == course.Id)
            .OrderBy(h => h.DueAt).ThenBy(h => h.Id)
            .ToListAsync(ct);

        var feedbackMentorIds = assignments
            .Where(h => h.PublishedFeedbackAtUtc.HasValue && h.PublishedByMentorId.HasValue && !mentorNames.ContainsKey(h.PublishedByMentorId.Value))
            .Select(h => h.PublishedByMentorId!.Value)
            .Distinct()
            .ToList();
        if (feedbackMentorIds.Count > 0)
        {
            var extraMentors = await uow.UserAccounts.Query().AsNoTracking()
                .Where(u => feedbackMentorIds.Contains(u.Id))
                .ToListAsync(ct);
            foreach (var m in extraMentors) RememberMentor(m);
        }

        var legacyHomework = course.Homework.OrderBy(h => h.DueAt).ThenBy(h => h.Id).ToList();
        var hwAssigned = assignments.Count + legacyHomework.Count;
        var hwSubmitted = assignments.Count(h => h.Submission is { IsDeleted: false })
            + legacyHomework.Count(h => h.SubmittedAt.HasValue);
        var hwGraded = assignments.Count(h => h.PublishedFeedbackAtUtc.HasValue)
            + legacyHomework.Count(h => h.ReviewedAt.HasValue);
        var hwOutstanding = Math.Max(0, hwAssigned - hwSubmitted);

        // Chỉ hiển thị nhận xét đã được Giáo viên công bố; tuyệt đối không lộ nháp AI (AiDraftFeedback/AiDraftScore).
        var homeworkItems = assignments.Select(h => new
        {
            Source = "CohortAssignment",
            HomeworkId = h.Id,
            h.Title,
            h.DueAt,
            Submitted = h.Submission is { IsDeleted: false },
            SubmittedAt = h.Submission is { IsDeleted: false } sub ? (DateTime?)sub.SubmittedAt : null,
            Graded = h.PublishedFeedbackAtUtc.HasValue,
            Passed = h.PublishedFeedbackAtUtc.HasValue ? h.Passed : null,
            Score = h.PublishedFeedbackAtUtc.HasValue ? h.MentorScore : null,
            PublishedFeedback = h.PublishedFeedbackAtUtc.HasValue ? h.MentorFeedback : null,
            h.PublishedFeedbackAtUtc,
            PublishedByMentorId = h.PublishedFeedbackAtUtc.HasValue ? h.PublishedByMentorId : null
        }).Concat(legacyHomework.Select(h => new
        {
            Source = "LegacyCourseHomework",
            HomeworkId = h.Id,
            h.Title,
            DueAt = (DateTime?)h.DueAt,
            Submitted = h.SubmittedAt.HasValue,
            h.SubmittedAt,
            Graded = h.ReviewedAt.HasValue,
            Passed = h.ReviewedAt.HasValue ? h.Passed : null,
            Score = (double?)null,
            PublishedFeedback = h.ReviewedAt.HasValue ? h.Feedback : null,
            PublishedFeedbackAtUtc = h.ReviewedAt,
            PublishedByMentorId = (int?)null
        })).ToList();

        var publishedFeedbackList = assignments
            .Where(h => h.PublishedFeedbackAtUtc.HasValue && !string.IsNullOrWhiteSpace(h.MentorFeedback))
            .Select(h =>
            {
                var mentorId = h.PublishedByMentorId ?? currentPrimaryMentorId;
                return new
                {
                    HomeworkId = h.Id,
                    Title = h.Title,
                    Feedback = h.MentorFeedback!.Trim(),
                    Score = h.MentorScore,
                    Passed = h.Passed,
                    PublishedAtUtc = h.PublishedFeedbackAtUtc!.Value,
                    MentorId = mentorId,
                    MentorName = mentorId.HasValue ? mentorNames.GetValueOrDefault(mentorId.Value, string.Empty) : null,
                    MentorAvatarUrl = mentorId.HasValue ? mentorAvatars.GetValueOrDefault(mentorId.Value) : null
                };
            })
            .Concat(legacyHomework
                .Where(h => h.ReviewedAt.HasValue && !string.IsNullOrWhiteSpace(h.Feedback))
                .Select(h => new
                {
                    HomeworkId = h.Id,
                    Title = h.Title,
                    Feedback = h.Feedback!.Trim(),
                    Score = (double?)null,
                    Passed = h.Passed,
                    PublishedAtUtc = h.ReviewedAt!.Value,
                    MentorId = currentPrimaryMentorId,
                    MentorName = currentPrimaryMentorId.HasValue ? mentorNames.GetValueOrDefault(currentPrimaryMentorId.Value, string.Empty) : null,
                    MentorAvatarUrl = currentPrimaryMentorId.HasValue ? mentorAvatars.GetValueOrDefault(currentPrimaryMentorId.Value) : null
                }))
            .OrderByDescending(f => f.PublishedAtUtc)
            .ToList();

        var retaken = await uow.StudyCourses.Query().AsNoTracking().AnyAsync(x => x.RetakeOfId == courseId, ct);
        var effectivePolicy = course.GroupPolicy ?? (groupPolicies is null
            ? null
            : await groupPolicies.GetEffectiveAsync(course.StartsAt ?? course.Cohort?.StartsAt ?? now, ct));
        var eligibility = CourseRetakePolicy.Evaluate(course, retaken, effectivePolicy, assignments);

        MentorLearnerSkillProfileResponse? skillProgress = null;
        var skillProgressAvailable = false;
        if (skillProfiles is not null)
        {
            try
            {
                var profileRes = await skillProfiles.GetSkillProfileAsync(course.LearnerId, course.CohortId, ct);
                if (profileRes is { IsSuccess: true, Result: not null })
                {
                    skillProgress = profileRes.Result;
                    skillProgressAvailable = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skillProgress = null;
                skillProgressAvailable = false;
            }
        }

        return new
        {
            CourseId = course.Id,
            CourseName = course.Name,
            course.CohortId,
            CohortName = course.Cohort?.Name,
            CohortStatus = course.Cohort?.Status,
            course.SessionType,
            Level = course.CertificateLevel?.Code,
            course.LearnerId,
            LearnerName = $"{course.Learner?.FirstName} {course.Learner?.LastName}".Trim(),
            course.Status,
            course.PaymentStatus,
            course.StartsAt,
            course.EndsAt,
            course.CompletedAt,
            course.TargetExamDate,
            course.DurationMonths,
            course.SessionsPerWeek,
            course.SessionMinutes,
            course.TotalSessions,
            TotalTeachingHours = TotalTeachingHoursOf(course.TotalSessions, course.SessionMinutes),
            CurrentPrimaryMentorId = currentPrimaryMentorId,
            CurrentPrimaryMentorName = currentPrimaryMentorId.HasValue
                ? mentorNames.GetValueOrDefault(currentPrimaryMentorId.Value, string.Empty)
                : null,
            Mentors = mentorsFromSessions,
            Sessions = new
            {
                Total = course.TotalSessions > 0 ? course.TotalSessions : orderedSessions.Count,
                ScheduledCount = orderedSessions.Count,
                Delivered = orderedSessions.Count(s => s.Status == "Present"),
                Absent = orderedSessions.Count(s => s.Status is "Absent" or "ExcusedAbsent"),
                Cancelled = orderedSessions.Count(s => s.Status is "CancelledByMentor" or "CancelledByLearner" or "TransferredOut"),
                Upcoming = orderedSessions.Count(s => s.Status == "Scheduled" && s.ScheduledAt > now),
                PendingAttendance = orderedSessions.Count(s => s.Status == "Scheduled" && s.ScheduledAt <= now)
            },
            Attendance = attendance,
            Homework = new
            {
                Assigned = hwAssigned,
                Submitted = hwSubmitted,
                Graded = hwGraded,
                Outstanding = hwOutstanding,
                Items = homeworkItems
            },
            MentorFeedback = publishedFeedbackList,
            Eligibility = eligibility,
            CompletionPolicy = new
            {
                HomeworkThresholdPercent = effectivePolicy?.RemediationCompletionThresholdPercent ?? course.MinimumHomeworkPercent,
                HomeworkThresholdComparison = effectivePolicy?.RemediationThresholdComparison ?? ">="
            },
            SkillProgressAvailable = skillProgressAvailable,
            SkillProgress = skillProgress,
            IsClassCompleted = course.Cohort is not null
                ? string.Equals(course.Cohort.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                : course.Status == "Completed",
            IsLearnerCompleted = course.Status == "Completed",
            IsCompleted = course.Status == "Completed",
            Certificate = (object?)null,
            Note = InternalCompletionNote
        };
    });
}
