using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorQuality;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorQuality;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;
using RikiPath.WebApi.Controllers;
using Xunit;

namespace RikiPath.Tests;

// Bộ kiểm thử nghiệp vụ Đánh giá (Reviews), Khiếu nại (Disputes) & Quản lý chất lượng Mentor (Stage 11/13)
public sealed class MentorReviewAndDisputeTests
{
    private static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IClaimService MockClaims(Role role, int userId)
    {
        var mock = new Mock<IClaimService>();
        mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });
        return mock.Object;
    }

    private static async Task<(UserAccount mentor, UserAccount learner, MentorAvailability slot, MentorBooking booking, UserSubscription sub)> SeedSessionScenarioAsync(
        AppDbContext db,
        int mentorId = 201,
        int learnerId = 101,
        double endHoursOffset = -2,
        MentorStatus bookingStatus = MentorStatus.Completed)
    {
        var now = DateTime.UtcNow;

        var mentor = new UserAccount
        {
            Id = mentorId,
            FirstName = "Kenji",
            LastName = "Yamada",
            Email = $"mentor{mentorId}@rikipath.edu.vn",
            Role = Role.Mentor,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(mentor);

        var learner = new UserAccount
        {
            Id = learnerId,
            FirstName = "An",
            LastName = "Nguyen",
            Email = $"learner{learnerId}@rikipath.edu.vn",
            Role = Role.Learner,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(learner);

        var sub = new UserSubscription
        {
            Id = 500 + learnerId,
            UserId = learnerId,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 10,
            MeetingSessionsUsed = 1,
            PurchasedAt = now.AddDays(-5),
            StartDate = now.AddDays(-5),
            EndDate = now.AddDays(10)
        };
        db.UserSubscriptions.Add(sub);

        var slot = new MentorAvailability
        {
            Id = 700 + mentorId,
            MentorId = mentorId,
            RoomId = Guid.NewGuid(),
            StartTime = now.AddHours(endHoursOffset).AddMinutes(-60),
            EndTime = now.AddHours(endHoursOffset),
            IsApproved = true,
            MaxLearners = 1,
            BookedCount = 1
        };
        db.MentorAvailabilities.Add(slot);

        var booking = new MentorBooking
        {
            Id = 900 + learnerId,
            UserSubscriptionId = sub.Id,
            MentorAvailabilityId = slot.Id,
            Status = bookingStatus,
            ScheduledAt = slot.StartTime,
            CompletedAt = bookingStatus == MentorStatus.Completed ? slot.EndTime : null
        };
        db.MentorBookings.Add(booking);

        await db.SaveChangesAsync();
        return (mentor, learner, slot, booking, sub);
    }

    [Fact]
    public async Task Learner_can_create_review_for_completed_session()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var result = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 5,
            Comment = "Mentor hướng dẫn Kaiwa rất nhiệt tình, sửa phát âm tỉ mỉ!"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.Equal(5, result.Result.Rating);
        Assert.Equal(mentor.Id, result.Result.MentorId);
        Assert.Equal(learner.Id, result.Result.LearnerId);
        Assert.True(result.Result.IsPublic);
        Assert.Equal(MentorReviewStatus.Approved, result.Result.ModerationStatus);

        // Kiểm tra database
        var review = await db.MentorReviews.FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id);
        Assert.NotNull(review);
        Assert.Equal(5, review.Rating);

        // Kiểm tra Outbox event
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == booking.Id.ToString() && e.EventType == "ReviewSubmitted");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Review_rejected_if_session_is_cancelled()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db, bookingStatus: MentorStatus.Cancelled);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var result = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 4
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("đã bị hủy", result.ErrorMessage);
    }

    [Fact]
    public async Task Review_rejected_if_session_has_not_completed_and_slot_has_not_ended()
    {
        await using var db = CreateInMemoryDb();
        // Buổi tư vấn trong tương lai (+5 tiếng) và chưa Completed
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: 5, bookingStatus: MentorStatus.Assigned);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var result = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 5
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("sau khi buổi tư vấn đã hoàn thành", result.ErrorMessage);
    }

    [Fact]
    public async Task Review_rejected_if_past_14_day_deadline()
    {
        await using var db = CreateInMemoryDb();
        // Buổi tư vấn hoàn thành 15 ngày trước
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -360); // 15 ngày

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var result = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 5
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("quá thời hạn 14 ngày", result.ErrorMessage);
    }

    [Fact]
    public async Task Duplicate_review_for_same_booking_is_rejected()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        // Gửi review lần 1
        var first = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 5
        });
        Assert.Equal((int)HttpStatusCode.OK, first.StatusCode);

        // Gửi review lần 2 cho cùng booking
        var second = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 4
        });
        Assert.Equal((int)HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("đã được gửi đánh giá trước đó", second.ErrorMessage);
    }

    [Fact]
    public async Task Invalid_rating_value_rejected()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var resLow = await service.CreateReviewAsync(new CreateMentorReviewRequest { MentorBookingId = booking.Id, Rating = 0 });
        Assert.Equal((int)HttpStatusCode.BadRequest, resLow.StatusCode);

        var resHigh = await service.CreateReviewAsync(new CreateMentorReviewRequest { MentorBookingId = booking.Id, Rating = 6 });
        Assert.Equal((int)HttpStatusCode.BadRequest, resHigh.StatusCode);
    }

    [Fact]
    public async Task Unauthorized_learner_cannot_review_others_booking()
    {
        await using var db = CreateInMemoryDb();
        var (_, _, _, booking, _) = await SeedSessionScenarioAsync(db);

        // Học viên ID 999 không sở hữu booking này
        var alienClaims = MockClaims(Role.Learner, 999);
        var service = new MentorReviewService(db, alienClaims, NullLogger<MentorReviewService>.Instance);

        var result = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 5
        });

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Contains("không có quyền", result.ErrorMessage);
    }

    [Fact]
    public async Task Learner_can_update_their_own_review()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var createRes = await service.CreateReviewAsync(new CreateMentorReviewRequest
        {
            MentorBookingId = booking.Id,
            Rating = 4,
            Comment = "Tốt"
        });
        var reviewId = createRes.Result!.Id;

        // Cập nhật lên 5 sao
        var updateRes = await service.UpdateReviewAsync(reviewId, new UpdateMentorReviewRequest
        {
            Rating = 5,
            Comment = "Rất tốt, đã ôn tập thêm từ vựng"
        });

        Assert.Equal((int)HttpStatusCode.OK, updateRes.StatusCode);
        Assert.Equal(5, updateRes.Result!.Rating);
        Assert.Equal("Rất tốt, đã ôn tập thêm từ vựng", updateRes.Result.Comment);
    }

    [Fact]
    public async Task Public_reviews_listing_filters_unapproved_and_non_public_reviews()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        // Thêm 1 review hợp lệ
        db.MentorReviews.Add(new MentorReview
        {
            Id = 1,
            MentorBookingId = booking.Id,
            MentorId = mentor.Id,
            LearnerId = learner.Id,
            Rating = 5,
            Comment = "Hợp lệ",
            IsPublic = true,
            ModerationStatus = MentorReviewStatus.Approved
        });

        // Thêm 1 review bị ẩn
        db.MentorReviews.Add(new MentorReview
        {
            Id = 2,
            MentorBookingId = 9999,
            MentorId = mentor.Id,
            LearnerId = learner.Id,
            Rating = 1,
            Comment = "Bị ẩn",
            IsPublic = false,
            ModerationStatus = MentorReviewStatus.Hidden
        });
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var listRes = await service.GetMentorReviewsAsync(mentor.Id);
        Assert.Equal((int)HttpStatusCode.OK, listRes.StatusCode);
        Assert.Single(listRes.Result!);
        Assert.Equal(1, listRes.Result![0].Id);
    }

    [Fact]
    public async Task Mentor_rating_summary_calculates_average_and_breakdown_correctly()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var ratings = new[] { 5, 5, 4, 3, 1 };
        for (int i = 0; i < ratings.Length; i++)
        {
            db.MentorReviews.Add(new MentorReview
            {
                Id = 10 + i,
                MentorBookingId = 1000 + i,
                MentorId = mentor.Id,
                LearnerId = learner.Id,
                Rating = ratings[i],
                IsPublic = true,
                ModerationStatus = MentorReviewStatus.Approved
            });
        }
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var summary = await service.GetMentorRatingSummaryAsync(mentor.Id);
        Assert.Equal((int)HttpStatusCode.OK, summary.StatusCode);
        Assert.NotNull(summary.Result);
        Assert.Equal(5, summary.Result.TotalReviews);
        Assert.Equal(3.6, summary.Result.AverageRating); // (5+5+4+3+1)/5 = 3.6
        Assert.Equal(2, summary.Result.RatingBreakdown[5]);
        Assert.Equal(1, summary.Result.RatingBreakdown[4]);
        Assert.Equal(1, summary.Result.RatingBreakdown[3]);
        Assert.Equal(0, summary.Result.RatingBreakdown[2]);
        Assert.Equal(1, summary.Result.RatingBreakdown[1]);
        Assert.Equal(60.0, summary.Result.PositiveRatePercent); // (2+1)/5 = 60%
    }

    [Fact]
    public async Task Mentor_can_reply_to_review()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var review = new MentorReview
        {
            Id = 55,
            MentorBookingId = booking.Id,
            MentorId = mentor.Id,
            LearnerId = learner.Id,
            Rating = 5,
            IsPublic = true,
            ModerationStatus = MentorReviewStatus.Approved
        };
        db.MentorReviews.Add(review);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Mentor, mentor.Id);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var res = await service.ReplyReviewAsync(review.Id, new MentorReplyReviewRequest
        {
            Reply = "Cảm ơn bạn đã tin tưởng và chăm chỉ học tập!"
        });

        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(res.Result!.MentorReply);
        Assert.NotNull(res.Result.MentorRepliedAtUtc);

        var updated = await db.MentorReviews.FindAsync(review.Id);
        Assert.NotNull(updated);
        Assert.Equal("Cảm ơn bạn đã tin tưởng và chăm chỉ học tập!", updated.MentorReply);
    }

    [Fact]
    public async Task Admin_can_moderate_review()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var review = new MentorReview
        {
            Id = 77,
            MentorBookingId = booking.Id,
            MentorId = mentor.Id,
            LearnerId = learner.Id,
            Rating = 1,
            IsPublic = true,
            ModerationStatus = MentorReviewStatus.Approved
        };
        db.MentorReviews.Add(review);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var service = new MentorReviewService(db, claims, NullLogger<MentorReviewService>.Instance);

        var res = await service.ModerateReviewAsync(review.Id, new ModerateMentorReviewRequest
        {
            IsPublic = false,
            Status = MentorReviewStatus.Hidden
        });

        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.False(res.Result!.IsPublic);
        Assert.Equal(MentorReviewStatus.Hidden, res.Result.ModerationStatus);
    }

    [Fact]
    public async Task Learner_can_create_dispute_within_48_hours()
    {
        await using var db = CreateInMemoryDb();
        // Buổi tư vấn kết thúc cách đây 5 giờ (trong khoảng 48h)
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -5);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var result = await service.CreateDisputeAsync(new CreateMentorDisputeRequest
        {
            MentorBookingId = booking.Id,
            Category = MentorDisputeCategory.MentorAbsent,
            Reason = "Mentor không xuất hiện trong phòng họp suốt 30 phút đầu",
            EvidenceUrls = "https://storage.rikipath.edu.vn/disputes/evidence1.png"
        });

        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.Equal(MentorDisputeStatus.Submitted, result.Result.Status);
        Assert.Equal(MentorDisputeCategory.MentorAbsent, result.Result.Category);

        // Kiểm tra database
        var dispute = await db.MentorDisputes.FirstOrDefaultAsync(d => d.MentorBookingId == booking.Id);
        Assert.NotNull(dispute);
        Assert.Equal(MentorDisputeStatus.Submitted, dispute.Status);

        // Kiểm tra audit history
        var history = await db.MentorDisputeHistories.FirstOrDefaultAsync(h => h.DisputeId == dispute.Id);
        Assert.NotNull(history);
        Assert.Equal("Created", history.Action);

        // Kiểm tra Outbox event
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == booking.Id.ToString() && e.EventType == "DisputeSubmitted");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Dispute_rejected_if_past_48_hours_from_reference_time()
    {
        await using var db = CreateInMemoryDb();
        // Buổi tư vấn kết thúc 50 giờ trước
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -50);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var result = await service.CreateDisputeAsync(new CreateMentorDisputeRequest
        {
            MentorBookingId = booking.Id,
            Category = MentorDisputeCategory.QualityIssue,
            Reason = "Gửi khiếu nại trễ"
        });

        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("quá thời hạn 48 giờ", result.ErrorMessage);
    }

    [Fact]
    public async Task Duplicate_active_dispute_for_same_booking_rejected()
    {
        await using var db = CreateInMemoryDb();
        var (_, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -5);

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var first = await service.CreateDisputeAsync(new CreateMentorDisputeRequest
        {
            MentorBookingId = booking.Id,
            Reason = "Khiếu nại lần 1"
        });
        Assert.Equal((int)HttpStatusCode.OK, first.StatusCode);

        var second = await service.CreateDisputeAsync(new CreateMentorDisputeRequest
        {
            MentorBookingId = booking.Id,
            Reason = "Khiếu nại lần 2"
        });
        Assert.Equal((int)HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("đã có một đơn khiếu nại đang được xử lý", second.ErrorMessage);
    }

    [Fact]
    public async Task Dispute_detail_includes_attendance_and_cancellation_evidence()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, slot, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -3);

        // Giả lập bản ghi điểm danh Stage 09
        var attendance = new MentorAttendanceRecord
        {
            MentorBookingId = booking.Id,
            RoomId = slot.RoomId,
            UserId = learner.Id,
            Role = "Learner",
            Status = MentorAttendanceStatus.Joined,
            FirstJoinedAtUtc = slot.StartTime.AddMinutes(2),
            LastJoinedAtUtc = slot.StartTime.AddMinutes(2),
            TotalDurationSeconds = 1200,
            ReconnectionCount = 1
        };
        db.MentorAttendanceRecords.Add(attendance);

        var dispute = new MentorDispute
        {
            Id = 301,
            MentorBookingId = booking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Category = MentorDisputeCategory.MediaRoomIssue,
            Status = MentorDisputeStatus.UnderReview,
            Reason = "Mất kết nối nhiều lần do server"
        };
        db.MentorDisputes.Add(dispute);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Learner, learner.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var detailRes = await service.GetDisputeDetailAsync(dispute.Id);
        Assert.Equal((int)HttpStatusCode.OK, detailRes.StatusCode);
        Assert.NotNull(detailRes.Result);
        Assert.Equal(1200, detailRes.Result.AttendanceTotalDurationSeconds);
        Assert.Equal(1, detailRes.Result.AttendanceReconnectionCount);
        Assert.False(detailRes.Result.CreditAlreadyRefunded);
    }

    [Fact]
    public async Task Mentor_can_submit_response_to_active_dispute()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -4);

        var dispute = new MentorDispute
        {
            Id = 401,
            MentorBookingId = booking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Category = MentorDisputeCategory.QualityIssue,
            Status = MentorDisputeStatus.Submitted,
            Reason = "Cần giải trình"
        };
        db.MentorDisputes.Add(dispute);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Mentor, mentor.Id);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var res = await service.MentorRespondDisputeAsync(dispute.Id, new MentorResponseDisputeRequest
        {
            ResponseText = "Tôi đã vào phòng họp đúng giờ và gửi tài liệu ôn tập qua chat"
        });

        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(MentorDisputeStatus.UnderReview, res.Result!.Status);
        Assert.NotNull(res.Result.MentorResponse);

        var history = await db.MentorDisputeHistories.FirstOrDefaultAsync(h => h.DisputeId == dispute.Id && h.Action == "MentorReplied");
        Assert.NotNull(history);
        Assert.Equal("Mentor", history.ActorRole);
    }

    [Fact]
    public async Task Admin_resolve_dispute_with_refund_approves_and_refunds_credit_and_extends()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, sub) = await SeedSessionScenarioAsync(db, endHoursOffset: -4);

        var dispute = new MentorDispute
        {
            Id = 501,
            MentorBookingId = booking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Category = MentorDisputeCategory.MentorAbsent,
            Status = MentorDisputeStatus.UnderReview,
            Reason = "Mentor vắng mặt có bằng chứng"
        };
        db.MentorDisputes.Add(dispute);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        mockCredit.Setup(x => x.RefundCreditAsync(learner.Id, booking.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditRefundResult
            {
                IsSuccess = true,
                RestoredBalance = 1,
                OperationKey = $"refund:dispute:{dispute.Id}"
            });

        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var res = await service.AdminResolveDisputeAsync(dispute.Id, new ResolveMentorDisputeRequest
        {
            ApproveRefund = true,
            AdminDecision = "RefundApproved",
            AdminNotes = "Xác minh Mentor không tham gia, chấp thuận hoàn tín dụng",
            ExtendDays = 7
        });

        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(MentorDisputeStatus.Resolved, res.Result!.Status);
        Assert.True(res.Result.IsRefundApproved);
        Assert.True(res.Result.CreditRefunded);

        var updatedDispute = await db.MentorDisputes.FindAsync(dispute.Id);
        Assert.NotNull(updatedDispute);
        Assert.True(updatedDispute.CreditRefunded);

        var updatedSub = await db.UserSubscriptions.FindAsync(sub.Id);
        Assert.NotNull(updatedSub);
        Assert.True(updatedSub.EndDate >= DateTime.UtcNow.AddDays(6));

        mockCredit.Verify(x => x.RefundCreditAsync(learner.Id, booking.Id, $"refund:dispute:{dispute.Id}", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Kiểm tra Outbox event
        var outbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == dispute.Id.ToString() && e.EventType == "DisputeResolved");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Admin_resolve_dispute_prevents_double_refund_if_already_refunded_in_stage10()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db, endHoursOffset: -4, bookingStatus: MentorStatus.Cancelled);

        // Giả lập booking này đã được hoàn vé ở Stage 10 (hủy sớm hoặc ngoại lệ)
        booking.CancelledAtUtc = DateTime.UtcNow.AddHours(-10);
        booking.CancellationReason = "Học viên hủy trước 24h";
        await db.SaveChangesAsync();

        var dispute = new MentorDispute
        {
            Id = 601,
            MentorBookingId = booking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Category = MentorDisputeCategory.PolicyDispute,
            Status = MentorDisputeStatus.UnderReview,
            Reason = "Yêu cầu kiểm tra"
        };
        db.MentorDisputes.Add(dispute);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var res = await service.AdminResolveDisputeAsync(dispute.Id, new ResolveMentorDisputeRequest
        {
            ApproveRefund = true,
            AdminDecision = "RefundApproved",
            AdminNotes = "Đã hoàn vé trước đó"
        });

        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Result!.IsRefundApproved);

        // Tuyệt đối không gọi hoàn tiền lần 2!
        mockCredit.Verify(x => x.RefundCreditAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("không thực hiện hoàn lại lần 2", res.Result.AdminNotes);
    }

    [Fact]
    public async Task Admin_resolve_dispute_rejects_without_refund()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        var dispute = new MentorDispute
        {
            Id = 701,
            MentorBookingId = booking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Category = MentorDisputeCategory.QualityIssue,
            Status = MentorDisputeStatus.UnderReview,
            Reason = "Khiếu nại không hợp lý"
        };
        db.MentorDisputes.Add(dispute);
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var res = await service.AdminResolveDisputeAsync(dispute.Id, new ResolveMentorDisputeRequest
        {
            ApproveRefund = false,
            AdminDecision = "DisputeRejected",
            AdminNotes = "Mentor đã dạy đủ thời lượng quy định"
        });

        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(MentorDisputeStatus.Rejected, res.Result!.Status);
        Assert.False(res.Result.IsRefundApproved);

        mockCredit.Verify(x => x.RefundCreditAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_get_quality_metrics_aggregates_stats()
    {
        await using var db = CreateInMemoryDb();
        var (mentor, learner, _, booking, _) = await SeedSessionScenarioAsync(db);

        // Review 5 sao
        db.MentorReviews.Add(new MentorReview
        {
            Id = 801,
            MentorBookingId = booking.Id,
            MentorId = mentor.Id,
            LearnerId = learner.Id,
            Rating = 5,
            IsPublic = true,
            ModerationStatus = MentorReviewStatus.Approved
        });

        // 1 Dispute
        db.MentorDisputes.Add(new MentorDispute
        {
            Id = 802,
            MentorBookingId = booking.Id,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            Category = MentorDisputeCategory.QualityIssue,
            Status = MentorDisputeStatus.Resolved,
            IsRefundApproved = true
        });
        await db.SaveChangesAsync();

        var claims = MockClaims(Role.Admin, 1);
        var mockCredit = new Mock<IMentorCreditLedgerService>();
        var service = new MentorDisputeService(db, claims, mockCredit.Object, NullLogger<MentorDisputeService>.Instance);

        var res = await service.AdminGetQualityMetricsAsync();
        Assert.Equal((int)HttpStatusCode.OK, res.StatusCode);
        Assert.Single(res.Result!);

        var metric = res.Result![0];
        Assert.Equal(mentor.Id, metric.MentorId);
        Assert.Equal(1, metric.CompletedSessionsCount);
        Assert.Equal(1, metric.TotalReviewsCount);
        Assert.Equal(5.0, metric.AverageRating);
        Assert.Equal(1, metric.TotalDisputesCount);
        Assert.Equal(1, metric.ResolvedRefundCount);
    }

    [Fact]
    public async Task Controllers_delegate_to_services_and_return_expected_status_codes()
    {
        // 1. Review Controller Test
        var mockReviewService = new Mock<IMentorReviewService>();
        mockReviewService.Setup(x => x.CreateReviewAsync(It.IsAny<CreateMentorReviewRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorReviewResponse>.Success(new MentorReviewResponse { Id = 10 }, HttpStatusCode.OK));
        mockReviewService.Setup(x => x.GetMentorReviewsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<MentorReviewResponse>>.Success(new List<MentorReviewResponse>(), HttpStatusCode.OK));

        var reviewController = new MentorReviewController(mockReviewService.Object);
        var createAction = await reviewController.CreateReview(new CreateMentorReviewRequest { MentorBookingId = 1, Rating = 5 }, CancellationToken.None);
        var createResult = Assert.IsType<ObjectResult>(createAction);
        Assert.Equal(200, createResult.StatusCode);

        var getReviewsAction = await reviewController.GetMentorReviews(201, 1, 10, CancellationToken.None);
        var getReviewsResult = Assert.IsType<ObjectResult>(getReviewsAction);
        Assert.Equal(200, getReviewsResult.StatusCode);

        // 2. Dispute Controller Test
        var mockDisputeService = new Mock<IMentorDisputeService>();
        mockDisputeService.Setup(x => x.CreateDisputeAsync(It.IsAny<CreateMentorDisputeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorDisputeResponse>.Success(new MentorDisputeResponse { Id = 20 }, HttpStatusCode.OK));
        mockDisputeService.Setup(x => x.AdminResolveDisputeAsync(20, It.IsAny<ResolveMentorDisputeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<MentorDisputeResponse>.Success(new MentorDisputeResponse { Id = 20 }, HttpStatusCode.OK));

        var disputeController = new MentorDisputeController(mockDisputeService.Object);
        var disputeAction = await disputeController.CreateDispute(new CreateMentorDisputeRequest { MentorBookingId = 1, Reason = "Lý do" }, CancellationToken.None);
        var disputeResult = Assert.IsType<ObjectResult>(disputeAction);
        Assert.Equal(200, disputeResult.StatusCode);

        var resolveAction = await disputeController.AdminResolve(20, new ResolveMentorDisputeRequest { ApproveRefund = true }, CancellationToken.None);
        var resolveResult = Assert.IsType<ObjectResult>(resolveAction);
        Assert.Equal(200, resolveResult.StatusCode);
    }
}
