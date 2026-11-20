//using System.Collections.Concurrent;
//using System.Text;
//using System.Text.Json;
//using Microsoft.AspNetCore.Http;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using RikiPath.Application;
//using RikiPath.Application.IClients;
//using RikiPath.Application.IServices;
//using RikiPath.Application.Requests.StudyCourses;
//using RikiPath.Application.Services;
//using RikiPath.Domain;
//using RikiPath.Domain.Entities;
//using RikiPath.Domain.Enums;
//using RikiPath.Infrastructure;
//using RikiPath.Infrastructure.Services;

//namespace RikiPath.Tests;

//// Kiểm thử tích hợp PostgreSQL (schema cô lập) cho Task05 S4:
//// đánh giá cố vấn lớp nhóm, khiếu nại có minh chứng và quyết định Admin, thông báo PaymentReview và tổng kết khóa học.
//public sealed class CohortFeedbackPostgresTests
//{
//    static CohortFeedbackPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

//    private const int AdminId = 9101, MentorA = 9102, MentorB = 9103, MentorUnrelated = 9104;
//    private static readonly int[] Learners = [9111, 9112];

//    private sealed class InMemoryPrivateStorage : IFileStorageService
//    {
//        private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _files = new();

//        public Task<UploadedFileResult> UploadAsync(
//            Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken)
//            => Task.FromResult(new UploadedFileResult($"https://cdn.invalid/{folder}/{fileName}", fileName, fileStream.Length));

//        public Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken)
//            => Task.CompletedTask;

//        public Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken cancellationToken)
//            => Task.FromResult(storedUrl);

//        public async Task<UploadedFileResult> UploadPrivateVerificationAsync(
//            Stream fileStream, string contentType, int ownerId, CancellationToken cancellationToken)
//        {
//            using var ms = new MemoryStream();
//            await fileStream.CopyToAsync(ms, cancellationToken);
//            var bytes = ms.ToArray();
//            var url = $"private/feedback-{ownerId}-{Guid.NewGuid():N}";
//            _files[url] = (bytes, contentType);
//            return new UploadedFileResult(url, url, bytes.Length);
//        }

//        public Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
//            => _files.ContainsKey(storedUrl) ? Task.CompletedTask : Task.FromException(new FileNotFoundException(storedUrl));

//        public Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken)
//        {
//            if (!_files.TryGetValue(storedUrl, out var item))
//                throw new FileNotFoundException(storedUrl);
//            return Task.FromResult(new PrivateVerificationReadResult(new MemoryStream(item.Bytes, writable: false), item.ContentType));
//        }
//    }

//    private sealed class World
//    {
//        public required string Schema { get; init; }
//        public required string Cs { get; init; }
//        public int LevelN3 { get; set; }
//        public int PlanN3 { get; set; }
//        public InMemoryPrivateStorage Storage { get; } = new();
//        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cs).Options);
//    }

//    private static IClaimService Claims(int userId, Role role)
//        => Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role });

//    private static StudyCourseService Service(
//        World w, AppDbContext db, int userId, Role role, IMentorLearnerSkillProfileService? skillProfiles = null)
//        => new(
//            new UnitOfWork(db),
//            Claims(userId, role),
//            Mock.Of<IPaymentGatewayClient>(MockBehavior.Strict),
//            w.Storage,
//            new AppSettings { FrontendBaseUrl = "https://test.invalid" },
//            groupPolicies: new MentorGroupPolicyService(db),
//            skillProfiles: skillProfiles);

//    private static DateTime Utc(DateTime v) => new(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0, DateTimeKind.Utc);

//    private static async Task<World> CreateWorldAsync()
//    {
//        var baseCs = Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!;
//        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(baseCs);
//        var w = new World { Schema = schema, Cs = cs };
//        await using var db = w.Db();

//        UserAccount User(int id, Role role, string first, MentorVerificationStatus? status = null) => new()
//        {
//            Id = id, Role = role, FirstName = first, LastName = id.ToString(), Email = $"fb{id}@test.invalid",
//            PasswordHash = [], PasswordSalt = [],
//            MentorProfile = status is null ? null : new MentorProfile { VerificationStatus = status.Value, JlptLevel = "N3" }
//        };

//        db.Users.AddRange(
//            User(AdminId, Role.Admin, "Admin"),
//            User(MentorA, Role.Mentor, "MentorA", MentorVerificationStatus.Approved),
//            User(MentorB, Role.Mentor, "MentorB", MentorVerificationStatus.Approved),
//            User(MentorUnrelated, Role.Mentor, "MentorC", MentorVerificationStatus.Approved),
//            User(Learners[0], Role.Learner, "LearnerOne"),
//            User(Learners[1], Role.Learner, "LearnerTwo"));

