//using System.Net;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using RikiPath.Application.IServices;
//using RikiPath.Application.Requests.MentorQuality;
//using RikiPath.Application.Responses;
//using RikiPath.Application.Responses.MentorQuality;
//using RikiPath.Domain.Entities;
//using RikiPath.Domain.Enums;
//using RikiPath.Infrastructure;

//namespace RikiPath.Infrastructure.Services;

//public sealed class MentorReviewService : IMentorReviewService
//{
//    private readonly AppDbContext _context;
//    private readonly IClaimService _claimService;
//    private readonly ILogger<MentorReviewService> _logger;

//    public MentorReviewService(
//        AppDbContext context,
//        IClaimService claimService,
//        ILogger<MentorReviewService> logger)
//    {
//        _context = context;
//        _claimService = claimService;
//        _logger = logger;
//    }

//    public async Task<ApiResponse<MentorReviewResponse>> CreateReviewAsync(
//        CreateMentorReviewRequest request,
//        CancellationToken cancellationToken = default)
//    {
//        if (request.MentorBookingId <= 0)
//            return ApiResponse<MentorReviewResponse>.Fail("Mã lịch hẹn không hợp lệ.");

//        if (request.Rating < 1 || request.Rating > 5)
//            return ApiResponse<MentorReviewResponse>.Fail("Điểm đánh giá phải nằm trong khoảng từ 1 đến 5 sao.");
//        if (request.Comment?.Length > 1000)
//            return ApiResponse<MentorReviewResponse>.Fail("Nhận xét tối đa 1000 ký tự.");

//        try
//        {
//            var claim = _claimService.GetUserClaim();

//            var booking = await _context.MentorBookings
//                .Include(b => b.UserSubscription)
//                .Include(b => b.MentorAvailability)
//                    .ThenInclude(a => a!.Mentor)
//                .FirstOrDefaultAsync(b => b.Id == request.MentorBookingId && !b.IsDeleted, cancellationToken);

//            if (booking is null)
//                return ApiResponse<MentorReviewResponse>.Fail("Không tìm thấy thông tin lịch hẹn tư vấn.", HttpStatusCode.NotFound);

//            // Kiểm tra quyền sở hữu lịch hẹn
//            var isOwner = booking.UserSubscription != null && !booking.UserSubscription.IsDeleted &&
//                booking.UserSubscription.UserId == claim.Id && booking.UserSubscription.PaymentStatus == PaymentStatus.Paid;
//            if (!isOwner || claim.Role != Role.Learner)
//                return ApiResponse<MentorReviewResponse>.Fail("Bạn không có quyền đánh giá lịch hẹn này.", HttpStatusCode.Forbidden);

//            // Không được đánh giá lịch hẹn đã hủy
//            if (booking.Status == MentorStatus.Cancelled)
//                return ApiResponse<MentorReviewResponse>.Fail("Không thể đánh giá lịch hẹn đã bị hủy.", HttpStatusCode.Conflict);

//            var nowUtc = DateTime.UtcNow;
//            var slot = booking.MentorAvailability;

//            // Kiểm tra điều kiện hoàn thành buổi tư vấn
//            var isCompleted = booking.Status == MentorStatus.Completed ||
//                (slot != null && slot.EndTime <= nowUtc && booking.AttendedAt.HasValue &&
//                    booking.Status is MentorStatus.Assigned or MentorStatus.Accepted);
//            if (!isCompleted)
//                return ApiResponse<MentorReviewResponse>.Fail("Chỉ có thể đánh giá sau khi buổi tư vấn đã hoàn thành.", HttpStatusCode.Conflict);

//            // Kiểm tra thời hạn gửi đánh giá (trong vòng 14 ngày sau khi buổi tư vấn kết thúc)
//            var completionTime = booking.CompletedAt ?? slot?.EndTime ?? nowUtc;
//            if ((nowUtc - completionTime).TotalDays > 14)
//                return ApiResponse<MentorReviewResponse>.Fail("Đã quá thời hạn 14 ngày để gửi đánh giá sau khi buổi tư vấn kết thúc.", HttpStatusCode.Conflict);

