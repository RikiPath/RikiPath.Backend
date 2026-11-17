using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Npgsql;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Options;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Clients;
using RikiPath.Infrastructure.Services;
using RikiPath.WebApi.Controllers;
using RikiPath.WebApi.Hubs;

namespace RikiPath.Tests;

public sealed class CanonicalGroupLearningHttpE2ETests
{
    static CanonicalGroupLearningHttpE2ETests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private const string JwtSecret = "E2E-Canonical-Group-Learning-Test-Secret-Key-AtLeast32Bytes!!";
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class InMemoryPrivateVerificationStorage : IFileStorageService
    {
        private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _files = new();

        public Task<UploadedFileResult> UploadAsync(
            Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken)
            => Task.FromResult(new UploadedFileResult($"https://cdn.invalid/{folder}/{fileName}", fileName, fileStream.Length));

        public Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken cancellationToken)
            => Task.FromResult(storedUrl);

        public async Task<UploadedFileResult> UploadPrivateVerificationAsync(
            Stream fileStream, string contentType, int ownerId, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            var url = $"private/qa-{ownerId}-{Guid.NewGuid():N}";
            _files[url] = (bytes, contentType);
            return new UploadedFileResult(url, url, bytes.Length);
        }

        public Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
        {
            if (!_files.ContainsKey(storedUrl)) throw new FileNotFoundException(storedUrl);
            return Task.CompletedTask;
        }

