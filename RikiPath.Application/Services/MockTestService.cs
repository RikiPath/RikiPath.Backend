using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RikiPath.Application.Services
{
    public class MockTestService(IUnitOfWork unitOfWork, IClaimService claimService) : IMockTestService
    {
        private const string StatusInProgress = "in_progress";
        private const string StatusCompleted = "completed";

        private static JsonElement? ParseJsonElement(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        private static bool IsStructuredResponseCorrect(MockQuestion question, JsonElement? response)
        {
            if (response is null || string.IsNullOrWhiteSpace(question.AnswerKeyJson)) return false;
            try
            {
                return JsonNode.DeepEquals(JsonNode.Parse(question.AnswerKeyJson), JsonNode.Parse(response.Value.GetRawText()));
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool IsMultipleChoiceCorrect(MockQuestion question, JsonElement? response)
        {
            if (response is not { ValueKind: JsonValueKind.Array }) return false;
            var submittedIds = response.Value.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _))
                .Select(x => x.GetInt32())
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
            if (submittedIds.Length != response.Value.GetArrayLength()) return false;
            var correctIds = (question.Options ?? [])
                .Where(x => x.IsCorrect)
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToArray();
            return submittedIds.SequenceEqual(correctIds);
        }

        public async Task<ApiResponse<MockTestDetailResponse>> GetPlacementTestsByCertificateLevelAsync(
           int certificateLevelId,
           CancellationToken cancellationToken = default)
        {
            try
            {
                if (certificateLevelId <= 0)
                    return ApiResponse<MockTestDetailResponse>.Fail(
                        "Cấp độ chứng chỉ không hợp lệ.", HttpStatusCode.BadRequest);

                var levelExists = await unitOfWork.CertificateLevels
                    .AnyAsync(l => l.Id == certificateLevelId && !l.IsDeleted, cancellationToken);
                if (!levelExists)
                    return ApiResponse<MockTestDetailResponse>.NotFound("Không tìm thấy cấp độ chứng chỉ.");

                var test = await unitOfWork.MockTests
                    .GetPlacementTestDetailByLevelAsync(certificateLevelId, cancellationToken);
                if (test is null)
                    return ApiResponse<MockTestDetailResponse>.NotFound(
                        "Chưa có bài đánh giá năng lực cho cấp độ này.");

                var result = new MockTestDetailResponse
                {
                    MockTestId = test.Id,
                    Title = test.Title,
                    Description = test.Description,
                    // Tên field JlptLevelName giữ nguyên để không vỡ FE; giá trị lấy từ CertificateLevel.Code.
                    JlptLevelName = test.CertificateLevel?.Code ?? string.Empty,
                    TimeLimitMinutes = test.TimeLimitMinutes,
                    Sections = (test.Sections ?? new List<MockTestSection>())
                        .OrderBy(s => s.SortOrder)
                        .ThenBy(s => s.Id)
                        .Select(section => new MockTestSectionResponse
                        {
                            SectionId = section.Id,
                            Title = section.Title,
                            SortOrder = section.SortOrder,
                            LanguageSkillName = section.LanguageSkill?.Name ?? string.Empty,
                            Questions = (section.Questions ?? new List<MockQuestion>())
                                .OrderBy(q => q.SortOrder)
                                .ThenBy(q => q.Id)
                                .Select(question => new MockQuestionResponse
                                {
                                    QuestionId = question.Id,
                                    QuestionText = question.QuestionText,
                                    QuestionType = question.QuestionType,
                                    InteractionDataJson = question.InteractionDataJson,
                                    AudioUrl = question.AudioUrl,
                                    ImageUrl = question.ImageUrl,
                                    SortOrder = question.SortOrder,
                                    Options = (question.Options ?? new List<MockQuestionOption>())
                                        .OrderBy(o => o.SortOrder)
                                        .ThenBy(o => o.Id)
                                        .Select(option => new MockQuestionOptionResponse
                                        {
                                            OptionId = option.Id,
                                            OptionText = option.OptionText,
                                            SortOrder = option.SortOrder,
                                        })
                                        .ToList(),
                                })
                                .ToList(),
                        })
                        .ToList(),
                };

                return ApiResponse<MockTestDetailResponse>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<MockTestDetailResponse>.Fail(
                    "Không thể tải bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(int practiceTestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(practiceTestId, cancellationToken);
                if (test is null || test.IsDeleted)
                    return ApiResponse<StartAttemptResponse>.NotFound(
                        $"Không tìm thấy MockTest có Id = {practiceTestId}.");

                var orderedQuestions = (test.Sections ?? [])
                    .Where(section => !section.IsDeleted)
                    .OrderBy(section => section.SortOrder)
                    .ThenBy(section => section.Id)
                    .SelectMany(section => (section.Questions ?? [])
                        .Where(question => !question.IsDeleted)
                        .OrderBy(question => question.SortOrder)
                        .ThenBy(question => question.Id)
                        .Select(question => (Section: section, Question: question)))
                    .ToList();

                if (orderedQuestions.Count == 0)
                    return ApiResponse<StartAttemptResponse>.Fail(
                        "Đề thi chưa có câu hỏi để bắt đầu.", HttpStatusCode.BadRequest);

                var previousAttempts = (await unitOfWork.MockTestAttempts.FindAsync(a =>
                    a.UserId == userId && a.IsCompleted && !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest, cancellationToken))
                    .Where(a => !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest)
                    .ToList();

                var previousAttemptIds = previousAttempts.Select(a => a.Id).ToList();
                var previouslyWrongQuestionIds = new List<int>();

                if (previousAttemptIds.Count > 0)
                {
                    var pastAnswers = await unitOfWork.MockTestAnswers.FindAsync(ans =>
                        previousAttemptIds.Contains(ans.MockTestAttemptId), cancellationToken);

                    var currentQuestionIds = orderedQuestions
                        .Select(item => item.Question.Id)
                        .ToHashSet();

                    previouslyWrongQuestionIds = pastAnswers
                        .Where(answer => currentQuestionIds.Contains(answer.MockQuestionId))
                        .GroupBy(ans => ans.MockQuestionId)
                        .Where(g => !g.OrderByDescending(x => x.MockTestAttemptId).First().IsCorrect)
                        .Select(g => g.Key)
                        .ToList();
                }

                ApiResponse<StartAttemptResponse>? response = null;
                await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
                {
                    var attempt = new MockTestAttempt
                    {
                        UserId = userId,
                        StartedAt = DateTime.UtcNow,
                        SubmittedAt = null,
                        TotalScore = 0,
                        IsCompleted = false,
                    };

                    await unitOfWork.MockTestAttempts.AddAsync(attempt, transactionCancellationToken);
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    // Snapshot bằng các answer rỗng để attempt giữ đúng bộ câu hỏi tại thời điểm bắt đầu mà không đổi schema.
                    var snapshots = orderedQuestions.Select(item => new MockTestAnswer
                    {
                        MockTestAttemptId = attempt.Id,
                        MockQuestionId = item.Question.Id,
                        SelectedOptionId = null,
                        IsCorrect = false,
                    });
                    await unitOfWork.MockTestAnswers.AddRangeAsync(snapshots, transactionCancellationToken);
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    response = ApiResponse<StartAttemptResponse>.Created(new StartAttemptResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = practiceTestId,
                        StartedAt = attempt.StartedAt,
                        TimeLimitMinutes = test.TimeLimitMinutes,
                        TotalPreviouslyWrongQuestions = previouslyWrongQuestionIds.Count,
                        PreviouslyWrongQuestionIds = previouslyWrongQuestionIds,
                        Questions = orderedQuestions.Select(item => new StartAttemptQuestionResponse
                        {
                            QuestionId = item.Question.Id,
                            MockTestSectionId = item.Section.Id,
                            SectionTitle = item.Section.Title,
                            SectionOrder = item.Section.SortOrder,
                            Order = item.Question.SortOrder,
                            Content = item.Question.QuestionText,
                            QuestionType = item.Question.QuestionType,
                            InteractionData = ParseJsonElement(item.Question.InteractionDataJson),
                            AudioUrl = item.Question.AudioUrl,
                            ImageUrl = item.Question.ImageUrl,
                            Options = (item.Question.Options ?? [])
                                .Where(option => !option.IsDeleted)
                                .OrderBy(option => option.SortOrder)
                                .ThenBy(option => option.Id)
                                .Select(option => new StartAttemptOptionResponse
                                {
                                    OptionId = option.Id,
                                    Content = option.OptionText,
                                    Order = option.SortOrder,
                                })
                                .ToList(),
                        }).ToList(),
                    });
                }, cancellationToken);

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<StartAttemptResponse>.Fail(
                    "Không thể bắt đầu lượt làm bài.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (request.Answers is null || request.Answers.Count == 0)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Danh sách câu trả lời không được để trống.", HttpStatusCode.BadRequest);

                // Từ chối duplicate để mỗi câu hỏi chỉ được chấm một lần và không vi phạm unique constraint.
                var duplicateQuestionIds = request.Answers
                    .GroupBy(answer => answer.QuestionId)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .OrderBy(id => id)
                    .ToList();
                if (duplicateQuestionIds.Count > 0)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        $"Không được gửi lặp câu hỏi: {string.Join(", ", duplicateQuestionIds)}.",
                        HttpStatusCode.BadRequest);

                ApiResponse<AttemptResultResponse>? response = null;
                await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
                {
                    var attempt = await unitOfWork.MockTestAttempts.GetForSubmissionAsync(
                        attemptId, transactionCancellationToken);
                    if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    {
                        response = ApiResponse<AttemptResultResponse>.NotFound(
                            $"Không tìm thấy MockTestAttempt có Id = {attemptId}.");
                        return;
                    }

                    // Chỉ Learner sở hữu attempt mới được phép submit; không tin attemptId từ client.
                    if (attempt.UserId != userId)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Bạn không có quyền nộp bài cho lượt làm bài này.", HttpStatusCode.Forbidden);
                        return;
                    }

                    // Attempt đã hoàn tất không được chấm lại khi client retry request.
                    if (attempt.IsCompleted)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Lượt làm bài này đã được nộp trước đó.", HttpStatusCode.BadRequest);
                        return;
                    }

                    var snapshotAnswers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(
                        attemptId, transactionCancellationToken);
                    if (snapshotAnswers.Count == 0)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Lượt làm bài không có snapshot câu hỏi hợp lệ. Vui lòng bắt đầu lại đề thi.",
                            HttpStatusCode.Conflict);
                        return;
                    }

                    var testIds = snapshotAnswers
                        .Select(answer => answer.MockQuestion.MockTestSection.MockTestId)
                        .Distinct()
                        .ToList();
                    if (testIds.Count != 1)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Snapshot câu hỏi của lượt làm bài không hợp lệ.", HttpStatusCode.Conflict);
                        return;
                    }

                    var mockTest = snapshotAnswers[0].MockQuestion.MockTestSection.MockTest;
                    // Thời gian làm bài lấy từ StartedAt phía server, không tin đồng hồ hoặc elapsed time từ client.
                    if (mockTest.TimeLimitMinutes > 0 &&
                        DateTime.UtcNow > attempt.StartedAt.AddMinutes(mockTest.TimeLimitMinutes))
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Lượt làm bài đã hết thời gian.", HttpStatusCode.BadRequest);
                        return;
                    }

                    var snapshotsByQuestionId = snapshotAnswers.ToDictionary(
                        answer => answer.MockQuestionId);
                    var invalidQuestionIds = request.Answers
                        .Select(answer => answer.QuestionId)
                        .Where(questionId => !snapshotsByQuestionId.ContainsKey(questionId))
                        .OrderBy(questionId => questionId)
                        .ToList();
                    // Chỉ chấp nhận câu hỏi thuộc snapshot của đúng attempt để ngăn gửi ID từ đề khác.
                    if (invalidQuestionIds.Count > 0)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            $"Câu hỏi không thuộc lượt làm bài: {string.Join(", ", invalidQuestionIds)}.",
                            HttpStatusCode.BadRequest);
                        return;
                    }

                    foreach (var submittedAnswer in request.Answers)
                    {
                        var question = snapshotsByQuestionId[submittedAnswer.QuestionId].MockQuestion;
                        if (question.QuestionType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase))
                        {
                            if (submittedAnswer.Response.HasValue)
                            {
                                response = ApiResponse<AttemptResultResponse>.Fail(
                                    $"Câu hỏi {submittedAnswer.QuestionId} yêu cầu chọn một đáp án.", HttpStatusCode.BadRequest);
                                return;
                            }
                        }
                        else if (question.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase))
                        {
                            if (submittedAnswer.Response is not { ValueKind: JsonValueKind.Array })
                            {
                                response = ApiResponse<AttemptResultResponse>.Fail(
                                    $"Câu hỏi {submittedAnswer.QuestionId} yêu cầu danh sách OptionId.", HttpStatusCode.BadRequest);
                                return;
                            }
                        }
                        else if (submittedAnswer.SelectedOptionId is not null || submittedAnswer.Response is null)
                        {
                            response = ApiResponse<AttemptResultResponse>.Fail(
                                $"Câu hỏi {submittedAnswer.QuestionId} yêu cầu câu trả lời theo dạng {question.QuestionType}.", HttpStatusCode.BadRequest);
                            return;
                        }
                        // Option phải thuộc đúng Question để client không thể ghép ID tùy ý khi chấm điểm.
                        if (submittedAnswer.SelectedOptionId is not null &&
                            !(question.Options ?? []).Any(option => option.Id == submittedAnswer.SelectedOptionId))
                        {
                            response = ApiResponse<AttemptResultResponse>.Fail(
                                $"Lựa chọn không thuộc câu hỏi {submittedAnswer.QuestionId}.",
                                HttpStatusCode.BadRequest);
                            return;
                        }
                    }

                    var correctByQuestion = snapshotAnswers.ToDictionary(
                        answer => answer.MockQuestionId,
                        _ => false);
                    foreach (var submittedAnswer in request.Answers)
                    {
                        var snapshot = snapshotsByQuestionId[submittedAnswer.QuestionId];
                        var question = snapshot.MockQuestion;
                        var selectedOption = submittedAnswer.SelectedOptionId is null
                            ? null
                            : snapshot.MockQuestion.Options?.Single(
                                option => option.Id == submittedAnswer.SelectedOptionId);
                        snapshot.SelectedOptionId = submittedAnswer.SelectedOptionId;
                        snapshot.ResponseJson = submittedAnswer.Response?.GetRawText();
                        snapshot.IsCorrect = question.QuestionType.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                            ? selectedOption?.IsCorrect == true
                            : question.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase)
                                ? IsMultipleChoiceCorrect(question, submittedAnswer.Response)
                                : IsStructuredResponseCorrect(question, submittedAnswer.Response);
                        correctByQuestion[submittedAnswer.QuestionId] = snapshot.IsCorrect;
                    }
                    var questions = snapshotAnswers.Select(answer => answer.MockQuestion).ToList();
                    var sections = BuildSectionResults(attemptId, questions, correctByQuestion);
                    await unitOfWork.MockTestSectionResults.AddRangeAsync(
                        sections.Entities, transactionCancellationToken);

                    attempt.SubmittedAt = DateTime.UtcNow;
                    attempt.IsCompleted = true;
                    attempt.TotalScore = sections.OverallScore;
                    unitOfWork.MockTestAttempts.Update(attempt);

                    // Answers, section results và trạng thái attempt cùng commit trong một transaction.
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    response = ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = mockTest.Id,
                        Status = StatusCompleted,
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt,
                        TotalScore = attempt.TotalScore.Value,
                        WrongQuestionIds = correctByQuestion
                            .Where(item => !item.Value)
                            .Select(item => item.Key)
                            .OrderBy(id => id)
                            .ToList(),
                        Sections = sections.Responses,
                    });
                }, cancellationToken);

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<AttemptResultResponse>.Fail(
                    "Không thể chấm điểm lượt làm bài.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(int attemptId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
                if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    return ApiResponse<AttemptResultResponse>.NotFound(
                        $"Không tìm thấy MockTestAttempt có Id = {attemptId}.");

                if (attempt.UserId != userId)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Bạn không có quyền xem lượt làm bài này.", HttpStatusCode.Forbidden);

                if (!attempt.IsCompleted)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Lượt làm bài này chưa được nộp/chấm điểm.", HttpStatusCode.BadRequest);

                var sectionResults = await unitOfWork.MockTestSectionResults.GetByAttemptIdAsync(
                    attemptId, cancellationToken);

                // Đồng bộ với SubmitAttemptAsync: trả luôn danh sách câu sai.
                var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(
                    attemptId, cancellationToken);

                var result = new AttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    MockTestId = sectionResults
                        .Select(section => section.MockTestSection.MockTestId)
                        .FirstOrDefault(),
                    Status = attempt.IsCompleted ? StatusCompleted : StatusInProgress,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    TotalScore = attempt.TotalScore.GetValueOrDefault(),
                    WrongQuestionIds = (answers ?? Enumerable.Empty<MockTestAnswer>())
                        .Where(a => !a.IsCorrect)
                        .Select(a => a.MockQuestionId)
                        .OrderBy(id => id)
                        .ToList(),
                    Sections = sectionResults.Select(r => new SectionResultItem
                    {
                        MockTestSectionId = r.MockTestSectionId,
                        SectionTitle = r.MockTestSection.Title,
                        LanguageSkillName = r.MockTestSection.LanguageSkill.Name,
                        CorrectCount = r.CorrectCount,
                        TotalCount = r.TotalCount,
                        Score = r.ScorePercent,
                    }).ToList(),
                };

                return ApiResponse<AttemptResultResponse>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<AttemptResultResponse>.Fail(
                    "Không thể tải kết quả lượt làm bài.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(
            int certificationLevelId, CancellationToken cancellationToken = default)
        {
            try
            {
                var tests = await unitOfWork.MockTests.GetByLevelAsync(certificationLevelId);

                var result = tests.Select(t => new TestSummaryResponse
                {
                    MockTestId = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    // NOTE: property response vẫn tên "JlptLevelName" (từ trước khi đổi model) - giữ tên
                    // field để không vỡ FE, nhưng lấy giá trị từ CertificateLevel.Code (không còn Name).
                    // Nên đổi tên field này thành CertificateLevelName trong Responses/MockTests
                    // khi bạn tiện cập nhật FE.
                    JlptLevelName = t.CertificateLevel?.Code ?? string.Empty,
                    TimeLimitMinutes = t.TimeLimitMinutes,
                }).ToList();

                return ApiResponse<List<TestSummaryResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail(
                    "Không thể tải danh sách đề thi.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MockTestDetailResponse>> GetPlacementTestDetailAsync(
           int certificateLevelId,
           CancellationToken cancellationToken = default)
        {
            try
            {
                if (certificateLevelId <= 0)
                    return ApiResponse<MockTestDetailResponse>.Fail(
                        "Cấp độ chứng chỉ không hợp lệ.", HttpStatusCode.BadRequest);

                var levelExists = await unitOfWork.CertificateLevels
                    .AnyAsync(l => l.Id == certificateLevelId, cancellationToken);
                if (!levelExists)
                    return ApiResponse<MockTestDetailResponse>.NotFound("Không tìm thấy cấp độ chứng chỉ.");

                var test = await unitOfWork.MockTests
                    .GetPlacementTestDetailByLevelAsync(certificateLevelId, cancellationToken);

                if (test is null)
                    return ApiResponse<MockTestDetailResponse>.NotFound(
                        "Chưa có bài đánh giá năng lực cho cấp độ này.");

                var result = new MockTestDetailResponse
                {
                    MockTestId = test.Id,
                    Title = test.Title,
                    Description = test.Description,
                    JlptLevelName = test.CertificateLevel?.Code ?? string.Empty,
                    TimeLimitMinutes = test.TimeLimitMinutes,
                    Sections = (test.Sections ?? new List<MockTestSection>())
                        .OrderBy(s => s.SortOrder)
                        .Select(section => new MockTestSectionResponse
                        {
                            SectionId = section.Id,
                            Title = section.Title,
                            SortOrder = section.SortOrder,
                            LanguageSkillName = section.LanguageSkill?.Name ?? string.Empty,
                            Questions = (section.Questions ?? new List<MockQuestion>())
                                .OrderBy(q => q.SortOrder)
                                .Select(question => new MockQuestionResponse
                                {
                                    QuestionId = question.Id,
                                    QuestionText = question.QuestionText,
                                    QuestionType = question.QuestionType,
                                    InteractionDataJson = question.InteractionDataJson,
                                    AudioUrl = question.AudioUrl,
                                    ImageUrl = question.ImageUrl,
                                    SortOrder = question.SortOrder,
                                    Options = (question.Options ?? new List<MockQuestionOption>())
                                        .OrderBy(o => o.SortOrder)
                                        .Select(option => new MockQuestionOptionResponse
                                        {
                                            OptionId = option.Id,
                                            OptionText = option.OptionText,
                                            SortOrder = option.SortOrder,
                                        })
                                        .ToList(),
                                })
                                .ToList(),
                        })
                        .ToList(),
                };

                return ApiResponse<MockTestDetailResponse>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<MockTestDetailResponse>.Fail(
                    "Không thể tải bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(int attemptId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
                if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    return ApiResponse<DetailedAttemptResultResponse>.NotFound(
                        $"Không tìm thấy MockTestAttempt có Id = {attemptId}.");

                if (attempt.UserId != userId)
                    return ApiResponse<DetailedAttemptResultResponse>.Fail(
                        "Bạn không có quyền xem lượt làm bài này.", HttpStatusCode.Forbidden);

                if (!attempt.IsCompleted)
                    return ApiResponse<DetailedAttemptResultResponse>.Fail(
                        "Lượt làm bài này chưa được nộp/chấm điểm.", HttpStatusCode.BadRequest);

                var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(
                    attemptId, cancellationToken);

                var questions = answers.Select(a => new QuestionReviewCard
                {
                    QuestionId = a.MockQuestionId,
                    QuestionText = a.MockQuestion.QuestionText,
                    QuestionType = a.MockQuestion.QuestionType,
                    Response = ParseJsonElement(a.ResponseJson),
                    AnswerKey = ParseJsonElement(a.MockQuestion.AnswerKeyJson),
                    Explanation = a.MockQuestion.Explanation,
                    SelectedOptionId = a.SelectedOptionId,
                    IsCorrect = a.IsCorrect,
                    WasPreviouslyIncorrect = !a.IsCorrect,
                    Options = (a.MockQuestion.Options ?? [])
                        .OrderBy(option => option.SortOrder)
                        .ThenBy(option => option.Id)
                        .Select(o => new QuestionOptionReview
                        {
                            OptionId = o.Id,
                            OptionText = o.OptionText,
                            IsCorrect = o.IsCorrect,
                        }).ToList(),
                }).ToList();

                return ApiResponse<DetailedAttemptResultResponse>.Success(new DetailedAttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    MockTestId = answers
                        .Select(answer => answer.MockQuestion.MockTestSection.MockTestId)
                        .FirstOrDefault(),
                    TotalScore = attempt.TotalScore.GetValueOrDefault(),
                    Questions = questions,
                });
            }
            catch (Exception)
            {
                return ApiResponse<DetailedAttemptResultResponse>.Fail(
                    "Không thể tải kết quả chi tiết.", HttpStatusCode.InternalServerError);
            }
        }

        // ---------------- Chế độ Ôn tập Câu hỏi Sai (Quizlet Review Mode) ----------------

        public async Task<ApiResponse<List<QuestionReviewCard>>> GetWrongQuestionsReviewSessionAsync(int? practiceTestId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                // 1. Lấy tất cả lượt làm bài đã hoàn thành
                var attempts = (await unitOfWork.MockTestAttempts.FindAsync(a =>
                    a.UserId == userId && a.IsCompleted && !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest, cancellationToken))
                    .ToList();

                var attemptIds = attempts.Select(a => a.Id).ToList();
                if (attemptIds.Count == 0)
                {
                    return ApiResponse<List<QuestionReviewCard>>.Success([]);
                }

                // 2. Lấy tất cả câu trả lời của Learner trong các lượt làm bài đó
                var pastAnswers = (await unitOfWork.MockTestAnswers.FindAsync(
                    ans => attemptIds.Contains(ans.MockTestAttemptId), cancellationToken)).ToList();

                // Nếu chỉ ôn một đề cụ thể thì giới hạn theo các câu hỏi thuộc đề đó.
                if (practiceTestId.HasValue)
                {
                    var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(
                        practiceTestId.Value, cancellationToken);
                    if (test is null || test.IsDeleted)
                        return ApiResponse<List<QuestionReviewCard>>.NotFound(
                            $"Không tìm thấy MockTest có Id = {practiceTestId}.");

                    var testQuestionIds = (test.Sections ?? [])
                        .SelectMany(s => s.Questions ?? [])
                        .Select(q => q.Id)
                        .ToHashSet();
                    pastAnswers = pastAnswers
                        .Where(ans => testQuestionIds.Contains(ans.MockQuestionId))
                        .ToList();
                }

                // 3. Nhóm câu hỏi & lọc câu mà lần trả lời gần đây nhất bị SAI
                var wrongStats = pastAnswers
                    .GroupBy(ans => ans.MockQuestionId)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        LatestAnswer = g.OrderByDescending(x => x.MockTestAttemptId).FirstOrDefault(),
                        TimesWrong = g.Count(x => !x.IsCorrect)
                    })
                    .Where(x => x.LatestAnswer != null && !x.LatestAnswer.IsCorrect)
                    .ToList();

                if (wrongStats.Count == 0)
                {
                    return ApiResponse<List<QuestionReviewCard>>.Success([]);
                }

                var wrongQuestionIds = wrongStats.Select(x => x.QuestionId).ToList();
                var questions = await unitOfWork.MockQuestions.GetByIdsWithOptionsAsync(wrongQuestionIds);
                var statsDict = wrongStats.ToDictionary(x => x.QuestionId);

                // 4. Map câu hỏi kèm thống kê Quizlet (ưu tiên câu sai nhiều lần lên trước)
                var reviewItems = questions.Select(q =>
                {
                    var stat = statsDict.GetValueOrDefault(q.Id);
                    return new QuestionReviewCard
                    {
                        QuestionId = q.Id,
                        QuestionText = q.QuestionText,
                        Explanation = q.Explanation,
                        SelectedOptionId = stat?.LatestAnswer?.SelectedOptionId,
                        IsCorrect = false,
                        WasPreviouslyIncorrect = true,
                        TimesAnsweredWrong = stat?.TimesWrong ?? 0,
                        Options = q.Options.Select(o => new QuestionOptionReview
                        {
                            OptionId = o.Id,
                            OptionText = o.OptionText,
                            IsCorrect = o.IsCorrect,
                        }).ToList(),
                    };
                }).OrderByDescending(x => x.TimesAnsweredWrong).ToList();

                return ApiResponse<List<QuestionReviewCard>>.Success(reviewItems);
            }
            catch (Exception)
            {
                return ApiResponse<List<QuestionReviewCard>>.Fail(
                    "Không thể tải danh sách câu hỏi cần ôn tập.", HttpStatusCode.InternalServerError);
            }
        }

        // ---------------- Placement Test (Bài đánh giá năng lực) ----------------

        private const string PlacementAttemptType = "placement";
        private const int DefaultPlacementMinutes = 60;      // dùng khi các đề placement không khai báo TimeLimitMinutes
        private const double PlacementPassPercent = 60d;     // ngưỡng đạt của từng level để xác định trình độ hiện tại

        private sealed record LevelInfo(int Id, int CertificateTypeId, string Name, int SortOrder);
        private sealed record PlacementAnswerInput(int QuestionId, int? SelectedOptionId);

        public async Task<ApiResponse<PlacementTestStatusResponse>> GetStatusAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<PlacementTestStatusResponse>.NotFound("Không tìm thấy người dùng.");

                var attempts = (await unitOfWork.MockTestAttempts.FindAsync(a =>
                    a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.Placement, cancellationToken))
                    .OrderByDescending(a => a.StartedAt)
                    .ToList();

                var status = new PlacementTestStatusResponse
                {
                    AttemptType = PlacementAttemptType,
                    LatestAttemptId = attempts.FirstOrDefault()?.Id,
                    DailyStudyMinutes = user.DailyStudyMinutes,
                    TargetExamDate = user.TargetExamDate,
                };

                // Attempt đang làm dở và còn hạn.
                var active = attempts.FirstOrDefault(a => !a.IsCompleted);
                if (active is not null)
                {
                    var activeAnswers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(active.Id, cancellationToken);
                    if (activeAnswers.Count > 0 && !IsExpired(active, activeAnswers))
                    {
                        status.HasActiveAttempt = true;
                        status.ActiveAttemptId = active.Id;
                        status.ActiveAttemptExpiresAt = ExpiresAt(active, activeAnswers);
                    }
                }

                // Lần đánh giá hoàn tất gần nhất.
                var completed = attempts.FirstOrDefault(a => a.IsCompleted);
                if (completed is not null)
                {
                    var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(completed.Id, cancellationToken);
                    if (answers.Count > 0)
                    {
                        var latest = await BuildPlacementResultAsync(completed, answers, cancellationToken);
                        status.HasCompletedPlacementTest = true;
                        status.LatestCompletedAttemptId = completed.Id;
                        status.LatestSubmittedAt = completed.SubmittedAt;
                        status.LatestScorePercent = latest.TotalScore;
                        status.AssessedJlptLevelId = latest.AssessedJlptLevelId;
                        status.AssessedJlptLevelName = latest.AssessedJlptLevelName;
                        status.RecommendedTargetJlptLevelId = latest.RecommendedTargetJlptLevelId;
                        status.RecommendedTargetJlptLevelName = latest.RecommendedTargetJlptLevelName;
                    }
                }

                var targetLevelId = user.TargetCertificateLevelId;
                if (targetLevelId is not null)
                {
                    var levels = await LoadLevelsAsync([targetLevelId.Value], cancellationToken);
                    status.TargetJlptLevelId = targetLevelId;
                    status.TargetJlptLevelName = levels.GetValueOrDefault(targetLevelId.Value)?.Name;
                }

                status.PlacementDecision = status.HasCompletedPlacementTest
                    ? "COMPLETED"
                    : ToDecisionCode(user.PlacementDecision);
                status.IsReadyForLearningPath =
                    (status.HasCompletedPlacementTest || user.PlacementDecision != PlacementDecision.NotTaken)
                    && targetLevelId is not null;

                return ApiResponse<PlacementTestStatusResponse>.Success(status);
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestStatusResponse>.Fail(
                    "Không thể tải trạng thái bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementTestStatusResponse>> SkipPlacementAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<PlacementTestStatusResponse>.NotFound("Không tìm thấy người dùng.");

                // Chỉ chuyển NotTaken -> Skipped; không ghi đè khi đã làm bài xong.
                if (user.PlacementDecision == PlacementDecision.NotTaken)
                {
                    user.PlacementDecision = PlacementDecision.Skipped;
                    unitOfWork.UserAccounts.Update(user);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                return await GetStatusAsync(cancellationToken);
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestStatusResponse>.Fail(
                    "Không thể bỏ qua bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementAttemptSnapshotResponse?>> GetActiveAttemptAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var active = (await unitOfWork.MockTestAttempts.FindAsync(a =>
                    a.UserId == userId && !a.IsDeleted && !a.IsCompleted && a.AttemptKind == AttemptKind.Placement, cancellationToken))
                    .OrderByDescending(a => a.StartedAt)
                    .FirstOrDefault();
                if (active is null)
                    return ApiResponse<PlacementAttemptSnapshotResponse?>.Success(null);

                var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(active.Id, cancellationToken);
                // Hết giờ thì không cho resume; FE cho học viên làm lại.
                if (answers.Count == 0 || IsExpired(active, answers))
                    return ApiResponse<PlacementAttemptSnapshotResponse?>.Success(null);

                return ApiResponse<PlacementAttemptSnapshotResponse?>.Success(
                    await BuildSnapshotAsync(active, answers, isResumed: true, cancellationToken));
            }
            catch (Exception)
            {
                return ApiResponse<PlacementAttemptSnapshotResponse?>.Fail(
                    "Không thể tải lượt làm bài đang dở.", HttpStatusCode.InternalServerError);
            }
        }

        public Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            CancellationToken cancellationToken = default)
            => StartPlacementTestAsync(null, cancellationToken);

        public async Task<ApiResponse<PlacementAttemptSnapshotResponse>> StartPlacementTestAsync(
            StartPlacementTestRequest? request, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (request is null || request.MockTestId <= 0)
                    return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                        "MockTestId không hợp lệ.", HttpStatusCode.BadRequest);

                var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(
                    request.MockTestId, cancellationToken);
                if (test is null || test.IsDeleted)
                    return ApiResponse<PlacementAttemptSnapshotResponse>.NotFound(
                        $"Không tìm thấy bài đánh giá năng lực có Id = {request.MockTestId}.");

                // Lấy toàn bộ câu hỏi của đề theo đúng thứ tự section -> câu hỏi.
                var questions = (test.Sections ?? [])
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                    .SelectMany(s => (s.Questions ?? [])
                        .Where(q => !q.IsDeleted)
                        .OrderBy(q => q.SortOrder).ThenBy(q => q.Id))
                    .ToList();
                if (questions.Count == 0)
                    return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                        "Bài đánh giá chưa có câu hỏi.", HttpStatusCode.BadRequest);

                ApiResponse<PlacementAttemptSnapshotResponse>? response = null;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    // Còn attempt dở và chưa hết giờ thì trả lại để học viên làm tiếp
                    // (đáp án đã autosave + thời gian còn lại tính từ ExpiresAt phía server).
                    var existing = (await unitOfWork.MockTestAttempts.FindAsync(a =>
                        a.UserId == userId && !a.IsDeleted && !a.IsCompleted && a.AttemptKind == AttemptKind.Placement, ct))
                        .OrderByDescending(a => a.StartedAt)
                        .FirstOrDefault();

                    if (existing is not null)
                    {
                        var existingAnswers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(existing.Id, ct);
                        if (existingAnswers.Count > 0 && !IsExpired(existing, existingAnswers))
                        {
                            response = ApiResponse<PlacementAttemptSnapshotResponse>.Success(
                                await BuildSnapshotAsync(existing, existingAnswers, isResumed: true, ct));
                            return;
                        }
                        // Attempt cũ đã hết giờ: KHÔNG xóa, giữ lại làm lịch sử số lần làm bài.
                    }

                    var limitMinutes = test.TimeLimitMinutes > 0 ? test.TimeLimitMinutes : DefaultPlacementMinutes;
                    var now = DateTime.UtcNow;
                    var attempt = new MockTestAttempt
                    {
                        UserId = userId,
                        AttemptKind = AttemptKind.Placement,
                        IsPlacementAssessment = true,
                        StartedAt = now,
                        ExpiresAt = now.AddMinutes(limitMinutes),
                        IsCompleted = false,
                        TotalScore = 0,
                    };
                    await unitOfWork.MockTestAttempts.AddAsync(attempt, ct);
                    await unitOfWork.SaveChangesAsync(ct);

                    // Snapshot bằng các answer rỗng để attempt giữ đúng bộ câu hỏi lúc bắt đầu.
                    await unitOfWork.MockTestAnswers.AddRangeAsync(questions.Select(q => new MockTestAnswer
                    {
                        MockTestAttemptId = attempt.Id,
                        MockQuestionId = q.Id,
                        IsCorrect = false,
                    }), ct);
                    await unitOfWork.SaveChangesAsync(ct);

                    // Load lại để có đủ navigation (Section/Test/Options) dựng snapshot.
                    var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attempt.Id, ct);
                    response = ApiResponse<PlacementAttemptSnapshotResponse>.Created(
                        await BuildSnapshotAsync(attempt, answers, isResumed: false, ct));
                }, cancellationToken);

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                    "Không thể bắt đầu bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementAnswerAckResponse>> SavePlacementAnswerAsync(
            int attemptId, SavePlacementAnswerRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var (attempt, code, error) = await GetOwnedActivePlacementAttemptAsync(attemptId, userId, cancellationToken);
                if (attempt is null)
                    return ApiResponse<PlacementAnswerAckResponse>.Fail(error!, code);

                var snapshots = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, cancellationToken);
                if (snapshots.Count == 0)
                    return ApiResponse<PlacementAnswerAckResponse>.Fail(
                        "Lượt làm bài không có snapshot hợp lệ.", HttpStatusCode.Conflict);
                if (IsExpired(attempt, snapshots))
                    return ApiResponse<PlacementAnswerAckResponse>.Fail(
                        "Lượt làm bài đã hết thời gian.", HttpStatusCode.BadRequest);

                var snapshot = snapshots.FirstOrDefault(s => s.MockQuestionId == request.QuestionId);
                if (snapshot is null)
                    return ApiResponse<PlacementAnswerAckResponse>.Fail(
                        $"Câu hỏi không thuộc lượt làm bài: {request.QuestionId}.", HttpStatusCode.BadRequest);

                var validationError = ApplyPlacementAnswer(
                    snapshot, new PlacementAnswerInput(request.QuestionId, request.SelectedOptionId));
                if (validationError is not null)
                    return ApiResponse<PlacementAnswerAckResponse>.Fail(validationError, HttpStatusCode.BadRequest);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<PlacementAnswerAckResponse>.Success(new PlacementAnswerAckResponse
                {
                    AttemptId = attemptId,
                    QuestionId = request.QuestionId,
                    SelectedOptionId = request.SelectedOptionId,
                    SavedAt = DateTime.UtcNow,
                });
            }
            catch (Exception)
            {
                return ApiResponse<PlacementAnswerAckResponse>.Fail(
                    "Không thể lưu câu trả lời.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementAnswersSavedResponse>> SavePlacementAnswersAsync(
            int attemptId, SubmitPlacementTestRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (request.Answers is null || request.Answers.Count == 0)
                    return ApiResponse<PlacementAnswersSavedResponse>.Fail(
                        "Danh sách câu trả lời không được để trống.", HttpStatusCode.BadRequest);

                var (attempt, code, error) = await GetOwnedActivePlacementAttemptAsync(attemptId, userId, cancellationToken);
                if (attempt is null)
                    return ApiResponse<PlacementAnswersSavedResponse>.Fail(error!, code);

                var snapshots = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, cancellationToken);
                if (snapshots.Count == 0 || IsExpired(attempt, snapshots))
                    return ApiResponse<PlacementAnswersSavedResponse>.Fail(
                        "Lượt làm bài không hợp lệ hoặc đã hết thời gian.", HttpStatusCode.BadRequest);

                var byQuestion = snapshots.ToDictionary(s => s.MockQuestionId);
                var saved = 0;
                // Autosave: nếu trùng câu thì lấy bản gửi sau cùng.
                foreach (var a in request.Answers.GroupBy(x => x.QuestionId).Select(g => g.Last()))
                {
                    if (!byQuestion.TryGetValue(a.QuestionId, out var snapshot))
                        return ApiResponse<PlacementAnswersSavedResponse>.Fail(
                            $"Câu hỏi không thuộc lượt làm bài: {a.QuestionId}.", HttpStatusCode.BadRequest);

                    var validationError = ApplyPlacementAnswer(
                        snapshot, new PlacementAnswerInput(a.QuestionId, a.SelectedOptionId));
                    if (validationError is not null)
                        return ApiResponse<PlacementAnswersSavedResponse>.Fail(validationError, HttpStatusCode.BadRequest);
                    saved++;
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<PlacementAnswersSavedResponse>.Success(new PlacementAnswersSavedResponse
                {
                    AttemptId = attemptId,
                    SavedAt = DateTime.UtcNow,
                    SavedQuestionCount = saved,
                });
            }
            catch (Exception)
            {
                return ApiResponse<PlacementAnswersSavedResponse>.Fail(
                    "Không thể lưu các câu trả lời.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementTestResultResponse>> SubmitPlacementTestAsync(
            int attemptId, SubmitPlacementTestRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                // Request có thể rỗng: các câu đã autosave vẫn được chấm.
                var inputs = (request.Answers ?? [])
                    .Select(a => new PlacementAnswerInput(a.QuestionId, a.SelectedOptionId))
                    .ToList();
                var duplicate = inputs.GroupBy(i => i.QuestionId).FirstOrDefault(g => g.Count() > 1);
                if (duplicate is not null)
                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        $"Không được gửi lặp câu hỏi: {duplicate.Key}.", HttpStatusCode.BadRequest);

                ApiResponse<PlacementTestResultResponse>? response = null;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var (attempt, code, error) = await GetOwnedActivePlacementAttemptAsync(attemptId, userId, ct);
                    if (attempt is null)
                    {
                        response = ApiResponse<PlacementTestResultResponse>.Fail(error!, code);
                        return;
                    }

                    var snapshots = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, ct);
                    if (snapshots.Count == 0)
                    {
                        response = ApiResponse<PlacementTestResultResponse>.Fail(
                            "Lượt làm bài không có snapshot hợp lệ. Vui lòng bắt đầu lại.", HttpStatusCode.Conflict);
                        return;
                    }
                    if (IsExpired(attempt, snapshots))
                    {
                        response = ApiResponse<PlacementTestResultResponse>.Fail(
                            "Lượt làm bài đã hết thời gian.", HttpStatusCode.BadRequest);
                        return;
                    }

                    var byQuestion = snapshots.ToDictionary(s => s.MockQuestionId);
                    foreach (var input in inputs)
                    {
                        if (!byQuestion.TryGetValue(input.QuestionId, out var snapshot))
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(
                                $"Câu hỏi không thuộc lượt làm bài: {input.QuestionId}.", HttpStatusCode.BadRequest);
                            return;
                        }
                        var validationError = ApplyPlacementAnswer(snapshot, input);
                        if (validationError is not null)
                        {
                            response = ApiResponse<PlacementTestResultResponse>.Fail(validationError, HttpStatusCode.BadRequest);
                            return;
                        }
                    }

                    var correctByQuestion = snapshots.ToDictionary(s => s.MockQuestionId, s => s.IsCorrect);
                    var sections = BuildSectionResults(
                        attemptId, snapshots.Select(s => s.MockQuestion).ToList(), correctByQuestion);
                    await unitOfWork.MockTestSectionResults.AddRangeAsync(sections.Entities, ct);

                    attempt.SubmittedAt = DateTime.UtcNow;
                    attempt.IsCompleted = true;
                    attempt.TotalScore = sections.OverallScore;
                    unitOfWork.MockTestAttempts.Update(attempt);
                    await unitOfWork.SaveChangesAsync(ct);

                    var result = await BuildPlacementResultAsync(attempt, snapshots, ct);

                    // Chỉ placement test hoàn tất hợp lệ mới được set AssessedCertificateLevelId.
                    var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, ct);
                    if (user is not null)
                    {
                        user.PlacementDecision = PlacementDecision.Completed;
                        user.AssessedCertificateLevelId = result.AssessedJlptLevelId;
                        unitOfWork.UserAccounts.Update(user);
                        await unitOfWork.SaveChangesAsync(ct);
                    }

                    response = ApiResponse<PlacementTestResultResponse>.Success(result);
                }, cancellationToken);

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestResultResponse>.Fail(
                    "Không thể chấm điểm bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PlacementTestResultResponse>> GetPlacementTestResultAsync(
            int attemptId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
                if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.Placement)
                    return ApiResponse<PlacementTestResultResponse>.NotFound(
                        $"Không tìm thấy lượt đánh giá năng lực có Id = {attemptId}.");
                if (attempt.UserId != userId)
                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        "Bạn không có quyền xem lượt làm bài này.", HttpStatusCode.Forbidden);
                if (!attempt.IsCompleted)
                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        "Lượt làm bài này chưa được nộp/chấm điểm.", HttpStatusCode.BadRequest);

                var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, cancellationToken);
                if (answers.Count == 0)
                    return ApiResponse<PlacementTestResultResponse>.Fail(
                        "Lượt làm bài không có dữ liệu hợp lệ.", HttpStatusCode.Conflict);

                return ApiResponse<PlacementTestResultResponse>.Success(
                    await BuildPlacementResultAsync(attempt, answers, cancellationToken));
            }
            catch (Exception)
            {
                return ApiResponse<PlacementTestResultResponse>.Fail(
                    "Không thể tải kết quả bài đánh giá năng lực.", HttpStatusCode.InternalServerError);
            }
        }

        // ---------------- Helpers ----------------

        /// <summary>Placement chỉ có câu chọn 1 đáp án: validate option thuộc câu hỏi rồi chấm luôn.</summary>
        private static string? ApplyPlacementAnswer(MockTestAnswer snapshot, PlacementAnswerInput input)
        {
            MockQuestionOption? selected = null;
            if (input.SelectedOptionId is not null)
            {
                selected = (snapshot.MockQuestion.Options ?? [])
                    .FirstOrDefault(o => !o.IsDeleted && o.Id == input.SelectedOptionId);
                if (selected is null)
                    return $"Lựa chọn không thuộc câu hỏi {input.QuestionId}.";
            }

            snapshot.SelectedOptionId = input.SelectedOptionId; // null = bỏ chọn
            snapshot.ResponseJson = null;
            snapshot.IsCorrect = selected?.IsCorrect == true;
            return null;
        }

        /// <summary>Lấy attempt placement đang làm dở của chính learner.</summary>
        private async Task<(MockTestAttempt? Attempt, HttpStatusCode Code, string? Error)> GetOwnedActivePlacementAttemptAsync(
            int attemptId, int userId, CancellationToken ct)
        {
            // Dùng GetByIdAsync (giống GetPlacementTestResultAsync) thay vì GetForSubmissionAsync vì method đó viết cho flow mock test cũ.
            var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, ct);
            if (attempt is null || attempt.IsDeleted)
                return (null, HttpStatusCode.NotFound, $"Không tìm thấy lượt đánh giá năng lực có Id = {attemptId}.");
            if (attempt.AttemptKind != AttemptKind.Placement)
                return (null, HttpStatusCode.NotFound, $"Lượt làm bài Id = {attemptId} không phải lượt đánh giá năng lực.");
            if (attempt.UserId != userId)
                return (null, HttpStatusCode.Forbidden, "Bạn không có quyền thao tác trên lượt làm bài này.");
            if (attempt.IsCompleted)
                return (null, HttpStatusCode.BadRequest, "Lượt làm bài này đã được nộp trước đó.");
            return (attempt, HttpStatusCode.OK, null);
        }

        // Tổng thời gian = tổng TimeLimitMinutes của các đề tham gia attempt; tính từ StartedAt phía server.
        private static int GetTimeLimitMinutes(IEnumerable<MockTestAnswer> answers)
        {
            var total = answers
                .Select(a => a.MockQuestion.MockTestSection.MockTest)
                .DistinctBy(t => t.Id)
                .Sum(t => t.TimeLimitMinutes);
            return total > 0 ? total : DefaultPlacementMinutes;
        }

        private static DateTime ExpiresAt(MockTestAttempt attempt, IEnumerable<MockTestAnswer> answers) =>
            attempt.StartedAt.AddMinutes(GetTimeLimitMinutes(answers));

        private static bool IsExpired(MockTestAttempt attempt, IEnumerable<MockTestAnswer> answers) =>
            DateTime.UtcNow > ExpiresAt(attempt, answers);

        // NotTaken -> "NOT_TAKEN", Skipped -> "SKIPPED"...
        private static string ToDecisionCode(PlacementDecision decision) =>
            System.Text.RegularExpressions.Regex.Replace(decision.ToString(), "(?<!^)([A-Z])", "_$1").ToUpperInvariant();

        private static int LevelIdOf(MockTestAnswer answer) =>
            answer.MockQuestion.MockTestSection.MockTest.CertificateLevelId;

        private async Task<Dictionary<int, LevelInfo>> LoadLevelsAsync(
            IEnumerable<int> levelIds, CancellationToken ct)
        {
            var ids = levelIds.ToHashSet();
            if (ids.Count == 0) return [];
            var levels = await unitOfWork.CertificateLevels.FindAsync(l => ids.Contains(l.Id), ct);
            return levels.ToDictionary(l => l.Id, l => new LevelInfo(l.Id, l.CertificateTypeId, l.Code, l.SortOrder));
        }

        private async Task<PlacementAttemptSnapshotResponse> BuildSnapshotAsync(
            MockTestAttempt attempt, IReadOnlyList<MockTestAnswer> answers, bool isResumed, CancellationToken ct)
        {
            var levels = await LoadLevelsAsync(answers.Select(LevelIdOf), ct);
            var limit = GetTimeLimitMinutes(answers);

            var ordered = answers
                .OrderBy(a => levels.GetValueOrDefault(LevelIdOf(a))?.SortOrder ?? int.MaxValue)
                .ThenBy(a => a.MockQuestion.MockTestSection.SortOrder)
                .ThenBy(a => a.MockQuestion.MockTestSection.Id)
                .ThenBy(a => a.MockQuestion.SortOrder)
                .ThenBy(a => a.MockQuestion.Id)
                .ToList();

            var order = 0;
            return new PlacementAttemptSnapshotResponse
            {
                AttemptId = attempt.Id,
                AttemptType = PlacementAttemptType,
                Status = "inProgress",
                IsResumed = isResumed,
                StartedAt = attempt.StartedAt,
                ExpiresAt = attempt.StartedAt.AddMinutes(limit),
                ServerNow = DateTime.UtcNow,
                TimeLimitMinutes = limit,
                TotalQuestions = ordered.Count,
                Questions = ordered.Select(a =>
                {
                    var question = a.MockQuestion;
                    var section = question.MockTestSection;
                    var levelId = LevelIdOf(a);
                    return new PlacementTestQuestionResponse
                    {
                        QuestionId = question.Id,
                        MockTestId = section.MockTestId,
                        MockTestSectionId = section.Id,
                        SectionTitle = section.Title,
                        SectionOrder = section.SortOrder,
                        JlptLevelId = levelId,
                        JlptLevelName = levels.GetValueOrDefault(levelId)?.Name ?? string.Empty,
                        Order = ++order,
                        Content = question.QuestionText,
                        SelectedOptionId = a.SelectedOptionId, // đáp án đã autosave để FE khôi phục
                        AudioUrl = question.AudioUrl,
                        ImageUrl = question.ImageUrl,
                        Options = (question.Options ?? [])
                            .Where(o => !o.IsDeleted)
                            .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                            .Select(o => new PlacementTestOptionResponse
                            {
                                OptionId = o.Id,
                                Content = o.OptionText,
                                Order = o.SortOrder,
                            }).ToList(),
                    };
                }).ToList(),
            };
        }

        /// <summary>
        /// Dựng kết quả placement từ các answer đã chấm. Trình độ hiện tại = level khó nhất đạt >= PlacementPassPercent
        /// (không level nào đạt thì lấy level dễ nhất); mục tiêu đề xuất = level liền trên đó.
        /// Giả định LevelInfo.SortOrder tăng dần theo độ khó (N5 -> N2).
        /// </summary>
        private async Task<PlacementTestResultResponse> BuildPlacementResultAsync(
            MockTestAttempt attempt, IReadOnlyList<MockTestAnswer> answers, CancellationToken ct)
        {
            var levels = await LoadLevelsAsync(answers.Select(LevelIdOf), ct);

            var breakdowns = answers
                .GroupBy(LevelIdOf)
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(a => a.IsCorrect);
                    return new PlacementLevelBreakdownItem
                    {
                        JlptLevelId = g.Key,
                        JlptLevelName = levels.GetValueOrDefault(g.Key)?.Name ?? string.Empty,
                        CorrectCount = correct,
                        TotalCount = total,
                        ScorePercent = total == 0 ? 0d : Math.Round(correct * 100d / total, 2),
                    };
                })
                .OrderBy(b => levels.GetValueOrDefault(b.JlptLevelId)?.SortOrder ?? int.MaxValue)
                .ToList();

            var assessedIndex = breakdowns.FindLastIndex(b => b.ScorePercent >= PlacementPassPercent);
            if (assessedIndex < 0) assessedIndex = 0;
            var assessed = breakdowns[assessedIndex];
            var recommended = breakdowns[Math.Min(assessedIndex + 1, breakdowns.Count - 1)];

            var correctByQuestion = answers.ToDictionary(a => a.MockQuestionId, a => a.IsCorrect);
            var sections = BuildSectionResults(
                attempt.Id, answers.Select(a => a.MockQuestion).ToList(), correctByQuestion);
            var endTime = attempt.SubmittedAt ?? DateTime.UtcNow;

            return new PlacementTestResultResponse
            {
                AttemptId = attempt.Id,
                AttemptType = PlacementAttemptType,
                Status = attempt.IsCompleted ? StatusCompleted : StatusInProgress,
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt,
                DurationSeconds = Math.Max(0, (int)(endTime - attempt.StartedAt).TotalSeconds),
                TotalScore = sections.OverallScore,
                CorrectCount = answers.Count(a => a.IsCorrect),
                TotalQuestions = answers.Count,
                AssessedJlptLevelId = assessed.JlptLevelId,
                AssessedJlptLevelName = assessed.JlptLevelName,
                RecommendedTargetJlptLevelId = recommended.JlptLevelId,
                RecommendedTargetJlptLevelName = recommended.JlptLevelName,
                WrongQuestionIds = answers.Where(a => !a.IsCorrect).Select(a => a.MockQuestionId).OrderBy(id => id).ToList(),
                LevelBreakdowns = breakdowns,
                Sections = sections.Responses,
            };
        }

        private static (List<MockTestSectionResult> Entities, List<SectionResultItem> Responses, double OverallScore)
            BuildSectionResults(
                int attemptId,
                List<MockQuestion> questions,
                Dictionary<int, bool> correctByQuestion)
        {
            var grouped = questions
                .GroupBy(q => q.MockTestSection)
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q => correctByQuestion[q.Id]);
                    var score = total == 0 ? 0d : Math.Round(correct * 100d / total, 2);
                    return (Section: g.Key, Correct: correct, Total: total, Score: score);
                })
                .ToList();

            var entities = grouped.Select(g => new MockTestSectionResult
            {
                MockTestAttemptId = attemptId,
                MockTestSectionId = g.Section.Id,
                CorrectCount = g.Correct,
                TotalCount = g.Total,
                ScorePercent = g.Score,
            }).ToList();

            var responses = grouped.Select(g => new SectionResultItem
            {
                MockTestSectionId = g.Section.Id,
                SectionTitle = g.Section.Title,
                LanguageSkillName = g.Section.LanguageSkill.Name,
                CorrectCount = g.Correct,
                TotalCount = g.Total,
                Score = g.Score,
            }).ToList();

            var totalCorrect = grouped.Sum(g => g.Correct);
            var totalCount = grouped.Sum(g => g.Total);
            var overallScore = totalCount == 0 ? 0d : Math.Round(totalCorrect * 100d / totalCount, 2);

            return (entities, responses, overallScore);
        }
    }
}