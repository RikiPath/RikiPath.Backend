using Microsoft.AspNetCore.SignalR;

using RikiPath.Application.Hubs;

using System;

using System.Collections.Generic;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;

using Moq;

using RikiPath.Application;

using RikiPath.Application.IServices;

using RikiPath.Application.Responses;

using RikiPath.Application.Services;

using RikiPath.Domain.Entities;

using RikiPath.Domain.Enums;

using RikiPath.Infrastructure;

using RikiPath.Infrastructure.Repositories;

using RikiPath.Infrastructure.Services;

using Xunit;

namespace RikiPath.Tests

{

    public class DeviceTokenAndFcmPushTests

    {

        private static AppDbContext CreateInMemoryDbContext(string dbName)

        {

            var options = new DbContextOptionsBuilder<AppDbContext>()

                .UseInMemoryDatabase(databaseName: dbName)

                .Options;

            return new AppDbContext(options);

        }

        private static async Task<UserAccount> SeedUserAsync(AppDbContext context, int userId, string email = "user@test.com")

        {

            var user = new UserAccount

            {

                Id = userId,

                Email = email,

                FirstName = "Test",

                LastName = "User",

                PasswordHash = new byte[32],

                PasswordSalt = new byte[16],

                Role = Role.Learner,

                SystemNotificationsEnabled = true,

                CreatedDate = DateTime.UtcNow

            };

            context.Users.Add(user);

            await context.SaveChangesAsync();

            return user;

        }

        [Fact]

        public async Task RegisterToken_NewDevice_CreatesActiveToken()

        {

            var db = CreateInMemoryDbContext(nameof(RegisterToken_NewDevice_CreatesActiveToken));

            await SeedUserAsync(db, 101);

            var repo = new UserDeviceTokenRepository(db);

            var token = await repo.RegisterOrUpdateTokenAsync(

                userId: 101,

                deviceIdentifier: "dev-uuid-1",

                platform: "Android",

                fcmToken: "fcm-token-alpha");

            Assert.NotNull(token);

            Assert.True(token.Id > 0);

            Assert.Equal(101, token.UserId);

            Assert.Equal("dev-uuid-1", token.DeviceIdentifier);

            Assert.Equal("Android", token.Platform);

            Assert.Equal("fcm-token-alpha", token.FcmToken);

            Assert.True(token.IsActive);

            var activeList = await repo.GetActiveTokensByUserIdAsync(101);

            Assert.Single(activeList);

            Assert.Equal("fcm-token-alpha", activeList[0].FcmToken);

        }

        [Fact]

        public async Task RegisterToken_TokenRotation_UpdatesExistingDeviceToken()

        {

            var db = CreateInMemoryDbContext(nameof(RegisterToken_TokenRotation_UpdatesExistingDeviceToken));

            await SeedUserAsync(db, 102);

            var repo = new UserDeviceTokenRepository(db);

            // First registration

            await repo.RegisterOrUpdateTokenAsync(102, "phone-1", "Android", "token-old");

            // Token refreshed / rotated on same device

            var updated = await repo.RegisterOrUpdateTokenAsync(102, "phone-1", "Android", "token-new");

            Assert.Equal("token-new", updated.FcmToken);

            Assert.True(updated.IsActive);

            var activeList = await repo.GetActiveTokensByUserIdAsync(102);

            Assert.Single(activeList);

            Assert.Equal("token-new", activeList[0].FcmToken);

        }

        [Fact]

        public async Task RegisterToken_CrossUserIsolation_DeactivatesPreviousUserToken()

        {

            var db = CreateInMemoryDbContext(nameof(RegisterToken_CrossUserIsolation_DeactivatesPreviousUserToken));

            await SeedUserAsync(db, 201, "usera@test.com");

            await SeedUserAsync(db, 202, "userb@test.com");

            var repo = new UserDeviceTokenRepository(db);

            // User A registers on shared device

            await repo.RegisterOrUpdateTokenAsync(201, "shared-device", "Android", "shared-fcm-token");

            var tokensA = await repo.GetActiveTokensByUserIdAsync(201);

            Assert.Single(tokensA);

            // User B logs in on same device with same FCM token

            await repo.RegisterOrUpdateTokenAsync(202, "shared-device", "Android", "shared-fcm-token");

            // User A's token MUST be deactivated

            var tokensAAfter = await repo.GetActiveTokensByUserIdAsync(201);

            Assert.Empty(tokensAAfter);

            // User B's token is active

            var tokensBAfter = await repo.GetActiveTokensByUserIdAsync(202);

            Assert.Single(tokensBAfter);

            Assert.Equal("shared-fcm-token", tokensBAfter[0].FcmToken);

        }

        [Fact]

        public async Task MultiDeviceFanOut_ReturnsAllActiveDevices()

