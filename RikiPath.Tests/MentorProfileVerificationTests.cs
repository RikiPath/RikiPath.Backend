using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using Xunit;

namespace RikiPath.Tests
{
    public sealed class MentorProfileVerificationTests
    {
        private static AppDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static IClaimService MockClaims(Role role, int userId)
        {
            var mock = new Mock<IClaimService>();
            mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });
            return mock.Object;
        }

        private static IFileStorageService VerifiedStorage()
        {
            var mock = new Mock<IFileStorageService>();
            mock.Setup(storage => storage.CreateSignedReadUrlAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://test.supabase.co/storage/v1/object/sign/mentor-private/test?token=qa");
            mock.Setup(storage => storage.VerifyPrivateVerificationAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(storage => storage.UploadPrivateVerificationAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string _, int ownerId, CancellationToken _) =>
                    new UploadedFileResult(
                        $"https://test.supabase.co/storage/v1/object/authenticated/mentor-private/mentor/{ownerId}/cert.pdf",
                        $"mentor/{ownerId}/cert.pdf", 100));
            return mock.Object;
        }

        [Fact]
        public async Task Learner_application_stays_learner_until_admin_approval()
        {
            await using var db = CreateInMemoryDb();
            db.Users.AddRange(
                new UserAccount { Id = 301, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] },
                new UserAccount { Id = 302, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var storage = VerifiedStorage();
            var applicant = new MentorProfileService(uow, MockClaims(Role.Learner, 301),
                NullLogger<MentorProfileService>.Instance, storage);
            var updated = await applicant.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            {
                Bio = "Hỗ trợ ôn luyện JLPT", Expertise = "JLPT N2", JlptLevel = "N2"
            });
            Assert.True(updated.IsSuccess);
            var uploaded = await applicant.UploadVerificationDocumentAsync(
                new MemoryStream([0x25, 0x50, 0x44, 0x46]), "application/pdf");
            Assert.True(uploaded.IsSuccess);
            Assert.True(uploaded.Result!.HasVerificationDocument);
            Assert.Null(uploaded.Result.GetType().GetProperty("VerificationDocumentUrl"));
            var submitted = await applicant.SubmitVerificationAsync(new SubmitMentorVerificationRequest());
            Assert.True(submitted.IsSuccess);
            Assert.Equal(Role.Learner, db.Users.Single(u => u.Id == 301).Role);
            Assert.False(await applicant.IsMentorEligibleForSchedulingAsync(301));

            var admin = new AdminMentorService(uow, MockClaims(Role.Admin, 302), storage);
            var pending = await admin.GetMentorProfilesAsync(MentorVerificationStatus.Pending, default);
            Assert.Contains(pending.Result!, p => p.MentorId == 301);
            var approved = await admin.ReviewVerificationAsync(301,
                new ReviewMentorVerificationRequest { Decision = MentorVerificationDecision.Approve }, default);
            Assert.True(approved.IsSuccess);
            Assert.Equal(Role.Mentor, db.Users.Single(u => u.Id == 301).Role);
            Assert.True(await applicant.IsMentorEligibleForSchedulingAsync(301));
        }

        [Fact]
        public async Task Client_supplied_public_document_url_is_rejected()
        {
            await using var db = CreateInMemoryDb();
            db.Users.Add(new UserAccount { Id = 303, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] });
            await db.SaveChangesAsync();
            var service = new MentorProfileService(new UnitOfWork(db), MockClaims(Role.Learner, 303),
                NullLogger<MentorProfileService>.Instance, VerifiedStorage());
            var update = await service.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            { VerificationDocumentUrl = "https://public.example/cert.pdf" });
            Assert.Equal(400, update.StatusCode);
            var submit = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest
            { VerificationDocumentUrl = "https://public.example/cert.pdf" });
            Assert.Equal(400, submit.StatusCode);
            Assert.Empty(db.MentorOutboxEvents);
        }

        [Fact]
        public async Task Existing_public_document_cannot_enter_review_without_private_reupload()
        {
            await using var db = CreateInMemoryDb();
            db.Users.Add(new UserAccount { Id = 304, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] });
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 304, Bio = "Ôn luyện JLPT", Expertise = "N2", JlptLevel = "N2",
                VerificationDocumentUrl = "https://public.example/cert.pdf",
                VerificationStatus = MentorVerificationStatus.Unverified
            });
            await db.SaveChangesAsync();
            var storage = new Mock<IFileStorageService>();
            storage.Setup(s => s.VerifyPrivateVerificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("not private"));
            var service = new MentorProfileService(new UnitOfWork(db), MockClaims(Role.Learner, 304),
                NullLogger<MentorProfileService>.Instance, storage.Object);

            var result = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest());

            Assert.Equal(409, result.StatusCode);
            Assert.Equal(MentorVerificationStatus.Unverified, db.MentorProfiles.Single().VerificationStatus);
            Assert.Empty(db.MentorOutboxEvents);
        }

        [Fact]
        public async Task Mentor_can_get_and_update_profile_successfully()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount
            {
                Id = 101,
                Email = "mentor101@rikipath.local",
                FirstName = "Hải",
                LastName = "Nguyễn",
                Role = Role.Mentor,
                PasswordHash = [],
                PasswordSalt = []
            };
            db.Users.Add(mentor);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 101);
            var service = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);

            // 1. Get initial profile -> auto creates Unverified profile
            var getResult = await service.GetMyProfileAsync();
            Assert.True(getResult.IsSuccess);
            Assert.Equal(101, getResult.Result!.MentorId);
            Assert.Equal(MentorVerificationStatus.Unverified, getResult.Result.VerificationStatus);
            Assert.False(getResult.Result.IsBookable);

            // 2. Update profile
            var updateResult = await service.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            {
                Bio = "Giảng viên tiếng Nhật JLPT N1 kinh nghiệm 6 năm",
                Expertise = "Luyện thi JLPT N1, Tiếng Nhật phỏng vấn",
                JlptLevel = "N1",
                YearsOfExperience = 6,
                Workplace = "FPT Software",
                IntroductionVideoUrl = "https://youtube.com/watch?v=intro101"
            });

            Assert.True(updateResult.IsSuccess);
            Assert.Equal("Giảng viên tiếng Nhật JLPT N1 kinh nghiệm 6 năm", updateResult.Result!.Bio);
            Assert.Equal("N1", updateResult.Result.JlptLevel);
            Assert.Equal(6, updateResult.Result.YearsOfExperience);
            Assert.Equal("FPT Software", updateResult.Result.Workplace);
            Assert.False(updateResult.Result.HasVerificationDocument);
        }

        [Fact]
        public async Task Mentor_cannot_submit_verification_if_required_fields_missing()
        {
            await using var db = CreateInMemoryDb();
            db.Users.Add(new UserAccount
            {
                Id = 102,
                Email = "mentor102@rikipath.local",
                FirstName = "Lan",
                LastName = "Trần",
                Role = Role.Mentor,
                PasswordHash = [],
                PasswordSalt = []
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 102);
            var service = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);

            // Nộp khi chưa có bio, expertise, jlpt, cert url
            var result = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest());

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("Vui lòng điền đầy đủ", result.ErrorMessage);
        }

        [Fact]
        public async Task Mentor_can_submit_verification_and_transitions_to_pending()
        {
            await using var db = CreateInMemoryDb();
            db.Users.Add(new UserAccount
            {
                Id = 103,
                Email = "mentor103@rikipath.local",
                FirstName = "Đức",
                LastName = "Lê",
                Role = Role.Mentor,
                PasswordHash = [],
                PasswordSalt = []
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 103);
            var service = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance, VerifiedStorage());

            await service.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            {
                Bio = "Chuyên gia Kaiwa",
                Expertise = "Luyện giao tiếp phản xạ",
                JlptLevel = "N2"
            });

            var uploaded = await service.UploadVerificationDocumentAsync(
                new MemoryStream([0x25, 0x50, 0x44, 0x46]), "application/pdf");
            Assert.True(uploaded.IsSuccess);

            var submitResult = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest
            {
                Notes = "Xin chào Admin, tôi gửi hồ sơ xác minh kèm chứng chỉ N2."
            });

            Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage ?? "Unknown error");
            Assert.Equal(MentorVerificationStatus.Pending, submitResult.Result!.VerificationStatus);
            Assert.NotNull(submitResult.Result.SubmittedAt);
            Assert.Equal("Xin chào Admin, tôi gửi hồ sơ xác minh kèm chứng chỉ N2.", submitResult.Result.ReviewNotes);
        }

        [Fact]
        public async Task Mentor_cannot_submit_verification_twice_when_pending()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount
            {
                Id = 104,
                Role = Role.Mentor,
                PasswordHash = [],
                PasswordSalt = []
            };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 104,
                Bio = "Bio",
                Expertise = "Exp",
                JlptLevel = "N1",
                VerificationDocumentUrl = "https://test.supabase.co/storage/v1/object/authenticated/mentor-private/mentor/108/cert.pdf",
                VerificationStatus = MentorVerificationStatus.Pending
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 104);
            var service = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);

            var result = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest());
            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("đang trong quá trình xét duyệt", result.ErrorMessage);
        }

        [Fact]
        public async Task Admin_cannot_apply_for_mentor_profile()
        {
            await using var db = CreateInMemoryDb();
            db.Users.Add(new UserAccount { Id = 201, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Admin, 201);
            var service = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);

            var getResult = await service.GetMyProfileAsync();
            Assert.False(getResult.IsSuccess);
            Assert.Equal(403, getResult.StatusCode);

            var updateResult = await service.UpdateMyProfileAsync(new UpdateMentorProfileRequest());
            Assert.False(updateResult.IsSuccess);
            Assert.Equal(403, updateResult.StatusCode);

            var submitResult = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest());
            Assert.False(submitResult.IsSuccess);
            Assert.Equal(403, submitResult.StatusCode);
        }

        [Fact]
        public async Task Learner_can_view_public_profile_without_sensitive_verification_data()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount
            {
                Id = 105,
                FirstName = "Hương",
                LastName = "Vũ",
                AvatarUrl = "https://example.com/avatar.jpg",
                Role = Role.Mentor,
                PasswordHash = [],
                PasswordSalt = []
            };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 105,
                Bio = "Giảng viên Tokyo University",
                Expertise = "Kinh doanh quốc tế",
                JlptLevel = "N1",
                YearsOfExperience = 7,
                Workplace = "Tokyo Univ",
                VerificationDocumentUrl = "https://private-bucket/sensitive-cert.pdf",
                IntroductionVideoUrl = "https://youtube.com/watch?v=public-video",
                VerificationStatus = MentorVerificationStatus.Approved,
                ReviewedByAdminName = "Admin System",
                ReviewNotes = "Private internal note"
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var service = new MentorProfileService(uow, MockClaims(Role.Learner, 999), NullLogger<MentorProfileService>.Instance);

            var result = await service.GetPublicProfileAsync(105);

            Assert.True(result.IsSuccess);
            Assert.Equal(105, result.Result!.MentorId);
            Assert.Equal("Hương Vũ", result.Result.Name);
            Assert.Equal("Giảng viên Tokyo University", result.Result.Bio);
            Assert.Equal("N1", result.Result.JlptLevel);
            Assert.Equal("https://youtube.com/watch?v=public-video", result.Result.IntroductionVideoUrl);
            Assert.True(result.Result.IsBookable);

            // Xác minh thuộc tính VerificationDocumentUrl không tồn tại trên PublicMentorProfileResponse
            var property = typeof(RikiPath.Application.Responses.MentorProfile.PublicMentorProfileResponse)
                .GetProperty("VerificationDocumentUrl");
            Assert.Null(property);
        }

        [Fact]
        public async Task Admin_can_approve_verification_and_make_mentor_bookable()
        {
            await using var db = CreateInMemoryDb();
            var admin = new UserAccount { Id = 1, FirstName = "Admin", LastName = "Boss", Role = Role.Admin, PasswordHash = [], PasswordSalt = [] };
            var mentor = new UserAccount { Id = 106, FirstName = "Tuấn", LastName = "Phạm", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(admin, mentor);
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 106,
                Bio = "Bio",
                Expertise = "Exp",
                JlptLevel = "N1",
                VerificationDocumentUrl = "http://doc.pdf",
                VerificationStatus = MentorVerificationStatus.Pending,
                SubmittedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var adminClaims = MockClaims(Role.Admin, 1);
            var adminService = new AdminMentorService(uow, adminClaims, VerifiedStorage());

            var result = await adminService.ReviewVerificationAsync(106, new ReviewMentorVerificationRequest
            {
                Decision = MentorVerificationDecision.Approve,
                ReviewNotes = "Hồ sơ và chứng chỉ N1 hoàn toàn hợp lệ."
            }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(MentorVerificationStatus.Approved, result.Result!.VerificationStatus);
            Assert.True(result.Result.IsBookable);
            Assert.NotNull(result.Result.ReviewedAt);
            Assert.Equal("Admin Boss", result.Result.ReviewedByAdminName);

            // Kiểm tra tính đủ điều kiện lịch
            var profileService = new MentorProfileService(uow, adminClaims, NullLogger<MentorProfileService>.Instance);
            Assert.True(await profileService.IsMentorEligibleForSchedulingAsync(106));

            // Kiểm tra danh sách bookable mentors của học viên có chứa mentor 106
            var meetingService = new MentorMeetingService(uow, Mock.Of<IPaymentGatewayClient>(),
                MockClaims(Role.Learner, 999), new AppSettings(), NullLogger<MentorMeetingService>.Instance,
                Mock.Of<IMentorCreditLedgerService>());
            var bookableList = await meetingService.GetMentorsAsync(default);
            Assert.Contains(bookableList.Result!, x => x.Id == 106);
        }

        [Fact]
        public async Task Admin_can_reject_verification_with_mandatory_reason()
        {
            await using var db = CreateInMemoryDb();
            var admin = new UserAccount { Id = 1, FirstName = "Admin", Role = Role.Admin, PasswordHash = [], PasswordSalt = [] };
            var mentor = new UserAccount { Id = 107, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(admin, mentor);
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 107,
                VerificationStatus = MentorVerificationStatus.Pending
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var adminService = new AdminMentorService(uow, MockClaims(Role.Admin, 1));

            // 1. Từ chối mà không có lý do -> 400
            var noReasonResult = await adminService.ReviewVerificationAsync(107, new ReviewMentorVerificationRequest
            {
                Decision = MentorVerificationDecision.Reject,
                RejectionReason = "   "
            }, default);
            Assert.False(noReasonResult.IsSuccess);
            Assert.Equal(400, noReasonResult.StatusCode);

            // 2. Từ chối có lý do hợp lệ -> Thành công
            var validRejectResult = await adminService.ReviewVerificationAsync(107, new ReviewMentorVerificationRequest
            {
                Decision = MentorVerificationDecision.Reject,
                RejectionReason = "Chứng chỉ JLPT đã hết hạn hoặc không rõ nét."
            }, default);
            Assert.True(validRejectResult.IsSuccess);
            Assert.Equal(MentorVerificationStatus.Rejected, validRejectResult.Result!.VerificationStatus);
            Assert.False(validRejectResult.Result.IsBookable);
            Assert.Equal("Chứng chỉ JLPT đã hết hạn hoặc không rõ nét.", validRejectResult.Result.RejectionReason);

            // Mentor không bookable trong danh sách học viên
            var meetingService = new MentorMeetingService(uow, Mock.Of<IPaymentGatewayClient>(),
                MockClaims(Role.Learner, 999), new AppSettings(), NullLogger<MentorMeetingService>.Instance,
                Mock.Of<IMentorCreditLedgerService>());
            var bookableList = await meetingService.GetMentorsAsync(default);
            Assert.DoesNotContain(bookableList.Result!, x => x.Id == 107);
        }

        [Fact]
        public async Task Admin_can_request_changes_and_mentor_can_resubmit()
        {
            await using var db = CreateInMemoryDb();
            var admin = new UserAccount { Id = 1, FirstName = "Admin", Role = Role.Admin, PasswordHash = [], PasswordSalt = [] };
            var mentor = new UserAccount { Id = 108, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(admin, mentor);
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 108,
                Bio = "Bio ban đầu",
                Expertise = "Exp",
                JlptLevel = "N2",
                VerificationDocumentUrl = "http://doc.pdf",
                VerificationStatus = MentorVerificationStatus.Pending
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var adminService = new AdminMentorService(uow, MockClaims(Role.Admin, 1));

            // Yêu cầu bổ sung thông tin
            var requestChangesResult = await adminService.ReviewVerificationAsync(108, new ReviewMentorVerificationRequest
            {
                Decision = MentorVerificationDecision.RequestChanges,
                ReviewNotes = "Vui lòng cập nhật chi tiết hơn phần kinh nghiệm giảng dạy."
            }, default);

            Assert.True(requestChangesResult.IsSuccess);
            Assert.Equal(MentorVerificationStatus.ChangesRequested, requestChangesResult.Result!.VerificationStatus);
            Assert.False(requestChangesResult.Result.IsBookable);

            // Mentor cập nhật và nộp lại
            var mentorService = new MentorProfileService(uow, MockClaims(Role.Mentor, 108), NullLogger<MentorProfileService>.Instance, VerifiedStorage());
            await mentorService.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            {
                Bio = "Đã cập nhật kinh nghiệm giảng dạy 4 năm tại trung tâm Tokyo."
            });

            var resubmitResult = await mentorService.SubmitVerificationAsync(new SubmitMentorVerificationRequest
            {
                Notes = "Đã bổ sung chi tiết theo yêu cầu của Admin."
            });

            Assert.True(resubmitResult.IsSuccess);
            Assert.Equal(MentorVerificationStatus.Pending, resubmitResult.Result!.VerificationStatus);
        }

        [Fact]
        public async Task Invalid_state_transitions_are_blocked()
        {
            await using var db = CreateInMemoryDb();
            var admin = new UserAccount { Id = 1, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] };
            var mentor = new UserAccount { Id = 109, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(admin, mentor);
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 109,
                VerificationStatus = MentorVerificationStatus.Unverified
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var adminService = new AdminMentorService(uow, MockClaims(Role.Admin, 1));

            // Duyệt khi mentor chưa nộp hồ sơ -> 400
            var result = await adminService.ReviewVerificationAsync(109, new ReviewMentorVerificationRequest
            {
                Decision = MentorVerificationDecision.Approve
            }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("chưa nộp hồ sơ", result.ErrorMessage);
        }

        [Fact]
        public async Task Non_admin_cannot_review_verification()
        {
            await using var db = CreateInMemoryDb();
            var mentor1 = new UserAccount { Id = 110, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            var mentor2 = new UserAccount { Id = 111, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(mentor1, mentor2);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 110, VerificationStatus = MentorVerificationStatus.Pending });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var nonAdminService = new AdminMentorService(uow, MockClaims(Role.Mentor, 111));

            var result = await nonAdminService.ReviewVerificationAsync(110, new ReviewMentorVerificationRequest
            {
                Decision = MentorVerificationDecision.Approve
            }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task Submission_persists_admin_outbox_event_with_profile()
        {
            await using var db = CreateInMemoryDb();
            db.Users.AddRange(
                new UserAccount { Id = 201, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] },
                new UserAccount { Id = 202, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] });
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 201, Bio = "Giới thiệu", Expertise = "JLPT N2", JlptLevel = "N2",
                VerificationDocumentUrl = "https://test.supabase.co/storage/v1/object/authenticated/mentor-private/mentor/201/cert.pdf", VerificationStatus = MentorVerificationStatus.Unverified
            });
            await db.SaveChangesAsync();

            var service = new MentorProfileService(new UnitOfWork(db), MockClaims(Role.Mentor, 201),
                NullLogger<MentorProfileService>.Instance, VerifiedStorage());
            var submitted = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest());

            Assert.True(submitted.IsSuccess);
            Assert.Equal(MentorVerificationStatus.Pending, db.MentorProfiles.Single(x => x.MentorId == 201).VerificationStatus);
            var evt = Assert.Single(db.MentorOutboxEvents);
            Assert.Equal("MentorVerificationSubmitted", evt.EventType);
            Assert.Contains("202", evt.PayloadJson);
            Assert.DoesNotContain("private-cert", evt.PayloadJson);
            var duplicate = await service.SubmitVerificationAsync(new SubmitMentorVerificationRequest());
            Assert.False(duplicate.IsSuccess);
            Assert.Single(db.MentorOutboxEvents);
        }

        [Fact]
        public async Task Review_requires_pending_and_persists_single_outbox_event()
        {
            await using var db = CreateInMemoryDb();
            db.Users.AddRange(
                new UserAccount { Id = 211, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] },
                new UserAccount { Id = 212, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] },
                new UserAccount { Id = 213, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] });
            db.MentorProfiles.Add(new MentorProfile { MentorId = 211, VerificationStatus = MentorVerificationStatus.Pending });
            await db.SaveChangesAsync();

            db.MentorProfiles.Single().VerificationDocumentUrl =
                "https://test.supabase.co/storage/v1/object/authenticated/mentor-private/mentor/211/cert.pdf";
            await db.SaveChangesAsync();
            var service = new AdminMentorService(new UnitOfWork(db), MockClaims(Role.Admin, 212), VerifiedStorage());
            var reviewed = await service.ReviewVerificationAsync(211,
                new ReviewMentorVerificationRequest { Decision = MentorVerificationDecision.Approve }, default);
            Assert.True(reviewed.IsSuccess);
            var evt = Assert.Single(db.MentorOutboxEvents);
            Assert.Equal("MentorVerificationReviewed", evt.EventType);
            Assert.Contains("213", evt.PayloadJson);
            var repeated = await service.ReviewVerificationAsync(211,
                new ReviewMentorVerificationRequest { Decision = MentorVerificationDecision.Reject, RejectionReason = "Không hợp lệ" }, default);
            Assert.False(repeated.IsSuccess);
            Assert.Equal(409, repeated.StatusCode);
            Assert.Single(db.MentorOutboxEvents);
        }

        [Fact]
        public async Task Pending_or_approved_mentor_cannot_replace_unreviewed_certificate()
        {
            await using var db = CreateInMemoryDb();
            db.Users.Add(new UserAccount { Id = 221, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] });
            db.MentorProfiles.Add(new MentorProfile
            {
                MentorId = 221, VerificationStatus = MentorVerificationStatus.Pending,
                VerificationDocumentUrl = "https://example.com/original", JlptLevel = "N2"
            });
            await db.SaveChangesAsync();
            var service = new MentorProfileService(new UnitOfWork(db), MockClaims(Role.Mentor, 221),
                NullLogger<MentorProfileService>.Instance);
            var pending = await service.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            { VerificationDocumentUrl = "https://example.com/replaced" });
            Assert.Equal(409, pending.StatusCode);
            db.MentorProfiles.Single().VerificationStatus = MentorVerificationStatus.Approved;
            await db.SaveChangesAsync();
            var approved = await service.UpdateMyProfileAsync(new UpdateMentorProfileRequest
            { VerificationDocumentUrl = "https://example.com/replaced" });
            Assert.Equal(409, approved.StatusCode);
            Assert.Equal("https://example.com/original", db.MentorProfiles.Single().VerificationDocumentUrl);
        }
    }
}