//        var type = new CertificateType { Name = "JLPT", IsActive = true };
//        var n3 = new CertificateLevel { Code = "N3", CertificateType = type, SortOrder = 3 };
//        var plan = new StudyCoursePlan
//        {
//            Name = "N3 Group Feedback", CertificateLevel = n3, SessionType = "Group", Capacity = 4,
//            SessionMinutes = 90, TotalSessions = 3, DurationMonths = 2, SessionsPerWeek = 3, Price = 500_000m, IsPublished = true
//        };
//        var policy = new MentorGroupPolicy
//        {
//            Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4,
//            EnrollmentCutoffMinutesBeforeFirstSession = 60, SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4,
//            PublicationHorizonDays = 30, CancellationLeadHours = 24, ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7,
//            PlacementAttemptLimit = 2, RemediationCompletionThresholdPercent = 90m, RemediationThresholdComparison = ">",
//            KpiReportingPeriodDays = 30, RecurringWeekdayPatternsJson = "[[1,3,5]]",
//            LearnerTransferEnabled = true, MentorReplacementEnabled = true, MentorReplacementLeadHours = 24,
//            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
//        };
//        db.AddRange(n3, plan, policy);
//        await db.SaveChangesAsync();

//        foreach (var mId in new[] { MentorA, MentorB, MentorUnrelated })
//        {
//            db.MentorTeachingQualifications.Add(new MentorTeachingQualification
//            {
//                MentorUserId = mId, CertificateLevelId = n3.Id, IsActive = true,
//                VerifiedByAdminId = AdminId, VerifiedAtUtc = DateTime.UtcNow.AddDays(-60)
//            });
//        }
//        await db.SaveChangesAsync();

//        w.LevelN3 = n3.Id;
//        w.PlanN3 = plan.Id;
//        return w;
//    }

//    private static Task DropAsync(World w) => CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(w.Schema, w.Cs);

//    private static async Task<(int CohortId, int CourseId)> SeedCohortEnrollmentAsync(
//        World w, int learnerId, int mentorId, DateTime[] starts, string[]? sessionStatuses = null)
//    {
//        await using var db = w.Db();
//        var cohort = new StudyCohort
//        {
//            PlanId = w.PlanN3, Name = "N3-Feedback", MentorId = mentorId, Capacity = 4, CapacitySnapshot = 4,
//            Status = "Published", StartsAt = starts.Min(), EndsAt = starts.Max().AddMinutes(90)
//        };
//        for (var i = 0; i < starts.Length; i++)
//        {
//            cohort.Sessions.Add(new StudyCohortSession
//            {
//                Number = i + 1,
//                Availability = new MentorAvailability
//                {
//                    MentorId = mentorId, StartTime = starts[i], EndTime = starts[i].AddMinutes(90),
//                    MaxLearners = 4, IsApproved = true, RoomId = Guid.NewGuid(), AdminName = "QA"
//                }
//            });
//        }
//        db.StudyCohorts.Add(cohort);
//        await db.SaveChangesAsync();

//        var course = new StudyCourse
//        {
//            CohortId = cohort.Id, MentorId = mentorId, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt,
//            SessionType = "Group", LearnerId = learnerId, PurchaseKey = Guid.NewGuid(), PlanId = w.PlanN3,
//            Name = "N3 Group Feedback", CertificateLevelId = w.LevelN3, PlanRevision = 1, DurationMonths = 2,
//            SessionsPerWeek = 3, TotalSessions = starts.Length, SessionMinutes = 90, Amount = 500_000m,
//            PaymentStatus = "Paid", Status = "Active", PaymentReference = $"PAID-{learnerId}-{cohort.Id}"
//        };
//        for (var i = 0; i < cohort.Sessions.Count; i++)
//        {
//            var s = cohort.Sessions[i];
//            var st = sessionStatuses is not null && i < sessionStatuses.Length ? sessionStatuses[i] : "Scheduled";
//            course.Sessions.Add(new StudyCourseSession
//            {
//                Number = s.Number, ScheduledAt = s.Availability.StartTime, AvailabilityId = s.AvailabilityId,
//                MeetingUrl = $"/meeting-room/{s.Availability.RoomId}", Status = st
//            });
//        }
//        db.StudyCourses.Add(course);
//        await db.SaveChangesAsync();
//        return (cohort.Id, course.Id);
//    }

