using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public class PlacementFourSkillSamplingTests
{
    [Theory]
    [InlineData(true, 48, null, null, null)]
    [InlineData(false, 48, null, null, null)]
    [InlineData(true, 24, "N2", 0, null)]
    [InlineData(true, 24, "N2", 11, null)]
    [InlineData(true, 24, "N2", 12, null)]
    [InlineData(false, 24, "N2", 0, null)]
    [InlineData(true, 24, "N3", 5, null)]
    [InlineData(true, 24, "N3", 12, null)]
    [InlineData(true, 24, "N4", 0, null)]
    [InlineData(true, 40, "N4", 20, "NONE")]
    [InlineData(true, 12, "N5", 0, null)]
    [InlineData(true, 12, "N5", 8, null)]
    [InlineData(true, 24, "N2", null, "NONE")]
    [InlineData(true, 24, "N3", null, "NONE")]
    [InlineData(true, 24, "N4", 9, "N5")]
    [InlineData(true, 24, "N3", 12, "N4")]
    [InlineData(true, 24, "N3", 12, "N5")]
    [InlineData(true, 24, "N2", 12, "N3")]
    [InlineData(true, 24, "N2", 12, "N4")]
    [InlineData(true, 32, "N2", 16, "N5")]
    public async Task Placement_compares_adjacent_levels_per_skill_and_allows_planning_after_low_scores(
        bool hasAudio, int expected, string? goal, int? correct, string? declared)
    {
        var isShortened = declared is not (null or "NONE");
        var lower = goal switch { "N2" => "N3", "N3" => "N4", "N4" => "N5", _ => null };
        var testedLevels = lower is null ? new[] { goal } : [lower, goal];
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var context = new AppDbContext(options);
        var certificate = new CertificateType { Id = 71, Name = "JLPT", IsActive = true };
        var skills = new[] { "Vocabulary", "Grammar", "Reading", "Listening", "Kanji" }
            .Select((name,index) => new LanguageSkill { Id = 100 + index, Name = name }).ToList();
        context.LanguageSkills.AddRange(skills);
        context.Users.Add(new UserAccount { Id = 7, Email = "four-skills@example.test", PasswordHash = [], PasswordSalt = [], Role = Role.Learner,
            TargetCertificateLevelId = goal is null ? null : 71000 + (goal[1] - '0'),
            DeclaredJlptCertificateCode = declared,
            TargetExamDate = goal == "N5" ? null : new DateTime(DateTime.UtcNow.Year + 1, 12, 1),
            DailyStudyMinutes = 30 });
        var questionId = 10000;
        foreach (var (code, index) in new[] { "N5", "N4", "N3", "N2", "N1" }.Select((code,index) => (code,index)))
        {
            if (declared == "NONE" && !testedLevels.Contains(code)) continue;
            var level = new CertificateLevel { Id = 71005 - index, Code = code, CertificateType = certificate };
            var testCount = goal == "N2" && correct == 0 && code == "N2" ? 2 : 1;
            for (var copy = 0; copy < testCount; copy++)
            {
                var test = new MockTest { Id = 200 + index + copy * 100, Title = "Đề đánh giá " + code, CertificateLevel = level, TimeLimitMinutes = 60, Sections = [] };
                foreach (var skill in skills)
                {
                    var section = new MockTestSection { Id = 1000 + index * 10 + skill.Id + copy * 100, Title = code + " " + skill.Name,
                        SortOrder = skill.Id, LanguageSkill = skill, CertificateSectionId = 1, Questions = [] };
                    var availableQuestions = expected is 32 or 40 ? 9 : testCount == 2 ? 4 : 3;
                    for (var number = 0; number < availableQuestions; number++)
                    {
                        var id = questionId++;
                        section.Questions.Add(new MockQuestion
                        {
                            Id = id, QuestionText = "Câu đánh giá " + id, SortOrder = number + 1,
                            AudioUrl = skill.Name == "Listening" && hasAudio ? "https://example.test/japanese-audio.mp3" : null,
                            Options = [new MockQuestionOption { Id = id * 10, OptionText = "Đáp án đúng", IsCorrect = true, SortOrder = 1 },
                                new MockQuestionOption { Id = id * 10 + 1, OptionText = "Đáp án khác", IsCorrect = false, SortOrder = 2 }]
                        });
                    }
                    if (declared == "NONE" && goal is ("N2" or "N3") && skill.Name == "Grammar" && code == goal)
                    {
                        section.Questions.Add(new MockQuestion
                        {
                            Id = questionId++, SortOrder = 4, QuestionText = "Sắp xếp câu",
                            QuestionType = "Ordering", InteractionDataJson = """{"tokens":["図書館","読みます","で","本を"]}""",
                            AnswerKeyJson = """["図書館","で","本を","読みます"]""",
                        });
                    }
                    test.Sections.Add(section);
                }
                context.MockTests.Add(test);
            }
        }
        await context.SaveChangesAsync();
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 7, Role = Role.Learner });
        var clockOffset = TimeSpan.Zero;
        var service = new PracticeTestService(new UnitOfWork(context), claims.Object)
        {
            UtcNowProvider = () => DateTime.UtcNow + clockOffset,
        };
        var result = await service.StartPlacementTestAsync(default);
        if (declared == "NONE" && goal is ("N2" or "N3"))
        {
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var attempt = result.Result!;
            Assert.False(attempt.IsComprehensiveAssessment);
            Assert.False(attempt.IsMiniAssessment);
            Assert.Equal(expected, attempt.TotalQuestions);
            Assert.Equal(testedLevels, attempt.Questions.Select(question => question.JlptLevelName).Distinct());
            Assert.Equal(8, attempt.Questions.Select(question => question.SectionTitle).Distinct().Count());
            Assert.DoesNotContain(attempt.Questions, q => q.SectionTitle.Contains("Kanji"));
            var ordering = Assert.Single(attempt.Questions.Where(q => q.QuestionType == "Ordering"));
            Assert.Equal(goal, ordering.JlptLevelName);
            Assert.Single(ordering.Options);
            Assert.Null(ordering.Response);
            Assert.Equal(4, ordering.InteractionData!.Value.GetProperty("tokens").GetArrayLength());
            var notStudiedQuestion = attempt.Questions.First(q => q.QuestionType != "Ordering" && q.SectionTitle.Contains("Grammar"));
            var invalid = await service.SavePlacementAnswersAsync(attempt.AttemptId, new SubmitPlacementTestRequest
            {
                Answers = [new() { QuestionId = ordering.QuestionId, Response = JsonSerializer.SerializeToElement(new[] { "で", "で" }) }]
            });
            Assert.False(invalid.IsSuccess);
            Assert.All(attempt.Questions, question =>
            {
                Assert.Equal(0, question.Options[^1].OptionId);
                Assert.Equal("Tôi chưa học, tôi không biết", question.Options[^1].Content);
                Assert.Single(question.Options.Where(option => option.OptionId == 0));
            });
            Assert.Equal(attempt.AttemptId, (await service.GetActiveAttemptAsync()).Result!.AttemptId);

            var answers = new SubmitPlacementTestRequest
            {
                Answers = attempt.Questions.Where(q => q.SectionTitle.Contains("Vocabulary"))
                    .GroupBy(q => q.JlptLevelName).SelectMany(group => group.Take(group.Key == goal ? 2 : 3))
                    .Select(q => new AnswerSubmissionItem
                {
                    QuestionId = q.QuestionId,
                    SelectedOptionId = q.QuestionId * 10
                }).ToList()
            };
            answers.Answers.Add(new AnswerSubmissionItem { QuestionId = notStudiedQuestion.QuestionId, SelectedOptionId = 0 });
            answers.Answers.Add(new AnswerSubmissionItem
            {
                QuestionId = ordering.QuestionId, Response = JsonSerializer.SerializeToElement(new[] { "図書館", "で", "本を", "読みます" })
            });
            if (goal == "N2")
            {
                Assert.True((await service.SavePlacementAnswersAsync(attempt.AttemptId, answers)).IsSuccess);
                var resumed = await service.GetActiveAttemptAsync();
                Assert.Equal(0, resumed.Result!.Questions.Single(q => q.QuestionId == notStudiedQuestion.QuestionId).SelectedOptionId);
                Assert.Equal("図書館", resumed.Result.Questions.Single(q => q.QuestionId == ordering.QuestionId).Response!.Value[0].GetString());
                clockOffset = TimeSpan.FromMinutes(attempt.TimeLimitMinutes + 1);
                var expired = await service.GetPlacementTestResultAsync(attempt.AttemptId);
                Assert.Equal("PLACEMENT_ATTEMPT_EXPIRED", expired.ErrorCode);

                // A timeout submits the answers saved before the deadline.
                answers = new SubmitPlacementTestRequest();
            }
            var submitted = await service.SubmitPlacementTestAsync(attempt.AttemptId, answers);
            Assert.True(submitted.IsSuccess, submitted.ErrorMessage);
            Assert.False(submitted.Result!.IsComprehensiveAssessment);
            Assert.Equal(1, submitted.Result.NotStudiedCount);
            Assert.Equal(6, submitted.Result.CorrectCount);
            var savedAnswer = await context.MockTestAnswers.SingleAsync(row =>
                row.MockTestAttemptId == attempt.AttemptId && row.MockQuestionId == notStudiedQuestion.QuestionId);
            Assert.Null(savedAnswer.SelectedOptionId);
            Assert.Contains("not_studied", savedAnswer.ResponseJson);
            Assert.False(savedAnswer.IsCorrect);
            Assert.False(submitted.Result.RequiresMiniAssessment);
            Assert.Equal(goal, submitted.Result.RecommendedTargetJlptLevelName);
            var vocabulary = submitted.Result.SkillAssessments.Single(x => x.SkillCode == "vocabulary");
            Assert.Equal(lower, vocabulary.EstimatedLevelCode);
            Assert.Equal($"{lower}–{goal}", vocabulary.EstimatedLevelBand);
            Assert.Equal(testedLevels, vocabulary.LevelBreakdowns.Select(level => level.LevelCode));
            var recovered = (await service.GetPlacementTestResultAsync(attempt.AttemptId)).Result!;
            Assert.Equal(vocabulary.EstimatedLevelBand, recovered.SkillAssessments.Single(x => x.SkillCode == "vocabulary").EstimatedLevelBand);
            Assert.Null(submitted.Result.SkillAssessments.Single(x => x.SkillCode == "grammar").EstimatedLevelCode);
            Assert.Equal(attempt.AttemptId, (await service.GetStatusAsync()).Result!.LatestCompletedAttemptId);
            Assert.False((await service.GetStatusAsync()).Result!.IsComprehensiveAssessmentRequired);
            return;
        }
        if (correct.HasValue)
        {
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var initial = result.Result!;
            Assert.Equal(expected, initial.Questions.Count);
            Assert.Equal(expected, initial.TotalQuestions);
            Assert.Equal(isShortened ? expected * 5 / 4 : lower is null ? 60 : 45, initial.TimeLimitMinutes);
            Assert.Equal(isShortened, initial.IsShortenedAssessment);
            if (isShortened)
            {
                var skillsInAttempt = initial.Questions.GroupBy(question => question.SectionTitle).ToList();
                Assert.Equal(8, skillsInAttempt.Count);
                Assert.All(skillsInAttempt, skill => Assert.Equal(expected / 8, skill.Count()));
                var resumed = await service.GetActiveAttemptAsync();
                Assert.True(resumed.Result!.IsShortenedAssessment);
                Assert.Equal(initial.TimeLimitMinutes, resumed.Result.TimeLimitMinutes);
            }
            Assert.Equal(testedLevels, initial.Questions.Select(question => question.JlptLevelName).Distinct());
            var sections = initial.Questions.GroupBy(question => question.SectionTitle).ToList();
            Assert.Equal(testedLevels.Length * 4, sections.Count);
            Assert.All(sections, section => Assert.Equal(expected / sections.Count, section.Count()));
            var submitted = await service.SubmitPlacementTestAsync(initial.AttemptId,
                new SubmitPlacementTestRequest
                {
                    Answers = initial.Questions.Select((question, index) => new AnswerSubmissionItem
                    {
                        QuestionId = question.QuestionId,
                        SelectedOptionId = question.QuestionId * 10 + (index < correct.Value ? 0 : 1)
                    }).ToList()
                });
            Assert.True(submitted.IsSuccess, submitted.ErrorMessage);
            Assert.Equal(isShortened, submitted.Result!.IsShortenedAssessment);
            if (goal == "N5" && correct == 0)
                Assert.Contains("N5 từ nền tảng", submitted.Result.AssessmentMessage);
            Assert.False(submitted.Result.RequiresMiniAssessment);
            Assert.Equal(goal, submitted.Result.ScreeningLevelName);
            var canRetake = correct.Value * 2 < expected;
            Assert.Equal(canRetake ? ["RETAKE_TARGET_ASSESSMENT"] : Array.Empty<string>(), submitted.Result.NextActions);
            Assert.Equal(goal, submitted.Result.RecommendedTargetJlptLevelName);
            Assert.Equal(submitted.Result.NextActions,
                (await service.GetPlacementTestResultAsync(initial.AttemptId)).Result!.NextActions);
            var status = await service.GetStatusAsync();
            Assert.False(status.Result!.RequiresMiniAssessment);
            Assert.True(status.Result.IsReadyForLearningPath);
            if (!canRetake)
            {
                if (goal == "N2" && declared is null)
                {
                    var openN2 = await service.StartPlacementTestAsync(default);
                    Assert.True(openN2.IsSuccess, openN2.ErrorMessage);
                    var learner = await context.Users.SingleAsync(user => user.Id == 7);
                    learner.TargetCertificateLevelId = 71003;
                    await context.SaveChangesAsync();
                    Assert.False((await service.GetStatusAsync()).Result!.HasActiveAttempt);
                    Assert.Null((await service.GetActiveAttemptAsync()).Result);
                    var newN3 = await service.StartPlacementTestAsync(default);
                    Assert.True(newN3.IsSuccess, newN3.ErrorMessage);
                    Assert.NotEqual(openN2.Result!.AttemptId, newN3.Result!.AttemptId);
                    Assert.Equal(["N4", "N3"], newN3.Result.Questions.Select(question => question.JlptLevelName).Distinct());
                    var oldQuestionId = openN2.Result.Questions[0].QuestionId;
                    var saved = await service.SavePlacementAnswersAsync(openN2.Result.AttemptId, new SubmitPlacementTestRequest
                    {
                        Answers = [new() { QuestionId = oldQuestionId, SelectedOptionId = oldQuestionId * 10 }]
                    });
                    Assert.True(saved.IsSuccess, saved.ErrorMessage);
                    var stillInTime = await service.SubmitPlacementTestAsync(openN2.Result.AttemptId,
                        new SubmitPlacementTestRequest());
                    Assert.True(stillInTime.IsSuccess, stillInTime.ErrorMessage);
                    Assert.Equal(openN2.Result.AttemptId, stillInTime.Result!.AttemptId);
                    Assert.Empty(stillInTime.Result.NextActions);
                    Assert.Equal("CURRENT_TARGET_MISMATCH", stillInTime.Result.FollowUpUnavailableReason);
                    Assert.Equal(1, stillInTime.Result.CorrectCount);
                    Assert.False((await service.GetStatusAsync()).Result!.RequiresMiniAssessment);
                    Assert.Equal(newN3.Result.AttemptId, (await service.GetActiveAttemptAsync()).Result!.AttemptId);
                }
                return;
            }

            if (goal == "N2" && correct == 0)
            {
                var oldAttempt = await context.MockTestAttempts.SingleAsync(attempt => attempt.Id == initial.AttemptId);
                var oldSnapshot = JsonNode.Parse(oldAttempt.QuestionSnapshotJson!)!.AsObject();
                var oldResult = oldSnapshot["completedResult"]!.AsObject();
                oldResult["miniAssessmentLevels"] = new JsonArray(JsonValue.Create("N5"));
                oldResult["recommendedTargetJlptLevelId"] = 0;
                oldResult["recommendedTargetJlptLevelName"] = string.Empty;
                oldAttempt.QuestionSnapshotJson = oldSnapshot.ToJsonString();

                var miniSnapshot = oldSnapshot.DeepClone();
                miniSnapshot["parentAssessmentAttemptId"] = initial.AttemptId;
                miniSnapshot["completedResult"] = null;
                var oldMini = new MockTestAttempt
                {
                    UserId = 7, AttemptKind = AttemptKind.Placement, StartedAt = oldAttempt.StartedAt,
                    ExpiresAt = oldAttempt.ExpiresAt, QuestionSnapshotJson = miniSnapshot.ToJsonString(),
                };
                context.MockTestAttempts.Add(oldMini);
                await context.SaveChangesAsync();

                var recovered = await service.GetPlacementTestResultAsync(initial.AttemptId);
                Assert.True(recovered.IsSuccess, recovered.ErrorMessage);
                Assert.False(recovered.Result!.RequiresMiniAssessment);
                Assert.Equal("N2", recovered.Result.RecommendedTargetJlptLevelName);
                Assert.True((await service.GetStatusAsync()).Result!.IsReadyForLearningPath);
                Assert.Null((await service.GetActiveAttemptAsync()).Result);

                var request = new StartPlacementTestRequest { RetakeTargetAssessment = true };
                var retake = await service.StartPlacementTestAsync(request);
                Assert.True(retake.IsSuccess, retake.ErrorMessage);
                var retry = retake.Result!;
                Assert.NotEqual(initial.AttemptId, retry.AttemptId);
                Assert.False(retry.IsMiniAssessment);
                Assert.Equal(expected, retry.TotalQuestions);
                Assert.Equal(["N3", "N2"], retry.Questions.Select(question => question.JlptLevelName).Distinct());
                Assert.Empty(retry.Questions.Where(question => question.JlptLevelName == goal).Select(question => question.QuestionId)
                    .Intersect(initial.Questions.Select(question => question.QuestionId)));
                Assert.Equal(retry.AttemptId, (await service.StartPlacementTestAsync(request)).Result!.AttemptId);
                var replaced = await service.SubmitPlacementTestAsync(oldMini.Id, new SubmitPlacementTestRequest());
                Assert.Equal("PLACEMENT_ATTEMPT_REPLACED", replaced.ErrorCode);
                var retryResult = await service.SubmitPlacementTestAsync(retry.AttemptId, new SubmitPlacementTestRequest
                {
                    Answers = retry.Questions.Take(expected / 2).Select(question => new AnswerSubmissionItem
                    {
                        QuestionId = question.QuestionId,
                        SelectedOptionId = question.QuestionId * 10
                    }).ToList()
                });
                Assert.True(retryResult.IsSuccess, retryResult.ErrorMessage);
                Assert.Empty((await service.GetPlacementTestResultAsync(initial.AttemptId)).Result!.NextActions);
                Assert.Equal(retry.AttemptId, (await service.GetStatusAsync()).Result!.LatestCompletedAttemptId);
            }
            return;
        }
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(expected, result.Result!.TotalQuestions);
        Assert.Contains(result.Result.Questions, q => q.JlptLevelName == "N2");
        using var snapshot = JsonDocument.Parse((await context.MockTestAttempts.SingleAsync()).QuestionSnapshotJson!);
        var chosen = snapshot.RootElement.GetProperty("questions").EnumerateArray().ToList();
        Assert.DoesNotContain(chosen, q => q.GetProperty("languageSkillName").GetString() == "Kanji");
        Assert.Contains(chosen, q => q.GetProperty("languageSkillName").GetString() == "Listening");
        foreach (var level in new[] { "N5", "N4", "N3", "N2" })
            Assert.Equal(12, chosen.Count(q => q.GetProperty("jlptLevelName").GetString() == level));
    }
}