        {

            var db = CreateInMemoryDbContext(nameof(MultiDeviceFanOut_ReturnsAllActiveDevices));

            await SeedUserAsync(db, 301);

            var repo = new UserDeviceTokenRepository(db);

            // User registers Phone and Tablet

            await repo.RegisterOrUpdateTokenAsync(301, "device-phone", "Android", "token-phone");

            await repo.RegisterOrUpdateTokenAsync(301, "device-tablet", "iOS", "token-tablet");

            var activeList = await repo.GetActiveTokensByUserIdAsync(301);

            Assert.Equal(2, activeList.Count);

            Assert.Contains(activeList, t => t.DeviceIdentifier == "device-phone" && t.Platform == "Android");

            Assert.Contains(activeList, t => t.DeviceIdentifier == "device-tablet" && t.Platform == "iOS");

        }

        [Fact]

        public async Task LogoutRevocation_DeactivatesDeviceAndToken()

        {

            var db = CreateInMemoryDbContext(nameof(LogoutRevocation_DeactivatesDeviceAndToken));

            await SeedUserAsync(db, 401);

            var repo = new UserDeviceTokenRepository(db);

            await repo.RegisterOrUpdateTokenAsync(401, "device-logout", "Android", "token-logout");

            // Verify active

            var before = await repo.GetActiveTokensByUserIdAsync(401);

            Assert.Single(before);

            // Deactivate device on logout

            await repo.DeactivateDeviceAsync(401, "device-logout");

            var after = await repo.GetActiveTokensByUserIdAsync(401);

            Assert.Empty(after);

            // Check db record exists but IsActive is false

            var stored = await db.UserDeviceTokens.FirstAsync(t => t.UserId == 401 && t.DeviceIdentifier == "device-logout");

            Assert.False(stored.IsActive);

        }

        [Fact]