//    private static IFormFile PdfEvidence(string name = "proof.pdf")
//    {
//        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 test evidence");
//        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "evidence", name)
//        {
//            Headers = new HeaderDictionary(),
//            ContentType = "application/pdf"
//        };
//    }

//    [CoursePostgresFact]
//    public async Task MentorReview_Rules_Eligibility_Duplicate_ReplacementHistory_ForeignAccess_Summary_And_NoSideEffects()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var t1 = Utc(DateTime.UtcNow.AddDays(-3));
//            var t2 = Utc(DateTime.UtcNow.AddDays(3));
//            var t3 = Utc(DateTime.UtcNow.AddDays(5));
//            var (cohortId, courseId) = await SeedCohortEnrollmentAsync(
//                w, Learners[0], MentorA, [t1, t2, t3], ["Scheduled", "Scheduled", "Scheduled"]);

//            // 1. Before any delivered (Present) session -> 422 COHORT_REVIEW_NOT_ELIGIBLE.
//            await using (var db = w.Db())
//            {
//                var early = await Service(w, db, Learners[0], Role.Learner).CreateCohortMentorReviewAsync(
//                    courseId, new CreateCohortMentorReviewRequest(MentorA, 5, "Chưa học buổi nào"), default);
//                Assert.Equal(422, early.StatusCode);
//                Assert.Equal("COHORT_REVIEW_NOT_ELIGIBLE", early.ErrorCode);
//            }

//            // Mark session 1 Present, then replace primary mentor to MentorB for sessions 2 & 3.
//            await using (var db = w.Db())
//            {
//                var course = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == courseId);
//                course.Sessions.Single(s => s.Number == 1).Status = "Present";
//                await db.SaveChangesAsync();

//                var replaceRes = await Service(w, db, AdminId, Role.Admin).ReplaceCohortMentorAsync(
//                    cohortId,
//                    new ReplaceCohortMentorRequest(MentorB, "Đổi cố vấn chính từ buổi 2", Guid.NewGuid(), 1, 2),
//                    default);
//                Assert.True(replaceRes.IsSuccess, replaceRes.ErrorMessage);
//            }

//            int reviewAId;
//            // 2. Historical mentor (MentorA taught session 1) is eligible; unrelated mentor rejected; foreign learner -> 403.
//            await using (var db = w.Db())
//            {
//                var foreign = await Service(w, db, Learners[1], Role.Learner).CreateCohortMentorReviewAsync(
//                    courseId, new CreateCohortMentorReviewRequest(MentorA, 4, "Không phải khóa của tôi"), default);
//                Assert.Equal(403, foreign.StatusCode);

//                var unrelated = await Service(w, db, Learners[0], Role.Learner).CreateCohortMentorReviewAsync(
//                    courseId, new CreateCohortMentorReviewRequest(MentorUnrelated, 5, "Cố vấn không liên quan"), default);
//                Assert.Equal(422, unrelated.StatusCode);
//                Assert.Equal("COHORT_REVIEW_NOT_ELIGIBLE", unrelated.ErrorCode);

//                // Negative rating (1 star) succeeds and creates NO transfer request, 1-1 booking, or subscription.
//                var okA = await Service(w, db, Learners[0], Role.Learner).CreateCohortMentorReviewAsync(
//                    courseId, new CreateCohortMentorReviewRequest(MentorA, 1, "Cần cải thiện tốc độ giảng bài."), default);
//                Assert.True(okA.IsSuccess, okA.ErrorMessage);
//                reviewAId = JsonSerializer.SerializeToElement(okA.Result).GetProperty("Id").GetInt32();

//                // Duplicate review for same (CourseId, MentorId) -> 409 COHORT_REVIEW_DUPLICATE.
//                var dup = await Service(w, db, Learners[0], Role.Learner).CreateCohortMentorReviewAsync(
//                    courseId, new CreateCohortMentorReviewRequest(MentorA, 5, "Đánh giá trùng"), default);
//                Assert.Equal(409, dup.StatusCode);
//                Assert.Equal("COHORT_REVIEW_DUPLICATE", dup.ErrorCode);

//                // Update own review succeeds; foreign learner update -> 403.
//                var foreignPut = await Service(w, db, Learners[1], Role.Learner).UpdateCohortMentorReviewAsync(
//                    courseId, reviewAId, new UpdateCohortMentorReviewRequest(5, "Sửa hộ"), default);
//                Assert.Equal(403, foreignPut.StatusCode);

