//using System.Net;
//using System.Text;
//using System.Text.Json;
//using Microsoft.AspNetCore.Http;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Diagnostics;
//using Microsoft.Extensions.Logging.Abstractions;
//using Moq;
//using RikiPath.Application;
//using RikiPath.Application.IClients;
//using RikiPath.Application.IServices;
//using RikiPath.Application.Requests.Homework;
//using RikiPath.Application.Requests.StudyCourses;
//using RikiPath.Application.Responses.Lessons;
//using RikiPath.Application.Responses.MockTests;
//using RikiPath.Application.Services;
//using RikiPath.Domain;
//using RikiPath.Domain.Entities;
//using RikiPath.Domain.Enums;
//using RikiPath.Infrastructure;
//using RikiPath.Infrastructure.Services;
//using Xunit;

//namespace RikiPath.Tests;

//public sealed class CanonicalStepsFtoITests
//{
//    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
//        .UseInMemoryDatabase($"canonical_f_to_i_{Guid.NewGuid():N}")
//        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
//        .Options);

//    private static IClaimService Claims(int userId, Role role) =>
//        Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role });

//    // PR #70: chấm AI dùng IAiEssayGradingClient (teammate) thay cho IHomeworkAiDraftGenerator giả lập.
//    private sealed class FakeEssayGrader(bool outage = false) : IAiEssayGradingClient
//    {
//        public Task<string> GenerateGradingJsonAsync(int userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
//            => outage ? throw new HttpRequestException("AI provider outage")
//                : Task.FromResult("{\"score\":81.5,\"feedback\":\"Nháp AI: cần luyện thêm trợ từ.\"}");
//    }

//    [Fact]
//    public async Task SkillProfile_ReturnsNullForUnknownSkills_IsolatesCohortScope_AndDeniesUnauthorized()
//    {
//        await using var db = CreateDb();
//        var mentorA = new UserAccount { Id = 10, Role = Role.Mentor, FirstName = "A", LastName = "Sensei", PasswordHash = [], PasswordSalt = [] };
//        var mentorB = new UserAccount { Id = 11, Role = Role.Mentor, FirstName = "B", LastName = "Sensei", PasswordHash = [], PasswordSalt = [] };
//        var learner1 = new UserAccount { Id = 101, Role = Role.Learner, FirstName = "Linh", LastName = "Nguyen", PasswordHash = [], PasswordSalt = [] };
//        var outsider = new UserAccount { Id = 102, Role = Role.Learner, FirstName = "Minh", LastName = "Tran", PasswordHash = [], PasswordSalt = [] };

//        var skillReading = new LanguageSkill { Id = 5, Name = "Reading" };
//        var skillGrammar = new LanguageSkill { Id = 3, Name = "Grammar" };
//        var certType = new CertificateType { Id = 1, Name = "JLPT", IsActive = true };
//        var level = new CertificateLevel { Id = 3, Code = "N3", CertificateTypeId = 1, CertificateType = certType };
//        var plan = new StudyCoursePlan { Id = 1, Name = "N3 Group", CertificateLevelId = 3, SessionType = "Group", Capacity = 4, SessionMinutes = 90, TotalSessions = 2, DurationMonths = 1, SessionsPerWeek = 3, Price = 100, IsPublished = true };
//        var cohortA = new StudyCohort { Id = 20, Name = "Cohort A", PlanId = 1, MentorId = mentorA.Id, Capacity = 4, Status = "Published", StartsAt = DateTime.UtcNow.AddDays(-10), EndsAt = DateTime.UtcNow.AddDays(20) };
//        var cohortB = new StudyCohort { Id = 21, Name = "Cohort B", PlanId = 1, MentorId = mentorB.Id, Capacity = 4, Status = "Published", StartsAt = DateTime.UtcNow.AddDays(-10), EndsAt = DateTime.UtcNow.AddDays(20) };

