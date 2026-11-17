using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

// PR #70 integration (PostgreSQL, schema cô lập): Mentor → Cohort → roster → giao bài → nộp (chữ/ảnh/PDF)
// → nháp AI (lỗi AI/OCR không làm mất bài nộp) → chấm/công bố (GradingResult canonical của teammate + bản sao Cohort)
// → tiến độ; chặn Mentor khác lớp; thay cố vấn và chuyển lớp không làm hỏng bài tập; bài lịch sử vẫn đọc được.
public sealed class HomeworkCohortIntegrationPostgresTests
{
    static HomeworkCohortIntegrationPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private const int MentorA = 9301, MentorB = 9302, MentorOther = 9303, L1 = 9311, L2 = 9312;
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];
    private static readonly byte[] Pdf = "%PDF-1.7\n%test\n"u8.ToArray();

    private sealed class PrivateStorage : IFileStorageService
    {
        public readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> Files = new();

        public Task<UploadedFileResult> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken ct)
            => Task.FromResult(new UploadedFileResult($"https://cdn.invalid/{folder}/{fileName}", fileName, fileStream.Length));
        public Task DeleteAsync(string storedFileName, string folder, CancellationToken ct) => Task.CompletedTask;
        public Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken ct) => Task.FromResult(storedUrl);
        public async Task<UploadedFileResult> UploadPrivateVerificationAsync(Stream fileStream, string contentType, int ownerId, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, ct);
            var url = $"private/hw-{ownerId}-{Guid.NewGuid():N}";
            Files[url] = (ms.ToArray(), contentType);
            return new UploadedFileResult(url, url, ms.Length);
        }
        public Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken ct)
            => Files.ContainsKey(storedUrl) ? Task.CompletedTask : Task.FromException(new FileNotFoundException(storedUrl));
        public Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(string storedUrl, CancellationToken ct)
            => Files.TryGetValue(storedUrl, out var f)
                ? Task.FromResult(new PrivateVerificationReadResult(new MemoryStream(f.Bytes, writable: false), f.ContentType))
                : throw new FileNotFoundException(storedUrl);
    }

    private sealed class FakeGrader : IAiEssayGradingClient
    {
        public bool Outage { get; set; }
        public Task<string> GenerateGradingJsonAsync(int userId, string systemPrompt, string userPrompt, CancellationToken ct)
            => Outage ? throw new HttpRequestException("simulated AI outage")
                : Task.FromResult("{\"score\":76,\"feedback\":\"Nháp AI: chú ý trợ từ は/が.\"}");
    }

    private static IClaimService Claims(int id, Role role)
        => Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = id, Role = role });

    private static IFormFile File(byte[] bytes, string contentType, string name)
        => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = contentType };

    [CoursePostgresFact]
    public async Task CohortHomework_Pr70_EndToEnd_Formats_AiSafety_Grading_Progress_Authorization_Replacement_Transfer_History()
    {
        var baseCs = Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!;
        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(baseCs);
        AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        var storage = new PrivateStorage();
        var grader = new FakeGrader();
        var ocr = new Mock<IAiOcrClient>();
        ocr.Setup(x => x.ScanHandwritingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("simulated OCR outage"));

        async Task<T> As<T>(int userId, Role role, Func<HomeworkService, Task<T>> action)
        {
            await using var db = Db();
            var svc = new HomeworkService(new UnitOfWork(db), Claims(userId, role), Mock.Of<ILessonProgressService>(),
                storage, grader, ocr.Object, new MentorGroupPolicyService(db));
            return await action(svc);
        }

        try
        {
            int cohortId, course1, course2, otherCohortId;
            await using (var db = Db())
            {
                UserAccount U(int id, Role r) => new()
                {
                    Id = id, Role = r, FirstName = r.ToString(), LastName = id.ToString(), Email = $"hw{id}@test.invalid",
                    PasswordHash = [], PasswordSalt = [],
                    MentorProfile = r == Role.Mentor ? new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3" } : null
                };
                db.Users.AddRange(U(MentorA, Role.Mentor), U(MentorB, Role.Mentor), U(MentorOther, Role.Mentor),
                    U(L1, Role.Learner), U(L2, Role.Learner));
                var n3 = new CertificateLevel { Code = "N3", CertificateType = new CertificateType { Name = "JLPT", IsActive = true }, SortOrder = 3 };
                var plan = new StudyCoursePlan
                {
                    Name = "N3 Group", CertificateLevel = n3, SessionType = "Group", Capacity = 4, SessionMinutes = 90,
                    TotalSessions = 30, DurationMonths = 3, SessionsPerWeek = 3, Price = 2_500_000m, IsPublished = true
                };
                db.AddRange(n3, plan);
                await db.SaveChangesAsync();
                var start = DateTime.UtcNow.AddDays(-7);
                StudyCohort NewCohort(int mentorId) => new()
                {
                    PlanId = plan.Id, Name = $"N3-HW-{mentorId}", MentorId = mentorId, Capacity = 4, CapacitySnapshot = 4,
                    Status = "Published", StartsAt = start, EndsAt = start.AddDays(70)
                };
                var cohort = NewCohort(MentorA);
                var other = NewCohort(MentorOther);
                db.StudyCohorts.AddRange(cohort, other);
                await db.SaveChangesAsync();
                StudyCourse Course(int learnerId, StudyCohort c) => new()
                {
                    CohortId = c.Id, MentorId = c.MentorId, StartsAt = c.StartsAt, EndsAt = c.EndsAt, SessionType = "Group",
                    LearnerId = learnerId, PurchaseKey = Guid.NewGuid(), PlanId = plan.Id, Name = plan.Name, CertificateLevelId = n3.Id,
                    PlanRevision = 1, DurationMonths = 3, SessionsPerWeek = 3, TotalSessions = 30, SessionMinutes = 90,
                    Amount = 2_500_000m, PaymentStatus = "Paid", Status = "Active", PaymentReference = $"PAID-{learnerId}-{c.Id}"
                };
                var c1 = Course(L1, cohort);
                var c2 = Course(L2, cohort);
                db.StudyCourses.AddRange(c1, c2);
                await db.SaveChangesAsync();
                (cohortId, course1, course2, otherCohortId) = (cohort.Id, c1.Id, c2.Id, other.Id);
            }

            // 1. Mentor → Cohort → roster: common assignment for every active Learner. Other mentors cannot assign to this Cohort.
            var common = await As(MentorA, Role.Mentor, s => s.CreateCohortAssignmentsAsync(cohortId,
                new CreateHomeworkRequest(null, "Viết đoạn văn", "Viết 5 câu về gia đình", null, null, null, cohortId), default));
            Assert.True(common.IsSuccess, common.ErrorMessage);
            Assert.Equal(2, common.Result!.Count);
            var denied = await As(MentorOther, Role.Mentor, s => s.CreateCohortAssignmentsAsync(cohortId,
                new CreateHomeworkRequest(null, "X", "Y", null, null, null, cohortId), default));
            Assert.Equal(403, denied.StatusCode);
            var docAssign = await As(MentorA, Role.Mentor, s => s.CreateCohortAssignmentsAsync(cohortId,
                new CreateHomeworkRequest(null, "Nộp bài PDF", "Đính kèm bài viết PDF", null, null, null, cohortId, [L1]), default));
            Assert.True(docAssign.IsSuccess, docAssign.ErrorMessage);

            int A1, A2;
            await using (var db = Db())
            {
                A1 = (await db.HomeworkAssignments.SingleAsync(a => a.LearnerId == L1 && a.AssignmentScope == "CohortCommon")).Id;
                A2 = (await db.HomeworkAssignments.SingleAsync(a => a.LearnerId == L2 && a.AssignmentScope == "CohortCommon")).Id;
            }
            var aDoc = docAssign.Result![0];

            // 2. Submissions: text (L1), image (L2 via canonical /scan), document PDF + text (L1 via Task 02 alias).
            var text = await As(L1, Role.Learner, s => s.SubmitAsync(A1, new SubmitHomeworkRequest("私の家族は四人です。", null), default));
            Assert.Equal(201, text.StatusCode);
            var badImage = await As(L2, Role.Learner, s => s.SubmitScanAsync(A2, File(Pdf, "image/png", "fake.png"), default));
            Assert.Equal(400, badImage.StatusCode);
            var image = await As(L2, Role.Learner, s => s.SubmitScanAsync(A2, File(Png, "image/png", "hw.png"), default));
            Assert.Equal(201, image.StatusCode);
            Assert.True(image.Result!.HasScan);
            var pdf = await As(L1, Role.Learner, s => s.SubmitWithScanAsync(aDoc, "Bài viết đính kèm", File(Pdf, "application/pdf", "hw.pdf"), default));
            Assert.Equal(201, pdf.StatusCode);
            Assert.True(pdf.Result!.HasScan);
            Assert.NotNull(await As(MentorA, Role.Mentor, s => s.ReadScanAsync(A2, default)));
            Assert.Null(await As(MentorOther, Role.Mentor, s => s.ReadScanAsync(A2, default))); // khác lớp
            Assert.Null(await As(L1, Role.Learner, s => s.ReadScanAsync(A2, default)));         // học viên khác

            // 3. AI/OCR failure never destroys the submission; success persists a Mentor-only draft.
            string? scanRef;
            await using (var db = Db()) scanRef = (await db.HomeworkSubmissions.SingleAsync(x => x.HomeworkAssignmentId == A2)).PrivateScanReference;
            var ocrFail = await As(MentorA, Role.Mentor, s => s.CreateDraftAsync(A2, default));
            Assert.Equal(502, ocrFail.StatusCode);
            grader.Outage = true;
            var aiFail = await As(MentorA, Role.Mentor, s => s.CreateDraftAsync(A1, default));
            Assert.Equal(502, aiFail.StatusCode);
            Assert.Equal("HOMEWORK_AI_DRAFT_UNAVAILABLE", aiFail.ErrorCode);
            grader.Outage = false;
            var pdfDraft = await As(MentorA, Role.Mentor, s => s.CreateDraftAsync(aDoc, default)); // PDF có phần chữ → AI chấm phần chữ
            Assert.True(pdfDraft.IsSuccess, pdfDraft.ErrorMessage);
            await using (var db = Db())
            {
                var s2 = await db.HomeworkSubmissions.SingleAsync(x => x.HomeworkAssignmentId == A2);
                Assert.Equal(scanRef, s2.PrivateScanReference);
                Assert.Equal("私の家族は四人です。", (await db.HomeworkSubmissions.SingleAsync(x => x.HomeworkAssignmentId == A1)).TextContent);
                Assert.NotNull((await db.HomeworkAssignments.FindAsync(A1))!.AiDraftUnavailableReason);
            }
            var draft = await As(MentorA, Role.Mentor, s => s.CreateDraftAsync(A1, default));
            Assert.True(draft.IsSuccess, draft.ErrorMessage);
            Assert.Null((await As(L1, Role.Learner, s => s.GetAssignmentsAsync(null, default, cohortId))).Single(x => x.Id == A1).AiDraft);
            Assert.NotNull((await As(MentorA, Role.Mentor, s => s.GetAssignmentsAsync(L1, default, cohortId))).Single(x => x.Id == A1).AiDraft);

            // 4. Unauthorized Mentor (other Cohort) is blocked from list, AI draft and grading.
            Assert.Empty(await As(MentorOther, Role.Mentor, s => s.GetAssignmentsAsync(null, default, cohortId)));
            Assert.Equal(404, (await As(MentorOther, Role.Mentor, s => s.CreateDraftAsync(A1, default))).StatusCode);
            Assert.Equal(404, (await As(MentorOther, Role.Mentor, s => s.PublishGradeAsync(A1,
                new PublishHomeworkGradeRequest(text.Result!.Id, 10, "x"), default))).StatusCode);
            Assert.Equal(403, (await As(MentorOther, Role.Mentor, s => s.GradeAndPublishAsync(A1,
                new GradeHomeworkAssignmentRequest(false, "x", 10), default))).StatusCode);

            // 5. Grade and publish (canonical PUT grade): idempotent, stale submission id rejected, learner notified.
            Assert.Equal(409, (await As(MentorA, Role.Mentor, s => s.PublishGradeAsync(A1,
                new PublishHomeworkGradeRequest(text.Result!.Id + 999, 80, "Tốt"), default))).StatusCode);
            var graded = await As(MentorA, Role.Mentor, s => s.PublishGradeAsync(A1,
                new PublishHomeworkGradeRequest(text.Result!.Id, 85.5m, "Tốt, chú ý trợ từ."), default));
            Assert.True(graded.IsSuccess, graded.ErrorMessage);
            var again = await As(MentorA, Role.Mentor, s => s.PublishGradeAsync(A1,
                new PublishHomeworkGradeRequest(text.Result!.Id, 85.5m, "Tốt, chú ý trợ từ."), default));
            Assert.Equal(graded.Result!.PublishedAt, again.Result!.PublishedAt);
            await using (var db = Db())
            {
                Assert.Equal(1, await db.GradingResults.CountAsync(g => g.HomeworkSubmissionId == text.Result!.Id));
                var a1 = await db.HomeworkAssignments.FindAsync(A1);
                Assert.Equal("Graded", a1!.Status);
                Assert.Equal(85.5, a1.MentorScore);
                Assert.Equal(MentorA, a1.PublishedByMentorId);
                Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == L1 && n.Type == "Homework"));
            }
            Assert.Equal(409, (await As(L1, Role.Learner, s => s.SubmitAsync(A1, new SubmitHomeworkRequest("sửa sau chấm", null), default))).StatusCode);

            // 6. Progress visible to Learner and Mentor (same published result; report counts).
            var learnerItem = (await As(L1, Role.Learner, s => s.GetAssignmentsAsync(null, default, cohortId))).Single(x => x.Id == A1);
            Assert.Equal(85.5, learnerItem.Submission!.Grade!.Score);
            Assert.Equal("Tốt, chú ý trợ từ.", learnerItem.PublishedGrade!.Feedback);
            var mentorItem = (await As(MentorA, Role.Mentor, s => s.GetAssignmentsAsync(L1, default, cohortId))).Single(x => x.Id == A1);
            Assert.Equal(85.5, mentorItem.Submission!.Grade!.Score);
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var learnerReport = await As(L1, Role.Learner, s => s.GetReportAsync(today.AddDays(-1), today.AddDays(1), null, default));
            Assert.True(learnerReport.IsSuccess, learnerReport.ErrorMessage);
            Assert.Equal(2, learnerReport.Result!.AssignedCount);
            Assert.Equal(2, learnerReport.Result.SubmittedCount);
            var mentorReport = await As(MentorA, Role.Mentor, s => s.GetReportAsync(today.AddDays(-1), today.AddDays(1), L1, default));
            Assert.True(mentorReport.IsSuccess, mentorReport.ErrorMessage);

            // 7. Mentor replacement: the new Cohort mentor grades pending work; original assigner/KPI attribution is kept.
            await using (var db = Db())
            {
                var c = await db.StudyCohorts.FindAsync(cohortId);
                c!.MentorId = MentorB;
                await db.SaveChangesAsync();
            }
            var byB = await As(MentorB, Role.Mentor, s => s.GradeAndPublishAsync(A2, new GradeHomeworkAssignmentRequest(true, "Đạt yêu cầu.", 70), default));
            Assert.True(byB.IsSuccess, byB.ErrorMessage);
            await using (var db = Db())
            {
                var a2 = await db.HomeworkAssignments.FindAsync(A2);
                Assert.Equal(MentorA, a2!.MentorId);
                Assert.Equal(MentorB, a2.PublishedByMentorId);
                Assert.True(a2.Passed);
            }

            // 8. Learner transfer: homework stays intact and readable; the Cohort mentor loses access to the transferred enrolment.
            await using (var db = Db())
            {
                var c2 = await db.StudyCourses.FindAsync(course2);
                c2!.Status = "Transferred";
                await db.SaveChangesAsync();
            }
            Assert.DoesNotContain(await As(MentorB, Role.Mentor, s => s.GetAssignmentsAsync(null, default, cohortId)), x => x.Id == A2);
            var l2View = (await As(L2, Role.Learner, s => s.GetAssignmentsAsync(null, default, cohortId))).Single(x => x.Id == A2);
            Assert.Equal(70, l2View.Submission!.Grade!.Score);
            Assert.True(l2View.Submission.HasScan);

            // 9. Historical homework (legacy ScanStorageUrl, plain-text FeedbackJson) stays readable after integration.
            var legacyUrl = (await storage.UploadPrivateVerificationAsync(new MemoryStream(Png), "image/png", L1, default)).Url;
            await using (var db = Db())
            {
                var legacy = new HomeworkAssignment
                {
                    MentorId = MentorA, LearnerId = L1, Title = "Bài cũ", Status = "Graded", CohortId = cohortId, StudyCourseId = course1,
                    AssignmentScope = "CohortIndividual",
                    Submission = new HomeworkSubmission
                    {
                        LearnerId = L1, TextContent = "cũ", ScanStorageUrl = legacyUrl, SubmittedAt = DateTime.UtcNow.AddDays(-30),
                        GradingResult = new GradingResult { OverallScore = 60, FeedbackJson = "Nhận xét cũ dạng văn bản", GradedAt = DateTime.UtcNow.AddDays(-29) }
                    }
                };
                db.HomeworkAssignments.Add(legacy);
                await db.SaveChangesAsync();
                var legacyItem = (await As(L1, Role.Learner, s => s.GetAssignmentsAsync(null, default, cohortId))).Single(x => x.Id == legacy.Id);
                Assert.Equal("Nhận xét cũ dạng văn bản", legacyItem.Submission!.Grade!.Feedback);
                Assert.True(legacyItem.Submission.HasScan);
                var legacyFile = await As(L1, Role.Learner, s => s.ReadScanAsync(legacy.Id, default));
                Assert.Equal("image/png", legacyFile!.ContentType);
            }

            // 10. No Cohort data dropped: both cohorts and enrolments still present.
            await using (var db = Db())
            {
                Assert.Equal(2, await db.StudyCohorts.CountAsync(c => c.Id == cohortId || c.Id == otherCohortId));
                Assert.Equal(2, await db.StudyCourses.CountAsync(c => c.CohortId == cohortId));
            }
        }
        finally
        {
            await CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(schema, cs);
        }
    }
}