//                var updated = await Service(w, db, Learners[0], Role.Learner).UpdateCohortMentorReviewAsync(
//                    courseId, reviewAId, new UpdateCohortMentorReviewRequest(2, "Đã trao đổi thêm với cố vấn."), default);
//                Assert.True(updated.IsSuccess, updated.ErrorMessage);
//                Assert.Equal(2, JsonSerializer.SerializeToElement(updated.Result).GetProperty("Rating").GetInt32());

//                // Negative rating must never trigger transfers, 1-1 bookings, subscriptions, or mentor payouts.
//                Assert.Equal(0, await db.CohortTransferRequests.CountAsync());
//                Assert.Equal(0, await db.MentorBookings.CountAsync());
//                Assert.Equal(0, await db.UserSubscriptions.CountAsync());
//                Assert.Equal(0, await db.MentorEarningEntries.CountAsync());
//            }

//            // 3. Access control for listing course reviews & mentor summary without learner identity.
//            await using (var db = w.Db())
//            {
//                Assert.Equal(403, (await Service(w, db, Learners[1], Role.Learner).CohortMentorReviewsAsync(courseId, default)).StatusCode);
//                Assert.Equal(403, (await Service(w, db, MentorUnrelated, Role.Mentor).CohortMentorReviewsAsync(courseId, default)).StatusCode);

//                var forMentorA = await Service(w, db, MentorA, Role.Mentor).CohortMentorReviewsAsync(courseId, default);
//                Assert.True(forMentorA.IsSuccess);
//                Assert.Single(JsonSerializer.SerializeToElement(forMentorA.Result).EnumerateArray());

//                var summary = await Service(w, db, Learners[1], Role.Learner).MentorCohortReviewSummaryAsync(MentorA, default);
//                Assert.True(summary.IsSuccess, summary.ErrorMessage);
//                var sumEl = JsonSerializer.SerializeToElement(summary.Result);
//                Assert.Equal(2.0, sumEl.GetProperty("AverageRating").GetDouble());
//                Assert.Equal(1, sumEl.GetProperty("ReviewCount").GetInt32());
//                var commentItem = sumEl.GetProperty("Comments").EnumerateArray().Single();
//                Assert.False(commentItem.TryGetProperty("LearnerId", out _));
//                Assert.False(commentItem.TryGetProperty("LearnerName", out _));
//                Assert.False(commentItem.TryGetProperty("CourseId", out _));

//                // Moderating IsHidden=true excludes the review from mentor summary.
//                var entity = await db.CohortMentorReviews.SingleAsync(r => r.Id == reviewAId);
//                entity.IsHidden = true;
//                await db.SaveChangesAsync();

//                var hiddenSummary = await Service(w, db, Learners[1], Role.Learner).MentorCohortReviewSummaryAsync(MentorA, default);
//                var hiddenEl = JsonSerializer.SerializeToElement(hiddenSummary.Result);
//                Assert.Equal(0, hiddenEl.GetProperty("ReviewCount").GetInt32());
//                Assert.Empty(hiddenEl.GetProperty("Comments").EnumerateArray());
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Complaint_Lifecycle_Idempotency_Privacy_Evidence_MentorResponse_And_AdminDecision_Outbox()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            DateTime[] starts = [Utc(DateTime.UtcNow.AddDays(-2)), Utc(DateTime.UtcNow.AddDays(2)), Utc(DateTime.UtcNow.AddDays(4))];
//            var (_, courseId) = await SeedCohortEnrollmentAsync(w, Learners[0], MentorA, starts, ["Present", "Scheduled", "Scheduled"]);

//            int teachingComplaintId;
//            int paymentComplaintId;
//            var keyTeaching = Guid.NewGuid();
//            var keyPayment = Guid.NewGuid();

//            // 1. Validation & creation + idempotency + foreign learner 403/404.
//            await using (var db = w.Db())
//            {
//                var learnerSvc = Service(w, db, Learners[0], Role.Learner);

//                // refundReviewRequested rejected for non-payment categories.
//                var badRefund = await learnerSvc.CreateCohortComplaintAsync(
//                    courseId, CohortComplaint.CategoryTeachingQuality,
//                    "Chất lượng giảng dạy chưa đạt như mô tả.", MentorA, true, Guid.NewGuid(), null, default);
//                Assert.Equal(422, badRefund.StatusCode);
//                Assert.Equal("COMPLAINT_REFUND_REVIEW_NOT_ALLOWED", badRefund.ErrorCode);

//                // Uninvolved mentor rejected.
//                var badMentor = await learnerSvc.CreateCohortComplaintAsync(
//                    courseId, CohortComplaint.CategoryMentorConduct,
//                    "Cố vấn không tham gia lớp học này.", MentorUnrelated, false, Guid.NewGuid(), null, default);
//                Assert.Equal(422, badMentor.StatusCode);
//                Assert.Equal("COMPLAINT_MENTOR_NOT_INVOLVED", badMentor.ErrorCode);