//        var enrollA = new StudyCourse
//        {
//            Id = 30, CohortId = cohortA.Id, LearnerId = learner1.Id, MentorId = mentorA.Id, PlanId = 1, CertificateLevelId = 3,
//            Name = "Cohort A", SessionType = "Group", DurationMonths = 1, SessionsPerWeek = 3, SessionMinutes = 90, TotalSessions = 2,
//            PaymentStatus = "Paid", Status = "Active", PurchaseKey = Guid.NewGuid(),
//            Homework = [new() { Title = "HW A", Instructions = "Do A", Required = true, DueAt = DateTime.UtcNow.AddDays(-1), SubmittedAt = DateTime.UtcNow.AddDays(-2), Passed = true }]
//        };
//        var enrollB = new StudyCourse
//        {
//            Id = 31, CohortId = cohortB.Id, LearnerId = learner1.Id, MentorId = mentorB.Id, PlanId = 1, CertificateLevelId = 3,
//            Name = "Cohort B", SessionType = "Group", DurationMonths = 1, SessionsPerWeek = 3, SessionMinutes = 90, TotalSessions = 2,
//            PaymentStatus = "Paid", Status = "Active", PurchaseKey = Guid.NewGuid(),
//            Homework = [new() { Title = "HW B", Instructions = "Do B", Required = true, DueAt = DateTime.UtcNow.AddDays(2) }]
//        };

//        var lesson = new Lesson { Id = 50, Title = "Lesson 1", CertificateLevelId = 3, LanguageSkillId = skillGrammar.Id, LanguageSkill = skillGrammar };
//        var exercise = new PracticeExercise { Id = 51, LessonId = 50, Lesson = lesson, LanguageSkillId = skillGrammar.Id, LanguageSkill = skillGrammar, Title = "Grammar Drill" };
//        var practiceAttempt = new PracticeExerciseAttempt { Id = 52, UserId = learner1.Id, PracticeExerciseId = 51, IsCompleted = true, ScorePercent = 82.5, StartedAt = DateTime.UtcNow.AddDays(-3), SubmittedAt = DateTime.UtcNow.AddDays(-3) };

//        var mockTest = new MockTest { Id = 60, Title = "N3 Mock", CertificateLevelId = 3 };
//        var section = new MockTestSection { Id = 61, MockTestId = 60, MockTest = mockTest, Title = "Reading Sec", LanguageSkillId = skillReading.Id, LanguageSkill = skillReading, CertificateSectionId = 1 };
//        var mockAttempt = new MockTestAttempt { Id = 62, UserId = learner1.Id, AttemptKind = AttemptKind.MockTest, IsCompleted = true, StartedAt = DateTime.UtcNow.AddDays(-2), SubmittedAt = DateTime.UtcNow.AddDays(-2) };
//        var sectionResult = new MockTestSectionResult { Id = 63, MockTestAttemptId = 62, MockTestAttempt = mockAttempt, MockTestSectionId = 61, MockTestSection = section, CorrectCount = 8, TotalCount = 10, ScorePercent = 80.0 };

//        db.AddRange(mentorA, mentorB, learner1, outsider, skillReading, skillGrammar, certType, level, plan, cohortA, cohortB, enrollA, enrollB, lesson, exercise, practiceAttempt, mockTest, section, mockAttempt, sectionResult);
//        await db.SaveChangesAsync();

//        var serviceA = new MentorLearnerSkillProfileService(new UnitOfWork(db), Claims(mentorA.Id, Role.Mentor));
//        var res = await serviceA.GetSkillProfileAsync(learner1.Id, cohortA.Id);
//        Assert.True(res.IsSuccess, res.ErrorMessage);
//        var profile = res.Result!;
//        Assert.Equal(80.0, profile.Skills.Single(s => s.Skill == "Reading").AverageScorePercent);
//        Assert.Equal(82.5, profile.Skills.Single(s => s.Skill == "Grammar").AverageScorePercent);
//        // Never fake 0 for unassessed Speaking / Writing skills.
//        Assert.Null(profile.Skills.Single(s => s.Skill == "Speaking").AverageScorePercent);
//        Assert.Equal(0, profile.Skills.Single(s => s.Skill == "Speaking").SampleCount);
//        Assert.Null(profile.Skills.Single(s => s.Skill == "Writing").AverageScorePercent);
//        // Cohort A homework stats must not include Cohort B's homework.
//        Assert.Equal(1, profile.HomeworkStats.AssignedCount);
//        Assert.Equal(1, profile.HomeworkStats.SubmittedCount);

