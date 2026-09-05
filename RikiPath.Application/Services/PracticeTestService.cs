using Domain.Entities;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeTests;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: file này THAY THẾ PracticeTestService cũ — 3 method đầu (StartAttemptAsync,
    // SubmitAttemptAsync, GetAttemptResultAsync) giữ nguyên y hệt bản trước, chỉ thêm
    // GetTestsByLevelAsync + GetDetailedResultAsync ở cuối cho Module 1.5.
    public class PracticeTestService(IUnitOfWork unitOfWork) : IPracticeTestService
    {
        private const string StatusInProgress = "in_progress";
        private const string StatusCompleted = "completed";

        public async Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(
            int userId, int practiceTestId, CancellationToken cancellationToken)
        {
            try
            {
                var test = await unitOfWork.PracticeTests.GetByIdAsync(practiceTestId);
                if (test is null)
                    return ApiResponse<StartAttemptResponse>.NotFound(
                        $"Không tìm thấy PracticeTest có Id = {practiceTestId}.");

                var attempt = new PracticeTestAttempt
                {
                    UserId = userId,
                    PracticeTestId = practiceTestId,
                    StartedAt = DateTime.UtcNow,
                    SubmittedAt = null,
                    TotalScore = 0,
                    IsCompleted = false,
                };
                await unitOfWork.PracticeTestAttempts.AddAsync(attempt);
                await unitOfWork.SaveChangesAsync();

                var result = new StartAttemptResponse
                {
                    AttemptId = attempt.Id,
                    PracticeTestId = practiceTestId,
                    StartedAt = attempt.StartedAt,
                    TimeLimitMinutes = test.TimeLimitMinutes,
                };

                return ApiResponse<StartAttemptResponse>.Created(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<StartAttemptResponse>.Fail(
                    "Không thể bắt đầu lượt làm bài.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(
            int userId, int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var attempt = await unitOfWork.PracticeTestAttempts.GetByIdAsync(attemptId);
                if (attempt is null)
                    return ApiResponse<AttemptResultResponse>.NotFound(
                        $"Không tìm thấy PracticeTestAttempt có Id = {attemptId}.");

                if (attempt.UserId != userId)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Bạn không có quyền nộp bài cho lượt làm bài này.", HttpStatusCode.Forbidden);

                if (attempt.IsCompleted)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Lượt làm bài này đã được nộp trước đó.", HttpStatusCode.BadRequest);

                if (request.Answers is null || request.Answers.Count == 0)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Danh sách câu trả lời không được để trống.", HttpStatusCode.BadRequest);

                var questionIds = request.Answers.Select(a => a.QuestionId).Distinct().ToList();

                var questions = await unitOfWork.PracticeQuestions.GetByIdsWithOptionsAsync(questionIds);

                var missingIds = questionIds.Except(questions.Select(q => q.Id)).ToList();
                if (missingIds.Count > 0)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        $"Câu hỏi không hợp lệ: {string.Join(", ", missingIds)}.", HttpStatusCode.BadRequest);

                var questionsById = questions.ToDictionary(q => q.Id);

                var (answers, correctByQuestion) = GradeAnswers(attemptId, request.Answers, questionsById);
                foreach (var answer in answers)
                    await unitOfWork.PracticeTestAnswers.AddAsync(answer);

                var sections = BuildSectionResults(attemptId, questions, correctByQuestion);
                foreach (var section in sections.Entities)
                    await unitOfWork.PracticeTestSectionResults.AddAsync(section);

                attempt.SubmittedAt = DateTime.UtcNow;
                attempt.IsCompleted = true;
                attempt.TotalScore = sections.OverallScore;
                unitOfWork.PracticeTestAttempts.Update(attempt);

                await unitOfWork.SaveChangesAsync();

                var result = new AttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    PracticeTestId = attempt.PracticeTestId,
                    Status = attempt.IsCompleted ? StatusCompleted : StatusInProgress,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    TotalScore = attempt.TotalScore.Value,
                    Sections = sections.Responses,
                };

                return ApiResponse<AttemptResultResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<AttemptResultResponse>.Fail(
                    "Không thể chấm điểm lượt làm bài.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(
            int userId, int attemptId, CancellationToken cancellationToken)
        {
            try
            {
                var attempt = await unitOfWork.PracticeTestAttempts.GetByIdAsync(attemptId);
                if (attempt is null)
                    return ApiResponse<AttemptResultResponse>.NotFound(
                        $"Không tìm thấy PracticeTestAttempt có Id = {attemptId}.");

                if (attempt.UserId != userId)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Bạn không có quyền xem lượt làm bài này.", HttpStatusCode.Forbidden);

                if (!attempt.IsCompleted)
                    return ApiResponse<AttemptResultResponse>.Fail(
                        "Lượt làm bài này chưa được nộp/chấm điểm.", HttpStatusCode.BadRequest);

                var sectionResults = await unitOfWork.PracticeTestSectionResults.GetByAttemptIdAsync(attemptId);

                var result = new AttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    PracticeTestId = attempt.PracticeTestId,
                    Status = attempt.IsCompleted ? StatusCompleted : StatusInProgress,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    TotalScore = attempt.TotalScore.Value,
                    Sections = sectionResults.Select(r => new SectionResultItem
                    {
                        PracticeTestSectionId = r.PracticeTestSectionId,
                        SectionTitle = r.PracticeTestSection.Title,
                        SkillName = r.PracticeTestSection.Skill.Name,
                        CorrectCount = r.CorrectCount,
                        TotalCount = r.TotalCount,
                        Score = r.ScorePercent,
                    }).ToList(),
                };

                return ApiResponse<AttemptResultResponse>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<AttemptResultResponse>.Fail(
                    "Không thể tải kết quả lượt làm bài.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        // ---------------- Module 1.5 additions ----------------

        public async Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(
            int jlptLevelId, CancellationToken cancellationToken)
        {
            try
            {
                // NOTE: cần IPracticeTestRepository.GetByLevelAsync(jlptLevelId), Include(JlptLevel).
                var tests = await unitOfWork.PracticeTests.GetByLevelAsync(jlptLevelId);

                var result = tests.Select(t => new TestSummaryResponse
                {
                    PracticeTestId = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    JlptLevelName = t.JlptLevel?.Name ?? string.Empty,
                    TimeLimitMinutes = t.TimeLimitMinutes,
                }).ToList();

                return ApiResponse<List<TestSummaryResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail(
                    "Không thể tải danh sách đề thi.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(
            int userId, int attemptId, CancellationToken cancellationToken)
        {
            try
            {
                var attempt = await unitOfWork.PracticeTestAttempts.GetByIdAsync(attemptId);
                if (attempt is null)
                    return ApiResponse<DetailedAttemptResultResponse>.NotFound(
                        $"Không tìm thấy PracticeTestAttempt có Id = {attemptId}.");

                if (attempt.UserId != userId)
                    return ApiResponse<DetailedAttemptResultResponse>.Fail(
                        "Bạn không có quyền xem lượt làm bài này.", HttpStatusCode.Forbidden);

                if (!attempt.IsCompleted)
                    return ApiResponse<DetailedAttemptResultResponse>.Fail(
                        "Lượt làm bài này chưa được nộp/chấm điểm.", HttpStatusCode.BadRequest);

                // NOTE: cần IPracticeTestAnswerRepository.GetByAttemptIdAsync(attemptId),
                // Include(Question.Options) — trả về từng câu kèm option đã chọn + đáp án đúng.
                var answers = await unitOfWork.PracticeTestAnswers.GetByAttemptIdAsync(attemptId);


                var questions = answers.Select(a => new QuestionReviewItem
                {
                    QuestionId = a.PracticeQuestionId,
                    QuestionText = a.PracticeQuestion.QuestionText,
                    Explanation = a.PracticeQuestion.Explanation,
                    SelectedOptionId = a.SelectedOptionId,
                    IsCorrect = a.IsCorrect,
                    Options = a.PracticeQuestion.Options.Select(o => new QuestionOptionReview
                    {
                        OptionId = o.Id,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect,
                    }).ToList(),
                }).ToList();

                return ApiResponse<DetailedAttemptResultResponse>.Success(new DetailedAttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    PracticeTestId = attempt.PracticeTestId,
                    TotalScore = attempt.TotalScore.Value,
                    Questions = questions,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<DetailedAttemptResultResponse>.Fail(
                    "Không thể tải kết quả chi tiết.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        // ---------------- helpers (giữ nguyên từ bản trước) ----------------

        private static (List<PracticeTestAnswer> Answers, Dictionary<int, bool> CorrectByQuestion) GradeAnswers(
            int attemptId,
            List<AnswerSubmissionItem> submitted,
            Dictionary<int, PracticeQuestion> questionsById)
        {
            var answers = new List<PracticeTestAnswer>();
            var correctByQuestion = new Dictionary<int, bool>();

            foreach (var item in submitted)
            {
                var question = questionsById[item.QuestionId];
                var selectedOption = item.SelectedOptionId is null
                    ? null
                    : question.Options.FirstOrDefault(o => o.Id == item.SelectedOptionId);

                var isCorrect = selectedOption is not null && selectedOption.IsCorrect;
                correctByQuestion[item.QuestionId] = isCorrect;

                answers.Add(new PracticeTestAnswer
                {
                    PracticeTestAttemptId = attemptId,
                    PracticeQuestionId = item.QuestionId,
                    SelectedOptionId = item.SelectedOptionId,
                    IsCorrect = isCorrect,
                });
            }

            return (answers, correctByQuestion);
        }

        private static (List<PracticeTestSectionResult> Entities, List<SectionResultItem> Responses, double OverallScore)
            BuildSectionResults(
                int attemptId,
                List<PracticeQuestion> questions,
                Dictionary<int, bool> correctByQuestion)
        {
            var grouped = questions
                .GroupBy(q => q.PracticeTestSection)
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q => correctByQuestion[q.Id]);
                    var score = total == 0 ? 0d : Math.Round(correct * 100d / total, 2);
                    return (Section: g.Key, Correct: correct, Total: total, Score: score);
                })
                .ToList();

            var entities = grouped.Select(g => new PracticeTestSectionResult
            {
                PracticeTestAttemptId = attemptId,
                PracticeTestSectionId = g.Section.Id,
                CorrectCount = g.Correct,
                TotalCount = g.Total,
                ScorePercent = g.Score,
            }).ToList();

            var responses = grouped.Select(g => new SectionResultItem
            {
                PracticeTestSectionId = g.Section.Id,
                SectionTitle = g.Section.Title,
                SkillName = g.Section.Skill.Name,
                CorrectCount = g.Correct,
                TotalCount = g.Total,
                Score = g.Score,
            }).ToList();

            var totalCorrect = grouped.Sum(g => g.Correct);
            var totalCount = grouped.Sum(g => g.Total);
            var overallScore = totalCount == 0 ? 0d : Math.Round(totalCorrect * 100d / totalCount, 2);

            return (entities, responses, overallScore);
        }

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