//                // Foreign learner cannot file complaint -> 403.
//                var foreignCreate = await Service(w, db, Learners[1], Role.Learner).CreateCohortComplaintAsync(
//                    courseId, CohortComplaint.CategoryOther,
//                    "Tôi không phải chủ sở hữu khóa học.", null, false, Guid.NewGuid(), null, default);
//                Assert.Equal(403, foreignCreate.StatusCode);

//                // Create complaint with evidence + idempotent retry.
//                var created = await learnerSvc.CreateCohortComplaintAsync(
//                    courseId, CohortComplaint.CategoryTeachingQuality,
//                    "Buổi học đầu tiên bắt đầu muộn và thiếu tài liệu ôn tập.", MentorA, false, keyTeaching, PdfEvidence(), default);
//                Assert.True(created.IsSuccess, created.ErrorMessage);
//                teachingComplaintId = JsonSerializer.SerializeToElement(created.Result).GetProperty("Id").GetInt32();

//                var retried = await learnerSvc.CreateCohortComplaintAsync(
//                    courseId, CohortComplaint.CategoryTeachingQuality,
//                    "Buổi học đầu tiên bắt đầu muộn và thiếu tài liệu ôn tập.", MentorA, false, keyTeaching, null, default);
//                Assert.True(retried.IsSuccess);
//                Assert.Equal(teachingComplaintId, JsonSerializer.SerializeToElement(retried.Result).GetProperty("Id").GetInt32());
//                Assert.Equal(1, await db.CohortComplaints.CountAsync());
//                Assert.Equal(1, await db.MentorOutboxEvents.CountAsync(e => e.EventKey == $"cohort-complaint:{teachingComplaintId}:created"));

//                // Create PaymentEnrollment complaint with refundReviewRequested=true (targeting MentorA to verify Mentor still cannot see payment fields or respond).
//                var paymentCreated = await learnerSvc.CreateCohortComplaintAsync(
//                    courseId, CohortComplaint.CategoryPaymentEnrollment,
//                    "Thanh toán đã trừ tiền nhưng đơn chưa được kích hoạt đúng hạn.", MentorA, true, keyPayment, null, default);
//                Assert.True(paymentCreated.IsSuccess, paymentCreated.ErrorMessage);
//                paymentComplaintId = JsonSerializer.SerializeToElement(paymentCreated.Result).GetProperty("Id").GetInt32();
//                Assert.True(JsonSerializer.SerializeToElement(paymentCreated.Result).GetProperty("RefundReviewRequested").GetBoolean());
//            }

//            // 2. Scope & privacy checks (404 outside scope; evidence admin/owner only; mentor sees no payment fields and cannot respond to PaymentEnrollment).
//            await using (var db = w.Db())
//            {
//                Assert.Equal(404, (await Service(w, db, Learners[1], Role.Learner).CohortComplaintDetailAsync(teachingComplaintId, default)).StatusCode);
//                Assert.Equal(404, (await Service(w, db, MentorUnrelated, Role.Mentor).CohortComplaintDetailAsync(teachingComplaintId, default)).StatusCode);

//                Assert.NotNull(await Service(w, db, AdminId, Role.Admin).CohortComplaintEvidenceAsync(teachingComplaintId, default));
//                Assert.NotNull(await Service(w, db, Learners[0], Role.Learner).CohortComplaintEvidenceAsync(teachingComplaintId, default));
//                Assert.Null(await Service(w, db, Learners[1], Role.Learner).CohortComplaintEvidenceAsync(teachingComplaintId, default));
//                Assert.Null(await Service(w, db, MentorA, Role.Mentor).CohortComplaintEvidenceAsync(teachingComplaintId, default));

//                var mentorList = await Service(w, db, MentorA, Role.Mentor).CohortComplaintsAsync(null, default);
//                Assert.True(mentorList.IsSuccess);
//                var items = JsonSerializer.SerializeToElement(mentorList.Result).EnumerateArray().ToList();
//                Assert.Equal(2, items.Count);
//                foreach (var item in items)
//                {
//                    Assert.False(item.TryGetProperty("RefundReviewRequested", out _));
//                    Assert.False(item.TryGetProperty("CoursePaymentStatus", out _));
//                    Assert.False(item.TryGetProperty("CoursePaymentReviewReason", out _));
//                }