//        // Mentor A requesting Cohort B (belongs to Mentor B) -> 403 Forbidden.
//        var crossCohort = await serviceA.GetSkillProfileAsync(learner1.Id, cohortB.Id);
//        Assert.Equal(403, crossCohort.StatusCode);

//        // Mentor A requesting outsider learner -> 403 Forbidden.
//        var unrelatedLearner = await serviceA.GetSkillProfileAsync(outsider.Id, null);
//        Assert.Equal(403, unrelatedLearner.StatusCode);
//    }

//    [Fact]
//    public async Task PlacementTest_ReadsAttemptLimitFromActiveMentorGroupPolicy()
//    {
//        await using var db = CreateDb();
//        var certType = new CertificateType { Id = 1, Name = "JLPT", IsActive = true };
//        var level = new CertificateLevel { Id = 3, Code = "N3", CertificateTypeId = 1, CertificateType = certType };
//        var learner = new UserAccount
//        {
//            Id = 77, Role = Role.Learner, FirstName = "Kaito", PasswordHash = [], PasswordSalt = [],
//            TargetCertificateLevelId = 3, TargetExamDate = DateTime.UtcNow.AddMonths(3)
//        };
//        var snapshotJson = JsonSerializer.Serialize(new
//        {
//            AssessmentTargetLevelId = 3,
//            Questions = new[] { new { QuestionId = 1, JlptLevelId = 3, JlptLevelName = "N3" } },
//            ExpiresAt = DateTime.UtcNow.AddHours(1)
//        });
//        db.AddRange(certType, level, learner,
//            new MentorGroupPolicy
//            {
//                Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-5), MaxLearners = 4, SessionMinutes = 90,
//                RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 30, CancellationLeadHours = 24,
//                ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 3,
//                RemediationCompletionThresholdPercent = 90m, KpiReportingPeriodDays = 30, CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
//            },
//            new MockTestAttempt { Id = 1, UserId = learner.Id, AttemptKind = AttemptKind.Placement, IsCompleted = true, StartedAt = DateTime.UtcNow.AddDays(-2), SubmittedAt = DateTime.UtcNow.AddDays(-2), QuestionSnapshotJson = snapshotJson },
//            new MockTestAttempt { Id = 2, UserId = learner.Id, AttemptKind = AttemptKind.Placement, IsCompleted = true, StartedAt = DateTime.UtcNow.AddDays(-1), SubmittedAt = DateTime.UtcNow.AddDays(-1), QuestionSnapshotJson = snapshotJson });
//        await db.SaveChangesAsync();

//        var service = new PracticeTestService(new UnitOfWork(db), Claims(learner.Id, Role.Learner), new MentorGroupPolicyService(db));
//        var status = await service.GetStatusAsync();

//        Assert.True(status.IsSuccess, status.ErrorMessage);
//        Assert.NotNull(status.Result!.AttemptAllowance);
//        Assert.Equal(3, status.Result.AttemptAllowance!.Limit);
//        Assert.Equal(2, status.Result.AttemptAllowance.Started);
//        Assert.Equal(1, status.Result.AttemptAllowance.Remaining);
//    }