        public Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
        {
            if (!_files.TryGetValue(storedUrl, out var item))
                throw new FileNotFoundException(storedUrl);
            return Task.FromResult(new PrivateVerificationReadResult(new MemoryStream(item.Bytes, writable: false), item.ContentType));
        }
    }

    // PR #70: giả lập IAiEssayGradingClient (client AI chấm bài của teammate); không gọi nhà cung cấp thật.
    internal sealed class SwitchableAiDraftGenerator : IAiEssayGradingClient
    {
        public bool SimulateOutage { get; set; }

        public Task<string> GenerateGradingJsonAsync(int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            if (SimulateOutage)
                throw new HttpRequestException("Simulated upstream AI outage");
            return Task.FromResult("{\"score\":88.5,\"feedback\":\"AI Draft Feedback: solid kanji usage.\"}");
        }
    }

    internal static string? GetBaseConnectionString() => Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB");

    internal static async Task<(string Schema, string ConnectionString)> CreateIsolatedSchemaAsync(string baseCs)
    {
        var schema = "http_e2e_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(baseCs) { SearchPath = schema }.ConnectionString;
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", conn))
            await create.ExecuteNonQueryAsync();
        await using var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        var operations = setup.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, setup.GetService<IDesignTimeModel>().Model.GetRelationalModel())
            .Where(x => x is not InsertDataOperation)
            .ToList();
        foreach (var sql in setup.GetService<IMigrationsSqlGenerator>().Generate(operations, setup.Model))
            await setup.Database.ExecuteSqlRawAsync(sql.CommandText.Replace("{", "{{").Replace("}", "}}"));
        return (schema, cs);
    }

    internal static async Task DropSchemaAsync(string schema, string cs)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", conn);
        await drop.ExecuteNonQueryAsync();
    }

    internal static string IssueJwt(int userId, Role role, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim(ClaimTypes.Email, email)
            ],
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool ImplementsInterface(Type type, Type iface)
    {
        if (!iface.IsGenericTypeDefinition)
            return iface.IsAssignableFrom(type);
        if (type.IsGenericTypeDefinition)
        {
            if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == iface))
                return true;
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == iface)
                    return true;
                baseType = baseType.BaseType;
            }
        }
        return false;
    }

    internal static async Task<(WebApplication App, string BaseUrl, SwitchableAiDraftGenerator AiGenerator)>
        StartE2EHostAsync(string connectionString, Mock<IPaymentGatewayClient> paymentMock)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["AppSettings:ConnectionStrings:DefaultConnection"] = connectionString,
            ["AppSettings:SecretToken:Value"] = JwtSecret,
            ["AppSettings:FrontendBaseUrl"] = "https://rikipath.test.invalid"
        });

        var appSettings = new AppSettings
        {
            FrontendBaseUrl = "https://rikipath.test.invalid",
            ConnectionStrings = new ConnectionStrings { DefaultConnection = connectionString },
            SecretToken = new SecretToken { Value = JwtSecret }
        };
        builder.Services.AddSingleton(appSettings);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();
        builder.Services.AddOptions<MentorSlotGenerationOptions>()
            .Configure(o =>
            {
                o.DailyBookingCap = 4;
                o.AutoPublicationEnabled = true;
            });

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npg => npg.EnableRetryOnFailure());
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));
        });

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        var repoInterfaceAssembly = typeof(IUserAccountRepository).Assembly;
        var repoImplAssembly = typeof(UnitOfWork).Assembly;
        var repoImpls = repoImplAssembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).ToList();
        foreach (var iface in repoInterfaceAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.EndsWith(".IRepositories")))
        {
            var expected = iface.Name.TrimStart('I');
            var candidates = repoImpls.Where(t => ImplementsInterface(t, iface)).ToList();
            var impl = candidates.FirstOrDefault(t => t.Name == expected) ?? candidates[0];
            builder.Services.AddScoped(iface, impl);
        }

        var skippedServices = new HashSet<Type>
        {
            typeof(IHomeworkAiDraftGenerator),
            typeof(IFileStorageService),
            typeof(IFcmPushService)
        };
        var appAssembly = typeof(ILessonProgressService).Assembly;
        var allImpls = appAssembly.GetTypes().Concat(repoImpls).Where(t => t.IsClass && !t.IsAbstract).ToList();
        foreach (var iface in appAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.EndsWith(".IServices")
                && !skippedServices.Contains(t)))
        {
            var expected = iface.Name.TrimStart('I');
            var candidates = allImpls.Where(t => ImplementsInterface(t, iface)).ToList();
            var impl = candidates.FirstOrDefault(t => t.Name == expected) ?? candidates.FirstOrDefault();
            if (impl is not null)
                builder.Services.AddScoped(iface, impl);
        }

        builder.Services.AddScoped<HomeworkService>();
        builder.Services.AddSingleton(Mock.Of<IExcelParser>());

        var aiGen = new SwitchableAiDraftGenerator();
        var privateStorage = new InMemoryPrivateVerificationStorage();

        builder.Services.AddSingleton<IAiEssayGradingClient>(aiGen);
        builder.Services.AddSingleton<IFileStorageService>(privateStorage);
        builder.Services.AddSingleton(paymentMock.Object);
        builder.Services.AddSingleton(Mock.Of<IFcmPushService>());
        builder.Services.AddSingleton(Mock.Of<IJlptExamSource>());
        builder.Services.AddSingleton(Mock.Of<IAiLearningPathClient>());
        builder.Services.AddSingleton(Mock.Of<IAiGradingClient>());
        builder.Services.AddSingleton(Mock.Of<IAiOcrClient>());
        builder.Services.AddSingleton(Mock.Of<IAiContentAuthoringClient>());
        builder.Services.AddSingleton(Mock.Of<IAiContentReviewClient>());
        builder.Services.AddSingleton(Mock.Of<ISpeechRecognitionClient>());
        builder.Services.AddSingleton(Mock.Of<IAzureSpeechClient>());
        builder.Services.AddSingleton(Mock.Of<ITranscribeSpeechClient>());
        builder.Services.AddSingleton(Mock.Of<IKanjiOcrClient>());
        builder.Services.AddSingleton(Mock.Of<IPayOsPayoutClient>());
        builder.Services.AddSingleton(Mock.Of<ICallConnectionTracker>());

        builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var rawId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var jwtRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
                        if (!int.TryParse(rawId, out var userId) || string.IsNullOrWhiteSpace(jwtRole))
                        {
                            context.Fail("Invalid session.");
                            return;
                        }
                        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var user = await db.Users.AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new { u.Role, u.IsDeleted })
                            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                        if (user is null || user.IsDeleted || !string.Equals(user.Role.ToString(), jwtRole, StringComparison.Ordinal))
                            context.Fail("Role mismatch.");
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs/mentor-meeting"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AdminMentorGroupPolicyController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<MentorMeetingHub>("/hubs/mentor-meeting");

        await app.StartAsync();
        var baseUrl = app.Urls.First();
        return (app, baseUrl, aiGen);
    }

    internal static HttpRequestMessage AuthReq(HttpMethod method, string url, string jwt, object? jsonBody = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        if (jsonBody is not null)
            req.Content = JsonContent.Create(jsonBody, options: JsonOptions);
        return req;
    }

    internal static async Task<JsonElement> ReadResultAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("result", out var res) ? res.Clone() : doc.RootElement.Clone();
    }

    internal static async Task<JsonElement> SendSignalRHubInvokeAsync(
        ClientWebSocket ws, string invocationId, string target, params object?[] args)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = 1,
            invocationId,
            target,
            arguments = args
        }, JsonOptions) + "\x1e";
        await ws.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[16384];
        var sb = new StringBuilder();
        while (true)
        {
            var r = await ws.ReceiveAsync(buffer, CancellationToken.None);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
            var full = sb.ToString();
            if (!full.Contains('\x1e')) continue;
            foreach (var frame in full.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var doc = JsonDocument.Parse(frame);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) && t.GetInt32() == 3
                    && root.TryGetProperty("invocationId", out var inv) && inv.GetString() == invocationId)
                {
                    return root.Clone();
                }
            }
            sb.Clear();
        }
    }

    internal static async Task<ClientWebSocket> ConnectSignalRRoomAsync(string baseUrl, string jwt)
    {
        var wsUri = new Uri(baseUrl.Replace("http://", "ws://") + $"/hubs/mentor-meeting?access_token={Uri.EscapeDataString(jwt)}");
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(wsUri, CancellationToken.None);
        var handshake = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\x1e");
        await ws.SendAsync(handshake, WebSocketMessageType.Text, true, CancellationToken.None);
        var buf = new byte[4096];
        await ws.ReceiveAsync(buf, CancellationToken.None); // {"}\x1e
        return ws;
    }

    [Fact]
    public async Task ProductionFailClosed_And_ScenariosAtoE_RealHttpAndSignalROverPostgres()
    {
        var baseCs = GetBaseConnectionString();
        if (string.IsNullOrWhiteSpace(baseCs))
            return;

        var (schema, cs) = await CreateIsolatedSchemaAsync(baseCs);
        var paymentMock = new Mock<IPaymentGatewayClient>();
        WebApplication? app = null;

        try
        {
            var host = await StartE2EHostAsync(cs, paymentMock);
            app = host.App;
            var baseUrl = host.BaseUrl;
            var aiGenerator = host.AiGenerator;

            int adminId, mentorId, otherMentorId, levelId, planId, creditPlanId;
            int[] learnerIds = new int[6]; // L1..L5 + Outsider
            Guid activeRoomId;
            DateTime sessionWindowStart, sessionWindowEnd;

            // Seed baseline catalog, users, skill samples, and unrelated 1-1 consultation credits.
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var admin = new UserAccount { Email = "admin@e2e.test", FirstName = "Admin", LastName = "Ops", Role = Role.Admin, PasswordHash = [], PasswordSalt = [] };
                var mentor = new UserAccount
                {
                    Email = "mentor@e2e.test", FirstName = "Sora", LastName = "Sensei", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [],
                    MentorProfile = new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3", Bio = "Group mentor" }
                };
                var mentorB = new UserAccount
                {
                    Email = "mentorb@e2e.test", FirstName = "Ren", LastName = "Sensei", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [],
                    MentorProfile = new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3", Bio = "Other mentor" }
                };
                var learners = Enumerable.Range(1, 6).Select(i => new UserAccount
                {
                    Email = $"learner{i}@e2e.test", FirstName = $"HọcViên{i}", LastName = "Test", Role = Role.Learner, PasswordHash = [], PasswordSalt = []
                }).ToArray();

                var certType = new CertificateType { Name = "JLPT", IsActive = true };
                var level = new CertificateLevel { Code = "N3", Description = "N3", CertificateType = certType, SortOrder = 3 };
                var skillReading = new LanguageSkill { Name = "Reading" };
                var skillGrammar = new LanguageSkill { Name = "Grammar" };
                var v1Policy = new MentorGroupPolicy
                {
                    Version = 1,
                    EffectiveFromUtc = DateTime.UtcNow.AddDays(-10),
                    MaxLearners = 4,
                    EnrollmentCutoffMinutesBeforeFirstSession = 60,
                    SessionMinutes = 90,
                    RestMinutes = 15,
                    DailySessionCap = 4,
                    PublicationHorizonDays = 30,
                    CancellationLeadHours = 24,
                    ReschedulingLeadHours = 24,
                    HomeworkDeadlineDays = 7,
                    PlacementAttemptLimit = 2,
                    RemediationCompletionThresholdPercent = 90m,
                    RemediationThresholdComparison = ">",
                    RequireOfficialExamEvidence = true,
                    RequireAdminRemediationReview = true,
                    KpiReportingPeriodDays = 30,
                    RecurringWeekdayPatternsJson = "[[1,3,5],[2,4,6]]",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                };

                db.AddRange(admin, mentor, mentorB, certType, level, skillReading, skillGrammar, v1Policy);
                db.AddRange(learners);
                await db.SaveChangesAsync();

                adminId = admin.Id;
                mentorId = mentor.Id;
                otherMentorId = mentorB.Id;
                levelId = level.Id;
                for (var i = 0; i < 6; i++) learnerIds[i] = learners[i].Id;

                // Seed skill evidence for Learner 1 + unrelated 1-1 ConsultationCredits for Learner 1.
                var certSection = new CertificateSection { Name = "Reading", Code = "READING", CertificateLevelId = levelId, MaxScore = 60, PassingScore = 19, SortOrder = 1 };
                var lesson = new Lesson { Title = "N3 Grammar Core", CertificateLevelId = levelId, LanguageSkillId = skillGrammar.Id };
                var exercise = new PracticeExercise { Lesson = lesson, LanguageSkillId = skillGrammar.Id, Title = "Grammar Practice" };
                var attemptEx = new PracticeExerciseAttempt { UserId = learnerIds[0], PracticeExercise = exercise, IsCompleted = true, ScorePercent = 85.0, StartedAt = DateTime.UtcNow.AddDays(-2), SubmittedAt = DateTime.UtcNow.AddDays(-2) };
                var mockTest = new MockTest { Title = "N3 Mock", CertificateLevelId = levelId };
                var sec = new MockTestSection { MockTest = mockTest, Title = "Reading Part", LanguageSkillId = skillReading.Id, CertificateSection = certSection };
                var mockAttempt = new MockTestAttempt { UserId = learnerIds[0], AttemptKind = AttemptKind.MockTest, IsCompleted = true, StartedAt = DateTime.UtcNow.AddDays(-3), SubmittedAt = DateTime.UtcNow.AddDays(-3) };
                var secRes = new MockTestSectionResult { MockTestAttempt = mockAttempt, MockTestSection = sec, CorrectCount = 8, TotalCount = 10, ScorePercent = 80.0 };

                var subPlan = new SubscriptionPlan { Name = "1-1 Tư vấn 3 buổi", Price = 300000, DurationDays = 30, MeetingSessionCount = 3, CertificateLevelId = levelId, IsActive = true };
                db.AddRange(certSection, lesson, exercise, attemptEx, mockTest, sec, mockAttempt, secRes, subPlan);
                await db.SaveChangesAsync();
                creditPlanId = subPlan.Id;

                var userSub = new UserSubscription
                {
                    UserId = learnerIds[0],
                    SubscriptionPlanId = creditPlanId,
                    CertificateLevelId = levelId,
                    AmountPaid = 300000m,
                    PaymentStatus = PaymentStatus.Paid,
                    MeetingSessionsIncluded = 3,
                    PurchasedAt = DateTime.UtcNow.AddDays(-1)
                };
                db.UserSubscriptions.Add(userSub);
                await db.SaveChangesAsync();

                db.MentorCreditGrants.Add(new MentorCreditGrant
                {
                    LearnerId = learnerIds[0],
                    UserSubscriptionId = userSub.Id,
                    CertificateLevelId = levelId,
                    PolicyVersion = 3,
                    InitialQuantity = 3,
                    AvailableQuantity = 3,
                    EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
                });
                await db.SaveChangesAsync();
            }

            var adminJwt = IssueJwt(adminId, Role.Admin, "admin@e2e.test");
            var mentorJwt = IssueJwt(mentorId, Role.Mentor, "mentor@e2e.test");
            var mentorBJwt = IssueJwt(otherMentorId, Role.Mentor, "mentorb@e2e.test");
            var learnerJwts = Enumerable.Range(0, 6).Select(i => IssueJwt(learnerIds[i], Role.Learner, $"learner{i + 1}@e2e.test")).ToArray();

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

            // =========================================================================
            // SCENARIO A: Policy v2 -> Mentor 2-4-6 & 3-5-7 -> Approve -> Group Plan -> Cohort
            // =========================================================================
            var vnToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var tomorrowVnMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(
                vnToday.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));

            var policyV2Res = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, "/api/admin/mentor-group-policies", adminJwt,
                new CreateMentorGroupPolicyRequest
                {
                    ExpectedCurrentVersion = 1,
                    EffectiveFromUtc = DateTime.SpecifyKind(tomorrowVnMidnightUtc, DateTimeKind.Utc),
                    MaxLearners = 4,
                    EnrollmentCutoffMinutesBeforeFirstSession = 60,
                    SessionMinutes = 90,
                    RestMinutes = 15,
                    DailySessionCap = 4,
                    PublicationHorizonDays = 60,
                    CancellationLeadHours = 24,
                    ReschedulingLeadHours = 24,
                    HomeworkDeadlineDays = 7,
                    PlacementAttemptLimit = 2,
                    RemediationCompletionThresholdPercent = 90m,
                    RemediationThresholdComparison = ">=",
                    RequireOfficialExamEvidence = true,
                    RequireAdminRemediationReview = true,
                    KpiReportingPeriodDays = 30,
                    RecurringWeekdayPatterns = [[1, 3, 5], [2, 4, 6]]
                })));
            Assert.Equal(2, policyV2Res.GetProperty("version").GetInt32());

            // Configure Mentor recurring rules on Tue/Thu/Sat (2,4,6) & Mon/Wed/Fri (1,3,5) -> Morning 07:00-11:00.
            var saveSchedResp = await http.SendAsync(AuthReq(HttpMethod.Put, "/api/mentors/me/schedule", mentorJwt, new UpdateMentorScheduleRequest
            {
                TimeZoneId = "Asia/Ho_Chi_Minh",
                AutoPublish = true,
                EffectiveFrom = vnToday,
                Rules =
                [
                    new() { Weekday = 2, DaypartCode = "Morning", LocalStart = new TimeOnly(7, 0), LocalEnd = new TimeOnly(11, 0), EffectiveFrom = vnToday },
                    new() { Weekday = 4, DaypartCode = "Morning", LocalStart = new TimeOnly(7, 0), LocalEnd = new TimeOnly(11, 0), EffectiveFrom = vnToday },
                    new() { Weekday = 6, DaypartCode = "Morning", LocalStart = new TimeOnly(7, 0), LocalEnd = new TimeOnly(11, 0), EffectiveFrom = vnToday }
                ]
            }));
            Assert.Equal(HttpStatusCode.OK, saveSchedResp.StatusCode);

            var pubStart = vnToday.AddDays(3);
            var pubEnd = vnToday.AddDays(16);
            var pubRes = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, "/api/mentors/me/schedule/publish-slots", mentorJwt,
                new PublishSlotsRequest { StartDate = pubStart, EndDate = pubEnd })));
            Assert.True(pubRes.GetProperty("createdCount").GetInt32() >= 4);

            var generatedIds = pubRes.GetProperty("publishedSlots").EnumerateArray().Select(s => s.GetProperty("availabilityId").GetInt32()).ToList();

            // Admin reviews & approves generated slots via HTTP.
            var approveResp = await http.SendAsync(AuthReq(HttpMethod.Post, "/api/admin/mentor-availabilities/review?approve=true", adminJwt, generatedIds));
            Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

            // Published 2-session Group Plan: a legacy (pre-10-week-rule) row seeded directly; the API now only accepts
            // policy-standard plans (10 weeks x 2/3/4 sessions) — covered by GroupLearningProductRulesPostgresTests.
            var nonStandardPlan = await http.SendAsync(AuthReq(HttpMethod.Post, "/api/study-courses/plans", adminJwt,
                new SaveCoursePlan("JLPT N3 Nhóm", levelId, 2, 3, 90, 2, 30, 1500000m, true, 1, "Group", 4)));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, nonStandardPlan.StatusCode);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var legacyPlan = new StudyCoursePlan
                {
                    Name = "JLPT N3 Nhóm", CertificateLevelId = levelId, DurationMonths = 2, SessionsPerWeek = 3, SessionMinutes = 90,
                    TotalSessions = 2, RetakeClaimDays = 30, Price = 1500000m, IsPublished = true, SessionType = "Group", Capacity = 4
                };
                db.StudyCoursePlans.Add(legacyPlan);
                await db.SaveChangesAsync();
                planId = legacyPlan.Id;
            }

            // Pick 2 future non-adjacent slots (spaced >= 15m rest) for Cohort A + 1 spare slot for reschedule test.
            var slot1 = generatedIds[0];
            var slot2 = generatedIds[2]; // different day or non-overlapping
            var spareSlot = generatedIds[4];

            var cohortARes = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, "/api/study-courses/cohorts", adminJwt,
                new SaveStudyCohort(planId, "N3 Nhóm T2-4-6", mentorId, 4, [slot1, slot2], true))));
            var cohortAId = cohortARes.GetProperty("id").GetInt32();

            // Confirm 1-1 slot browsing endpoint NEVER exposes Cohort A slots!
            var fromIso = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));
            var toIso = Uri.EscapeDataString(DateTime.UtcNow.AddDays(25).ToString("O"));
            var oneOnOneSlots = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor-meetings/availability?from={fromIso}&to={toIso}", learnerJwts[0])));
            var exposedIds = oneOnOneSlots.EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).ToHashSet();
            Assert.DoesNotContain(slot1, exposedIds);
            Assert.DoesNotContain(slot2, exposedIds);

            // Confirm Mentor & Admin slot views expose isInCohort + cohortId, and 1-1 request-cancel is blocked 409 on Cohort slots!
            var mentorMySlots = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/mentor/availability", mentorJwt)));
            var slot1MentorView = mentorMySlots.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == slot1);
            Assert.True(slot1MentorView.GetProperty("isInCohort").GetBoolean());
            Assert.Equal(cohortAId, slot1MentorView.GetProperty("cohortId").GetInt32());

            var adminSlotsView = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/admin/mentor-availabilities", adminJwt)));
            var slot1AdminView = adminSlotsView.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == slot1);
            Assert.True(slot1AdminView.GetProperty("isInCohort").GetBoolean());
            Assert.Equal(cohortAId, slot1AdminView.GetProperty("cohortId").GetInt32());

            var cohortSlotCancelResp = await http.SendAsync(AuthReq(HttpMethod.Post, "/api/mentor/availability/request-cancel", mentorJwt,
                new RikiPath.Application.Requests.MentorMeetings.CancelAvailabilitiesRequest { Ids = [slot1], Reason = "Should fail" }));
            Assert.Equal(HttpStatusCode.Conflict, cohortSlotCancelResp.StatusCode);

            // =========================================================================
            // PRODUCTION FAIL-CLOSED ENTITLEMENT CHECK + NO 1-1 CREDIT CONVERSION
            // =========================================================================
            // Without an entitlement the canonical path is a paid PayOS checkout (never a free enrollment).
            paymentMock.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/e2e", "plink", ""));
            paymentMock.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/e2e", "plink", ""));
            var cohortsBefore = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/cohorts", learnerJwts[0])));
            var cohortAView = cohortsBefore.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == cohortAId);
            Assert.True(cohortAView.GetProperty("enrollmentOpen").GetBoolean());
            Assert.False(cohortAView.GetProperty("hasApprovedEntitlement").GetBoolean());
            Assert.Equal("PayOS", cohortAView.GetProperty("checkoutMode").GetString());

            var paidCheckoutResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[0],
                new EnrollCohortRequest(Guid.NewGuid())));
            Assert.Equal(HttpStatusCode.OK, paidCheckoutResp.StatusCode);
            var paidCheckout = await ReadResultAsync(paidCheckoutResp);
            Assert.Equal("Pending", paidCheckout.GetProperty("paymentStatus").GetString());
            Assert.StartsWith("https://", paidCheckout.GetProperty("checkoutUrl").GetString());
            var pendingCheckoutId = paidCheckout.GetProperty("id").GetInt32();

            // Release the seat held by the pending checkout so the scenarios below keep the original capacity.
            var releaseResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{pendingCheckoutId}/cancel-pending-enrollment", learnerJwts[0]));
            Assert.Equal(HttpStatusCode.OK, releaseResp.StatusCode);

            // Attempt to use a 1-1 Consultation Credit reference directly is rejected 409;
            // Admin attempting to grant SourceType = "ConsultationCredit" is rejected 400!
            var creditRefEnrollResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[0],
                new EnrollCohortRequest(Guid.NewGuid(), "CREDIT-1ON1-999")));
            Assert.Equal(HttpStatusCode.Conflict, creditRefEnrollResp.StatusCode);

            var creditGrantRejectResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/entitlements", adminJwt,
                new GrantCohortEntitlementRequest(learnerIds[0], SourceType: "ConsultationCredit", Note: "Must fail")));
            Assert.Equal(HttpStatusCode.BadRequest, creditGrantRejectResp.StatusCode);
            // Only the single paid checkout above created a payment link; credit/grant rejections never did.
            paymentMock.Verify(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);

            // =========================================================================
            // SCENARIO B: Admin Grants Real Entitlements -> Revoke Test -> Re-grant -> Enroll 1..3 -> Concurrent Race -> Cancel -> Reclaim -> Capacity -> Cutoff
            // =========================================================================
            // Grant & Revoke lifecycle check on Learner 6 first:
            var l6GrantTemp = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/entitlements", adminJwt,
                new GrantCohortEntitlementRequest(learnerIds[5], SourceType: "AdminManualGrant", Note: "Temp grant"))));
            var l6GrantId = l6GrantTemp.GetProperty("id").GetInt32();
            var revokeResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/entitlements/{l6GrantId}/revoke", adminJwt,
                new RevokeCohortEntitlementRequest("Hủy tạm trước khi cấp lại")));
            Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

            // Grant real DB entitlements for Learners 1..6 on Cohort A:
            var cohortARefs = new string[6];
            for (var i = 0; i < 6; i++)
            {
                var g = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/entitlements", adminJwt,
                    new GrantCohortEntitlementRequest(learnerIds[i], i == 0 ? "ScholarshipGrant" : "AdminManualGrant", Note: $"E2E Cohort A L{i + 1}"))));
                cohortARefs[i] = g.GetProperty("entitlementReference").GetString()!;
            }

            // Confirm GET /cohorts now sees Approved Entitlement & EnrollmentOpen == true for Learner 1!
            var myEnts = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/my-entitlements", learnerJwts[0])));
            Assert.Contains(myEnts.EnumerateArray(), x => x.GetProperty("entitlementReference").GetString() == cohortARefs[0] && x.GetProperty("isUsable").GetBoolean());
            var cohortsWithEnt = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/cohorts", learnerJwts[0])));
            var cohortAOpenView = cohortsWithEnt.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == cohortAId);
            Assert.True(cohortAOpenView.GetProperty("enrollmentOpen").GetBoolean());
            Assert.True(cohortAOpenView.GetProperty("hasApprovedEntitlement").GetBoolean());

            int[] enrolledCourseIds = new int[4];
            for (var i = 0; i < 3; i++)
            {
                var enroll = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[i],
                    new EnrollCohortRequest(Guid.NewGuid(), cohortARefs[i]))));
                enrolledCourseIds[i] = enroll.GetProperty("id").GetInt32();
                Assert.Equal("Active", enroll.GetProperty("status").GetString());
            }

            // Verify consumed entitlement ledger rows cannot be reused by Learner 1!
            var myEntsAfterConsume = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/my-entitlements", learnerJwts[0])));
            Assert.Contains(myEntsAfterConsume.EnumerateArray(), x =>
                x.GetProperty("entitlementReference").GetString() == cohortARefs[0]
                && x.GetProperty("status").GetString() == "Consumed"
                && !x.GetProperty("isUsable").GetBoolean());

            // Concurrent race for last (4th) seat between Learner 4 and Learner 5 using their real entitlements.
            var raceResponses = await Task.WhenAll(
                http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[3], new EnrollCohortRequest(Guid.NewGuid(), cohortARefs[3]))),
                http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[4], new EnrollCohortRequest(Guid.NewGuid(), cohortARefs[4]))));

            Assert.Single(raceResponses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(raceResponses, r => r.StatusCode == HttpStatusCode.Conflict);

            var winnerIdx = raceResponses[0].StatusCode == HttpStatusCode.OK ? 3 : 4;
            var loserIdx = winnerIdx == 3 ? 4 : 3;
            var winnerResult = await ReadResultAsync(raceResponses[winnerIdx == 3 ? 0 : 1]);
            var winnerCourseId = winnerResult.GetProperty("id").GetInt32();

            // Learner winner cancels (> 24h before start) -> releases 1 seat, leaves Learners 1..3 intact, 1-1 credits untouched.
            var cancelResp = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{winnerCourseId}/cancel-enrollment", learnerJwts[winnerIdx],
                new CancelCohortEnrollmentRequest("Bận lịch đột xuất"))));
            Assert.True(cancelResp.GetProperty("seatReleased").GetBoolean());

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var creditGrant = await db.MentorCreditGrants.AsNoTracking().SingleAsync(g => g.LearnerId == learnerIds[0]);
                Assert.Equal(3, creditGrant.AvailableQuantity);
            }

            // Loser Learner now takes released 4th seat using their still-Active entitlement!
            var reclaimRes = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[loserIdx],
                new EnrollCohortRequest(Guid.NewGuid(), cohortARefs[loserIdx]))));
            enrolledCourseIds[3] = reclaimRes.GetProperty("id").GetInt32();

            // Learner 6 (even with valid entitlement) is blocked at Cohort capacity (4/4)!
            var fifthResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortAId}/enroll", learnerJwts[5],
                new EnrollCohortRequest(Guid.NewGuid(), cohortARefs[5])));
            Assert.Equal(HttpStatusCode.Conflict, fifthResp.StatusCode);

            // Admin reschedules Cohort A Session 2 across ALL active learners without duplicates.
            var l1CourseDetail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{enrolledCourseIds[0]}", learnerJwts[0])));
            var session2Id = l1CourseDetail.GetProperty("sessions")[1].GetProperty("id").GetInt32();
            var changeReqResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{enrolledCourseIds[0]}/change-requests", learnerJwts[0],
                new RequestCourseChange(session2Id, "Move", "Cả lớp xin dời buổi 2 sang ngày rảnh khác")));
            Assert.Equal(HttpStatusCode.OK, changeReqResp.StatusCode);

            var changeReqs = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/change-requests", adminJwt)));
            var reqId = changeReqs.EnumerateArray().First(r => r.GetProperty("courseId").GetInt32() == enrolledCourseIds[0]).GetProperty("id").GetInt32();

            var reviewChangeResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{enrolledCourseIds[0]}/change-requests/{reqId}/review", adminJwt,
                new ReviewCourseChange(true, "Admin duyệt dời cho cả lớp", spareSlot, EntireClassConfirmed: true)));
            Assert.Equal(HttpStatusCode.OK, reviewChangeResp.StatusCode);

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var activeEnrollments = await db.StudyCourses.Include(c => c.Sessions)
                    .Where(c => c.CohortId == cohortAId && c.Status == "Active")
                    .ToListAsync();
                Assert.Equal(4, activeEnrollments.Count);
                Assert.All(activeEnrollments, e =>
                {
                    Assert.Equal(2, e.Sessions.Count);
                    Assert.Contains(e.Sessions, s => s.AvailabilityId == spareSlot);
                    Assert.DoesNotContain(e.Sessions, s => s.AvailabilityId == slot2);
                });
            }

            // Cutoff test: Cohort inside 60-minute cutoff rejects enrollment even with open seats.
            int cutoffPlanId, cutoffCohortId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var policyV2 = await db.MentorGroupPolicies.SingleAsync(p => p.Version == 2);
                var ruleId = await db.MentorScheduleRules.Select(r => r.Id).FirstAsync();
                var cutStart = DateTime.UtcNow.AddMinutes(30);
                var cutoffSlot = new MentorAvailability
                {
                    MentorId = mentorId,
                    MentorScheduleRuleId = ruleId,
                    GenerationKey = $"e2e-cutoff-{Guid.NewGuid():N}",
                    LocalBusinessDate = vnToday,
                    StartTime = cutStart,
                    EndTime = cutStart.AddMinutes(90),
                    RoomId = Guid.NewGuid(),
                    GroupPolicyId = policyV2.Id,
                    PolicyVersion = 2,
                    SessionMinutesSnapshot = 90,
                    RestMinutesSnapshot = 15,
                    MaxLearners = 4,
                    IsApproved = true,
                    PublishedAt = DateTime.UtcNow.AddHours(-1)
                };
                var cutoffPlan = new StudyCoursePlan
                {
                    Name = "N3 Cutoff Check", CertificateLevelId = levelId, DurationMonths = 1, SessionsPerWeek = 1,
                    SessionMinutes = 90, TotalSessions = 1, Price = 500000m, IsPublished = true, SessionType = "Group", Capacity = 4
                };
                db.AddRange(cutoffSlot, cutoffPlan);
                await db.SaveChangesAsync();
                cutoffPlanId = cutoffPlan.Id;

                var cohortCut = new StudyCohort
                {
                    Name = "Cutoff Cohort", PlanId = cutoffPlanId, MentorId = mentorId, Capacity = 4, CapacitySnapshot = 4,
                    GroupPolicyId = policyV2.Id, PolicyVersionSnapshot = policyV2.Version, Status = "Published",
                    StartsAt = cutoffSlot.StartTime, EndsAt = cutoffSlot.EndTime,
                    Sessions = [new StudyCohortSession { Number = 1, AvailabilityId = cutoffSlot.Id }]
                };
                db.Add(cohortCut);
                await db.SaveChangesAsync();
                cutoffCohortId = cohortCut.Id;
            }
            // Grant real entitlement on Cutoff Cohort too so we specifically prove 60m cutoff blocks even with valid entitlement!
            var cutoffGrant = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cutoffCohortId}/entitlements", adminJwt,
                new GrantCohortEntitlementRequest(learnerIds[5], "AdminManualGrant"))));
            var cutoffEnroll = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cutoffCohortId}/enroll", learnerJwts[5],
                new EnrollCohortRequest(Guid.NewGuid(), cutoffGrant.GetProperty("entitlementReference").GetString())));
            Assert.Equal(HttpStatusCode.Conflict, cutoffEnroll.StatusCode);

            // =========================================================================
            // SCENARIO C & D: Active Live Cohort C -> Shared Room Access, SignalR Reconnect, Skill Profile & Homework
            // =========================================================================
            int cohortCId, livePlanId;
            int[] cCourseIds = new int[4];
            sessionWindowStart = DateTime.UtcNow.AddHours(3);
            sessionWindowEnd = sessionWindowStart.AddMinutes(90);
            int liveSlotId;

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var policyV2 = await db.MentorGroupPolicies.SingleAsync(p => p.Version == 2);
                var ruleId = await db.MentorScheduleRules.Select(r => r.Id).FirstAsync();
                var liveSlot = new MentorAvailability
                {
                    MentorId = mentorId,
                    MentorScheduleRuleId = ruleId,
                    GenerationKey = $"e2e-live-{Guid.NewGuid():N}",
                    LocalBusinessDate = vnToday,
                    StartTime = sessionWindowStart,
                    EndTime = sessionWindowEnd,
                    RoomId = Guid.NewGuid(),
                    GroupPolicyId = policyV2.Id,
                    PolicyVersion = 2,
                    SessionMinutesSnapshot = 90,
                    RestMinutesSnapshot = 15,
                    MaxLearners = 4,
                    IsApproved = true,
                    PublishedAt = DateTime.UtcNow.AddDays(-1)
                };
                var livePlan = new StudyCoursePlan
                {
                    Name = "N3 Live Group", CertificateLevelId = levelId, DurationMonths = 1, SessionsPerWeek = 3,
                    SessionMinutes = 90, TotalSessions = 1, Price = 800000m, IsPublished = true, SessionType = "Group", Capacity = 4
                };
                db.AddRange(liveSlot, livePlan);
                await db.SaveChangesAsync();
                liveSlotId = liveSlot.Id;
                livePlanId = livePlan.Id;
                activeRoomId = liveSlot.RoomId;

                var cohortC = new StudyCohort
                {
                    Name = "Live Group C", PlanId = livePlanId, MentorId = mentorId, Capacity = 4, CapacitySnapshot = 4,
                    GroupPolicyId = policyV2.Id, PolicyVersionSnapshot = policyV2.Version, Status = "Published",
                    StartsAt = liveSlot.StartTime, EndsAt = liveSlot.EndTime,
                    Sessions = [new StudyCohortSession { Number = 1, AvailabilityId = liveSlot.Id }]
                };
                db.Add(cohortC);
                await db.SaveChangesAsync();
                cohortCId = cohortC.Id;
            }

            // Enroll 4 Learners before cutoff (3h ahead > 60m cutoff) using real Admin-issued entitlements.
            for (var i = 0; i < 4; i++)
            {
                var grant = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortCId}/entitlements", adminJwt,
                    new GrantCohortEntitlementRequest(learnerIds[i], "AdminManualGrant"))));
                var enroll = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{cohortCId}/enroll", learnerJwts[i],
                    new EnrollCohortRequest(Guid.NewGuid(), grant.GetProperty("entitlementReference").GetString()))));
                cCourseIds[i] = enroll.GetProperty("id").GetInt32();
            }

            // Simulate wall-clock time reaching live session start window (StartsAt = UtcNow + 5m, inside -10m..EndTime).
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var slot = await db.MentorAvailabilities.SingleAsync(x => x.Id == liveSlotId);
                slot.StartTime = now.AddMinutes(5);
                slot.EndTime = now.AddMinutes(95);
                var c = await db.StudyCohorts.Include(x => x.Enrollments).ThenInclude(x => x.Sessions).SingleAsync(x => x.Id == cohortCId);
                c.StartsAt = slot.StartTime;
                c.EndsAt = slot.EndTime;
                foreach (var e in c.Enrollments)
                {
                    e.StartsAt = slot.StartTime;
                    e.EndsAt = slot.EndTime;
                    foreach (var s in e.Sessions) s.ScheduledAt = slot.StartTime;
                }
                await db.SaveChangesAsync();
            }

            // Verify Shared RoomId access: Mentor + 4 Learners = 200 OK; Stranger = 403 Forbidden.
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor-meetings/rooms/{activeRoomId}/access", mentorJwt))).StatusCode);
            for (var i = 0; i < 4; i++)
                Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor-meetings/rooms/{activeRoomId}/access", learnerJwts[i]))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor-meetings/rooms/{activeRoomId}/access", learnerJwts[5]))).StatusCode);

            // Real SignalR WebSocket joins & Mentor disconnect + reconnect keeps 1 MentorAttendanceRecord!
            using (var mentorWs1 = await ConnectSignalRRoomAsync(baseUrl, mentorJwt))
            using (var learner1Ws = await ConnectSignalRRoomAsync(baseUrl, learnerJwts[0]))
            using (var strangerWs = await ConnectSignalRRoomAsync(baseUrl, learnerJwts[5]))
            {
                var mJoin1 = await SendSignalRHubInvokeAsync(mentorWs1, "m1", "JoinRoom", activeRoomId, "Sora Sensei", "Mentor");
                Assert.False(mJoin1.TryGetProperty("error", out _), mJoin1.ToString());

                var lJoin1 = await SendSignalRHubInvokeAsync(learner1Ws, "l1", "JoinRoom", activeRoomId, "HV1", "Learner");
                Assert.False(lJoin1.TryGetProperty("error", out _), lJoin1.ToString());

                var strangerJoin = await SendSignalRHubInvokeAsync(strangerWs, "s1", "JoinRoom", activeRoomId, "Stranger", "Learner");
                Assert.True(strangerJoin.TryGetProperty("error", out _));

                await Task.Delay(1100); // Ensure non-zero elapsed seconds on reconnect.
                await mentorWs1.CloseAsync(WebSocketCloseStatus.NormalClosure, "drop", CancellationToken.None);
            }
            await Task.Delay(300);
            using (var mentorWs2 = await ConnectSignalRRoomAsync(baseUrl, mentorJwt))
            {
                var mJoin2 = await SendSignalRHubInvokeAsync(mentorWs2, "m2", "JoinRoom", activeRoomId, "Sora Sensei", "Mentor");
                Assert.False(mJoin2.TryGetProperty("error", out _), mJoin2.ToString());

                var roomAttendance = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor-meetings/rooms/{activeRoomId}/attendance", adminJwt)));
                var mentorAttRecords = roomAttendance.EnumerateArray().Where(a => a.GetProperty("userId").GetInt32() == mentorId).ToList();
                Assert.Single(mentorAttRecords);
                Assert.True(mentorAttRecords[0].GetProperty("reconnectionCount").GetInt32() >= 1);
            }

            // SCENARIO D: Cohort-scoped Skill Profile & Cross-cohort 403
            var skillProfile = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor/learners/{learnerIds[0]}/skill-profile?cohortId={cohortCId}", mentorJwt)));
            var skillsArr = skillProfile.GetProperty("skills").EnumerateArray().ToList();
            Assert.Equal(80.0, skillsArr.Single(s => s.GetProperty("skill").GetString() == "Reading").GetProperty("averageScorePercent").GetDouble());
            Assert.Equal(85.0, skillsArr.Single(s => s.GetProperty("skill").GetString() == "Grammar").GetProperty("averageScorePercent").GetDouble());
            Assert.False(skillsArr.Single(s => s.GetProperty("skill").GetString() == "Speaking").TryGetProperty("averageScorePercent", out _));

            // Unauthorized other mentor + outsider 403:
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor/learners/{learnerIds[0]}/skill-profile?cohortId={cohortCId}", mentorBJwt))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor/learners/{learnerIds[5]}/skill-profile?cohortId={cohortCId}", mentorJwt))).StatusCode);

            // Homework Common + Individual + Submit/Revise + Private AI Draft + Outage Manual Publish
            var commonHwIds = (await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/homework/cohorts/{cohortCId}", mentorJwt,
                new CreateHomeworkRequest(null, "Bài tập chung lớp C", "Viết 5 câu N3", DateTimeOffset.UtcNow.AddDays(2), null, null)))))
                .EnumerateArray().Select(e => e.GetInt32()).ToList();
            Assert.Equal(4, commonHwIds.Count);

            var indivHwIds = (await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/homework/cohorts/{cohortCId}", mentorJwt,
                new CreateHomeworkRequest(null, "Bài bổ sung HV1", "Ôn trợ từ", DateTimeOffset.UtcNow.AddDays(2), null, null, TargetLearnerIds: [learnerIds[0]])))))
                .EnumerateArray().Select(e => e.GetInt32()).ToList();
            Assert.Single(indivHwIds);

            var l1HwList = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/homework?cohortId={cohortCId}", learnerJwts[0])));
            var l1CommonHwId = l1HwList.EnumerateArray().First(h => h.GetProperty("title").GetString() == "Bài tập chung lớp C").GetProperty("id").GetInt32();
            var l1IndivHwId = indivHwIds[0];

            // L1 submits text + real multipart PNG scan upload and revises before grading!
            using (var scanForm = new MultipartFormDataContent())
            {
                scanForm.Add(new StringContent("Bản nháp lần 1 kèm ảnh scan"), "textContent");
                var pngBytes = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]);
                pngBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                scanForm.Add(pngBytes, "scan", "handwriting.png");
                var scanReq = new HttpRequestMessage(HttpMethod.Post, $"/api/homework/{l1CommonHwId}/submit-scan") { Content = scanForm };
                scanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", learnerJwts[0]);
                var scanSubRes = await ReadResultAsync(await http.SendAsync(scanReq));
                Assert.True(scanSubRes.GetProperty("hasScan").GetBoolean());
            }
            var scanDownloadResp = await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/homework/{l1CommonHwId}/submission-scan", mentorJwt));
            Assert.Equal(HttpStatusCode.OK, scanDownloadResp.StatusCode);
            Assert.Equal("no-store", scanDownloadResp.Headers.CacheControl?.ToString());

            var revisedSub = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Put, $"/api/homework/{l1CommonHwId}/submit", learnerJwts[0],
                new SubmitHomeworkRequest("Bản sửa lần 2", "https://cdn.example.test/hw1-v2.png"))));
            Assert.Equal(2, revisedSub.GetProperty("revisionCount").GetInt32());

            // Mentor generates AI draft -> visible to Mentor only, hidden from Learner.
            var aiDraftRes = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/homework/{l1CommonHwId}/ai-draft", mentorJwt)));
            Assert.Contains("AI Draft Feedback", aiDraftRes.GetProperty("feedback").GetString());

            var l1VisibleBeforeGrade = (await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/homework?cohortId={cohortCId}", learnerJwts[0]))))
                .EnumerateArray().Single(h => h.GetProperty("id").GetInt32() == l1CommonHwId);
            Assert.False(l1VisibleBeforeGrade.TryGetProperty("aiDraft", out _));
            Assert.False(l1VisibleBeforeGrade.TryGetProperty("publishedGrade", out _));

            // L1 submits individual assignment; simulate AI outage -> manual grading & publish still succeeds!
            await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/homework/{l1IndivHwId}/submit", learnerJwts[0],
                new SubmitHomeworkRequest("Bài cá nhân", null))));
            aiGenerator.SimulateOutage = true;
            var outageResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/homework/{l1IndivHwId}/ai-draft", mentorJwt));
            Assert.Equal(HttpStatusCode.BadGateway, outageResp.StatusCode);
            Assert.Contains("HOMEWORK_AI_DRAFT_UNAVAILABLE", await outageResp.Content.ReadAsStringAsync());

            var manualGrade = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/homework/{l1IndivHwId}/grade", mentorJwt,
                new GradeHomeworkAssignmentRequest(true, "Chấm thủ công thành công dù AI gián đoạn", 95.0))));
            Assert.Equal("Chấm thủ công thành công dù AI gián đoạn", manualGrade.GetProperty("feedback").GetString());

            // Missing homework on Learner 2 NEVER blocks room access!
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor-meetings/rooms/{activeRoomId}/access", learnerJwts[1]))).StatusCode);

            // =========================================================================
            // SCENARIO E: Failed JLPT Evidence + Policy > vs >= + Mandatory Reason + KPI Deduplication (No Payroll)
            // =========================================================================
            int retakenSourceCourseId, nextCohortId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ruleId = await db.MentorScheduleRules.Select(r => r.Id).FirstAsync();
                var policyInclusive = await db.MentorGroupPolicies.SingleAsync(p => p.Version == 2);
                var pastStart = DateTime.UtcNow.AddDays(-20);
                var nextStart = DateTime.UtcNow.AddDays(30);
                var pastSlot = new MentorAvailability
                {
                    MentorId = mentorId,
                    StartTime = pastStart,
                    EndTime = pastStart.AddMinutes(90),
                    RoomId = Guid.NewGuid(),
                    MaxLearners = 4,
                    IsApproved = true
                };
                var nextSlot = new MentorAvailability
                {
                    MentorId = mentorId,
                    MentorScheduleRuleId = ruleId,
                    GenerationKey = $"e2e-next-{Guid.NewGuid():N}",
                    LocalBusinessDate = vnToday.AddDays(30),
                    StartTime = nextStart,
                    EndTime = nextStart.AddMinutes(90),
                    RoomId = Guid.NewGuid(),
                    GroupPolicyId = policyInclusive.Id,
                    PolicyVersion = 2,
                    SessionMinutesSnapshot = 90,
                    RestMinutesSnapshot = 15,
                    MaxLearners = 4,
                    IsApproved = true
                };
                var retPlan = new StudyCoursePlan
                {
                    Name = "N3 Hoàn thành", CertificateLevelId = levelId, DurationMonths = 2, SessionsPerWeek = 3,
                    SessionMinutes = 90, TotalSessions = 1, RetakeClaimDays = 60, Price = 1000000m, IsPublished = true, SessionType = "Group", Capacity = 4
                };
                var policyStrict = new MentorGroupPolicy
                {
                    Version = 3, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, SessionMinutes = 90,
                    RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 60, CancellationLeadHours = 24, ReschedulingLeadHours = 24,
                    HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2, RemediationCompletionThresholdPercent = 90m,
                    RemediationThresholdComparison = ">", RequireOfficialExamEvidence = true, RequireAdminRemediationReview = true,
                    KpiReportingPeriodDays = 30, CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
                };

                db.AddRange(pastSlot, nextSlot, retPlan, policyStrict);
                await db.SaveChangesAsync();

                var cohortCompletedStrict = new StudyCohort { Name = "Completed Strict", PlanId = retPlan.Id, MentorId = mentorId, Capacity = 4, GroupPolicyId = policyStrict.Id, PolicyVersionSnapshot = 3, Status = "Published", StartsAt = DateTime.UtcNow.AddDays(-25), EndsAt = DateTime.UtcNow.AddDays(-5) };
                var cohortCompletedInclusive = new StudyCohort { Name = "Completed Inclusive", PlanId = retPlan.Id, MentorId = mentorId, Capacity = 4, GroupPolicyId = policyInclusive.Id, PolicyVersionSnapshot = 2, Status = "Published", StartsAt = DateTime.UtcNow.AddDays(-25), EndsAt = DateTime.UtcNow.AddDays(-5) };
                var cohortNext = new StudyCohort
                {
                    Name = "Next Cohort", PlanId = retPlan.Id, MentorId = mentorId, Capacity = 4, GroupPolicyId = policyInclusive.Id,
                    PolicyVersionSnapshot = 2, Status = "Published", StartsAt = nextSlot.StartTime, EndsAt = nextSlot.EndTime,
                    Sessions = [new StudyCohortSession { Number = 1, AvailabilityId = nextSlot.Id }]
                };
                db.AddRange(cohortCompletedStrict, cohortCompletedInclusive, cohortNext);
                await db.SaveChangesAsync();
                nextCohortId = cohortNext.Id;

                var courseStrict = new StudyCourse
                {
                    LearnerId = learnerIds[1], MentorId = mentorId, CohortId = cohortCompletedStrict.Id, PlanId = retPlan.Id, Name = "Strict 90%",
                    CertificateLevelId = levelId, DurationMonths = 2, SessionsPerWeek = 3, SessionMinutes = 90, TotalSessions = 1, RetakeClaimDays = 60,
                    GroupPolicyId = policyStrict.Id, PolicyVersionSnapshot = 3, Amount = 1000000m, PaymentStatus = "Paid", Status = "Completed",
                    StartsAt = DateTime.UtcNow.AddDays(-25), EndsAt = DateTime.UtcNow.AddDays(-5), CompletedAt = DateTime.UtcNow.AddDays(-5),
                    PurchaseKey = Guid.NewGuid(), SessionType = "Group",
                    Sessions = [new StudyCourseSession { Number = 1, ScheduledAt = pastSlot.StartTime, Status = "Present", AvailabilityId = pastSlot.Id, ConfirmedAt = DateTime.UtcNow.AddDays(-20) }]
                };
                var courseInclusive = new StudyCourse
                {
                    LearnerId = learnerIds[0], MentorId = mentorId, CohortId = cohortCompletedInclusive.Id, PlanId = retPlan.Id, Name = "Inclusive 90%",
                    CertificateLevelId = levelId, DurationMonths = 2, SessionsPerWeek = 3, SessionMinutes = 90, TotalSessions = 1, RetakeClaimDays = 60,
                    GroupPolicyId = policyInclusive.Id, PolicyVersionSnapshot = 2, Amount = 1000000m, PaymentStatus = "Paid", Status = "Completed",
                    StartsAt = DateTime.UtcNow.AddDays(-25), EndsAt = DateTime.UtcNow.AddDays(-5), CompletedAt = DateTime.UtcNow.AddDays(-5),
                    PurchaseKey = Guid.NewGuid(), SessionType = "Group",
                    Sessions = [new StudyCourseSession { Number = 1, ScheduledAt = pastSlot.StartTime, Status = "Present", AvailabilityId = pastSlot.Id, ConfirmedAt = DateTime.UtcNow.AddDays(-20) }]
                };
                db.AddRange(courseStrict, courseInclusive);
                await db.SaveChangesAsync();
                retakenSourceCourseId = courseInclusive.Id;

                // Give BOTH courses 9/10 on-time Cohort HomeworkAssignments (exactly 90%).
                foreach (var targetCourse in new[] { courseStrict, courseInclusive })
                {
                    for (var k = 0; k < 10; k++)
                    {
                        var due = DateTime.UtcNow.AddDays(-10);
                        var submittedAt = k < 9 ? due.AddHours(-2) : due.AddHours(2);
                        db.HomeworkAssignments.Add(new HomeworkAssignment
                        {
                            MentorId = mentorId, LearnerId = targetCourse.LearnerId, CohortId = targetCourse.CohortId, StudyCourseId = targetCourse.Id,
                            AssignmentScope = "CohortCommon", Title = $"HW {k + 1}", Instructions = "Test", DueAt = due, Status = "Submitted",
                            Submission = new HomeworkSubmission { LearnerId = targetCourse.LearnerId, TextContent = "Ans", SubmittedAt = submittedAt }
                        });
                    }
                }
                await db.SaveChangesAsync();

                // Verify Strict > rejects 90% while Inclusive >= accepts 90% on GET /api/study-courses/{id}!
                var strictDetail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{courseStrict.Id}", learnerJwts[1])));
                Assert.False(strictDetail.GetProperty("eligibility").GetProperty("homeworkMet").GetBoolean());
                Assert.False(strictDetail.GetProperty("eligibility").GetProperty("canClaim").GetBoolean());
            }

            var inclusiveDetail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{retakenSourceCourseId}", learnerJwts[0])));
            Assert.True(inclusiveDetail.GetProperty("eligibility").GetProperty("homeworkMet").GetBoolean());
            Assert.True(inclusiveDetail.GetProperty("eligibility").GetProperty("canClaim").GetBoolean());

            // Missing reason on Cohort retake fails 400!
            using (var badForm = new MultipartFormDataContent())
            {
                badForm.Add(new StringContent(DateTime.UtcNow.AddDays(-4).ToString("O")), "examDate");
                badForm.Add(new StringContent(DateTime.UtcNow.AddDays(-1).ToString("O")), "resultPublishedAt");
                var badPdf = new ByteArrayContent("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n"u8.ToArray());
                badPdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                badForm.Add(badPdf, "evidence", "jlpt-fail.pdf");
                var badReq = new HttpRequestMessage(HttpMethod.Post, $"/api/study-courses/{retakenSourceCourseId}/retake") { Content = badForm };
                badReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", learnerJwts[0]);
                Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(badReq)).StatusCode);
            }

            // Upload real multipart failed JLPT evidence WITH mandatory reason.
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(DateTime.UtcNow.AddDays(-4).ToString("O")), "examDate");
            form.Add(new StringContent(DateTime.UtcNow.AddDays(-1).ToString("O")), "resultPublishedAt");
            form.Add(new StringContent("Thiêu 3 điểm phần đọc hiểu JLPT N3."), "reason");
            var pdfBytes = new ByteArrayContent("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n"u8.ToArray());
            pdfBytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(pdfBytes, "evidence", "jlpt-fail.pdf");

            var retakeReq = new HttpRequestMessage(HttpMethod.Post, $"/api/study-courses/{retakenSourceCourseId}/retake") { Content = form };
            retakeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", learnerJwts[0]);
            var retakeRes = await ReadResultAsync(await http.SendAsync(retakeReq));
            Assert.False(retakeRes.GetProperty("autoApproved").GetBoolean()); // Never auto-grants without Admin approval!

            var detailAfterReq = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{retakenSourceCourseId}", learnerJwts[0])));
            var reqItem = detailAfterReq.GetProperty("requests")[0];
            Assert.Equal("Thiêu 3 điểm phần đọc hiểu JLPT N3.", reqItem.GetProperty("reason").GetString());
            var retakeRequestId = reqItem.GetProperty("id").GetInt32();

            // Mentor CANNOT save support plan while retake is still Pending (409 Conflict)!
            var preSupportPlanResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{retakenSourceCourseId}/retake/{retakeRequestId}/support-plan", mentorJwt,
                new SaveRetakeSupportPlan("Kế hoạch sớm")));
            Assert.Equal(HttpStatusCode.Conflict, preSupportPlanResp.StatusCode);

            // Admin review without mandatory reason fails 400.
            var missingReasonResp = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{retakenSourceCourseId}/retake/{retakeRequestId}/review", adminJwt,
                new ReviewCourseRetake(true, true, "   ", CohortId: nextCohortId)));
            Assert.Equal(HttpStatusCode.BadRequest, missingReasonResp.StatusCode);

            // Admin approves retake into nextCohortId with verified JLPT failure + mandatory reason -> creates 1 free retake StudyCourse.
            var approveRetakeRes = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{retakenSourceCourseId}/retake/{retakeRequestId}/review", adminJwt,
                new ReviewCourseRetake(true, true, "Đã đối chiếu bảng điểm trượt JLPT N3 chính thức.", CohortId: nextCohortId))));
            Assert.True(approveRetakeRes.GetProperty("newCourseId").GetInt32() > 0);

            // After Admin approval, Mentor creates the official remediation support plan -> Learner views it!
            var supportPlanRes = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/{retakenSourceCourseId}/retake/{retakeRequestId}/support-plan", mentorJwt,
                new SaveRetakeSupportPlan("Lộ trình 2 tuần luyện đề đọc hiểu N3 + sửa lỗi ngữ pháp."))));
            Assert.Equal("Lộ trình 2 tuần luyện đề đọc hiểu N3 + sửa lỗi ngữ pháp.", supportPlanRes.GetProperty("mentorSupportPlan").GetString());
            var detailWithSupport = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{retakenSourceCourseId}", learnerJwts[0])));
            Assert.Equal("Lộ trình 2 tuần luyện đề đọc hiểu N3 + sửa lỗi ngữ pháp.", detailWithSupport.GetProperty("requests")[0].GetProperty("mentorSupportPlan").GetString());

            // KPI check: deduplicates 4-learner cohort sessions by AvailabilityId + contains NO payroll/salary fields!
            var fromKpi = vnToday.AddDays(-30).ToString("yyyy-MM-dd");
            var toKpi = vnToday.AddDays(30).ToString("yyyy-MM-dd");
            var kpiHttpResp = await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/mentor/kpi/summary?from={fromKpi}&to={toKpi}", mentorJwt));
            var kpiRaw = await kpiHttpResp.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, kpiHttpResp.StatusCode);

            using var kpiDoc = JsonDocument.Parse(kpiRaw);
            var kpiRes = kpiDoc.RootElement.GetProperty("result");
            // Cohort A (2 unique AvailabilityIds) + Cohort C (1) + Completed past (1) + Next retake (1) = 5 deduplicated sessions (NOT 4x = 20)!
            Assert.Equal(5, kpiRes.GetProperty("assignedCourseSessionsCount").GetInt32());
            Assert.Equal(1, kpiRes.GetProperty("completedValidSessions").GetInt32()); // Past session Present
            Assert.True(kpiRes.GetProperty("homeworkAssignedCount").GetInt32() >= 5);
            Assert.DoesNotContain("earning", kpiRaw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("payroll", kpiRaw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("salary", kpiRaw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("payout", kpiRaw, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (app is not null)
                await app.DisposeAsync();
            await DropSchemaAsync(schema, cs);
        }
    }
}