//            // Chống trùng lặp đánh giá cho cùng một booking
//            var existingReview = await _context.MentorReviews
//                .AnyAsync(r => r.MentorBookingId == request.MentorBookingId, cancellationToken);
//            if (existingReview)
//                return ApiResponse<MentorReviewResponse>.Fail("Lịch hẹn này đã được gửi đánh giá trước đó.", HttpStatusCode.Conflict);

//            var mentorId = slot?.MentorId ?? 0;
//            if (mentorId <= 0)
//                return ApiResponse<MentorReviewResponse>.Fail("Lịch hẹn không có thông tin Chuyên gia hợp lệ.", HttpStatusCode.BadRequest);

//            var review = new MentorReview
//            {
//                MentorBookingId = booking.Id,
//                MentorId = mentorId,
//                LearnerId = claim.Id,
//                Rating = request.Rating,
//                Comment = request.Comment?.Trim(),
//                IsPublic = false,
//                ModerationStatus = MentorReviewStatus.Approved,
//                CreatedAtUtc = nowUtc,
//                UpdatedAtUtc = nowUtc
//            };

//            _context.MentorReviews.Add(review);

//            await _context.SaveChangesAsync(cancellationToken);

//            var learner = await _context.Users.FindAsync([claim.Id], cancellationToken);
//            var mentor = await _context.Users.FindAsync([mentorId], cancellationToken);

//            var response = new MentorReviewResponse
//            {
//                Id = review.Id,
//                MentorBookingId = review.MentorBookingId,
//                MentorId = review.MentorId,
//                MentorName = mentor != null ? $"{mentor.FirstName} {mentor.LastName}".Trim() : string.Empty,
//                LearnerId = review.LearnerId,
//                LearnerName = learner != null ? $"{learner.FirstName} {learner.LastName}".Trim() : string.Empty,
//                Rating = review.Rating,
//                Comment = review.Comment,
//                IsPublic = review.IsPublic,
//                ModerationStatus = review.ModerationStatus,
//                CreatedAtUtc = review.CreatedAtUtc,
//                UpdatedAtUtc = review.UpdatedAtUtc
//            };

//            return ApiResponse<MentorReviewResponse>.Success(response, HttpStatusCode.OK, "Đánh giá buổi tư vấn thành công.");
//        }
//        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
//            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_MentorReviews_MentorBookingId" })
//        {
//            return ApiResponse<MentorReviewResponse>.Fail("Lịch hẹn này đã được gửi đánh giá trước đó.", HttpStatusCode.Conflict);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi khi tạo đánh giá cho lịch hẹn {BookingId}: {Message}", request.MentorBookingId, ex.Message);
//            return ApiResponse<MentorReviewResponse>.Fail("Không thể gửi đánh giá cho buổi tư vấn.", HttpStatusCode.InternalServerError);
//        }
//    }

//    public async Task<ApiResponse<MentorReviewResponse>> UpdateReviewAsync(
//        int reviewId,
//        UpdateMentorReviewRequest request,
//        CancellationToken cancellationToken = default)
//    {
//        if (reviewId <= 0)
//            return ApiResponse<MentorReviewResponse>.Fail("Mã đánh giá không hợp lệ.");

//        if (request.Rating < 1 || request.Rating > 5)
//            return ApiResponse<MentorReviewResponse>.Fail("Điểm đánh giá phải nằm trong khoảng từ 1 đến 5 sao.");
//        if (request.Comment?.Length > 1000)
//            return ApiResponse<MentorReviewResponse>.Fail("Nhận xét tối đa 1000 ký tự.");

//        try
//        {
//            var claim = _claimService.GetUserClaim();