//    [Fact]
//    public async Task CohortHomework_SupportsCommonAndIndividualAssignments_Revisions_PrivateAiDraft_AndManualGradingDuringAiOutage()
//    {
//        await using var db = CreateDb();
//        var mentor = new UserAccount { Id = 10, Role = Role.Mentor, FirstName = "Hana", PasswordHash = [], PasswordSalt = [] };
//        var l1 = new UserAccount { Id = 101, Role = Role.Learner, FirstName = "One", PasswordHash = [], PasswordSalt = [] };
//        var l2 = new UserAccount { Id = 102, Role = Role.Learner, FirstName = "Two", PasswordHash = [], PasswordSalt = [] };
//        var outsider = new UserAccount { Id = 103, Role = Role.Learner, FirstName = "Out", PasswordHash = [], PasswordSalt = [] };
//        var cohort = new StudyCohort
//        {
//            Id = 55, Name = "N3 Cohort", MentorId = mentor.Id, Capacity = 4, Status = "Published",
//            Enrollments =
//            [
//                new StudyCourse { Id = 201, LearnerId = l1.Id, PlanId = 1, Name = "N3 Cohort", PurchaseKey = Guid.NewGuid(), PaymentStatus = "Paid", Status = "Active" },
//                new StudyCourse { Id = 202, LearnerId = l2.Id, PlanId = 1, Name = "N3 Cohort", PurchaseKey = Guid.NewGuid(), PaymentStatus = "Paid", Status = "Active" }
//            ]
//        };
//        db.AddRange(mentor, l1, l2, outsider, cohort,
//            new MentorGroupPolicy
//            {
//                Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-2), MaxLearners = 4, SessionMinutes = 90,
//                RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 30, CancellationLeadHours = 24,
//                ReschedulingLeadHours = 24, HomeworkDeadlineDays = 5, PlacementAttemptLimit = 2,
//                RemediationCompletionThresholdPercent = 90m, KpiReportingPeriodDays = 30, CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
//            });
//        await db.SaveChangesAsync();

//        var lessonSvc = Mock.Of<ILessonProgressService>();
//        var uow = new UnitOfWork(db);
//        var mentorSvc = new HomeworkService(uow, Claims(mentor.Id, Role.Mentor), lessonSvc, grader: new FakeEssayGrader(),
//            groupPolicies: new MentorGroupPolicyService(db));
//        var l1Svc = new HomeworkService(uow, Claims(l1.Id, Role.Learner), lessonSvc);

//        // 1. Common cohort assignment broadcasts to both active learners; outsider target is denied.
//        var deniedTarget = await mentorSvc.CreateCohortAssignmentsAsync(cohort.Id,
//            new CreateHomeworkRequest(null, "Individual Outsider", "Test", null, null, null, cohort.Id, [outsider.Id]), default);
//        Assert.Equal(403, deniedTarget.StatusCode);

//        var commonRes = await mentorSvc.CreateCohortAssignmentsAsync(cohort.Id,
//            new CreateHomeworkRequest(null, "Common Cohort Essay", "Write 3 sentences", null, null, null, cohort.Id), default);
//        Assert.True(commonRes.IsSuccess, commonRes.ErrorMessage);
//        Assert.Equal(2, commonRes.Result!.Count);

//        var l1AssignId = (await db.HomeworkAssignments.SingleAsync(a => a.CohortId == cohort.Id && a.LearnerId == l1.Id)).Id;

//        // 2. Individual cohort assignment creates only for learner 2.
//        var indivRes = await mentorSvc.CreateCohortAssignmentsAsync(cohort.Id,
//            new CreateHomeworkRequest(null, "Extra Kanji L2", "Practice Kanji", null, null, null, cohort.Id, [l2.Id]), default);
//        Assert.True(indivRes.IsSuccess);
//        Assert.Single(indivRes.Result!);
//        Assert.Equal("CohortIndividual", (await db.HomeworkAssignments.FindAsync(indivRes.Result![0]))!.AssignmentScope);

//        // 3. Learner submits and revises before grading.
//        var sub1 = await l1Svc.SubmitAsync(l1AssignId, new SubmitHomeworkRequest("Draft answer v1", null), default);
//        Assert.Equal(201, sub1.StatusCode);
//        Assert.Equal(1, sub1.Result!.RevisionCount);

//        var sub2 = await l1Svc.SubmitAsync(l1AssignId, new SubmitHomeworkRequest("Revised answer v2", null), default);
//        Assert.Equal(200, sub2.StatusCode);
//        Assert.Equal(2, sub2.Result!.RevisionCount);
//        Assert.NotNull(sub2.Result.LastRevisedAtUtc);

