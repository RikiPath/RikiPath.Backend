using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Infrastructure.Repositories;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Options;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;
using RikiPath.Infrastructure;
using Xunit;

namespace RikiPath.Tests
{
    public sealed class MentorScheduleManagementTests
    {
        private static AppDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new AppDbContext(options);
        }

        private static IClaimService MockClaims(Role role, int userId)
        {
            var mock = new Mock<IClaimService>();
            mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });
            return mock.Object;
        }

        private static IMentorSlotGenerator CreateSlotGenerator()
        {
            var options = Options.Create(new MentorSlotGenerationOptions());
            return new MentorSlotGenerator(options);
        }

        [Fact]
        public async Task Approved_mentor_can_create_and_update_schedule_with_rules()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 201, Role = Role.Mentor, FirstName = "Minh", LastName = "Tran", PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 201, VerificationStatus = MentorVerificationStatus.Approved });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 201);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var updateResult = await service.UpdateMyScheduleAsync(new UpdateMentorScheduleRequest
            {
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 1, // Thứ hai
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    },
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 3, // Thứ tư
                        DaypartCode = "Evening",
                        LocalStart = new TimeOnly(18, 0),
                        LocalEnd = new TimeOnly(22, 0),
                        EffectiveFrom = today
                    }
                ]
            });

            Assert.True(updateResult.IsSuccess, updateResult.ErrorMessage);
            Assert.NotNull(updateResult.Result);
            Assert.Equal(201, updateResult.Result.MentorId);
            Assert.Equal(2, updateResult.Result.Rules.Count);
            Assert.Contains(updateResult.Result.Rules, r => r.Weekday == 1 && r.DaypartCode == "Morning");
            Assert.Contains(updateResult.Result.Rules, r => r.Weekday == 3 && r.DaypartCode == "Evening");

            // Kiểm tra truy vấn lại
            var getResult = await service.GetMyScheduleAsync();
            Assert.True(getResult.IsSuccess);
            Assert.Equal(2, getResult.Result!.Rules.Count);
        }

        [Theory]
        [InlineData(MentorVerificationStatus.Unverified)]
        [InlineData(MentorVerificationStatus.Pending)]
        [InlineData(MentorVerificationStatus.Rejected)]
        [InlineData(MentorVerificationStatus.ChangesRequested)]
        public async Task Unapproved_mentor_cannot_preview_or_publish_slots(MentorVerificationStatus status)
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 202, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 202, VerificationStatus = status });

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var schedule = new MentorSchedule
            {
                MentorId = 202,
                PolicyVersion = 3,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRule
                    {
                        Weekday = 1,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    }
                ]
            };
            db.MentorSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 202);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            // 1. Thử xem trước
            var previewResult = await service.PreviewSlotsAsync(new PreviewSlotsRequest
            {
                StartDate = today,
                EndDate = today.AddDays(7)
            });
            Assert.False(previewResult.IsSuccess);
            Assert.Equal(403, previewResult.StatusCode);
            Assert.Contains("chưa được Quản trị viên phê duyệt", previewResult.ErrorMessage);

            // 2. Thử công bố
            var publishResult = await service.PublishSlotsAsync(new PublishSlotsRequest
            {
                StartDate = today,
                EndDate = today.AddDays(7)
            });
            Assert.False(publishResult.IsSuccess);
            Assert.Equal(403, publishResult.StatusCode);
            Assert.Contains("chưa được Quản trị viên phê duyệt", publishResult.ErrorMessage);
        }

        [Fact]
        public async Task Legacy_mentor_without_profile_cannot_bypass_verification_to_publish_slots()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 203, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor); // Không tạo MentorProfile (nhóm legacy unprofiled)
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 203);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            // Legacy mentor không có profile phải bị chặn công bố slot mới
            var publishResult = await service.PublishSlotsAsync(new PublishSlotsRequest
            {
                StartDate = today,
                EndDate = today.AddDays(7)
            });

            Assert.False(publishResult.IsSuccess);
            Assert.Equal(403, publishResult.StatusCode);
            Assert.Contains("chưa được Quản trị viên phê duyệt", publishResult.ErrorMessage);
        }

        [Fact]
        public async Task Learner_cannot_access_mentor_schedule_endpoints()
        {
            await using var db = CreateInMemoryDb();
            var learner = new UserAccount { Id = 301, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(learner);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var learnerClaims = MockClaims(Role.Learner, 301);
            var profileService = new MentorProfileService(uow, learnerClaims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, learnerClaims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var getResult = await service.GetMyScheduleAsync();
            Assert.False(getResult.IsSuccess);
            Assert.Equal(403, getResult.StatusCode);

            var updateResult = await service.UpdateMyScheduleAsync(new UpdateMentorScheduleRequest
            {
                EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            Assert.False(updateResult.IsSuccess);
            Assert.Equal(403, updateResult.StatusCode);
        }

        [Fact]
        public async Task Schedule_rules_validation_fails_for_invalid_weekday_or_time_window()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 204, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 204, VerificationStatus = MentorVerificationStatus.Approved });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 204);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            // 1. Thứ không hợp lệ (Weekday = 7)
            var invalidWeekday = await service.UpdateMyScheduleAsync(new UpdateMentorScheduleRequest
            {
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 7, // Ngoài phạm vi 0-6
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    }
                ]
            });
            Assert.False(invalidWeekday.IsSuccess);
            Assert.Equal(400, invalidWeekday.StatusCode);
            Assert.Contains("Thứ trong tuần phải nằm trong khoảng từ 0", invalidWeekday.ErrorMessage);

            // 2. Giờ kết thúc <= giờ bắt đầu (LocalEnd <= LocalStart)
            var invalidTime = await service.UpdateMyScheduleAsync(new UpdateMentorScheduleRequest
            {
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 1,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(11, 0),
                        LocalEnd = new TimeOnly(7, 0), // Nhỏ hơn giờ bắt đầu
                        EffectiveFrom = today
                    }
                ]
            });
            Assert.False(invalidTime.IsSuccess);
            Assert.Equal(400, invalidTime.StatusCode);
            Assert.Contains("phải sau giờ bắt đầu", invalidTime.ErrorMessage);
        }

        [Fact]
        public async Task Schedule_rules_validation_fails_for_duplicate_or_overlapping_rules_on_same_weekday()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 205, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 205, VerificationStatus = MentorVerificationStatus.Approved });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 205);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            // Trùng lặp quy tắc trên cùng (Weekday, DaypartCode, EffectiveFrom)
            var duplicateResult = await service.UpdateMyScheduleAsync(new UpdateMentorScheduleRequest
            {
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 1,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    },
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 1,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    }
                ]
            });
            Assert.False(duplicateResult.IsSuccess);
            Assert.Equal(400, duplicateResult.StatusCode);
            Assert.Contains("Trùng lặp quy tắc ca làm việc", duplicateResult.ErrorMessage);

            // Xung đột giao nhau thời gian trong cùng một ngày
            var overlapResult = await service.UpdateMyScheduleAsync(new UpdateMentorScheduleRequest
            {
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 2,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(8, 0),
                        LocalEnd = new TimeOnly(12, 0),
                        EffectiveFrom = today
                    },
                    new MentorScheduleRuleRequest
                    {
                        Weekday = 2,
                        DaypartCode = "Afternoon",
                        LocalStart = new TimeOnly(11, 0), // Giao với 08:00-12:00
                        LocalEnd = new TimeOnly(15, 0),
                        EffectiveFrom = today
                    }
                ]
            });
            Assert.False(overlapResult.IsSuccess);
            Assert.Equal(400, overlapResult.StatusCode);
            Assert.Contains("Xung đột thời gian", overlapResult.ErrorMessage);
        }

        [Fact]
        public async Task Slot_generation_produces_deterministic_90min_slots_with_15min_rest()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 206, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 206, VerificationStatus = MentorVerificationStatus.Approved });

            // Tìm ngày Thứ hai trong tương lai
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var nextMonday = today;
            while (nextMonday.DayOfWeek != DayOfWeek.Monday)
                nextMonday = nextMonday.AddDays(1);

            var schedule = new MentorSchedule
            {
                MentorId = 206,
                PolicyVersion = 3,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRule
                    {
                        Weekday = 1, // Thứ hai
                        DaypartCode = "Morning", // 07:00 - 11:00
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    }
                ]
            };
            db.MentorSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 206);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var preview = await service.PreviewSlotsAsync(new PreviewSlotsRequest
            {
                StartDate = nextMonday,
                EndDate = nextMonday
            });

            Assert.True(preview.IsSuccess, preview.ErrorMessage);
            Assert.NotNull(preview.Result);
            // Trong ca Morning 07:00 - 11:00 (240 phút):
            // Slot 1: 07:00 - 08:30 (90 min)
            // Nghỉ: 08:30 - 08:45 (15 min)
            // Slot 2: 08:45 - 10:15 (90 min)
            // Nghỉ: 10:15 - 10:30 (15 min)
            // Tiếp theo 10:30 + 90 min = 12:00 > 11:00 -> Dừng.
            // Tổng cộng đúng 2 slots!
            Assert.Equal(2, preview.Result.TotalCandidateSlots);

            var slot1 = preview.Result.Slots[0];
            var slot2 = preview.Result.Slots[1];

            Assert.Equal("07:00", slot1.LocalStart);
            Assert.Equal("08:30", slot1.LocalEnd);
            Assert.Equal(TimeSpan.FromMinutes(90), slot1.EndUtc - slot1.StartUtc);

            Assert.Equal("08:45", slot2.LocalStart);
            Assert.Equal("10:15", slot2.LocalEnd);
            Assert.Equal(TimeSpan.FromMinutes(90), slot2.EndUtc - slot2.StartUtc);

            // Khoảng nghỉ giữa slot 1 và slot 2 đúng bằng 15 phút
            Assert.Equal(TimeSpan.FromMinutes(15), slot2.StartUtc - slot1.EndUtc);
        }

        [Fact]
        public async Task Publish_slots_is_idempotent_and_creates_zero_duplicates_on_rerun()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 207, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 207, VerificationStatus = MentorVerificationStatus.Approved });

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var nextWednesday = today;
            while (nextWednesday.DayOfWeek != DayOfWeek.Wednesday)
                nextWednesday = nextWednesday.AddDays(1);

            var schedule = new MentorSchedule
            {
                MentorId = 207,
                PolicyVersion = 3,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRule
                    {
                        Weekday = 3, // Thứ tư
                        DaypartCode = "Noon", // 11:00 - 13:00 (1 slot: 11:00 - 12:30)
                        LocalStart = new TimeOnly(11, 0),
                        LocalEnd = new TimeOnly(13, 0),
                        EffectiveFrom = today
                    }
                ]
            };
            db.MentorSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 207);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            // Lần chạy 1: Công bố
            var firstRun = await service.PublishSlotsAsync(new PublishSlotsRequest
            {
                StartDate = nextWednesday,
                EndDate = nextWednesday
            });

            Assert.True(firstRun.IsSuccess, firstRun.ErrorMessage);
            Assert.Equal(1, firstRun.Result!.CreatedCount);
            Assert.Equal(0, firstRun.Result.SkippedExistingCount);

            // Kiểm tra DB có đúng 1 slot
            var countInDb = await db.MentorAvailabilities.CountAsync(x => x.MentorId == 207 && !x.IsDeleted);
            Assert.Equal(1, countInDb);

            // Lần chạy 2: Chạy lại chính xác cùng ngày
            var secondRun = await service.PublishSlotsAsync(new PublishSlotsRequest
            {
                StartDate = nextWednesday,
                EndDate = nextWednesday
            });

            Assert.True(secondRun.IsSuccess, secondRun.ErrorMessage);
            Assert.Equal(0, secondRun.Result!.CreatedCount); // 0 slot mới
            Assert.Equal(1, secondRun.Result.SkippedExistingCount); // 1 slot đã tồn tại được bỏ qua

            // DB vẫn giữ nguyên 1 slot, tuyệt đối không nhân đôi
            var finalCountInDb = await db.MentorAvailabilities.CountAsync(x => x.MentorId == 207 && !x.IsDeleted);
            Assert.Equal(1, finalCountInDb);
        }

        [Fact]
        public async Task Publish_slots_does_not_overwrite_or_alter_booked_slots()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 208, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            var learner = new UserAccount { Id = 308, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(mentor, learner);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 208, VerificationStatus = MentorVerificationStatus.Approved });

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var nextFriday = today;
            while (nextFriday.DayOfWeek != DayOfWeek.Friday)
                nextFriday = nextFriday.AddDays(1);

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            var slotStartUtc = TimeZoneInfo.ConvertTimeToUtc(nextFriday.ToDateTime(new TimeOnly(18, 0)), tz);
            var slotEndUtc = slotStartUtc.AddMinutes(90);

            // Tạo trước một slot đã có người đặt (BookedCount = 1)
            var bookedSlot = new MentorAvailability
            {
                Id = 987,
                MentorId = 208,
                GenerationKey = $"mentor-slot:p3:m208:{nextFriday:yyyy-MM-dd}:1800",
                PolicyVersion = 3,
                LocalBusinessDate = nextFriday,
                StartTime = slotStartUtc,
                EndTime = slotEndUtc,
                BookedCount = 1, // ĐÃ CÓ HỌC VIÊN ĐẶT
                MaxLearners = 1,
                IsApproved = true,
                RoomId = Guid.NewGuid(),
                IsDeleted = false
            };
            db.MentorAvailabilities.Add(bookedSlot);

            var schedule = new MentorSchedule
            {
                MentorId = 208,
                PolicyVersion = 3,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRule
                    {
                        Weekday = 5, // Thứ sáu
                        DaypartCode = "Evening",
                        LocalStart = new TimeOnly(18, 0),
                        LocalEnd = new TimeOnly(22, 0),
                        EffectiveFrom = today
                    }
                ]
            };
            db.MentorSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 208);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            // Chạy công bố
            var publishResult = await service.PublishSlotsAsync(new PublishSlotsRequest
            {
                StartDate = nextFriday,
                EndDate = nextFriday
            });

            Assert.True(publishResult.IsSuccess);

            // Slot đã có booking phải được giữ nguyên hoàn toàn:
            var checkedSlot = await db.MentorAvailabilities.FindAsync(987);
            Assert.NotNull(checkedSlot);
            Assert.Equal(1, checkedSlot.BookedCount);
            Assert.False(checkedSlot.IsDeleted);
        }

        [Fact]
        public async Task DisableSlot_can_soft_delete_unbooked_future_slot_and_fails_for_booked_slot()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount { Id = 209, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(new MentorProfile { MentorId = 209, VerificationStatus = MentorVerificationStatus.Approved });

            var unbookedSlot = new MentorAvailability
            {
                Id = 991,
                MentorId = 209,
                StartTime = DateTime.UtcNow.AddDays(2),
                EndTime = DateTime.UtcNow.AddDays(2).AddMinutes(90),
                BookedCount = 0,
                IsDeleted = false
            };

            var bookedSlot = new MentorAvailability
            {
                Id = 992,
                MentorId = 209,
                StartTime = DateTime.UtcNow.AddDays(2),
                EndTime = DateTime.UtcNow.AddDays(2).AddMinutes(90),
                BookedCount = 1, // ĐÃ CÓ NGƯỜI ĐẶT
                IsDeleted = false
            };

            db.MentorAvailabilities.AddRange(unbookedSlot, bookedSlot);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 209);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            // 1. Hủy slot chưa có người đặt -> THÀNH CÔNG
            var disableUnbooked = await service.DisableSlotAsync(991, new DisableSlotRequest { Reason = "Bận việc đột xuất" });
            Assert.True(disableUnbooked.IsSuccess);
            var reloadedUnbooked = await db.MentorAvailabilities.FindAsync(991);
            Assert.True(reloadedUnbooked!.IsDeleted);
            Assert.Equal("Bận việc đột xuất", reloadedUnbooked.CancelReason);

            // 2. Hủy slot đã có người đặt -> THẤT BẠI (400 BadRequest)
            var disableBooked = await service.DisableSlotAsync(992);
            Assert.False(disableBooked.IsSuccess);
            Assert.Equal(400, disableBooked.StatusCode);
            Assert.Contains("Không thể hủy khung giờ đã có học viên đặt lịch", disableBooked.ErrorMessage);
            var reloadedBooked = await db.MentorAvailabilities.FindAsync(992);
            Assert.False(reloadedBooked!.IsDeleted); // Vẫn an toàn không bị xóa
        }

        [Fact]
        public async Task Learner_available_slots_filters_out_unverified_and_rejected_mentors()
        {
            await using var db = CreateInMemoryDb();
            var approvedMentor = new UserAccount { Id = 210, Role = Role.Mentor, FirstName = "Duyet", LastName = "Nguyen", PasswordHash = [], PasswordSalt = [] };
            var unverifiedMentor = new UserAccount { Id = 211, Role = Role.Mentor, FirstName = "ChuaDuyet", LastName = "Tran", PasswordHash = [], PasswordSalt = [] };
            var rejectedMentor = new UserAccount { Id = 212, Role = Role.Mentor, FirstName = "TuChoi", LastName = "Le", PasswordHash = [], PasswordSalt = [] };
            db.Users.AddRange(approvedMentor, unverifiedMentor, rejectedMentor);

            db.MentorProfiles.AddRange(
                new MentorProfile { MentorId = 210, VerificationStatus = MentorVerificationStatus.Approved },
                new MentorProfile { MentorId = 211, VerificationStatus = MentorVerificationStatus.Unverified },
                new MentorProfile { MentorId = 212, VerificationStatus = MentorVerificationStatus.Rejected }
            );

            var futureStart = DateTime.UtcNow.AddDays(1);
            var futureEnd = futureStart.AddMinutes(90);

            db.MentorAvailabilities.AddRange(
                new MentorAvailability { Id = 1001, MentorId = 210, StartTime = futureStart, EndTime = futureEnd, MaxLearners = 1, BookedCount = 0, IsApproved = true, IsDeleted = false },
                new MentorAvailability { Id = 1002, MentorId = 211, StartTime = futureStart, EndTime = futureEnd, MaxLearners = 1, BookedCount = 0, IsApproved = true, IsDeleted = false },
                new MentorAvailability { Id = 1003, MentorId = 212, StartTime = futureStart, EndTime = futureEnd, MaxLearners = 1, BookedCount = 0, IsApproved = true, IsDeleted = false }
            );
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var availableSlots = await uow.MentorAvailabilities.GetAvailableSlotsAsync(null, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

            // Chỉ slot 1001 của mentor 210 (Approved) mới hiển thị cho learner
            Assert.Single(availableSlots);
            Assert.Equal(1001, availableSlots[0].Id);
            Assert.Equal(210, availableSlots[0].MentorId);
        }

        [Fact]
        public async Task Dev_mentor_seed_only_has_access_if_approved_and_loses_access_if_rejected()
        {
            await using var db = CreateInMemoryDb();
            var devMentor = new UserAccount { Id = 900002, Role = Role.Mentor, Email = "mentor1@rikipath.local", PasswordHash = [], PasswordSalt = [] };
            db.Users.Add(devMentor);
            var devProfile = new MentorProfile { MentorId = 900002, VerificationStatus = MentorVerificationStatus.Approved };
            db.MentorProfiles.Add(devProfile);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var schedule = new MentorSchedule
            {
                MentorId = 900002,
                PolicyVersion = 3,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRule
                    {
                        Weekday = (int)today.DayOfWeek,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    }
                ]
            };
            db.MentorSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claims = MockClaims(Role.Mentor, 900002);
            var profileService = new MentorProfileService(uow, claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uow, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            // Khi dev mentor ở trạng thái Approved -> Được phép xem trước
            var previewApproved = await service.PreviewSlotsAsync(new PreviewSlotsRequest { StartDate = today, EndDate = today });
            Assert.True(previewApproved.IsSuccess);

            // Khi chuyển trạng thái của dev mentor sang Rejected -> Quyền công bố/xem trước lập tức bị thu hồi
            devProfile.VerificationStatus = MentorVerificationStatus.Rejected;
            await db.SaveChangesAsync();

            var previewRejected = await service.PreviewSlotsAsync(new PreviewSlotsRequest { StartDate = today, EndDate = today });
            Assert.False(previewRejected.IsSuccess);
            Assert.Equal(403, previewRejected.StatusCode);
            Assert.Contains("chưa được Quản trị viên phê duyệt", previewRejected.ErrorMessage);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_WhenMentorVerificationIsRevoked_ExcludesSlotsFromLearnerDiscovery()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount
            {
                Id = 209,
                Role = Role.Mentor,
                FirstName = "Tanaka",
                LastName = "Sensei",
                Email = "tanaka@example.com",
                PasswordHash = [],
                PasswordSalt = [],
                IsDeleted = false
            };
            var profile = new MentorProfile
            {
                MentorId = 209,
                Bio = "Experienced tutor",
                Expertise = "JLPT N2",
                VerificationStatus = MentorVerificationStatus.Approved
            };
            mentor.MentorProfile = profile;
            db.Users.Add(mentor);
            db.MentorProfiles.Add(profile);

            var slotStart = DateTime.UtcNow.AddDays(2);
            var slot = new MentorAvailability
            {
                Id = 8801,
                MentorId = 209,
                Mentor = mentor,
                StartTime = slotStart,
                EndTime = slotStart.AddMinutes(90),
                BookedCount = 0,
                MaxLearners = 1,
                IsApproved = true,
                PolicyVersion = 3,
                GenerationKey = "mentor-slot:p3:m209:2026-11-02:0900",
                RoomId = Guid.NewGuid(),
                IsDeleted = false
            };
            db.MentorAvailabilities.Add(slot);
            await db.SaveChangesAsync();

            var repo = new MentorAvailabilityRepository(db);

            // 1. Khi Mentor được Approved -> Slot xuất hiện trong discovery của học viên
            var initialSlots = await repo.GetAvailableSlotsAsync(209, DateTime.UtcNow, DateTime.UtcNow.AddDays(10));
            Assert.Single(initialSlots);

            // 2. Khi Mentor bị thu hồi phê duyệt (chuyển sang Rejected hoặc Suspended)
            profile.VerificationStatus = MentorVerificationStatus.Rejected;
            await db.SaveChangesAsync();

            // Slot ngay lập tức bị ẩn khỏi danh sách tìm kiếm của học viên
            var slotsAfterRevoke = await repo.GetAvailableSlotsAsync(209, DateTime.UtcNow, DateTime.UtcNow.AddDays(10));
            Assert.Empty(slotsAfterRevoke);

            // Truy vấn trực tiếp theo ID cũng bị từ chối
            var slotById = await repo.GetAvailableSlotByIdAsync(8801, DateTime.UtcNow);
            Assert.Null(slotById);
        }

        [Fact]
        public async Task PublishSlotsAsync_WhenDbUpdateExceptionOccursDueToConcurrency_ReturnsConflict()
        {
            await using var db = CreateInMemoryDb();
            var mentor = new UserAccount
            {
                Id = 210,
                Role = Role.Mentor,
                FirstName = "Suzuki",
                LastName = "Sensei",
                Email = "suzuki@example.com",
                PasswordHash = [],
                PasswordSalt = [],
                IsDeleted = false
            };
            var profile = new MentorProfile
            {
                MentorId = 210,
                Bio = "Sensei Suzuki",
                Expertise = "JLPT N1",
                VerificationStatus = MentorVerificationStatus.Approved
            };
            db.Users.Add(mentor);
            db.MentorProfiles.Add(profile);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var schedule = new MentorSchedule
            {
                MentorId = 210,
                PolicyVersion = 3,
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = today,
                Rules =
                [
                    new MentorScheduleRule
                    {
                        Weekday = (int)today.DayOfWeek,
                        DaypartCode = "Morning",
                        LocalStart = new TimeOnly(7, 0),
                        LocalEnd = new TimeOnly(11, 0),
                        EffectiveFrom = today
                    }
                ]
            };
            db.MentorSchedules.Add(schedule);
            await db.SaveChangesAsync();

            var uowMock = new Moq.Mock<IUnitOfWork>();
            var scheduleRepoMock = new Moq.Mock<IMentorScheduleRepository>();
            var availRepoMock = new Moq.Mock<IMentorAvailabilityRepository>();

            scheduleRepoMock.Setup(x => x.GetByMentorIdWithRulesAsync(210, 3, Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync(schedule);
            availRepoMock.Setup(x => x.GetSlotsInRangeAsync(210, Moq.It.IsAny<DateTime>(), Moq.It.IsAny<DateTime>(), Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MentorAvailability>());

            uowMock.Setup(x => x.MentorSchedules).Returns(scheduleRepoMock.Object);
            uowMock.Setup(x => x.MentorAvailabilities).Returns(availRepoMock.Object);
            uowMock.Setup(x => x.ExecuteInTransactionAsync(Moq.It.IsAny<Func<CancellationToken, Task>>(), Moq.It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Duplicate key unique constraint violation on GenerationKey"));

            var claims = MockClaims(Role.Mentor, 210);
            var profileService = new MentorProfileService(new UnitOfWork(db), claims, NullLogger<MentorProfileService>.Instance);
            var slotGen = CreateSlotGenerator();
            var service = new MentorScheduleManagementService(uowMock.Object, claims, profileService, slotGen, NullLogger<MentorScheduleManagementService>.Instance);

            var result = await service.PublishSlotsAsync(new PublishSlotsRequest { StartDate = today, EndDate = today });

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
            Assert.Contains("xung đột khi công bố khung giờ làm việc", result.ErrorMessage);
        }
    }
}