//            if (claim.Role is not (Role.Admin or Role.Learner))
//                return ApiResponse<MentorReviewResponse>.Fail("Bạn không có quyền chỉnh sửa phản hồi.", HttpStatusCode.Forbidden);

//            var review = await _context.MentorReviews.AsTracking()
//                .Include(r => r.Mentor)
//                .Include(r => r.Learner)
//                .FirstOrDefaultAsync(r => r.Id == reviewId && (claim.Role == Role.Admin || r.LearnerId == claim.Id), cancellationToken);

//            if (review is null)
//                return ApiResponse<MentorReviewResponse>.Fail("Không tìm thấy thông tin đánh giá.", HttpStatusCode.NotFound);

//            review.Rating = request.Rating;
//            review.Comment = request.Comment?.Trim();
//            review.IsPublic = false;
//            review.UpdatedAtUtc = DateTime.UtcNow;

//            await _context.SaveChangesAsync(cancellationToken);

//            var response = new MentorReviewResponse
//            {
//                Id = review.Id,
//                MentorBookingId = review.MentorBookingId,
//                MentorId = review.MentorId,
//                MentorName = $"{review.Mentor.FirstName} {review.Mentor.LastName}".Trim(),
//                LearnerId = review.LearnerId,
//                LearnerName = $"{review.Learner.FirstName} {review.Learner.LastName}".Trim(),
//                Rating = review.Rating,
//                Comment = review.Comment,
//                IsPublic = review.IsPublic,
//                ModerationStatus = review.ModerationStatus,
//                MentorReply = review.MentorReply,
//                MentorRepliedAtUtc = review.MentorRepliedAtUtc,
//                CreatedAtUtc = review.CreatedAtUtc,
//                UpdatedAtUtc = review.UpdatedAtUtc
//            };

//            return ApiResponse<MentorReviewResponse>.Success(response, HttpStatusCode.OK, "Cập nhật đánh giá thành công.");
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi khi cập nhật đánh giá {ReviewId}: {Message}", reviewId, ex.Message);
//            return ApiResponse<MentorReviewResponse>.Fail("Không thể cập nhật đánh giá.", HttpStatusCode.InternalServerError);
//        }
//    }

//    public async Task<ApiResponse<List<MentorReviewResponse>>> GetMentorReviewsAsync(
//        int mentorId,
//        int page = 1,
//        int pageSize = 10,
//        CancellationToken cancellationToken = default)
//    {
//        if (mentorId <= 0)
//            return ApiResponse<List<MentorReviewResponse>>.Fail("Cố vấn được chọn không hợp lệ.");

//        if (page < 1) page = 1;
//        if (pageSize < 1 || pageSize > 50) pageSize = 10;

//        try
//        {
//            var actor = _claimService.GetUserClaim();
//            if (actor.Role is not (Role.Admin or Role.Learner))
//                return ApiResponse<List<MentorReviewResponse>>.Fail("Bạn không có quyền xem phản hồi.", HttpStatusCode.Forbidden);
//            var query = _context.MentorReviews.AsNoTracking()
//                .Where(r => r.MentorId == mentorId && (actor.Role == Role.Admin || r.LearnerId == actor.Id))
//                .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id);