//                // Mentor can respond to TeachingQuality -> status becomes UnderReview and notifies admins.
//                var mentorResp = await Service(w, db, MentorA, Role.Mentor).RespondCohortComplaintAsync(
//                    teachingComplaintId, new RespondCohortComplaintRequest("Đã bổ sung tài liệu và điều chỉnh tiến độ."), default);
//                Assert.True(mentorResp.IsSuccess, mentorResp.ErrorMessage);
//                Assert.Equal("UnderReview", JsonSerializer.SerializeToElement(mentorResp.Result).GetProperty("Status").GetString());
//                Assert.Equal(1, await db.MentorOutboxEvents.CountAsync(e => e.AggregateType == "CohortComplaint" && e.AggregateId == teachingComplaintId.ToString() && e.EventKey.Contains("mentor-responded")));

//                // Mentor CANNOT respond to PaymentEnrollment.
//                var blockedPaymentResp = await Service(w, db, MentorA, Role.Mentor).RespondCohortComplaintAsync(
//                    paymentComplaintId, new RespondCohortComplaintRequest("Cố vấn thử phản hồi thanh toán"), default);
//                Assert.False(blockedPaymentResp.IsSuccess);
//                Assert.Equal("COMPLAINT_MENTOR_RESPONSE_NOT_ALLOWED", blockedPaymentResp.ErrorCode);
//            }

//            // 3. Admin decision requires text and notifies via outbox (learner + mentor when not PaymentEnrollment; learner only for PaymentEnrollment).
//            await using (var db = w.Db())
//            {
//                var adminSvc = Service(w, db, AdminId, Role.Admin);
//                var missingText = await adminSvc.DecideCohortComplaintAsync(
//                    teachingComplaintId, new DecideCohortComplaintRequest(CohortComplaint.Resolved, "   "), default);
//                Assert.Equal(422, missingText.StatusCode);
//                Assert.Equal("COMPLAINT_DECISION_REQUIRED", missingText.ErrorCode);

//                var resolvedTeaching = await adminSvc.DecideCohortComplaintAsync(
//                    teachingComplaintId, new DecideCohortComplaintRequest(CohortComplaint.Resolved, "Đã nhắc nhở cố vấn và bù tài liệu."), default);
//                Assert.True(resolvedTeaching.IsSuccess, resolvedTeaching.ErrorMessage);

//                var teachingOutbox = await db.MentorOutboxEvents
//                    .SingleAsync(e => e.EventKey == $"cohort-complaint:{teachingComplaintId}:decided");
//                using (var doc = JsonDocument.Parse(teachingOutbox.PayloadJson))
//                {
//                    var userIds = doc.RootElement.GetProperty("Notifications").EnumerateArray()
//                        .Select(n => n.GetProperty("UserId").GetInt32()).ToList();
//                    Assert.Contains(Learners[0], userIds);
//                    Assert.Contains(MentorA, userIds);
//                }

//                var rejectedPayment = await adminSvc.DecideCohortComplaintAsync(
//                    paymentComplaintId, new DecideCohortComplaintRequest(CohortComplaint.Rejected, "Giao dịch đã được xử lý trước đó."), default);
//                Assert.True(rejectedPayment.IsSuccess, rejectedPayment.ErrorMessage);

//                var paymentOutbox = await db.MentorOutboxEvents
//                    .SingleAsync(e => e.EventKey == $"cohort-complaint:{paymentComplaintId}:decided");
//                using (var doc = JsonDocument.Parse(paymentOutbox.PayloadJson))
//                {
//                    var userIds = doc.RootElement.GetProperty("Notifications").EnumerateArray()
//                        .Select(n => n.GetProperty("UserId").GetInt32()).ToList();
//                    Assert.Equal(new[] { Learners[0] }, userIds);
//                }
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task PaymentReview_Transition_Queues_Learner_And_Admin_Outbox_Notifications_Without_ProviderReference()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            int courseId;
//            await using (var db = w.Db())
//            {
//                var cohort = new StudyCohort
//                {
//                    PlanId = w.PlanN3, Name = "N3-PayReview", MentorId = MentorA, Capacity = 4, CapacitySnapshot = 4,
//                    Status = "Published", StartsAt = Utc(DateTime.UtcNow.AddDays(5)), EndsAt = Utc(DateTime.UtcNow.AddDays(10))
//                };
//                db.StudyCohorts.Add(cohort);
//                await db.SaveChangesAsync();