//        // 4. AI Draft is visible ONLY to Mentor and strictly hidden from Learner.
//        var aiRes = await mentorSvc.CreateDraftAsync(l1AssignId, default);
//        Assert.True(aiRes.IsSuccess, aiRes.ErrorMessage);
//        Assert.Equal(81.5, (await db.HomeworkAssignments.FindAsync(l1AssignId))!.AiDraftScore);

//        var mentorView = (await mentorSvc.GetAssignmentsAsync(l1.Id, default, cohort.Id)).Single(x => x.Id == l1AssignId);
//        var learnerView = (await l1Svc.GetAssignmentsAsync(null, default, cohort.Id)).Single(x => x.Id == l1AssignId);
//        Assert.NotNull(mentorView.AiDraft);
//        Assert.Null(learnerView.AiDraft);
//        Assert.Null(learnerView.PublishedGrade);

//        // 5. Even when AI provider fails/outages, manual Mentor grading & publishing succeeds without blocking!
//        var outageMentorSvc = new HomeworkService(uow, Claims(mentor.Id, Role.Mentor), lessonSvc, grader: new FakeEssayGrader(outage: true));
//        var failedAi = await outageMentorSvc.CreateDraftAsync(l1AssignId, default);
//        Assert.False(failedAi.IsSuccess);
//        Assert.Equal(502, failedAi.StatusCode);
//        Assert.Equal("HOMEWORK_AI_DRAFT_UNAVAILABLE", failedAi.ErrorCode);
//        var afterOutage = await db.HomeworkSubmissions.SingleAsync(s => s.HomeworkAssignmentId == l1AssignId);
//        Assert.Equal("Revised answer v2", afterOutage.TextContent); // bài nộp không bị ảnh hưởng khi AI lỗi
//        Assert.NotNull((await db.HomeworkAssignments.FindAsync(l1AssignId))!.AiDraftUnavailableReason);

//        var published = await outageMentorSvc.GradeAndPublishAsync(l1AssignId,
//            new GradeHomeworkAssignmentRequest(true, "Good improvement in v2!", 92.0), default);
//        Assert.True(published.IsSuccess, published.ErrorMessage);

//        var learnerAfterGrade = (await l1Svc.GetAssignmentsAsync(null, default, cohort.Id)).Single(x => x.Id == l1AssignId);
//        Assert.NotNull(learnerAfterGrade.PublishedGrade);
//        Assert.Equal("Good improvement in v2!", learnerAfterGrade.PublishedGrade!.Feedback);
//        // Một writer: GradingResult canonical (teammate) và bản sao trên assignment trùng nhau.
//        Assert.Equal(92.0, learnerAfterGrade.Submission!.Grade!.Score);
//        Assert.Equal("Good improvement in v2!", learnerAfterGrade.Submission.Grade.Feedback);
//        Assert.Null(learnerAfterGrade.AiDraft);

//        // Revisions blocked after published grading.
//        var postGradeRevise = await l1Svc.SubmitAsync(l1AssignId, new SubmitHomeworkRequest("Attempt v3", null), default);
//        Assert.Equal(409, postGradeRevise.StatusCode);
//    }

//    [Fact]
//    public async Task RemediationPolicy_HonorsComparisonOperator_CohortAssignments_AndAutoApprovalFlags()
//    {
//        var strictPolicy = new MentorGroupPolicy
//        {
//            Id = 1, Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-5), MaxLearners = 4, SessionMinutes = 90,
//            RemediationCompletionThresholdPercent = 90m, RemediationThresholdComparison = ">",
//            RequireOfficialExamEvidence = false, RequireAdminRemediationReview = false
//        };
//        var inclusivePolicy = new MentorGroupPolicy
//        {
//            Id = 2, Version = 2, EffectiveFromUtc = DateTime.UtcNow.AddDays(-5), MaxLearners = 4, SessionMinutes = 90,
//            RemediationCompletionThresholdPercent = 90m, RemediationThresholdComparison = ">="
//        };

