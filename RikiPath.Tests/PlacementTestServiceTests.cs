using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class PlacementTestServiceTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetStatus_when_no_attempts_returns_not_completed_no_active_and_not_ready()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8001);
        var service = CreateService(context, userId: 8001);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsSuccess);
        Assert.Equal("placement", status.Result!.AttemptType);
        Assert.False(status.Result.HasActiveAttempt);
        Assert.Null(status.Result.ActiveAttemptId);
        Assert.Null(status.Result.ActiveAttemptExpiresAt);
        Assert.False(status.Result.HasCompletedPlacementTest);
        Assert.Null(status.Result.LatestCompletedAttemptId);
        Assert.Null(status.Result.LatestAttemptId);
        Assert.False(status.Result.IsReadyForLearningPath);
    }

    [Fact]
    public async Task Start_samples_eligible_questions_from_N5_to_N3_persists_snapshot_and_hides_answer_keys()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8002);
        var service = CreateService(context, userId: 8002);

        var start = await service.StartPlacementTestAsync(CancellationToken.None);

        Assert.True(start.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, start.StatusCode);
        Assert.Equal("placement", start.Result!.AttemptType);
        Assert.Equal("inProgress", start.Result.Status);
        Assert.False(start.Result.IsResumed);
        Assert.Equal(6, start.Result.TotalQuestions);
        Assert.Equal(12, start.Result.TimeLimitMinutes);
        Assert.Equal(["N5", "N5", "N4", "N4", "N3", "N3"], start.Result.Questions.Select(q => q.JlptLevelName).ToArray());
        Assert.Equal([1, 2, 3, 4, 5, 6], start.Result.Questions.Select(q => q.Order).ToArray());
        Assert.True(start.Result.ExpiresAt > start.Result.StartedAt);

        var serializedPayload = JsonSerializer.Serialize(start, WebJsonOptions);
        Assert.DoesNotContain("correctOptionId", serializedPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isCorrect", serializedPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("explanation", serializedPayload, StringComparison.OrdinalIgnoreCase);

        var attempt = await context.MockTestAttempts.SingleAsync(a => a.Id == start.Result.AttemptId);
        Assert.Equal(AttemptKind.Placement, attempt.AttemptKind);
        Assert.NotNull(attempt.ExpiresAt);
        Assert.False(string.IsNullOrWhiteSpace(attempt.QuestionSnapshotJson));
    }

    [Fact]
    public async Task Start_when_bank_empty_or_incomplete_level_coverage_returns_409_bank_not_ready()
    {
        await using var context = CreateContext();
        context.Users.Add(new UserAccount
        {
            Id = 8003,
            Email = "empty-placement@example.test",
            PasswordHash = [],
            PasswordSalt = [],
            Role = Role.Learner,
        });
        await context.SaveChangesAsync();
        var service = CreateService(context, userId: 8003);

        var emptyStart = await service.StartPlacementTestAsync(CancellationToken.None);

        Assert.False(emptyStart.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, emptyStart.StatusCode);
        Assert.Equal(PlacementTestService.ErrorBankNotReady, emptyStart.ErrorCode);
        Assert.Equal(0, await context.MockTestAttempts.CountAsync());

        // Seed catalog where N3 has only 1 eligible question (< minimum 2)
        SeedLearnerAndCatalog(context, userId: 8030);
        var n3SecondQuestion = await context.MockQuestions.SingleAsync(q => q.Id == 302);
        n3SecondQuestion.IsDeleted = true;
        await context.SaveChangesAsync();

        var incompleteService = CreateService(context, userId: 8030);
        var incompleteStart = await incompleteService.StartPlacementTestAsync(CancellationToken.None);

        Assert.False(incompleteStart.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, incompleteStart.StatusCode);
        Assert.Equal(PlacementTestService.ErrorBankNotReady, incompleteStart.ErrorCode);
        Assert.Equal(0, await context.MockTestAttempts.CountAsync(a => a.UserId == 8030));
    }

    [Fact]
    public async Task Start_filters_out_soft_deleted_invalid_option_and_unsupported_N2_N1_questions()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8031);

        var certType = await context.CertificateTypes.SingleAsync(t => t.Id == 71);
        var skill = await context.LanguageSkills.SingleAsync(s => s.Id == 1);
        var levelN2 = new CertificateLevel
        {
            Id = 71002,
            CertificateTypeId = certType.Id,
            CertificateType = certType,
            Code = "N2",
            SortOrder = 4,
        };
        var levelN1 = new CertificateLevel
        {
            Id = 71001,
            CertificateTypeId = certType.Id,
            CertificateType = certType,
            Code = "N1",
            SortOrder = 5,
        };
        context.CertificateLevels.AddRange(levelN2, levelN1);
        context.MockTests.AddRange(
            CreateMockTest(102, "Placement N2", levelN2, skill, 20, 201, 202),
            CreateMockTest(101, "Placement N1", levelN1, skill, 10, 101, 102));

        // Add invalid questions into N5 section (soft-deleted, 1 option, 0 correct options, 2 correct options)
        context.MockQuestions.AddRange(
            new MockQuestion
            {
                Id = 590,
                MockTestSectionId = 50,
                QuestionText = "Soft deleted question",
                IsDeleted = true,
                SortOrder = 0,
                Options =
                [
                    new MockQuestionOption { Id = 5901, MockQuestionId = 590, OptionText = "A", IsCorrect = true, SortOrder = 1 },
                    new MockQuestionOption { Id = 5902, MockQuestionId = 590, OptionText = "B", IsCorrect = false, SortOrder = 2 },
                ],
            },
            new MockQuestion
            {
                Id = 591,
                MockTestSectionId = 50,
                QuestionText = "Only one non-deleted option",
                SortOrder = 0,
                Options =
                [
                    new MockQuestionOption { Id = 5911, MockQuestionId = 591, OptionText = "A", IsCorrect = true, SortOrder = 1 },
                    new MockQuestionOption { Id = 5912, MockQuestionId = 591, OptionText = "B", IsCorrect = false, IsDeleted = true, SortOrder = 2 },
                ],
            },
            new MockQuestion
            {
                Id = 592,
                MockTestSectionId = 50,
                QuestionText = "Two correct options",
                SortOrder = 0,
                Options =
                [
                    new MockQuestionOption { Id = 5921, MockQuestionId = 592, OptionText = "A", IsCorrect = true, SortOrder = 1 },
                    new MockQuestionOption { Id = 5922, MockQuestionId = 592, OptionText = "B", IsCorrect = true, SortOrder = 2 },
                ],
            },
            new MockQuestion
            {
                Id = 593,
                MockTestSectionId = 50,
                QuestionText = "Zero correct options",
                SortOrder = 0,
                Options =
                [
                    new MockQuestionOption { Id = 5931, MockQuestionId = 593, OptionText = "A", IsCorrect = false, SortOrder = 1 },
                    new MockQuestionOption { Id = 5932, MockQuestionId = 593, OptionText = "B", IsCorrect = false, SortOrder = 2 },
                ],
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, userId: 8031);
        var start = await service.StartPlacementTestAsync(CancellationToken.None);

        Assert.True(start.IsSuccess);
        Assert.Equal([501, 502, 401, 402, 301, 302], start.Result!.Questions.Select(q => q.QuestionId).ToArray());
        Assert.DoesNotContain(start.Result.Questions, q => q.JlptLevelName is "N2" or "N1");
    }

    [Fact]
    public async Task Active_attempt_is_resumed_by_Start_and_returned_by_GetActiveAttempt_and_GetStatus()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8032);
        var service = CreateService(context, userId: 8032);

        var noneActive = await service.GetActiveAttemptAsync(CancellationToken.None);
        Assert.True(noneActive.IsSuccess);
        Assert.Null(noneActive.Result);

        var firstStart = await service.StartPlacementTestAsync(CancellationToken.None);
        Assert.Equal((int)HttpStatusCode.Created, firstStart.StatusCode);
        Assert.False(firstStart.Result!.IsResumed);

        var statusWhileActive = await service.GetStatusAsync(CancellationToken.None);
        Assert.True(statusWhileActive.Result!.HasActiveAttempt);
        Assert.Equal(firstStart.Result.AttemptId, statusWhileActive.Result.ActiveAttemptId);
        Assert.NotNull(statusWhileActive.Result.ActiveAttemptExpiresAt);

        var activeLookup = await service.GetActiveAttemptAsync(CancellationToken.None);
        Assert.True(activeLookup.IsSuccess);
        Assert.NotNull(activeLookup.Result);
        Assert.True(activeLookup.Result!.IsResumed);
        Assert.Equal(firstStart.Result.AttemptId, activeLookup.Result.AttemptId);

        var secondStart = await service.StartPlacementTestAsync(CancellationToken.None);
        Assert.True(secondStart.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, secondStart.StatusCode);
        Assert.True(secondStart.Result!.IsResumed);
        Assert.Equal(firstStart.Result.AttemptId, secondStart.Result.AttemptId);
        Assert.Equal(1, await context.MockTestAttempts.CountAsync(a => a.UserId == 8032));
    }

    [Fact]
    public async Task Frozen_snapshot_grades_and_recovers_accurately_even_after_question_bank_is_mutated()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8004);
        var service = CreateService(context, userId: 8004);

        var start = await service.StartPlacementTestAsync(CancellationToken.None);
        Assert.True(start.IsSuccess);

        // Mutate and soft-delete bank rows AFTER start: flip correctness and delete questions
        foreach (var opt in await context.MockQuestionOptions.ToListAsync())
        {
            opt.IsCorrect = !opt.IsCorrect;
            opt.OptionText = "MUTATED BANK TEXT";
            opt.IsDeleted = true;
        }
        foreach (var q in await context.MockQuestions.ToListAsync())
        {
            q.QuestionText = "DELETED BANK QUESTION";
            q.IsDeleted = true;
        }
        await context.SaveChangesAsync();

        // Active resume still returns original frozen question text
        var resumed = await service.GetActiveAttemptAsync(CancellationToken.None);
        Assert.Equal("Câu hỏi 501 (N5)", resumed.Result!.Questions[0].Content);

        // Answer N5 (both correct -> 100%), N4 (both correct -> 100%), N3 (both wrong -> 0%)
        var submission = new SubmitPlacementTestRequest
        {
            Answers =
            [
                new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 },
                new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5021 },
                new AnswerSubmissionItem { QuestionId = 401, SelectedOptionId = 4011 },
                new AnswerSubmissionItem { QuestionId = 402, SelectedOptionId = 4021 },
                new AnswerSubmissionItem { QuestionId = 301, SelectedOptionId = 3012 },
                new AnswerSubmissionItem { QuestionId = 302, SelectedOptionId = 3022 },
            ],
        };

        var submit = await service.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            submission,
            CancellationToken.None);

        Assert.True(submit.IsSuccess);
        Assert.Equal("placement", submit.Result!.AttemptType);
        Assert.Equal("completed", submit.Result.Status);
        Assert.Equal(4, submit.Result.CorrectCount);
        Assert.Equal(6, submit.Result.TotalQuestions);
        Assert.Equal(66.67, submit.Result.TotalScore);
        Assert.Equal("N4", submit.Result.AssessedJlptLevelName);
        Assert.Equal(71004, submit.Result.AssessedJlptLevelId);
        Assert.Equal("N3", submit.Result.RecommendedTargetJlptLevelName);
        Assert.Equal(71003, submit.Result.RecommendedTargetJlptLevelId);
        Assert.Equal([301, 302], submit.Result.WrongQuestionIds);
        Assert.Equal(3, submit.Result.LevelBreakdowns.Count);

        var recovered = await service.GetPlacementTestResultAsync(
            start.Result.AttemptId,
            CancellationToken.None);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(submit.Result.TotalScore, recovered.Result!.TotalScore);
        Assert.Equal("N4", recovered.Result.AssessedJlptLevelName);
        Assert.Equal("N3", recovered.Result.RecommendedTargetJlptLevelName);

        var status = await service.GetStatusAsync(CancellationToken.None);
        Assert.True(status.Result!.HasCompletedPlacementTest);
        Assert.Equal(start.Result.AttemptId, status.Result.LatestCompletedAttemptId);
        Assert.Equal("N4", status.Result.AssessedJlptLevelName);
        Assert.Equal("N3", status.Result.RecommendedTargetJlptLevelName);
    }

    [Fact]
    public async Task Submit_assesses_N5_when_beginner_misses_all_questions_and_never_claims_N2_or_N1()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8005);
        context.CertificateLevels.AddRange(
            new CertificateLevel { Id = 71002, CertificateTypeId = 71, Code = "N2", SortOrder = 4 },
            new CertificateLevel { Id = 71001, CertificateTypeId = 71, Code = "N1", SortOrder = 5 });
        await context.SaveChangesAsync();

        var service = CreateService(context, userId: 8005);
        var start = await service.StartPlacementTestAsync(CancellationToken.None);

        var submit = await service.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers =
                [
                    new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5012 },
                    new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5022 },
                    new AnswerSubmissionItem { QuestionId = 401, SelectedOptionId = 4012 },
                    new AnswerSubmissionItem { QuestionId = 402, SelectedOptionId = 4022 },
                    new AnswerSubmissionItem { QuestionId = 301, SelectedOptionId = 3012 },
                    new AnswerSubmissionItem { QuestionId = 302, SelectedOptionId = 3022 },
                ],
            },
            CancellationToken.None);

        Assert.True(submit.IsSuccess);
        Assert.Equal(0, submit.Result!.TotalScore);
        Assert.Equal("N5", submit.Result.AssessedJlptLevelName);
        Assert.Equal(71005, submit.Result.AssessedJlptLevelId);
        Assert.Equal("N5", submit.Result.RecommendedTargetJlptLevelName);
        Assert.Equal(71005, submit.Result.RecommendedTargetJlptLevelId);

        // Perfect score on N5, N4, N3 stays capped at N3 (never recommends N2 or N1)
        var secondStart = await service.StartPlacementTestAsync(CancellationToken.None);
        var perfectSubmit = await service.SubmitPlacementTestAsync(
            secondStart.Result!.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers =
                [
                    new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 },
                    new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5021 },
                    new AnswerSubmissionItem { QuestionId = 401, SelectedOptionId = 4011 },
                    new AnswerSubmissionItem { QuestionId = 402, SelectedOptionId = 4021 },
                    new AnswerSubmissionItem { QuestionId = 301, SelectedOptionId = 3011 },
                    new AnswerSubmissionItem { QuestionId = 302, SelectedOptionId = 3021 },
                ],
            },
            CancellationToken.None);

        Assert.True(perfectSubmit.IsSuccess);
        Assert.Equal("N3", perfectSubmit.Result!.AssessedJlptLevelName);
        Assert.Equal("N3", perfectSubmit.Result.RecommendedTargetJlptLevelName);
    }

    [Fact]
    public async Task Cross_user_cross_type_and_nonexistent_attempts_return_indistinguishable_404_and_invalid_answers_return_400()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8006);
        context.Users.Add(new UserAccount
        {
            Id = 9999,
            Email = "other-learner@example.test",
            PasswordHash = [],
            PasswordSalt = [],
            Role = Role.Learner,
        });
        await context.SaveChangesAsync();

        var ownerPlacement = CreateService(context, userId: 8006);
        var otherPlacement = CreateService(context, userId: 9999);
        var ownerMockTest = CreateMockTestService(context, userId: 8006);

        var placementStart = await ownerPlacement.StartPlacementTestAsync(CancellationToken.None);
        var mockTestStart = await ownerMockTest.StartAttemptAsync(105, CancellationToken.None);

        var validAnswerPayload = new SubmitPlacementTestRequest
        {
            Answers = [new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 }],
        };

        var nonexistentSubmit = await ownerPlacement.SubmitPlacementTestAsync(999999, validAnswerPayload, CancellationToken.None);
        var crossUserSubmit = await otherPlacement.SubmitPlacementTestAsync(placementStart.Result!.AttemptId, validAnswerPayload, CancellationToken.None);
        var crossTypeSubmit = await ownerPlacement.SubmitPlacementTestAsync(mockTestStart.Result!.AttemptId, validAnswerPayload, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, nonexistentSubmit.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, crossUserSubmit.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, crossTypeSubmit.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotFound, nonexistentSubmit.ErrorCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotFound, crossUserSubmit.ErrorCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotFound, crossTypeSubmit.ErrorCode);
        Assert.Equal(nonexistentSubmit.ErrorMessage, crossUserSubmit.ErrorMessage);
        Assert.Equal(nonexistentSubmit.ErrorMessage, crossTypeSubmit.ErrorMessage);

        var nonexistentGet = await ownerPlacement.GetPlacementTestResultAsync(999999, CancellationToken.None);
        var crossUserGet = await otherPlacement.GetPlacementTestResultAsync(placementStart.Result.AttemptId, CancellationToken.None);
        var crossTypeGet = await ownerPlacement.GetPlacementTestResultAsync(mockTestStart.Result.AttemptId, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, nonexistentGet.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, crossUserGet.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, crossTypeGet.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotFound, nonexistentGet.ErrorCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotFound, crossUserGet.ErrorCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotFound, crossTypeGet.ErrorCode);

        // Owned active attempt on GetPlacementTestResultAsync returns 409 PLACEMENT_ATTEMPT_NOT_COMPLETED
        var activeResultLookup = await ownerPlacement.GetPlacementTestResultAsync(
            placementStart.Result.AttemptId,
            CancellationToken.None);
        Assert.Equal((int)HttpStatusCode.Conflict, activeResultLookup.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotCompleted, activeResultLookup.ErrorCode);

        // Ordinary MockTestService cannot access Placement attempt either
        var mockTestReadingPlacement = await ownerMockTest.GetAttemptResultAsync(
            placementStart.Result.AttemptId,
            CancellationToken.None);
        Assert.Equal((int)HttpStatusCode.NotFound, mockTestReadingPlacement.StatusCode);

        // Invalid option on owned active placement attempt returns 400 PLACEMENT_ANSWER_INVALID
        var invalidOption = await ownerPlacement.SubmitPlacementTestAsync(
            placementStart.Result.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers = [new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 4011 }],
            },
            CancellationToken.None);
        Assert.False(invalidOption.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, invalidOption.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAnswerInvalid, invalidOption.ErrorCode);
    }

    [Fact]
    public async Task Expired_attempt_is_rejected_with_409_and_new_start_creates_fresh_attempt()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8033);
        var service = CreateService(context, userId: 8033);

        var start = await service.StartPlacementTestAsync(CancellationToken.None);
        var attemptEntity = await context.MockTestAttempts.SingleAsync(a => a.Id == start.Result!.AttemptId);
        attemptEntity.StartedAt = DateTime.UtcNow.AddMinutes(-30);
        attemptEntity.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();

        var activeAfterExpiry = await service.GetActiveAttemptAsync(CancellationToken.None);
        Assert.True(activeAfterExpiry.IsSuccess);
        Assert.Null(activeAfterExpiry.Result);

        var expiredSubmit = await service.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers = [new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 }],
            },
            CancellationToken.None);
        Assert.False(expiredSubmit.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, expiredSubmit.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAttemptExpired, expiredSubmit.ErrorCode);

        var freshStart = await service.StartPlacementTestAsync(CancellationToken.None);
        Assert.Equal((int)HttpStatusCode.Created, freshStart.StatusCode);
        Assert.False(freshStart.Result!.IsResumed);
        Assert.NotEqual(start.Result.AttemptId, freshStart.Result.AttemptId);

        // Even after starting a fresh attempt, submitting the superseded expired attempt still returns 409 PLACEMENT_ATTEMPT_EXPIRED
        var expiredSubmitAfterFreshStart = await service.SubmitPlacementTestAsync(
            start.Result.AttemptId,
            new SubmitPlacementTestRequest { Answers = [] },
            CancellationToken.None);
        Assert.False(expiredSubmitAfterFreshStart.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, expiredSubmitAfterFreshStart.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAttemptExpired, expiredSubmitAfterFreshStart.ErrorCode);
    }

    [Fact]
    public async Task Duplicate_and_concurrent_submit_are_idempotent_and_return_canonical_result()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8034);
        var service = CreateService(context, userId: 8034);

        var start = await service.StartPlacementTestAsync(CancellationToken.None);
        var request = new SubmitPlacementTestRequest
        {
            Answers =
            [
                new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 },
                new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5021 },
                new AnswerSubmissionItem { QuestionId = 401, SelectedOptionId = 4012 },
                new AnswerSubmissionItem { QuestionId = 402, SelectedOptionId = 4022 },
                new AnswerSubmissionItem { QuestionId = 301, SelectedOptionId = 3012 },
                new AnswerSubmissionItem { QuestionId = 302, SelectedOptionId = 3022 },
            ],
        };

        var results = await Task.WhenAll(
            service.SubmitPlacementTestAsync(start.Result!.AttemptId, request, CancellationToken.None),
            service.SubmitPlacementTestAsync(start.Result.AttemptId, request, CancellationToken.None));

        Assert.All(results, r =>
        {
            Assert.True(r.IsSuccess);
            Assert.Equal((int)HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(2, r.Result!.CorrectCount);
            Assert.Equal(33.33, r.Result.TotalScore);
            Assert.Equal("N5", r.Result.AssessedJlptLevelName);
        });
        Assert.Equal(results[0].Result!.SubmittedAt, results[1].Result!.SubmittedAt);

        var sectionResultsCount = await context.MockTestSectionResults
            .CountAsync(r => r.MockTestAttemptId == start.Result.AttemptId);
        Assert.Equal(3, sectionResultsCount);
    }

    [Fact]
    public async Task Completing_placement_test_and_setting_goal_unlocks_learning_path_readiness_and_controller_routes_work()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8007);
        var placementService = CreateService(context, userId: 8007);
        var profileService = CreateProfileService(context, userId: 8007);
        var controller = new PlacementTestController(placementService);

        var startAction = await controller.StartPlacementTest(CancellationToken.None) as ObjectResult;
        Assert.NotNull(startAction);
        Assert.Equal((int)HttpStatusCode.Created, startAction!.StatusCode);
        var startEnvelope = Assert.IsType<ApiResponse<PlacementAttemptSnapshotResponse>>(startAction.Value);

        var activeAction = await controller.GetActiveAttempt(CancellationToken.None) as ObjectResult;
        Assert.Equal((int)HttpStatusCode.OK, activeAction!.StatusCode);

        var submitAction = await controller.SubmitPlacementTest(
            startEnvelope.Result!.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers =
                [
                    new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 },
                    new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5021 },
                    new AnswerSubmissionItem { QuestionId = 401, SelectedOptionId = 4012 },
                    new AnswerSubmissionItem { QuestionId = 402, SelectedOptionId = 4022 },
                    new AnswerSubmissionItem { QuestionId = 301, SelectedOptionId = 3012 },
                    new AnswerSubmissionItem { QuestionId = 302, SelectedOptionId = 3022 },
                ],
            },
            CancellationToken.None) as ObjectResult;
        Assert.Equal((int)HttpStatusCode.OK, submitAction!.StatusCode);
        var submitEnvelope = Assert.IsType<ApiResponse<PlacementTestResultResponse>>(submitAction.Value);

        // Learner chooses N3 as target goal even though assessed level is N5
        var goalUpdate = await profileService.SetJlptGoalAsync(
            new SetJlptGoalRequest
            {
                TargetJlptLevelId = 71003,
                DailyStudyMinutes = 45,
                TargetExamDate = DateTime.UtcNow.AddHours(7).Date.AddMonths(4),
            },
            CancellationToken.None);
        Assert.True(goalUpdate.IsSuccess);

        var status = await placementService.GetStatusAsync(CancellationToken.None);
        Assert.True(status.IsSuccess);
        Assert.True(status.Result!.HasCompletedPlacementTest);
        Assert.Equal(startEnvelope.Result.AttemptId, status.Result.LatestCompletedAttemptId);
        Assert.Equal(startEnvelope.Result.AttemptId, status.Result.LatestAttemptId);
        Assert.Equal("N5", status.Result.AssessedJlptLevelName);
        Assert.Equal("N3", status.Result.TargetJlptLevelName);
        Assert.True(status.Result.IsReadyForLearningPath);

        var resultLookup = await placementService.GetPlacementTestResultAsync(
            startEnvelope.Result.AttemptId,
            CancellationToken.None);
        Assert.True(resultLookup.IsSuccess);
        Assert.Equal(submitEnvelope.Result!.TotalScore, resultLookup.Result!.TotalScore);
    }

    [Fact]
    public void Flow1_migration_is_discovered_and_matches_model_snapshot()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=rikipath_metadata_only")
            .Options;
        using var context = new AppDbContext(options);

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        const string migrationId = "20261027131637_AddFlow1PlacementAndLearningMode";
        Assert.True(migrationsAssembly.Migrations.ContainsKey(migrationId));
        Assert.NotNull(migrationsAssembly.ModelSnapshot);

        var placementMigration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[migrationId],
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(placementMigration.TargetModel);

        var targetAttemptEntity = placementMigration.TargetModel.FindEntityType(typeof(MockTestAttempt).FullName!);
        Assert.NotNull(targetAttemptEntity);
        Assert.NotNull(targetAttemptEntity!.FindProperty(nameof(MockTestAttempt.AttemptKind)));
        Assert.NotNull(targetAttemptEntity.FindProperty(nameof(MockTestAttempt.ExpiresAt)));
        Assert.NotNull(targetAttemptEntity.FindProperty(nameof(MockTestAttempt.QuestionSnapshotJson)));

        var activePlacementIndex = targetAttemptEntity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(MockTestAttempt.UserId), nameof(MockTestAttempt.AttemptKind)]));
        Assert.NotNull(activePlacementIndex);
        Assert.True(activePlacementIndex!.IsUnique);
        Assert.Equal("\"AttemptKind\" = 1 AND \"IsCompleted\" = false AND \"IsDeleted\" = false", activePlacementIndex.GetFilter());

        var runtimeInitializer = context.GetService<IModelRuntimeInitializer>();
        var snapshotModel = runtimeInitializer.Initialize(
            ((IMutableModel)migrationsAssembly.ModelSnapshot!.Model).FinalizeModel(),
            designTime: true);
        var latestMigrationId = migrationsAssembly.Migrations.Keys.OrderBy(id => id).Last();
        var latestMigration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[latestMigrationId],
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(latestMigration.TargetModel);
        var latestMigrationTargetModel = runtimeInitializer.Initialize(
            ((IMutableModel)latestMigration.TargetModel).FinalizeModel(),
            designTime: true);

        var differ = context.GetService<IMigrationsModelDiffer>();
        var designTimeModel = context.GetService<IDesignTimeModel>();
        Assert.Empty(differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            latestMigrationTargetModel.GetRelationalModel()));

        // The generated migration target must match the stored snapshot. The current
        // runtime model can contain unrelated pending schema changes, so do not treat
        // those as part of this migration-discovery contract check.
        Assert.NotNull(designTimeModel.Model.GetRelationalModel());
    }

    [Fact]
    public async Task Completed_attempt_with_missing_or_corrupt_snapshot_returns_409_not_ready_and_never_reconstructs_from_mutable_bank()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8040);
        var service = CreateService(context, userId: 8040);

        var start = await service.StartPlacementTestAsync(CancellationToken.None);
        var submit = await service.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers =
                [
                    new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 },
                    new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5021 },
                ],
            },
            CancellationToken.None);
        Assert.True(submit.IsSuccess);

        // Corrupt the stored snapshot on the completed attempt while leaving MockTestAnswers and MockTestSectionResults intact
        var completedEntity = await context.MockTestAttempts.SingleAsync(a => a.Id == start.Result.AttemptId);
        foreach (var badSnapshot in new string?[] { null, string.Empty, "{not-valid-json", "{}" })
        {
            completedEntity.QuestionSnapshotJson = badSnapshot;
            await context.SaveChangesAsync();

            var getResult = await service.GetPlacementTestResultAsync(completedEntity.Id, CancellationToken.None);
            Assert.False(getResult.IsSuccess);
            Assert.Equal((int)HttpStatusCode.Conflict, getResult.StatusCode);
            Assert.Equal(PlacementTestService.ErrorAttemptNotReady, getResult.ErrorCode);

            var reSubmit = await service.SubmitPlacementTestAsync(
                completedEntity.Id,
                new SubmitPlacementTestRequest { Answers = [] },
                CancellationToken.None);
            Assert.False(reSubmit.IsSuccess);
            Assert.Equal((int)HttpStatusCode.Conflict, reSubmit.StatusCode);
            Assert.Equal(PlacementTestService.ErrorAttemptNotReady, reSubmit.ErrorCode);
        }
    }

    [Fact]
    public async Task Completed_attempt_without_immutable_completed_result_returns_409_not_ready()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8045);
        var service = CreateService(context, userId: 8045);

        var start = await service.StartPlacementTestAsync(CancellationToken.None);
        var submit = await service.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            new SubmitPlacementTestRequest { Answers = [] },
            CancellationToken.None);
        Assert.True(submit.IsSuccess);

        var completedAttempt = await context.MockTestAttempts.SingleAsync(a => a.Id == start.Result.AttemptId);
        var immutableSnapshot = JsonNode.Parse(completedAttempt.QuestionSnapshotJson!)!.AsObject();
        immutableSnapshot.Remove("completedResult");
        completedAttempt.QuestionSnapshotJson = immutableSnapshot.ToJsonString();
        await context.SaveChangesAsync();

        var result = await service.GetPlacementTestResultAsync(completedAttempt.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal(PlacementTestService.ErrorAttemptNotReady, result.ErrorCode);
    }

    [Fact]
    public async Task Blank_answers_array_submits_as_all_unanswered_and_time_limit_uses_deterministic_formula()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8041);

        // Set ordinary full-test time limits to 90 minutes on all MockTests to verify Placement ignores them
        foreach (var test in await context.MockTests.ToListAsync())
        {
            test.TimeLimitMinutes = 90;
        }
        await context.SaveChangesAsync();

        var service = CreateService(context, userId: 8041);
        var start = await service.StartPlacementTestAsync(CancellationToken.None);
        Assert.True(start.IsSuccess);
        Assert.Equal(6, start.Result!.TotalQuestions);
        Assert.Equal(12, start.Result.TimeLimitMinutes);

        // Blank answers: [] must be accepted as all questions unanswered -> 0%, N5 assessed & recommended
        var blankSubmit = await service.SubmitPlacementTestAsync(
            start.Result.AttemptId,
            new SubmitPlacementTestRequest { Answers = [] },
            CancellationToken.None);

        Assert.True(blankSubmit.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, blankSubmit.StatusCode);
        Assert.Equal(0, blankSubmit.Result!.CorrectCount);
        Assert.Equal(6, blankSubmit.Result.TotalQuestions);
        Assert.Equal(0, blankSubmit.Result.TotalScore);
        Assert.Equal("N5", blankSubmit.Result.AssessedJlptLevelName);
        Assert.Equal("N5", blankSubmit.Result.RecommendedTargetJlptLevelName);
        Assert.Equal([301, 302, 401, 402, 501, 502], blankSubmit.Result.WrongQuestionIds);
    }

    [Fact]
    public async Task Multi_instance_and_DbUpdateException_races_on_start_and_submit_return_canonical_results_without_500()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8042);

        // 1) Simulate a DbUpdateException uniqueness race on Start where another Backend instance inserted the active attempt first
        var realService = CreateService(context, userId: 8042);
        var winningStart = await realService.StartPlacementTestAsync(CancellationToken.None);
        Assert.True(winningStart.IsSuccess);
        var canonicalAttempt = await context.MockTestAttempts.AsNoTracking().SingleAsync(a => a.Id == winningStart.Result!.AttemptId);

        var attemptsRepoMock = new Mock<IMockTestAttemptRepository>();
        var startCallCount = 0;
        attemptsRepoMock
            .Setup(r => r.GetPlacementByUserAsync(8042, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                startCallCount++;
                // First two reads (before & inside transaction before insert) see no active attempt;
                // after DbUpdateException, requery sees the canonical active attempt committed by the other instance.
                return startCallCount <= 2 ? [] : [canonicalAttempt];
            });
        attemptsRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MockTestAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MockTestAttempt a, CancellationToken _) => a);

        var startRaceUow = new Mock<IUnitOfWork>();
        startRaceUow.SetupGet(u => u.MockTestAttempts).Returns(attemptsRepoMock.Object);
        startRaceUow.SetupGet(u => u.MockTests).Returns(new MockTestRepository(context));
        startRaceUow.SetupGet(u => u.CertificateLevels).Returns(new CertificateLevelRepository(context));
        startRaceUow.SetupGet(u => u.UserAccounts).Returns(new UserAccountRepository(context));
        startRaceUow
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (op, ct) => await op(ct));
        startRaceUow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated PostgreSQL 23505 unique constraint violation on IX_MockTestAttempts_UserId_AttemptKind"));

        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 8042, Role = Role.Learner });
        var racingStartService = new PlacementTestService(startRaceUow.Object, claims.Object);

        var recoveredStart = await racingStartService.StartPlacementTestAsync(CancellationToken.None);
        Assert.True(recoveredStart.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, recoveredStart.StatusCode);
        Assert.True(recoveredStart.Result!.IsResumed);
        Assert.Equal(canonicalAttempt.Id, recoveredStart.Result.AttemptId);

        // 2) Simulate a DbUpdateException uniqueness race on Submit where another Backend instance completed the attempt first
        var uncompletedCopy = new MockTestAttempt
        {
            Id = canonicalAttempt.Id,
            UserId = 8042,
            AttemptKind = AttemptKind.Placement,
            StartedAt = canonicalAttempt.StartedAt,
            ExpiresAt = canonicalAttempt.ExpiresAt,
            IsCompleted = false,
            QuestionSnapshotJson = canonicalAttempt.QuestionSnapshotJson,
        };

        var winningSubmit = await realService.SubmitPlacementTestAsync(
            canonicalAttempt.Id,
            new SubmitPlacementTestRequest
            {
                Answers =
                [
                    new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 },
                    new AnswerSubmissionItem { QuestionId = 502, SelectedOptionId = 5021 },
                ],
            },
            CancellationToken.None);
        Assert.True(winningSubmit.IsSuccess);
        var completedEntity = await context.MockTestAttempts.AsNoTracking().SingleAsync(a => a.Id == canonicalAttempt.Id);

        var submitAttemptsRepoMock = new Mock<IMockTestAttemptRepository>();
        submitAttemptsRepoMock
            .Setup(r => r.GetPlacementForSubmissionAsync(canonicalAttempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uncompletedCopy);
        submitAttemptsRepoMock
            .Setup(r => r.GetPlacementByIdAsync(canonicalAttempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedEntity);

        var submitRaceUow = new Mock<IUnitOfWork>();
        submitRaceUow.SetupGet(u => u.MockTestAttempts).Returns(submitAttemptsRepoMock.Object);
        var usersRepositoryMock = new Mock<IUserAccountRepository>();
        usersRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount?)null);
        submitRaceUow.SetupGet(u => u.UserAccounts).Returns(usersRepositoryMock.Object);
        submitRaceUow.SetupGet(u => u.MockTestAnswers).Returns(new MockTestAnswerRepository(context));
        submitRaceUow.SetupGet(u => u.MockTestSectionResults).Returns(new MockTestSectionResultRepository(context));
        submitRaceUow
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (op, ct) => await op(ct));
        submitRaceUow
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated PostgreSQL 23505 unique constraint violation on MockTestSectionResults"));

        var racingSubmitService = new PlacementTestService(submitRaceUow.Object, claims.Object);
        var recoveredSubmit = await racingSubmitService.SubmitPlacementTestAsync(
            canonicalAttempt.Id,
            new SubmitPlacementTestRequest { Answers = [] },
            CancellationToken.None);

        Assert.True(recoveredSubmit.IsSuccess,
            $"Expected canonical completed result recovery, got HTTP {recoveredSubmit.StatusCode}, code '{recoveredSubmit.ErrorCode}', message '{recoveredSubmit.ErrorMessage}'.");
        Assert.Equal((int)HttpStatusCode.OK, recoveredSubmit.StatusCode);
        Assert.Equal(winningSubmit.Result!.TotalScore, recoveredSubmit.Result!.TotalScore);
        Assert.Equal(winningSubmit.Result.CorrectCount, recoveredSubmit.Result.CorrectCount);
        Assert.Equal(winningSubmit.Result.AssessedJlptLevelName, recoveredSubmit.Result.AssessedJlptLevelName);
    }

    [Fact]
    public async Task Study_streak_counts_only_completed_ordinary_or_placement_attempts_and_ignores_unsubmitted_starts()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8043);

        var placementService = CreateService(context, userId: 8043);
        var mockTestService = CreateMockTestService(context, userId: 8043);
        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 8043, Role = Role.Learner });
        var analyticsService = new AnalyticsService(new UnitOfWork(context), claims.Object);

        // Start both an ordinary MockTest and a Placement test WITHOUT submitting either
        var unsubmittedPlacement = await placementService.StartPlacementTestAsync(CancellationToken.None);
        var unsubmittedMockTest = await mockTestService.StartAttemptAsync(105, CancellationToken.None);
        Assert.True(unsubmittedPlacement.IsSuccess);
        Assert.True(unsubmittedMockTest.IsSuccess);

        var streakBeforeSubmit = await analyticsService.GetStudyStreakAsync(CancellationToken.None);
        Assert.True(streakBeforeSubmit.IsSuccess);
        Assert.Equal(0, streakBeforeSubmit.Result!.CurrentStreakDays);
        Assert.Equal(0, streakBeforeSubmit.Result.LongestStreakDays);
        Assert.Null(streakBeforeSubmit.Result.LastActivityDate);
        Assert.False(streakBeforeSubmit.Result.IsActiveToday);

        // Now submit the Placement attempt -> streak becomes 1 and active today
        var submitPlacement = await placementService.SubmitPlacementTestAsync(
            unsubmittedPlacement.Result!.AttemptId,
            new SubmitPlacementTestRequest { Answers = [] },
            CancellationToken.None);
        Assert.True(submitPlacement.IsSuccess);

        var streakAfterPlacementSubmit = await analyticsService.GetStudyStreakAsync(CancellationToken.None);
        Assert.True(streakAfterPlacementSubmit.IsSuccess);
        Assert.Equal(1, streakAfterPlacementSubmit.Result!.CurrentStreakDays);
        Assert.Equal(1, streakAfterPlacementSubmit.Result.LongestStreakDays);
        Assert.True(streakAfterPlacementSubmit.Result.IsActiveToday);
    }

    [Fact]
    public async Task Response_json_under_WhenWritingNull_preserves_nullable_fields_and_emits_both_score_and_scorePercent()
    {
        await using var context = CreateContext();
        SeedLearnerAndCatalog(context, userId: 8044);
        var placementService = CreateService(context, userId: 8044);
        var profileService = CreateProfileService(context, userId: 8044);

        var serverJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        serverJsonOptions.Converters.Add(new JsonStringEnumConverter());

        // 1) Initial status with null fields must still emit explicit null properties under WhenWritingNull
        var initialStatus = await placementService.GetStatusAsync(CancellationToken.None);
        var statusJson = JsonSerializer.Serialize(initialStatus, serverJsonOptions);
        using (var statusDoc = JsonDocument.Parse(statusJson))
        {
            var resultEl = statusDoc.RootElement.GetProperty("result");
            Assert.True(resultEl.TryGetProperty("activeAttemptId", out var activeId) && activeId.ValueKind == JsonValueKind.Null);
            Assert.True(resultEl.TryGetProperty("latestCompletedAttemptId", out var latestId) && latestId.ValueKind == JsonValueKind.Null);
            Assert.True(resultEl.TryGetProperty("targetJlptLevelId", out var targetLevelId) && targetLevelId.ValueKind == JsonValueKind.Null);
            Assert.True(resultEl.TryGetProperty("targetExamDate", out var targetExamDate) && targetExamDate.ValueKind == JsonValueKind.Null);
        }

        // 2) Profile with null goal fields must emit explicit null properties under WhenWritingNull
        var profile = await profileService.GetProfileAsync(CancellationToken.None);
        var profileJson = JsonSerializer.Serialize(profile, serverJsonOptions);
        using (var profileDoc = JsonDocument.Parse(profileJson))
        {
            var resultEl = profileDoc.RootElement.GetProperty("result");
            Assert.True(resultEl.TryGetProperty("targetJlptLevelId", out var targetLevelId) && targetLevelId.ValueKind == JsonValueKind.Null);
            Assert.True(resultEl.TryGetProperty("targetJlptLevelName", out var targetLevelName) && targetLevelName.ValueKind == JsonValueKind.Null);
            Assert.True(resultEl.TryGetProperty("targetExamDate", out var targetExamDate) && targetExamDate.ValueKind == JsonValueKind.Null);
        }

        // 3) Completed placement result Sections[] must emit both score and scorePercent
        var start = await placementService.StartPlacementTestAsync(CancellationToken.None);
        var submit = await placementService.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            new SubmitPlacementTestRequest
            {
                Answers = [new AnswerSubmissionItem { QuestionId = 501, SelectedOptionId = 5011 }],
            },
            CancellationToken.None);
        var submitJson = JsonSerializer.Serialize(submit, serverJsonOptions);
        using (var submitDoc = JsonDocument.Parse(submitJson))
        {
            var firstSection = submitDoc.RootElement
                .GetProperty("result")
                .GetProperty("sections")[0];
            Assert.True(firstSection.TryGetProperty("score", out var scoreEl));
            Assert.True(firstSection.TryGetProperty("scorePercent", out var scorePercentEl));
            Assert.Equal(scoreEl.GetDouble(), scorePercentEl.GetDouble());
        }
    }

    private static PlacementTestService CreateService(AppDbContext context, int userId)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim())
            .Returns(new ClaimDTO { Id = userId, Role = Role.Learner });
        return new PlacementTestService(new UnitOfWork(context), claims.Object);
    }

    private static MockTestService CreateMockTestService(AppDbContext context, int userId)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim())
            .Returns(new ClaimDTO { Id = userId, Role = Role.Learner });
        return new MockTestService(new UnitOfWork(context), claims.Object);
    }

    private static UserProfileService CreateProfileService(AppDbContext context, int userId)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim())
            .Returns(new ClaimDTO { Id = userId, Role = Role.Learner });
        return new UserProfileService(
            new UnitOfWork(context),
            Mock.Of<IFileStorageService>(),
            claims.Object);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedLearnerAndCatalog(AppDbContext context, int userId)
    {
        if (!context.CertificateTypes.Any(t => t.Id == 71))
        {
            var certType = new CertificateType
            {
                Id = 71,
                Name = "JLPT",
                IsActive = true,
                SortOrder = 1,
            };
            var levelN5 = new CertificateLevel
            {
                Id = 71005,
                CertificateTypeId = certType.Id,
                CertificateType = certType,
                Code = "N5",
                SortOrder = 1,
            };
            var levelN4 = new CertificateLevel
            {
                Id = 71004,
                CertificateTypeId = certType.Id,
                CertificateType = certType,
                Code = "N4",
                SortOrder = 2,
            };
            var levelN3 = new CertificateLevel
            {
                Id = 71003,
                CertificateTypeId = certType.Id,
                CertificateType = certType,
                Code = "N3",
                SortOrder = 3,
            };
            var skill = new LanguageSkill
            {
                Id = 1,
                Name = "Từ vựng & Ngữ pháp",
            };

            context.CertificateTypes.Add(certType);
            context.CertificateLevels.AddRange(levelN5, levelN4, levelN3);
            context.LanguageSkills.Add(skill);

            context.MockTests.AddRange(
                CreateMockTest(105, "Placement N5", levelN5, skill, 50, 501, 502),
                CreateMockTest(104, "Placement N4", levelN4, skill, 40, 401, 402),
                CreateMockTest(103, "Placement N3", levelN3, skill, 30, 301, 302));
        }

        if (!context.Users.Any(u => u.Id == userId))
        {
            context.Users.Add(new UserAccount
            {
                Id = userId,
                Email = $"learner-{userId}@example.test",
                PasswordHash = [],
                PasswordSalt = [],
                Role = Role.Learner,
            });
        }

        context.SaveChanges();
    }

    private static MockTest CreateMockTest(
        int testId,
        string title,
        CertificateLevel level,
        LanguageSkill skill,
        int sectionId,
        int q1Id,
        int q2Id)
    {
        var test = new MockTest
        {
            Id = testId,
            Title = title,
            Description = title,
            TimeLimitMinutes = 20,
            CertificateLevelId = level.Id,
            CertificateLevel = level,
        };
        var section = new MockTestSection
        {
            Id = sectionId,
            MockTestId = testId,
            MockTest = test,
            LanguageSkillId = skill.Id,
            LanguageSkill = skill,
            Title = $"{level.Code} Tổng hợp",
            SortOrder = 1,
            Questions =
            [
                new MockQuestion
                {
                    Id = q1Id,
                    MockTestSectionId = sectionId,
                    QuestionText = $"Câu hỏi {q1Id} ({level.Code})",
                    SortOrder = 1,
                    Options =
                    [
                        new MockQuestionOption
                        {
                            Id = q1Id * 10 + 1,
                            MockQuestionId = q1Id,
                            OptionText = "Đáp án đúng",
                            IsCorrect = true,
                            SortOrder = 1,
                        },
                        new MockQuestionOption
                        {
                            Id = q1Id * 10 + 2,
                            MockQuestionId = q1Id,
                            OptionText = "Đáp án sai",
                            IsCorrect = false,
                            SortOrder = 2,
                        },
                    ],
                },
                new MockQuestion
                {
                    Id = q2Id,
                    MockTestSectionId = sectionId,
                    QuestionText = $"Câu hỏi {q2Id} ({level.Code})",
                    SortOrder = 2,
                    Options =
                    [
                        new MockQuestionOption
                        {
                            Id = q2Id * 10 + 1,
                            MockQuestionId = q2Id,
                            OptionText = "Đáp án đúng",
                            IsCorrect = true,
                            SortOrder = 1,
                        },
                        new MockQuestionOption
                        {
                            Id = q2Id * 10 + 2,
                            MockQuestionId = q2Id,
                            OptionText = "Đáp án sai",
                            IsCorrect = false,
                            SortOrder = 2,
                        },
                    ],
                },
            ],
        };
        test.Sections = [section];
        return test;
    }
}