//                var order = new StudyCourse
//                {
//                    CohortId = cohort.Id, MentorId = MentorA, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt,
//                    SessionType = "Group", LearnerId = Learners[0], PurchaseKey = Guid.NewGuid(), PlanId = w.PlanN3,
//                    Name = "N3 Group Feedback", CertificateLevelId = w.LevelN3, PlanRevision = 1, DurationMonths = 2,
//                    SessionsPerWeek = 3, TotalSessions = 3, SessionMinutes = 90, Amount = 500_000m,
//                    PaymentStatus = "Expired", Status = "PaymentExpired", PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-15)
//                };
//                db.StudyCourses.Add(order);
//                await db.SaveChangesAsync();
//                courseId = order.Id;
//            }

//            const string providerRef = "PAYOS-SECRET-TX-998877";
//            await using (var db = w.Db())
//            {
//                var res = await Service(w, db, AdminId, Role.Admin).ConfirmPaymentAsync(
//                    courseId,
//                    new PaymentCallbackResult(true, true, 700_000_000L + courseId, 500_000m, $"COURSE {courseId}"),
//                    providerRef,
//                    default);
//                Assert.True(res.IsSuccess, res.ErrorMessage);
//                Assert.Equal("PaymentReview", JsonSerializer.SerializeToElement(res.Result).GetProperty("Status").GetString());

//                var outbox = await db.MentorOutboxEvents.SingleAsync(e => e.EventKey == $"course-payment-review:{courseId}");
//                Assert.DoesNotContain(providerRef, outbox.PayloadJson);

//                using var doc = JsonDocument.Parse(outbox.PayloadJson);
//                var notifications = doc.RootElement.GetProperty("Notifications").EnumerateArray().ToList();
//                var learnerNotif = notifications.Single(n => n.GetProperty("UserId").GetInt32() == Learners[0]);
//                Assert.Equal(
//                    "Giao dịch đã được ghi nhận nhưng chưa thể hoàn tất ghi danh. Bộ phận quản trị sẽ xem xét yêu cầu của bạn.",
//                    learnerNotif.GetProperty("Message").GetString());
//                Assert.Contains(notifications, n => n.GetProperty("UserId").GetInt32() == AdminId);
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task CompletionSummary_Attributes_ReplacedMentors_HidesAiDrafts_And_Returns_NullCertificate()
//    {
//        var w = await CreateWorldAsync();
//        const string aiDraftSecret = "SECRET_AI_DRAFT_DO_NOT_EXPOSE";
//        try
//        {
//            var t1 = Utc(DateTime.UtcNow.AddDays(-5));
//            var t2 = Utc(DateTime.UtcNow.AddDays(2));
//            var t3 = Utc(DateTime.UtcNow.AddDays(4));
//            var (cohortId, courseId) = await SeedCohortEnrollmentAsync(
//                w, Learners[0], MentorA, [t1, t2, t3], ["Present", "Scheduled", "Scheduled"]);

//            await using (var db = w.Db())
//            {
//                var replaceRes = await Service(w, db, AdminId, Role.Admin).ReplaceCohortMentorAsync(
//                    cohortId,
//                    new ReplaceCohortMentorRequest(MentorB, "Thay cố vấn chính từ buổi 2", Guid.NewGuid(), 1, 2),
//                    default);
//                Assert.True(replaceRes.IsSuccess, replaceRes.ErrorMessage);

//                var course = await db.StudyCourses.Include(c => c.Sessions).Include(c => c.Homework).SingleAsync(c => c.Id == courseId);
//                course.Sessions.Single(s => s.Number == 2).Status = "Absent";
//                course.Status = "Completed";
//                course.CompletedAt = DateTime.UtcNow;
//                course.Homework.Add(new StudyCourseHomework
//                {
//                    Title = "Legacy HW", Instructions = "Review kanji", DueAt = DateTime.UtcNow.AddDays(-1),
//                    SubmittedAt = DateTime.UtcNow.AddDays(-2), Passed = true, ReviewedAt = DateTime.UtcNow.AddDays(-1),
//                    Feedback = "Làm tốt phần chữ Hán"
//                });