//        StudyCourse BuildCourse(int submittedOnTime, int total)
//        {
//            var c = new StudyCourse
//            {
//                Id = 500, LearnerId = 9, Status = "Completed", PaymentStatus = "Paid", TotalSessions = 1,
//                StartsAt = DateTime.UtcNow.AddDays(-20), EndsAt = DateTime.UtcNow.AddDays(-1)
//            };
//            c.Sessions.Add(new() { Number = 1, Status = "Present", ScheduledAt = DateTime.UtcNow.AddDays(-10) });
//            for (var i = 1; i <= total; i++)
//                c.Homework.Add(new() { Id = i, Required = true, DueAt = DateTime.UtcNow.AddDays(-5), SubmittedAt = i <= submittedOnTime ? DateTime.UtcNow.AddDays(-6) : DateTime.UtcNow.AddDays(-3) });
//            return c;
//        }

//        // Exact 90% fails under strict ">" comparison and passes under ">=".
//        Assert.False(CourseRetakePolicy.Evaluate(BuildCourse(9, 10), false, strictPolicy).CanClaim);
//        Assert.True(CourseRetakePolicy.Evaluate(BuildCourse(9, 10), false, inclusivePolicy).CanClaim);
//        Assert.True(CourseRetakePolicy.Evaluate(BuildCourse(10, 10), false, strictPolicy).CanClaim);

//        // End-to-end auto-remediation & optional evidence when policy disables OfficialExamEvidence & AdminReview.
//        await using var db = CreateDb();
//        var certType = new CertificateType { Id = 1, Name = "JLPT", IsActive = true };
//        var level = new CertificateLevel { Id = 3, Code = "N3", CertificateTypeId = 1, CertificateType = certType };
//        var learner = new UserAccount { Id = 9, Role = Role.Learner, FirstName = "Ken", PasswordHash = [], PasswordSalt = [] };
//        var completedCourse = new StudyCourse
//        {
//            Id = 600, LearnerId = 9, MentorId = 2, PlanId = 1, Name = "N3 1-1", CertificateLevelId = 3, PlanRevision = 1,
//            DurationMonths = 1, SessionsPerWeek = 2, SessionMinutes = 90, TotalSessions = 1, RetakeClaimDays = 60,
//            MinimumHomeworkPercent = 90, MaximumAbsencePercent = 20, Status = "Completed", PaymentStatus = "Paid",
//            StartsAt = DateTime.UtcNow.AddDays(-20), EndsAt = DateTime.UtcNow.AddDays(-1), PurchaseKey = Guid.NewGuid(),
//            GroupPolicy = strictPolicy
//        };
//        completedCourse.Sessions.Add(new() { Id = 1, Number = 1, Status = "Present", ScheduledAt = DateTime.UtcNow.AddDays(-10) });
//        for (var i = 1; i <= 10; i++)
//            completedCourse.Homework.Add(new() { Id = i, Required = true, DueAt = DateTime.UtcNow.AddDays(-5), SubmittedAt = DateTime.UtcNow.AddDays(-6) });

//        db.AddRange(certType, level, learner, strictPolicy, completedCourse);
//        await db.SaveChangesAsync();

//        var courseSvc = new StudyCourseService(new UnitOfWork(db), Claims(learner.Id, Role.Learner),
//            Mock.Of<IPaymentGatewayClient>(), Mock.Of<IFileStorageService>(),
//            new AppSettings { FrontendBaseUrl = "https://test.invalid" }, groupPolicies: new MentorGroupPolicyService(db));
//        var retakeRes = await courseSvc.RequestRetakeAsync(
//            completedCourse.Id, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), null, default);

//        Assert.True(retakeRes.IsSuccess, retakeRes.ErrorMessage);
//        Assert.Equal(1, await db.StudyCourses.CountAsync(x => x.RetakeOfId == completedCourse.Id));
//    }
//}
