using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using System.Net;

namespace RikiPath.Application.Services
{
    public class MockTestService(IUnitOfWork unitOfWork, IClaimService claimService) : IMockTestService
    {
        private const string StatusInProgress = "in_progress";
        private const string StatusCompleted = "completed";

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
                        AttemptKind = AttemptKind.MockTest,
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
                        var selectedOption = submittedAnswer.SelectedOptionId is null
                            ? null
                            : snapshot.MockQuestion.Options?.Single(
                                option => option.Id == submittedAnswer.SelectedOptionId);
                        snapshot.SelectedOptionId = submittedAnswer.SelectedOptionId;
                        snapshot.IsCorrect = selectedOption?.IsCorrect == true;
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
                    a.UserId == userId && a.IsCompleted && !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest && (!practiceTestId.HasValue), cancellationToken))
                    .Where(a => !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest)
                    .ToList();

                var attemptIds = attempts.Select(a => a.Id).ToList();
                if (attemptIds.Count == 0)
                {
                    return ApiResponse<List<QuestionReviewCard>>.Success([]);
                }

                // 2. Lấy tất cả câu trả lời của Learner trong các lượt làm bài đó
                var pastAnswers = await unitOfWork.MockTestAnswers.FindAsync(ans => attemptIds.Contains(ans.MockTestAttemptId), cancellationToken);

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

        // ---------------- Helpers ----------------

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