//            var reviews = await query
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .Select(r => new MentorReviewResponse
//                {
//                    Id = r.Id,
//                    MentorBookingId = r.MentorBookingId,
//                    MentorId = r.MentorId,
//                    MentorName = (r.Mentor.FirstName + " " + r.Mentor.LastName).Trim(),
//                    LearnerId = r.LearnerId,
//                    LearnerName = (r.Learner.FirstName + " " + r.Learner.LastName).Trim(),
//                    Rating = r.Rating,
//                    Comment = r.Comment,
//                    IsPublic = r.IsPublic,
//                    ModerationStatus = r.ModerationStatus,
//                    MentorReply = r.MentorReply,
//                    MentorRepliedAtUtc = r.MentorRepliedAtUtc,
//                    CreatedAtUtc = r.CreatedAtUtc,
//                    UpdatedAtUtc = r.UpdatedAtUtc
//                })
//                .ToListAsync(cancellationToken);

//            return ApiResponse<List<MentorReviewResponse>>.Success(reviews);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi khi lấy danh sách đánh giá của Mentor {MentorId}: {Message}", mentorId, ex.Message);
//            return ApiResponse<List<MentorReviewResponse>>.Fail("Không thể tải danh sách đánh giá.", HttpStatusCode.InternalServerError);
//        }
//    }

//    public async Task<ApiResponse<MentorRatingSummaryResponse>> GetMentorRatingSummaryAsync(
//        int mentorId,
//        CancellationToken cancellationToken = default)
//    {
//        if (mentorId <= 0)
//            return ApiResponse<MentorRatingSummaryResponse>.Fail("Cố vấn được chọn không hợp lệ.");

//        try
//        {
//            var ratings = await _context.MentorReviews
//                .Where(r => r.MentorId == mentorId && r.ModerationStatus == MentorReviewStatus.Approved)
//                .Select(r => r.Rating)
//                .ToListAsync(cancellationToken);

//            var total = ratings.Count;
//            var avg = total > 0 ? Math.Round(ratings.Average(), 1) : 0.0;

//            var breakdown = new Dictionary<int, int>
//            {
//                { 5, ratings.Count(r => r == 5) },
//                { 4, ratings.Count(r => r == 4) },
//                { 3, ratings.Count(r => r == 3) },
//                { 2, ratings.Count(r => r == 2) },
//                { 1, ratings.Count(r => r == 1) }
//            };

//            var positiveCount = breakdown[5] + breakdown[4];
//            var positiveRate = total > 0 ? Math.Round((double)positiveCount * 100 / total, 1) : 0.0;

//            var response = new MentorRatingSummaryResponse
//            {
//                MentorId = mentorId,
//                TotalReviews = total,
//                AverageRating = avg,
//                RatingBreakdown = breakdown,
//                PositiveRatePercent = positiveRate
//            };

//            return ApiResponse<MentorRatingSummaryResponse>.Success(response);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi khi tổng hợp xếp hạng Mentor {MentorId}: {Message}", mentorId, ex.Message);
//            return ApiResponse<MentorRatingSummaryResponse>.Fail("Không thể tải thông tin xếp hạng.", HttpStatusCode.InternalServerError);
//        }
//    }

//    public async Task<ApiResponse<MentorReviewResponse>> ReplyReviewAsync(
//        int reviewId,
//        MentorReplyReviewRequest request,
//        CancellationToken cancellationToken = default)
//    {
//        if (reviewId <= 0)
//            return ApiResponse<MentorReviewResponse>.Fail("Mã đánh giá không hợp lệ.");

//        if (string.IsNullOrWhiteSpace(request.Reply))
//            return ApiResponse<MentorReviewResponse>.Fail("Nội dung phản hồi không được để trống.");
//        if (request.Reply.Length > 1000)
//            return ApiResponse<MentorReviewResponse>.Fail("Nội dung phản hồi tối đa 1000 ký tự.");

//        try
//        {
//            var claim = _claimService.GetUserClaim();
//            if (claim.Role != Role.Admin)
//                return ApiResponse<MentorReviewResponse>.Fail("Bạn không có quyền xem phản hồi.", HttpStatusCode.Forbidden);

//            var review = await _context.MentorReviews.AsTracking()
//                .Include(r => r.Mentor)
//                .Include(r => r.Learner)
//                .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);

//            if (review is null)
//                return ApiResponse<MentorReviewResponse>.Fail("Không tìm thấy thông tin đánh giá.", HttpStatusCode.NotFound);

//            review.MentorReply = request.Reply.Trim();
//            review.MentorRepliedAtUtc = DateTime.UtcNow;
//            review.UpdatedAtUtc = DateTime.UtcNow;

//            await _context.SaveChangesAsync(cancellationToken);

//            var response = new MentorReviewResponse
//            {
//                Id = review.Id,
//                MentorBookingId = review.MentorBookingId,
//                MentorId = review.MentorId,
//                MentorName = $"{review.Mentor.FirstName} {review.Mentor.LastName}".Trim(),
//                LearnerId = review.LearnerId,
//                LearnerName = $"{review.Learner.FirstName} {review.Learner.LastName}".Trim(),
//                Rating = review.Rating,
//                Comment = review.Comment,
//                IsPublic = review.IsPublic,
//                ModerationStatus = review.ModerationStatus,
//                MentorReply = review.MentorReply,
//                MentorRepliedAtUtc = review.MentorRepliedAtUtc,
//                CreatedAtUtc = review.CreatedAtUtc,
//                UpdatedAtUtc = review.UpdatedAtUtc
//            };

//            return ApiResponse<MentorReviewResponse>.Success(response, HttpStatusCode.OK, "Phản hồi đánh giá thành công.");
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi khi phản hồi đánh giá {ReviewId}: {Message}", reviewId, ex.Message);
//            return ApiResponse<MentorReviewResponse>.Fail("Không thể gửi phản hồi đánh giá.", HttpStatusCode.InternalServerError);
//        }
//    }

//    public async Task<ApiResponse<MentorReviewResponse>> ModerateReviewAsync(
//        int reviewId,
//        ModerateMentorReviewRequest request,
//        CancellationToken cancellationToken = default)
//    {
//        if (reviewId <= 0)
//            return ApiResponse<MentorReviewResponse>.Fail("Mã đánh giá không hợp lệ.");

//        try
//        {
//            var claim = _claimService.GetUserClaim();
//            if (claim.Role != Role.Admin)
//                return ApiResponse<MentorReviewResponse>.Fail("Chỉ Quản trị viên mới có quyền kiểm duyệt đánh giá.", HttpStatusCode.Forbidden);

//            var review = await _context.MentorReviews.AsTracking()
//                .Include(r => r.Mentor)
//                .Include(r => r.Learner)
//                .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);

//            if (review is null)
//                return ApiResponse<MentorReviewResponse>.Fail("Không tìm thấy thông tin đánh giá.", HttpStatusCode.NotFound);

//            review.IsPublic = false;
//            review.ModerationStatus = request.Status;
//            review.UpdatedAtUtc = DateTime.UtcNow;

//            await _context.SaveChangesAsync(cancellationToken);

//            var response = new MentorReviewResponse
//            {
//                Id = review.Id,
//                MentorBookingId = review.MentorBookingId,
//                MentorId = review.MentorId,
//                MentorName = $"{review.Mentor.FirstName} {review.Mentor.LastName}".Trim(),
//                LearnerId = review.LearnerId,
//                LearnerName = $"{review.Learner.FirstName} {review.Learner.LastName}".Trim(),
//                Rating = review.Rating,
//                Comment = review.Comment,
//                IsPublic = review.IsPublic,
//                ModerationStatus = review.ModerationStatus,
//                MentorReply = review.MentorReply,
//                MentorRepliedAtUtc = review.MentorRepliedAtUtc,
//                CreatedAtUtc = review.CreatedAtUtc,
//                UpdatedAtUtc = review.UpdatedAtUtc
//            };

//            return ApiResponse<MentorReviewResponse>.Success(response, HttpStatusCode.OK, "Kiểm duyệt đánh giá thành công.");
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi khi kiểm duyệt đánh giá {ReviewId}: {Message}", reviewId, ex.Message);
//            return ApiResponse<MentorReviewResponse>.Fail("Không thể kiểm duyệt đánh giá.", HttpStatusCode.InternalServerError);
//        }
//    }
//}
