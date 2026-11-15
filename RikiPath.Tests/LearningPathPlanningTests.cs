using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public class LearningPathPlanningTests
{
    [Theory]
    [InlineData(0, 90, true, 60, 14)]
    [InlineData(0, 20, true, 60, 60)]
    [InlineData(0, 90, false, 60, 60)]
    [InlineData(1, 50, true, 30, 120)]
    [InlineData(1, 50, true, 120, 30)]
    public void Estimate_accounts_for_weak_scores_missing_data_and_daily_time(int gap, double score, bool hasData, int minutes, int expected)
        => Assert.Equal(expected, LearningPathPlanningPolicy.EstimateStudyDays(gap, score, hasData, minutes));

    [Fact]
    public async Task Near_target_learner_has_short_term_classification_even_with_distant_exam_and_full_schedule()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest
        { TargetExamDate = fixture.Today.AddDays(70) }, default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var path = result.Result!;
        Assert.True(path.IsExamDateRealistic);
        Assert.False(path.IsAlternativePlan);
        Assert.Equal("short_term", path.Horizon);
        Assert.Equal(67, path.PlanDurationDays);
        Assert.Equal(67, path.DailySchedules.Count);
        Assert.Equal(3, fixture.Prompts.Count);
        Assert.Equal(fixture.Today.AddDays(67), path.RestStartDate);
        Assert.Equal(fixture.Today.AddDays(66), path.LastStudyDate);
        Assert.Equal(Enumerable.Range(1, 67), path.DailySchedules.Select(x => x.Day));
        Assert.All(path.DailySchedules, day => Assert.True(day.Date < path.RestStartDate));
        Assert.Contains(JsonEncodedText.Encode("日本").ToString(), fixture.Prompts[0]);
        Assert.Contains(JsonEncodedText.Encode("学校").ToString(), fixture.Prompts[0]);
        Assert.Contains(JsonEncodedText.Encode("です").ToString(), fixture.Prompts[0]);
        Assert.DoesNotContain(JsonEncodedText.Encode("削除済み").ToString(), fixture.Prompts[0]);
        Assert.Equal(4, path.SectionAssessments.Count);
        Assert.All(path.SectionAssessments, section => Assert.Equal(90, section.ScorePercent));
        Assert.Equal(1, path.PlacementAttemptId);
        Assert.Equal(1, path.Phases[0].StartDay);
        Assert.Equal(67, path.Phases[^1].EndDay);
        var stored = await fixture.Context.RecommendedLearningPaths.SingleAsync();
        Assert.Contains("restStartDate", stored.SuggestionJson);
    }

    [Fact]
    public async Task Too_close_exam_returns_high_risk_advice_and_explicit_alternative_without_changing_goal()
    {
        await using var fixture = await Fixture.CreateAsync(20, new DateTime(2026, 12, 1));
        var originalDate = fixture.Today.AddDays(5);
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest
        { TargetExamDate = originalDate }, default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var path = result.Result!;
        Assert.False(path.IsExamDateRealistic);
        Assert.True(path.IsAlternativePlan);
        Assert.Equal("at_risk", path.Readiness);
        Assert.Equal(originalDate, path.TargetExamDate);
        Assert.Equal(2, path.AvailableStudyDays);
        Assert.Equal(new DateTime(2027, 7, 1), path.SuggestedExamDate);
        Assert.Equal(path.SuggestedExamDate, path.PlanningExamDate);
        Assert.Equal(LearningPathPlanningPolicy.AvailableStudyDays(fixture.Today, path.SuggestedExamDate!.Value), path.DailySchedules.Count);
        Assert.Contains("không thể khẳng định chắc chắn", path.Feasibility);
        Assert.All(path.SectionAssessments, section => Assert.Equal("high", section.Priority));
    }

    [Fact]
    public async Task Latest_completed_snapshot_drives_plan_even_if_profile_assessment_changes()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        await fixture.AddPlacementAsync(2, 20, fixture.Today.AddHours(5));
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest(), default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.Result!.PlacementAttemptId);
        Assert.Equal(60, result.Result.RecommendedStudyDays);
        Assert.All(result.Result.SectionAssessments, section => Assert.Equal(20, section.ScorePercent));
    }

    [Fact]
    public void Rest_boundary_counts_today_as_a_study_day_and_reserves_full_three_days()
    {
        var today = new DateTime(2026, 11, 8);
        Assert.Equal(0, LearningPathPlanningPolicy.AvailableStudyDays(today, today.AddDays(3)));
        Assert.Equal(1, LearningPathPlanningPolicy.AvailableStudyDays(today, today.AddDays(4)));
        Assert.Equal(new DateTime(2026, 12, 1), LearningPathPlanningPolicy.SuggestedExamDate(today, 14));
        Assert.Equal(new DateTime(2026, 12, 8), LearningPathPlanningPolicy.SuggestedExamDate(today, 27));
    }

    [Fact]
    public async Task Generate_and_preview_reject_exam_months_outside_july_and_december()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        var invalidDate = new DateTime(2026, 11, 15);
        var generated = await fixture.Service.GenerateLearningPathAsync(
            new GenerateLearningPathRequest { TargetExamDate = invalidDate }, default);
        var preview = await fixture.Service.PreviewLearningPathAsync(
            new PreviewLearningPathRequest { TargetExamDate = invalidDate }, default);
        Assert.Equal("JLPT_EXAM_MONTH_INVALID", generated.ErrorCode);
        Assert.Equal("JLPT_EXAM_MONTH_INVALID", preview.ErrorCode);
        Assert.Empty(fixture.Prompts);
    }

    [Fact]
    public async Task Missing_skill_evidence_is_not_reported_as_a_feasible_exam()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        var attempt = await fixture.Context.MockTestAttempts.SingleAsync();
        var snapshot = System.Text.Json.Nodes.JsonNode.Parse(attempt.QuestionSnapshotJson)!;
        snapshot["completedResult"]!["sections"]!.AsArray().RemoveAt(2);
        attempt.QuestionSnapshotJson = snapshot.ToJsonString();
        await fixture.Context.SaveChangesAsync();
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest
        { TargetExamDate = fixture.Today.AddDays(70) }, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Null(result.Result!.IsExamDateRealistic);
        Assert.Equal("insufficient_data", result.Result.Readiness);
        Assert.Contains(result.Result.SectionAssessments, x => !x.HasData && x.Priority == "needs_assessment");
    }

    [Fact]
    public async Task Failed_later_batch_does_not_persist_a_partial_plan()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        fixture.FailFromCall = 2;
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest
        { TargetExamDate = fixture.Today.AddDays(70) }, default);
        Assert.False(result.IsSuccess);
        Assert.Empty(await fixture.Context.RecommendedLearningPaths.ToListAsync());
        Assert.Equal(3, fixture.Prompts.Count);
    }

    [Fact]
    public async Task Schedule_prioritizes_below_target_skill_when_catalog_has_multiple_lessons()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        await fixture.AddMixedLevelsAsync();
        fixture.Context.Lessons.AddRange(
            new Lesson { Id = 605, Title = "Luyện thêm ngữ pháp N5", CertificateLevelId = 71005, LanguageSkillId = 1 },
            new Lesson { Id = 606, Title = "Luyện thêm từ vựng N2", CertificateLevelId = 71002, LanguageSkillId = 2 },
            new Lesson { Id = 607, Title = "Củng cố ngữ pháp N5", CertificateLevelId = 71005, LanguageSkillId = 1 });
        await fixture.Context.SaveChangesAsync();
        fixture.ScheduleLessonIds = [501, 605, 601, 606, 603, 604];
        var request = new GenerateLearningPathRequest
        {
            CertificateTypeId = 71, TargetCertificateLevelId = 71002, DailyStudyMinutes = 60,
        };

        var equal = await fixture.Service.GenerateLearningPathAsync(request, default);
        Assert.False(equal.IsSuccess);
        Assert.Contains("Ngữ pháp", equal.ErrorMessage);
        Assert.Empty(await fixture.Context.RecommendedLearningPaths.ToListAsync());

        fixture.ScheduleLessonIds = [501, 605, 607, 601, 606, 603, 604];
        var prioritized = await fixture.Service.GenerateLearningPathAsync(request, default);
        Assert.True(prioritized.IsSuccess, prioritized.ErrorMessage);
        var lessonIds = prioritized.Result!.DailySchedules.SelectMany(day => day.LessonIds).ToList();
        Assert.True(lessonIds.Count(id => id is 501 or 605 or 607) > lessonIds.Count(id => id is 601 or 606));
    }

    [Fact]
    public async Task Mixed_skill_levels_route_lessons_independently_and_compare_15_with_60_minutes()
    {
        await using var fixture = await Fixture.CreateAsync(90, new DateTime(2026, 9, 15));
        await fixture.AddMixedLevelsAsync();
        fixture.ScheduleLessonIds = [501, 601, 603, 604];
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest
        {
            CertificateTypeId = 71, TargetCertificateLevelId = 71002,
            DailyStudyMinutes = 15, TargetExamDate = fixture.Today.AddDays(100), Horizon = "short_term"
        }, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var path = result.Result!;
        Assert.Equal(4, path.SectionAssessments.Count);
        var levels = path.SectionAssessments.ToDictionary(x => x.SkillCode, x => x.EstimatedLevelCode);
        Assert.Equal("N2", levels["vocabulary"]);
        Assert.Equal("N5", levels["grammar"]);
        Assert.Equal("N3", levels["reading"]);
        Assert.Equal("N4", levels["listening"]);
        var shares = path.SectionAssessments.ToDictionary(x => x.SkillCode, x => x.SuggestedStudySharePercent!.Value);
        Assert.True(shares["grammar"] > shares["listening"]);
        Assert.True(shares["listening"] > shares["reading"]);
        Assert.True(shares["reading"] > shares["vocabulary"]);
        Assert.InRange(shares.Values.Sum(), 99.8, 100.2);
        Assert.Contains("SuggestedStudySharePercent", fixture.Prompts[0]);
        Assert.Contains("TỶ TRỌNG LƯỢT BÀI ĐÃ XẾP", fixture.Prompts[1]);
        Assert.False(path.IsExamDateRealistic);
        Assert.True(path.RequiresMoreDailyStudyTime);
        Assert.Equal(60, path.SuggestedDailyStudyMinutes);
        Assert.True(path.StudyTimeScenarios.Single(x => x.MinutesPerDay == 60).CanMeetRequestedExamDate);
        Assert.False(path.StudyTimeScenarios.Single(x => x.MinutesPerDay == 15).CanMeetRequestedExamDate);
        Assert.Equal(path.TotalStudyDays, path.PlanDurationDays);
        Assert.Equal("long_term", path.Horizon);
        Assert.True(path.TotalStudyDays > 30);
        Assert.All(path.DailySchedules, day => Assert.Equal(15, day.CoreStudyMinutes));
        Assert.DoesNotContain("id=699 |", fixture.Prompts[0]);
        Assert.Contains("id=601 |", fixture.Prompts[0]);
        Assert.Contains("id=501 |", fixture.Prompts[0]);
        Assert.Equal(71, path.CertificateTypeId);
        Assert.Equal(71002, path.TargetCertificateLevelId);
        var preview = await fixture.Service.PreviewLearningPathAsync(new PreviewLearningPathRequest
        {
            CertificateTypeId = 71, TargetCertificateLevelId = 71002, Horizon = "short_term",
            DailyStudyMinutesMin = 30, DailyStudyMinutesMax = 60, TargetExamDate = fixture.Today.AddDays(100)
        }, default);
        Assert.True(preview.IsSuccess, preview.ErrorMessage);
        Assert.True(preview.Result!.StudyTimeScenarios.Single(x => x.MinutesPerDay == 60).WithinRequestedRange);
        Assert.Equal(15, (await fixture.Context.Users.SingleAsync(x => x.Id == 7)).DailyStudyMinutes);
        var confirmation = await fixture.Service.ConfirmPreviewAsync(preview.Result.PathId, default);
        Assert.True(confirmation.IsSuccess, confirmation.ErrorMessage);
        var user = await fixture.Context.Users.SingleAsync(x => x.Id == 7);
        Assert.Equal(30, user.DailyStudyMinutesMin);
        Assert.Equal(60, user.DailyStudyMinutesMax);
    }

    [Fact]
    public async Task Invalid_time_range_is_rejected_before_AI_and_does_not_mutate_profile()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest
        { DailyStudyMinutesMin = 60, DailyStudyMinutesMax = 15 }, default);
        Assert.False(result.IsSuccess);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(await fixture.Context.RecommendedLearningPaths.ToListAsync());
    }

    [Fact]
    public async Task AI_cannot_promise_a_guaranteed_pass_even_if_it_also_mentions_uncertainty()
    {
        await using var fixture = await Fixture.CreateAsync(90);
        fixture.AiSummary = "Không bảo đảm đỗ trong mọi trường hợp. Nhưng bạn chắc chắn đỗ N2 nếu học 15 phút mỗi ngày.";
        var result = await fixture.Service.GenerateLearningPathAsync(new GenerateLearningPathRequest { Horizon = "short_term" }, default);
        Assert.False(result.IsSuccess);
        Assert.Empty(await fixture.Context.RecommendedLearningPaths.ToListAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public AppDbContext Context { get; private init; } = null!;
        public LearningPathService Service { get; private set; } = null!;
        public DateTime Today { get; private init; }
        public List<string> Prompts { get; } = new();
        public int FailFromCall { get; set; } = int.MaxValue;
        public List<int> ScheduleLessonIds { get; set; } = [501];
        public string AiSummary { get; set; } = "Ôn luyện theo kết quả đánh giá.";

        public static async Task<Fixture> CreateAsync(int score, DateTime? today = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
            var fixture = new Fixture { Context = new AppDbContext(options), Today = today ?? new DateTime(2026, 9, 25) };
            var certificate = new CertificateType { Id = 71, Name = "JLPT", IsActive = true };
            var level = new CertificateLevel { Id = 71005, CertificateType = certificate, Code = "N5" };
            var skill = new LanguageSkill { Id = 1, Name = "Grammar" };
            fixture.Context.Users.Add(new UserAccount
            {
                Id = 7, Email = "planning@example.test", PasswordHash = [], PasswordSalt = [], Role = Role.Learner,
                StartingCertificateLevel = level, TargetCertificateLevel = level, DailyStudyMinutes = 60
            });
            fixture.Context.Lessons.Add(new Lesson
            {
                Id = 501, Title = "Bài nền tảng", CertificateLevel = level, LanguageSkill = skill,
                LessonKanjis = [new LessonKanji { Kanji = new Kanji { Character = "日本", Meaning = "Nhật Bản" } }],
                LessonVocabularies = [new LessonVocabulary { Vocabulary = new Vocabulary { Word = "学校", Reading = "がっこう", Meaning = "Trường học" } },
                    new LessonVocabulary { Vocabulary = new Vocabulary { Word = "削除済み", Reading = "none", Meaning = "Deleted", IsDeleted = true } }],
                LessonGrammars = [new LessonGrammar { GrammarPattern = new GrammarPattern { Title = "です", Structure = "N です" } }]
            });
            await fixture.Context.SaveChangesAsync();
            await fixture.AddPlacementAsync(1, score, fixture.Today.AddHours(4));
            var claims = new Mock<IClaimService>();
            claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 7, Role = Role.Learner });
            var ai = new Mock<IAiLearningPathClient>();
            ai.Setup(x => x.GenerateLearningPathJsonAsync(7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int _, string prompt, CancellationToken _) =>
                {
                    fixture.Prompts.Add(prompt);
                    if (fixture.Prompts.Count >= fixture.FailFromCall) return "not JSON";
                    var count = int.Parse(Regex.Match(prompt, @"Lập đúng (\d+) ngày").Groups[1].Value);
                    return JsonSerializer.Serialize(new
                    {
                        summary = fixture.AiSummary,
                        skillFeedback = new[] { "vocabulary", "grammar", "reading", "listening" }.Select(skill => new { skillCode = skill, comment = "Ôn luyện theo bằng chứng từng cấp độ.", recommendation = "Duy trì đều và kiểm tra tiến bộ." }),
                        focusTopics = new[] { "Củng cố kỹ năng" },
                        dailySchedules = Enumerable.Range(1, count).Select(day => new
                        { day, title = "Ôn tập nền tảng", focus = "Luyện theo nội dung bài học", lessonIds = fixture.ScheduleLessonIds })
                    });
                });
            fixture.Service = new LearningPathService(new UnitOfWork(fixture.Context), ai.Object, claims.Object,
                Mock.Of<ILogger<LearningPathService>>()) { UtcNowProvider = () => DateTime.SpecifyKind(fixture.Today, DateTimeKind.Utc) };
            return fixture;
        }

        public async Task AddPlacementAsync(int id, int score, DateTime submittedAt)
        {
            var sections = new[] { "Vocabulary", "Grammar", "Reading", "Listening" }.Select((name, index) => new SectionResultItem
            { MockTestSectionId = 10 + index, SectionTitle = "N5 " + name, LanguageSkillName = name, CorrectCount = score / 10, TotalCount = 10, Score = score, HasAudioEvidence = name == "Listening", JlptLevelName = "N5" }).ToList();
            var completedResult = new PlacementTestResultResponse
            {
                AttemptId = id, StartedAt = submittedAt.AddMinutes(-30), SubmittedAt = submittedAt,
                TotalScore = score, CorrectCount = 4 * score / 10, TotalQuestions = 40,
                AssessedJlptLevelId = 71005, AssessedJlptLevelName = "N5",
                RecommendedTargetJlptLevelId = 71005, RecommendedTargetJlptLevelName = "N5", Sections = sections,
                LevelBreakdowns = [new PlacementLevelBreakdownItem { JlptLevelId = 71005, JlptLevelName = "N5", CorrectCount = 4 * score / 10, TotalCount = 40, ScorePercent = score }]
            };
            Context.MockTestAttempts.Add(new MockTestAttempt
            {
                Id = id, UserId = 7, AttemptKind = AttemptKind.Placement, IsCompleted = true, TotalScore = score,
                StartedAt = completedResult.StartedAt, SubmittedAt = submittedAt,
                QuestionSnapshotJson = JsonSerializer.Serialize(new { schemaVersion = 2, completedResult }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();

        public async Task AddMixedLevelsAsync()
        {
            foreach (var code in new[] { "N4", "N3", "N2" })
                Context.CertificateLevels.Add(new CertificateLevel { Id = 71000 + int.Parse(code[1..]), CertificateTypeId = 71, Code = code });
            Context.LanguageSkills.AddRange(new LanguageSkill { Id = 2, Name = "Vocabulary" }, new LanguageSkill { Id = 3, Name = "Reading" },
                new LanguageSkill { Id = 4, Name = "Listening" }, new LanguageSkill { Id = 5, Name = "Kanji" });
            Context.Lessons.AddRange(
                new Lesson { Id = 601, Title = "Từ vựng N2", CertificateLevelId = 71002, LanguageSkillId = 2 },
                new Lesson { Id = 603, Title = "Đọc hiểu N3", CertificateLevelId = 71003, LanguageSkillId = 3 },
                new Lesson { Id = 604, Title = "Nghe hiểu N4", CertificateLevelId = 71004, LanguageSkillId = 4 },
                new Lesson { Id = 699, Title = "Kanji riêng không phải kỹ năng thứ năm", CertificateLevelId = 71005, LanguageSkillId = 5 });
            var attempt = await Context.MockTestAttempts.SingleAsync();
            var root = System.Text.Json.Nodes.JsonNode.Parse(attempt.QuestionSnapshotJson!)!;
            var completed = root["completedResult"]!.Deserialize<PlacementTestResultResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            completed.Sections = new[] { ("Vocabulary", "N2"), ("Grammar", "N5"), ("Reading", "N3"), ("Listening", "N4"), ("Kanji", "N2") }
                .Select((item,index) => new SectionResultItem
                {
                    MockTestSectionId = 20 + index, SectionTitle = item.Item2 + " " + item.Item1, LanguageSkillName = item.Item1,
                    JlptLevelName = item.Item2, TotalCount = 10, CorrectCount = 9, Score = 90, HasAudioEvidence = item.Item1 == "Listening"
                }).ToList();
            root["completedResult"] = JsonSerializer.SerializeToNode(completed, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            attempt.QuestionSnapshotJson = root.ToJsonString();
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }
    }
}