        public async Task FcmPushService_WhenDisabled_SkipsSendingSafely()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_WhenDisabled_SkipsSendingSafely));

            await SeedUserAsync(db, 501);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(501, "d1", "Android", "token-valid");

            var transportMock = new Mock<IFcmTransport>();

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = false });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(501, new FcmPushPayload

            {

                Title = "Test Notification",

                Body = "Hello learner!"

            });

            Assert.True(result.Success);

            Assert.Equal(1, result.TotalTokens);

            Assert.Equal("FCM push disabled via configuration.", result.Note);

            transportMock.Verify(t => t.SendAsync(It.IsAny<string>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()), Times.Never);

        }

        [Fact]

        public async Task FcmPushService_WhenActive_DispatchesPayloadSuccessfully()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_WhenActive_DispatchesPayloadSuccessfully));

            await SeedUserAsync(db, 502);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(502, "d1", "Android", "token-1");

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(502, "d2", "iOS", "token-2");

            var transportMock = new Mock<IFcmTransport>();

            transportMock.Setup(t => t.SendAsync(It.IsAny<string>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmTransportResponse { IsSuccess = true, StatusCode = 200 });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 2 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var payload = new FcmPushPayload

            {

                Title = "Lịch hẹn mới",

                Body = "Bạn có buổi tư vấn lúc 10:00",

                Data = new Dictionary<string, string> { ["bookingId"] = "99" }

            };

            var result = await service.SendPushToUserAsync(502, payload);

            Assert.True(result.Success);

            Assert.Equal(2, result.TotalTokens);

            Assert.Equal(2, result.SuccessCount);

            Assert.Equal(0, result.FailureCount);

            transportMock.Verify(t => t.SendAsync(It.IsAny<string>(), payload, It.IsAny<CancellationToken>()), Times.Exactly(2));

        }

        [Fact]

        public async Task FcmPushService_PermanentError_DeactivatesInvalidTokenWithoutRetry()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_PermanentError_DeactivatesInvalidTokenWithoutRetry));

            await SeedUserAsync(db, 503);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(503, "dead-device", "Android", "stale-fcm-token");

            var transportMock = new Mock<IFcmTransport>();

            transportMock.Setup(t => t.SendAsync("stale-fcm-token", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmTransportResponse

                {

                    IsSuccess = false,

                    StatusCode = 404,

                    ErrorCode = "UNREGISTERED"

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 3 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(503, new FcmPushPayload { Title = "Ping", Body = "Hello" });

            Assert.False(result.Success);

            Assert.Equal(1, result.FailureCount);

            Assert.Single(result.DeactivatedTokens);

            Assert.Contains("stale-fcm-token", result.DeactivatedTokens);

            // Transport called only ONCE (permanent error stops immediately, no retries)

            transportMock.Verify(t => t.SendAsync("stale-fcm-token", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()), Times.Once);

            // Token deactivated in database

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(503);

            Assert.Empty(activeTokens);

        }

        [Fact]

        public async Task FcmPushService_TransientError_RetriesWithBackoffAndSucceeds()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_TransientError_RetriesWithBackoffAndSucceeds));

            await SeedUserAsync(db, 504);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(504, "d1", "Android", "token-flaky");

            var transportMock = new Mock<IFcmTransport>();

            var callCount = 0;

            transportMock.Setup(t => t.SendAsync("token-flaky", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(() =>

                {

                    callCount++;

                    if (callCount == 1)

                    {

                        // 1st call fails with transient 503 Unavailable

                        return new FcmTransportResponse { IsSuccess = false, StatusCode = 503, ErrorCode = "UNAVAILABLE" };

                    }

                    // 2nd call succeeds

                    return new FcmTransportResponse { IsSuccess = true, StatusCode = 200 };

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 3, InitialRetryDelayMs = 10, QuotaExceededDefaultDelaySeconds = 0 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(504, new FcmPushPayload { Title = "Retry Test", Body = "Test" });

            Assert.True(result.Success);

            Assert.Equal(1, result.SuccessCount);

            Assert.Equal(2, callCount); // Retried once and succeeded

        }

        [Fact]

        public async Task NotificationService_SendNotificationToUserAsync_DispatchesBothSignalRAndFcm()

        {

            var db = CreateInMemoryDbContext(nameof(NotificationService_SendNotificationToUserAsync_DispatchesBothSignalRAndFcm));

            await SeedUserAsync(db, 601);

            var uow = new UnitOfWork(db);

            var claimServiceMock = new Mock<IClaimService>();

            var fcmServiceMock = new Mock<IFcmPushService>();

            fcmServiceMock.Setup(f => f.SendPushToUserAsync(601, It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmSendResult { Success = true, TotalTokens = 1, SuccessCount = 1 });

            var notifService = new NotificationService(uow, claimServiceMock.Object, hubContext: null, fcmPushService: fcmServiceMock.Object);

            var response = await notifService.SendNotificationToUserAsync(

                targetUserId: 601,

                title: "Lịch hẹn mới",

                message: "Buổi tư vấn #42 đã được tạo",

                type: "MentorBooking",

                linkUrl: "/mentor/bookings/42",

                cancellationToken: CancellationToken.None);

            Assert.True(response.IsSuccess);

            Assert.Equal("Lịch hẹn mới", response.Result!.Title);

            // Verify notification saved in database

            var (items, count) = await uow.Notifications.GetByUserAsync(601, false, 1, 10);

            Assert.Equal(1, count);

            Assert.Equal("/mentor/bookings/42", items[0].LinkUrl);

            // Verify FCM service invoked with correct deep link metadata

            fcmServiceMock.Verify(f => f.SendPushToUserAsync(

                601,

                It.Is<FcmPushPayload>(p =>

                    p.Title == "Lịch hẹn mới" &&

                    p.Data.ContainsKey("bookingId") && p.Data["bookingId"] == "42" &&

                    p.Data.ContainsKey("screen") && p.Data["screen"] == "booking_detail"),

                It.IsAny<CancellationToken>()), Times.Once);

        }

        [Fact]

        public async Task FcmPushService_InvalidArgument_WhenPayloadError_DoesNotDeactivateToken()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_InvalidArgument_WhenPayloadError_DoesNotDeactivateToken));

            await SeedUserAsync(db, 701);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(701, "phone-valid", "Android", "token-valid-701");

            var transportMock = new Mock<IFcmTransport>();

            // 400 error caused by payload message exceeding size, NOT bad token

            transportMock.Setup(t => t.SendAsync("token-valid-701", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmTransportResponse

                {

                    IsSuccess = false,

                    StatusCode = 400,

                    ErrorCode = "INVALID_ARGUMENT",

                    ErrorMessage = "Payload data size exceeds maximum limit of 4096 bytes",

                    FieldViolations = new List<string> { "message.data" }

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 2 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(701, new FcmPushPayload { Title = "Big data", Body = "Hello" });

            Assert.False(result.Success);

            Assert.Equal(1, result.FailureCount);

            // CRITICAL: Token MUST NOT be deactivated when error is in the payload!

            Assert.Empty(result.DeactivatedTokens);

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(701);

            Assert.Single(activeTokens);

            Assert.Equal("token-valid-701", activeTokens[0].FcmToken);

        }

        [Fact]

        public async Task FcmPushService_SenderIdMismatch_LogsErrorAndPreservesToken()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_SenderIdMismatch_LogsErrorAndPreservesToken));

            await SeedUserAsync(db, 702);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(702, "phone-mismatch", "Android", "token-sender-id-test");

            var transportMock = new Mock<IFcmTransport>();

            transportMock.Setup(t => t.SendAsync("token-sender-id-test", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmTransportResponse

                {

                    IsSuccess = false,

                    StatusCode = 403,

                    ErrorCode = "SENDER_ID_MISMATCH",

                    ErrorMessage = "Token does not match project Sender ID"

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 2 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(702, new FcmPushPayload { Title = "Ping", Body = "Hello" });

            Assert.False(result.Success);

            // CRITICAL: SENDER_ID_MISMATCH is a project configuration issue; tokens MUST NOT be deactivated!

            Assert.Empty(result.DeactivatedTokens);

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(702);

            Assert.Single(activeTokens);

        }

        [Fact]

        public async Task FcmPushService_AuthOrPermissionError_PreservesToken()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_AuthOrPermissionError_PreservesToken));

            await SeedUserAsync(db, 703);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(703, "phone-auth", "Android", "token-auth-test");

            var transportMock = new Mock<IFcmTransport>();

            transportMock.Setup(t => t.SendAsync("token-auth-test", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmTransportResponse

                {

                    IsSuccess = false,

                    StatusCode = 401,

                    ErrorCode = "UNAUTHENTICATED",

                    ErrorMessage = "Request had invalid authentication credentials"

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 2 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(703, new FcmPushPayload { Title = "Auth test", Body = "Hello" });

            Assert.False(result.Success);

            // CRITICAL: 401/403 credentials error MUST NOT deactivate user tokens!

            Assert.Empty(result.DeactivatedTokens);

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(703);

            Assert.Single(activeTokens);

        }

        [Fact]

        public async Task FcmPushService_ApnsConfigurationBlocker_PreservesToken()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_ApnsConfigurationBlocker_PreservesToken));

            await SeedUserAsync(db, 704);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(704, "ipad-apns", "iOS", "token-ios-apns");

            var transportMock = new Mock<IFcmTransport>();

            transportMock.Setup(t => t.SendAsync("token-ios-apns", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(new FcmTransportResponse

                {

                    IsSuccess = false,

                    StatusCode = 403,

                    ErrorCode = "APNS_AUTH_ERROR",

                    ErrorMessage = "A message could not be sent to an APNs device due to invalid credentials"

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 2 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(704, new FcmPushPayload { Title = "APNs test", Body = "Hello" });

            Assert.False(result.Success);

            // CRITICAL: APNs authentication error on console MUST NOT deactivate user tokens!

            Assert.Empty(result.DeactivatedTokens);

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(704);

            Assert.Single(activeTokens);

        }

        [Fact]

        public async Task FcmPushService_QuotaExceeded_RespectsRetryAfterAndRetries()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_QuotaExceeded_RespectsRetryAfterAndRetries));

            await SeedUserAsync(db, 705);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(705, "phone-quota", "Android", "token-quota-test");

            var transportMock = new Mock<IFcmTransport>();

            var attempts = 0;

            transportMock.Setup(t => t.SendAsync("token-quota-test", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(() =>

                {

                    attempts++;

                    if (attempts == 1)

                    {

                        return new FcmTransportResponse

                        {

                            IsSuccess = false,

                            StatusCode = 429,

                            ErrorCode = "RESOURCE_EXHAUSTED",

                            ErrorMessage = "Quota exceeded for project",

                            RetryAfterSeconds = 0 // Immediate retry in test

                        };

                    }

                    return new FcmTransportResponse { IsSuccess = true, StatusCode = 200 };

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions { Enabled = true, MaxRetries = 3, InitialRetryDelayMs = 10, QuotaExceededMinDelayMs = 10 });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(705, new FcmPushPayload { Title = "Quota test", Body = "Hello" });

            Assert.True(result.Success);

            Assert.Equal(1, result.SuccessCount);

            Assert.Equal(2, attempts); // Retried once and succeeded

            Assert.Empty(result.DeactivatedTokens);

        }

        [Fact]

        public async Task FcmPushService_QuotaExceeded_WhenLongRetryAfter_DefersToBackgroundWorkerWithoutBlocking()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_QuotaExceeded_WhenLongRetryAfter_DefersToBackgroundWorkerWithoutBlocking));

            await SeedUserAsync(db, 706);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(706, "phone-quota-long", "Android", "token-quota-long");

            var transportMock = new Mock<IFcmTransport>();

            var attempts = 0;

            transportMock.Setup(t => t.SendAsync("token-quota-long", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(() =>

                {

                    attempts++;

                    return new FcmTransportResponse

                    {

                        IsSuccess = false,

                        StatusCode = 429,

                        ErrorCode = "RESOURCE_EXHAUSTED",

                        ErrorMessage = "Quota exceeded. Please retry later.",

                        RetryAfterSeconds = 60

                    };

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions

            {

                Enabled = true,

                MaxRetries = 3,

                InitialRetryDelayMs = 10,

                QuotaExceededMinDelayMs = 10,

                MaxImmediateRetryDelayMs = 5000

            });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(706, new FcmPushPayload { Title = "Long quota test", Body = "Hello" });

            Assert.False(result.Success);

            Assert.Equal(1, result.FailureCount);

            Assert.True(result.HasDeferredRetry);

            Assert.True(result.RecommendedBackoffSeconds >= 60);

            Assert.Equal(1, attempts);

            Assert.Empty(result.DeactivatedTokens);

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(706);

            Assert.Single(activeTokens);

        }

        [Fact]

        public async Task FcmPushService_QuotaExceeded_WithoutRetryAfter_DefaultsTo60SecondsDurableDeferral()

        {

            var db = CreateInMemoryDbContext(nameof(FcmPushService_QuotaExceeded_WithoutRetryAfter_DefaultsTo60SecondsDurableDeferral));

            await SeedUserAsync(db, 707);

            var uow = new UnitOfWork(db);

            await uow.UserDeviceTokens.RegisterOrUpdateTokenAsync(707, "phone-quota-default", "Android", "token-quota-default");

            var transportMock = new Mock<IFcmTransport>();

            var attempts = 0;

            transportMock.Setup(t => t.SendAsync("token-quota-default", It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))

                .ReturnsAsync(() =>

                {

                    attempts++;

                    return new FcmTransportResponse

                    {

                        IsSuccess = false,

                        StatusCode = 429,

                        ErrorCode = "RESOURCE_EXHAUSTED",

                        ErrorMessage = "Quota exceeded for project. Please wait.",

                        RetryAfterSeconds = null // No Retry-After header provided by Firebase!

                    };

                });

            var loggerMock = new Mock<ILogger<FcmPushService>>();

            var options = Options.Create(new FcmOptions

            {

                Enabled = true,

                MaxRetries = 3,

                QuotaExceededDefaultDelaySeconds = 60, // Firebase official default

                MaxImmediateRetryDelayMs = 5000

            });

            var service = new FcmPushService(uow, transportMock.Object, options, loggerMock.Object);

            var result = await service.SendPushToUserAsync(707, new FcmPushPayload { Title = "Scale test", Body = "Test" });

            Assert.False(result.Success);

            Assert.Equal(1, result.FailureCount);

            // Must defer with >= 60 seconds (60s + jitter) per Firebase Scale guidance

            Assert.True(result.HasDeferredRetry);

            Assert.True(result.RecommendedBackoffSeconds >= 60, "Recommended backoff must be at least 60 seconds default.");

            Assert.Equal(1, attempts);

            Assert.Empty(result.DeactivatedTokens); // Token preserved in DB!

            var activeTokens = await uow.UserDeviceTokens.GetActiveTokensByUserIdAsync(707);

            Assert.Single(activeTokens);

        }

        [Fact]

        public async Task MentorOutboxDispatcherService_WhenFcmQuotaExceeded_PersistsAvailableAtUtcInDatabase()

        {

            var db = CreateInMemoryDbContext(nameof(MentorOutboxDispatcherService_WhenFcmQuotaExceeded_PersistsAvailableAtUtcInDatabase));

            // Seed a pending outbox event

            var evt = new MentorOutboxEvent

            {

                Id = 999,

                EventKey = "evt-quota-defer-001",

                AggregateType = "MentorBooking",

                AggregateId = "1001",

                EventType = "BookingCreated",

                EventVersion = 1,

                PayloadJson = "{\"BookingId\": 1001, \"LearnerId\": 801, \"MentorId\": 802}",

                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),

                AvailableAtUtc = DateTime.UtcNow.AddMinutes(-5),

                DeliveredAtUtc = null,

                Attempts = 0

            };

            db.MentorOutboxEvents.Add(evt);

            await db.SaveChangesAsync();

            var notifServiceMock = new Mock<INotificationService>();

            // Mock notification service returning deferred push retry due to HTTP 429

            notifServiceMock.Setup(n => n.SendNotificationToUserAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))

                .ReturnsAsync(ApiResponse<RikiPath.Application.Responses.Notifications.NotificationResponse>.Success(

                    new RikiPath.Application.Responses.Notifications.NotificationResponse

                    {

                        Id = 1,

                        Title = "Test",

                        HasDeferredPushRetry = true,

                        PushBackoffSeconds = 60

                    }));

            var serviceProviderMock = new Mock<IServiceProvider>();

            serviceProviderMock.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(db);

            serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationService))).Returns(notifServiceMock.Object);

            var scopeMock = new Mock<IServiceScope>();

            scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();

            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var loggerMock = new Mock<ILogger<MentorOutboxDispatcherService>>();

            var dispatcher = new MentorOutboxDispatcherService(scopeFactoryMock.Object, loggerMock.Object);

            var beforeProcess = DateTime.UtcNow;

            var processed = await dispatcher.ProcessPendingOutboxEventsAsync(CancellationToken.None);

            // Re-fetch event from database

            var updatedEvt = await db.MentorOutboxEvents.FindAsync(999L);

            Assert.NotNull(updatedEvt);

            // CRITICAL: DeliveredAtUtc must remain null so background worker will pick it up later!

            Assert.Null(updatedEvt!.DeliveredAtUtc);

            // CRITICAL: AvailableAtUtc must be updated into the future (~60s from now) in the database!

            Assert.True(updatedEvt.AvailableAtUtc >= beforeProcess.AddSeconds(55),

                $"AvailableAtUtc {updatedEvt.AvailableAtUtc} must be scheduled ~60s in future from {beforeProcess}");

            Assert.Equal(1, updatedEvt.Attempts);

        }

        [Fact]
        public async Task NotificationService_ChannelLevelIdempotency_WhenRetried_DoesNotDuplicateNotificationOrSignalR()
        {
            var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
            db.UserDeviceTokens.Add(new UserDeviceToken
            {
                UserId = 10,
                DeviceIdentifier = "dev-10",
                Platform = "Android",
                FcmToken = "fcm_token_10",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var uow = new UnitOfWork(db);
            var claimMock = new Mock<IClaimService>();
            claimMock.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 10, Role = Role.Learner });

            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

            var hubMock = new Mock<IHubContext<NotificationHub>>();
            hubMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

            var fcmCalls = 0;
            var fcmMock = new Mock<IFcmPushService>();
            fcmMock.Setup(f => f.SendPushToTokensAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))
                .Returns<IEnumerable<string>, FcmPushPayload, CancellationToken>((tokens, payload, ct) =>
                {
                    fcmCalls++;
                    if (fcmCalls == 1)
                    {
                        return Task.FromResult(new FcmSendResult
                        {
                            Success = false,
                            HasDeferredRetry = true,
                            RecommendedBackoffSeconds = 60,
                            FailedTokens = new List<string>(tokens)
                        });
                    }
                    return Task.FromResult(new FcmSendResult
                    {
                        Success = true,
                        SuccessfulTokens = new List<string>(tokens)
                    });
                });

            var notifService = new NotificationService(uow, claimMock.Object, hubMock.Object, fcmMock.Object);

            // Attempt 1: First dispatch with explicit idempotency key
            var firstResult = await notifService.SendNotificationToUserAsync(
                10,
                "Lịch tư vấn mới",
                "Bạn có lịch hẹn tư vấn mới #123",
                "MentorBooking",
                "/mentor/bookings/123",
                CancellationToken.None,
                idempotencyKey: "outbox:123:user:10");

            Assert.True(firstResult.IsSuccess);
            Assert.NotNull(firstResult.Result);
            var firstId = firstResult.Result!.Id;

            // Database should have exactly 1 notification
            var notifList = await db.Notifications.ToListAsync();
            Assert.Single(notifList);
            Assert.Equal("Lịch tư vấn mới", notifList[0].Title);

            // SignalR should have received 1 NotificationReceived and 1 UnreadCountChanged
            clientProxyMock.Verify(c => c.SendCoreAsync("NotificationReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
            fcmMock.Verify(f => f.SendPushToTokensAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()), Times.Once);

            // Attempt 2: Retry dispatch for the exact same event
            var secondResult = await notifService.SendNotificationToUserAsync(
                10,
                "Lịch tư vấn mới",
                "Bạn có lịch hẹn tư vấn mới #123",
                "MentorBooking",
                "/mentor/bookings/123",
                CancellationToken.None,
                idempotencyKey: "outbox:123:user:10");

            Assert.True(secondResult.IsSuccess);
            Assert.NotNull(secondResult.Result);
            Assert.Equal(firstId, secondResult.Result!.Id);

            // IDEMPOTENCY VERIFICATION:
            // 1. Database still has only 1 notification (no duplicates!)
            var notifListAfter = await db.Notifications.ToListAsync();
            Assert.Single(notifListAfter);

            // 2. SignalR was NOT called a second time (no duplicate toasts or badge increments)
            clientProxyMock.Verify(c => c.SendCoreAsync("NotificationReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);

            // 3. FCM push WAS invoked on the retry attempt to complete delivery
            fcmMock.Verify(f => f.SendPushToTokensAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task NotificationService_DistinctBusinessEvents_WithIdenticalTitleAndLinkUrl_AreNotMerged()
        {
            var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var uow = new UnitOfWork(db);
            var claimMock = new Mock<IClaimService>();
            claimMock.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 10, Role = Role.Learner });

            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

            var hubMock = new Mock<IHubContext<NotificationHub>>();
            hubMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

            var fcmMock = new Mock<IFcmPushService>();
            fcmMock.Setup(f => f.SendPushToTokensAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FcmSendResult { Success = true });

            var notifService = new NotificationService(uow, claimMock.Object, hubMock.Object, fcmMock.Object);

            // Event 1: Booking #101 created for mentor
            var res1 = await notifService.SendNotificationToUserAsync(
                10,
                "Lịch tư vấn mới",
                "Bạn có lịch hẹn tư vấn mới #101 từ học viên.",
                "MentorBooking",
                "/mentor/schedule",
                CancellationToken.None,
                idempotencyKey: "outbox:101:user:10");

            // Event 2: Booking #102 created for the same mentor (SAME title, SAME linkUrl, SAME type)
            var res2 = await notifService.SendNotificationToUserAsync(
                10,
                "Lịch tư vấn mới",
                "Bạn có lịch hẹn tư vấn mới #102 từ học viên.",
                "MentorBooking",
                "/mentor/schedule",
                CancellationToken.None,
                idempotencyKey: "outbox:102:user:10");

            Assert.True(res1.IsSuccess);
            Assert.True(res2.IsSuccess);
            Assert.NotEqual(res1.Result!.Id, res2.Result!.Id);

            // CRITICAL: Both distinct business events must exist in database!
            var notifs = await db.Notifications.OrderBy(n => n.Id).ToListAsync();
            Assert.Equal(2, notifs.Count);
            Assert.Equal("outbox:101:user:10", notifs[0].IdempotencyKey);
            Assert.Equal("outbox:102:user:10", notifs[1].IdempotencyKey);

            // Both SignalR events were dispatched
            clientProxyMock.Verify(c => c.SendCoreAsync("NotificationReceived", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task NotificationService_PerDeviceFcmPush_WhenOneDeviceSucceedsAndOneFails_RetriesOnlyFailedDevice()
        {
            var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var uow = new UnitOfWork(db);

            // Register 2 devices for user 20
            db.UserDeviceTokens.Add(new UserDeviceToken
            {
                UserId = 20,
                DeviceIdentifier = "dev-pixel-9",
                Platform = "Android",
                FcmToken = "token_pixel_A",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            });
            db.UserDeviceTokens.Add(new UserDeviceToken
            {
                UserId = 20,
                DeviceIdentifier = "dev-ipad-pro",
                Platform = "iOS",
                FcmToken = "token_ipad_B",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var claimMock = new Mock<IClaimService>();
            var hubMock = new Mock<IHubContext<NotificationHub>>();
            var fcmMock = new Mock<IFcmPushService>();

            var capturedTokenBatches = new List<List<string>>();
            fcmMock.Setup(f => f.SendPushToTokensAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<FcmPushPayload>(), It.IsAny<CancellationToken>()))
                .Returns<IEnumerable<string>, FcmPushPayload, CancellationToken>((tokens, payload, ct) =>
                {
                    var list = tokens.ToList();
                    capturedTokenBatches.Add(list);

                    // First dispatch: token_pixel_A succeeds, token_ipad_B throttled with 429
                    if (list.Contains("token_pixel_A"))
                    {
                        return Task.FromResult(new FcmSendResult
                        {
                            Success = false,
                            HasDeferredRetry = true,
                            RecommendedBackoffSeconds = 60,
                            SuccessfulTokens = new List<string> { "token_pixel_A" },
                            FailedTokens = new List<string> { "token_ipad_B" }
                        });
                    }

                    // Retry dispatch: only token_ipad_B should be sent, and it now succeeds!
                    return Task.FromResult(new FcmSendResult
                    {
                        Success = true,
                        HasDeferredRetry = false,
                        SuccessfulTokens = new List<string> { "token_ipad_B" },
                        FailedTokens = new List<string>()
                    });
                });

            var notifService = new NotificationService(uow, claimMock.Object, hubMock.Object, fcmMock.Object);

            // Attempt 1
            var firstResult = await notifService.SendNotificationToUserAsync(
                20,
                "Nhắc lịch tư vấn",
                "Lịch hẹn sẽ bắt đầu sau 15 phút.",
                "MentorMeeting",
                "/mentor/meetings/50",
                CancellationToken.None,
                idempotencyKey: "outbox:201:user:20");

            Assert.True(firstResult.IsSuccess);
            Assert.True(firstResult.Result!.HasDeferredPushRetry);

            // Attempt 1 sent to both tokens
            Assert.Equal(2, capturedTokenBatches[0].Count);
            Assert.Contains("token_pixel_A", capturedTokenBatches[0]);
            Assert.Contains("token_ipad_B", capturedTokenBatches[0]);

            // Database recorded token_pixel_A in DeliveredTokens
            var notifInDb = await db.Notifications.SingleAsync(n => n.IdempotencyKey == "outbox:201:user:20");
            Assert.Contains("token_pixel_A", notifInDb.DeliveredTokens ?? "");

            // Attempt 2 (Retry after backoff)
            var retryResult = await notifService.SendNotificationToUserAsync(
                20,
                "Nhắc lịch tư vấn",
                "Lịch hẹn sẽ bắt đầu sau 15 phút.",
                "MentorMeeting",
                "/mentor/meetings/50",
                CancellationToken.None,
                idempotencyKey: "outbox:201:user:20");

            Assert.True(retryResult.IsSuccess);
            Assert.False(retryResult.Result!.HasDeferredPushRetry);

            // CRITICAL PER-DEVICE VERIFICATION:
            // Attempt 2 sent ONLY to token_ipad_B (token_pixel_A was NOT re-sent to!)
            Assert.Equal(2, capturedTokenBatches.Count);
            Assert.Single(capturedTokenBatches[1]);
            Assert.Equal("token_ipad_B", capturedTokenBatches[1][0]);

            // Database now records both tokens as delivered
            var finalNotif = await db.Notifications.SingleAsync(n => n.IdempotencyKey == "outbox:201:user:20");
            Assert.Contains("token_pixel_A", finalNotif.DeliveredTokens ?? "");
            Assert.Contains("token_ipad_B", finalNotif.DeliveredTokens ?? "");
        }

        [Fact]
        public async Task MentorOutboxDispatcherService_WorkerCrashAndRestart_RecoversFromDeferredStateWithoutDuplicates()
        {
            var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var evt = new MentorOutboxEvent
            {
                Id = 777L,
                EventKey = "booking_777_created",
                AggregateType = "MentorBooking",
                AggregateId = "777",
                EventType = "BookingCreated",
                EventVersion = 1,
                PayloadJson = "{\"BookingId\":777,\"LearnerId\":31,\"MentorId\":32}",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                AvailableAtUtc = DateTime.UtcNow.AddMinutes(-1),
                DeliveredAtUtc = null,
                Attempts = 0
            };
            db.MentorOutboxEvents.Add(evt);
            await db.SaveChangesAsync();

            var attemptCount = 0;
            var notifServiceMock = new Mock<INotificationService>();
            notifServiceMock.Setup(n => n.SendNotificationToUserAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
                .Returns<int, string, string, string, string?, CancellationToken, string?>((userId, title, msg, type, link, ct, key) =>
                {
                    attemptCount++;
                    if (attemptCount <= 2)
                    {
                        // First run: FCM deferral
                        return Task.FromResult(ApiResponse<RikiPath.Application.Responses.Notifications.NotificationResponse>.Success(
                            new RikiPath.Application.Responses.Notifications.NotificationResponse
                            {
                                Id = 500,
                                Title = title,
                                HasDeferredPushRetry = true,
                                PushBackoffSeconds = 60
                            }));
                    }

                    // Second run (after worker restart): Successful push
                    return Task.FromResult(ApiResponse<RikiPath.Application.Responses.Notifications.NotificationResponse>.Success(
                        new RikiPath.Application.Responses.Notifications.NotificationResponse
                        {
                            Id = 500,
                            Title = title,
                            HasDeferredPushRetry = false
                        }));
                });

            var scopeMock = new Mock<IServiceScope>();
            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(AppDbContext))).Returns(db);
            spMock.Setup(s => s.GetService(typeof(INotificationService))).Returns(notifServiceMock.Object);
            scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
            var loggerMock = new Mock<ILogger<MentorOutboxDispatcherService>>();

            // 1. Worker Run 1: Throttled, event is deferred
            var worker1 = new MentorOutboxDispatcherService(scopeFactoryMock.Object, loggerMock.Object);
            await worker1.ProcessPendingOutboxEventsAsync(CancellationToken.None);

            var deferredEvt = await db.MentorOutboxEvents.FindAsync(777L);
            Assert.Null(deferredEvt!.DeliveredAtUtc);
            Assert.True(deferredEvt.AvailableAtUtc > DateTime.UtcNow);

            // 2. Worker Crash & Restart: Advance time to make event available again
            deferredEvt.AvailableAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();

            // Run 2: Restarted worker picks up and finishes delivery
            var worker2 = new MentorOutboxDispatcherService(scopeFactoryMock.Object, loggerMock.Object);
            var processed = await worker2.ProcessPendingOutboxEventsAsync(CancellationToken.None);
            Assert.Equal(1, processed);

            var completedEvt = await db.MentorOutboxEvents.FindAsync(777L);
            Assert.NotNull(completedEvt!.DeliveredAtUtc);
        }

        [Fact]
        public async Task MentorOutboxDispatcherService_ConcurrentWorkers_DoNotDuplicateProcessingOfEvents()
        {
            var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
            db.MentorOutboxEvents.Add(new MentorOutboxEvent
            {
                Id = 801L,
                EventKey = "booking_801_created",
                AggregateType = "MentorBooking",
                AggregateId = "801",
                EventType = "BookingCreated",
                PayloadJson = "{\"BookingId\":801,\"LearnerId\":101}",
                AvailableAtUtc = DateTime.UtcNow.AddSeconds(-10),
                DeliveredAtUtc = null
            });
            await db.SaveChangesAsync();

            var notifServiceMock = new Mock<INotificationService>();
            notifServiceMock.Setup(n => n.SendNotificationToUserAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
                .ReturnsAsync(ApiResponse<RikiPath.Application.Responses.Notifications.NotificationResponse>.Success(
                    new RikiPath.Application.Responses.Notifications.NotificationResponse { Id = 1, HasDeferredPushRetry = false }));

            var scopeMock = new Mock<IServiceScope>();
            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(s => s.GetService(typeof(AppDbContext))).Returns(db);
            spMock.Setup(s => s.GetService(typeof(INotificationService))).Returns(notifServiceMock.Object);
            scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
            var loggerMock = new Mock<ILogger<MentorOutboxDispatcherService>>();

            var worker1 = new MentorOutboxDispatcherService(scopeFactoryMock.Object, loggerMock.Object);
            var worker2 = new MentorOutboxDispatcherService(scopeFactoryMock.Object, loggerMock.Object);

            // Worker 1 runs and claims the batch
            var processed1 = await worker1.ProcessPendingOutboxEventsAsync(CancellationToken.None);
            Assert.Equal(1, processed1);

            // Concurrent Worker 2 runs immediately after: finds 0 events because worker 1 already completed/claimed it
            var processed2 = await worker2.ProcessPendingOutboxEventsAsync(CancellationToken.None);
            Assert.Equal(0, processed2);
        }
    }
}