//                var hw1 = new HomeworkAssignment
//                {
//                    LearnerId = Learners[0], MentorId = MentorA, CohortId = cohortId, StudyCourseId = courseId,
//                    AssignmentScope = "CohortCommon", Title = "Cohort HW 1", Status = "Graded",
//                    AiDraftFeedback = aiDraftSecret, AiDraftScore = 77.5, AiDraftGeneratedAtUtc = DateTime.UtcNow.AddDays(-2),
//                    MentorFeedback = "Nhận xét chính thức từ cố vấn", MentorScore = 92, Passed = true,
//                    PublishedFeedbackAtUtc = DateTime.UtcNow.AddDays(-1), PublishedByMentorId = MentorB,
//                    Submission = new HomeworkSubmission { LearnerId = Learners[0], TextContent = "Bài làm 1", SubmittedAt = DateTime.UtcNow.AddDays(-2) }
//                };
//                var hw2 = new HomeworkAssignment
//                {
//                    LearnerId = Learners[0], MentorId = MentorB, CohortId = cohortId, StudyCourseId = courseId,
//                    AssignmentScope = "CohortIndividual", Title = "Cohort HW 2", Status = "Assigned",
//                    AiDraftFeedback = aiDraftSecret
//                };
//                db.HomeworkAssignments.AddRange(hw1, hw2);
//                await db.SaveChangesAsync();
//            }

//            await using (var db = w.Db())
//            {
//                // Without IMentorLearnerSkillProfileService -> SkillProgressAvailable=false.
//                var res = await Service(w, db, Learners[0], Role.Learner).CourseCompletionSummaryAsync(courseId, default);
//                Assert.True(res.IsSuccess, res.ErrorMessage);
//                var json = JsonSerializer.Serialize(res.Result);
//                Assert.DoesNotContain(aiDraftSecret, json);

//                var el = JsonSerializer.SerializeToElement(res.Result);
//                Assert.Equal(MentorB, el.GetProperty("CurrentPrimaryMentorId").GetInt32());
//                Assert.True(el.GetProperty("IsCompleted").GetBoolean());
//                Assert.Equal(JsonValueKind.Null, el.GetProperty("Certificate").ValueKind);
//                Assert.Equal("Đây là báo cáo học tập nội bộ, không phải chứng chỉ JLPT chính thức.", el.GetProperty("Note").GetString());
//                Assert.False(el.GetProperty("SkillProgressAvailable").GetBoolean());

//                var mentors = el.GetProperty("Mentors").EnumerateArray().ToList();
//                var mA = mentors.Single(m => m.GetProperty("MentorId").GetInt32() == MentorA);
//                var mB = mentors.Single(m => m.GetProperty("MentorId").GetInt32() == MentorB);
//                Assert.Equal(new[] { 1 }, mA.GetProperty("SessionNumbers").EnumerateArray().Select(x => x.GetInt32()).ToArray());
//                Assert.False(mA.GetProperty("IsCurrentPrimary").GetBoolean());
//                Assert.Equal(new[] { 2, 3 }, mB.GetProperty("SessionNumbers").EnumerateArray().Select(x => x.GetInt32()).ToArray());
//                Assert.True(mB.GetProperty("IsCurrentPrimary").GetBoolean());

//                var sessions = el.GetProperty("Sessions");
//                Assert.Equal(3, sessions.GetProperty("Total").GetInt32());
//                Assert.Equal(1, sessions.GetProperty("Delivered").GetInt32());
//                Assert.Equal(1, sessions.GetProperty("Absent").GetInt32());
//                Assert.Equal(0, sessions.GetProperty("Cancelled").GetInt32());
//                Assert.Equal(1, sessions.GetProperty("Upcoming").GetInt32());

//                var hw = el.GetProperty("Homework");
//                Assert.Equal(3, hw.GetProperty("Assigned").GetInt32());
//                Assert.Equal(2, hw.GetProperty("Submitted").GetInt32());
//                Assert.Equal(2, hw.GetProperty("Graded").GetInt32());
//                Assert.Equal(1, hw.GetProperty("Outstanding").GetInt32());

//                // Historical mentor (MentorA) can read completion summary; foreign learner cannot.
//                Assert.True((await Service(w, db, MentorA, Role.Mentor).CourseCompletionSummaryAsync(courseId, default)).IsSuccess);
//                Assert.Equal(403, (await Service(w, db, Learners[1], Role.Learner).CourseCompletionSummaryAsync(courseId, default)).StatusCode);

//                // Current primary mentor (MentorB) with IMentorLearnerSkillProfileService resolved -> SkillProgressAvailable=true.
//                var skillSvc = new MentorLearnerSkillProfileService(new UnitOfWork(db), Claims(MentorB, Role.Mentor));
//                var withSkills = await Service(w, db, MentorB, Role.Mentor, skillSvc).CourseCompletionSummaryAsync(courseId, default);
//                Assert.True(withSkills.IsSuccess, withSkills.ErrorMessage);
//                Assert.True(JsonSerializer.SerializeToElement(withSkills.Result).GetProperty("SkillProgressAvailable").GetBoolean());
//            }
//        }
//        finally { await DropAsync(w); }
//    }
//}
