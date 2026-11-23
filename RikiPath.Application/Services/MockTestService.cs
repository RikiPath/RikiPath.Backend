using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using System.Net;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RikiPath.Application.Services
{
    public class MockTestService(IUnitOfWork unitOfWork, IClaimService claimService) : IMockTestService
    {
        private const string StatusInProgress = "in_progress";
        private const string StatusCompleted = "completed";
        private static readonly JsonSerializerOptions MockSnapshotJson = new(JsonSerializerDefaults.Web);

        private static bool IsSingleChoiceQuestion(string type) =>
            JlptMockQuestionFormats.IsSingleChoice(type);

        private sealed class MockAttemptSnapshot
        {
            public MockAttemptSnapshot() { }
            public int MockTestId { get; set; }
            public string? Title { get; set; }
            public string? JlptLevelName { get; set; }
            public List<int> PreviouslyWrongQuestionIds { get; set; } = [];
            public List<MockQuestionSnapshot> Questions { get; set; } = [];
        }

        private sealed class MockQuestionSnapshot
        {
            public MockQuestionSnapshot() { }
            public StartAttemptQuestionResponse Prompt { get; set; } = new();
            public List<int> CorrectOptionIds { get; set; } = [];
            public string? AnswerKeyJson { get; set; }
            public string? Explanation { get; set; }
        }

        private static MockAttemptSnapshot? ReadMockSnapshot(MockTestAttempt attempt)
        {
            if (string.IsNullOrWhiteSpace(attempt.QuestionSnapshotJson)) return null;
            var snapshot = JsonSerializer.Deserialize<MockAttemptSnapshot>(attempt.QuestionSnapshotJson, MockSnapshotJson);
            if (snapshot?.Questions is { Count: > 0 } && snapshot.Questions.All(question => question.Prompt.QuestionId > 0))
                return snapshot;
            // Older attempts stored the client response directly, without an answer key.
            if (JsonSerializer.Deserialize<StartAttemptResponse>(attempt.QuestionSnapshotJson, MockSnapshotJson)
                is { AttemptId: > 0, Questions.Count: > 0 } legacy && legacy.AttemptId == attempt.Id)
                return null;
            throw new JsonException("Dữ liệu đề thi đã lưu không hợp lệ.");
        }

        private static Dictionary<int, MockQuestionSnapshot>? ReadFrozenQuestions(
            MockTestAttempt attempt, IEnumerable<MockTestAnswer> answers)
        {
            var snapshot = ReadMockSnapshot(attempt);
            if (snapshot is null) return null; // Lượt thi cũ chưa có bản chụp nội dung.
            var questions = snapshot.Questions.ToDictionary(item => item.Prompt.QuestionId);
            if (!questions.Keys.ToHashSet().SetEquals(answers.Select(answer => answer.MockQuestionId)))
                throw new JsonException("Bản chụp đề thi không khớp các câu hỏi của lượt thi.");
            return questions;
        }

        private static bool IsFrozenAnswerCorrect(
            MockQuestionSnapshot question, int? selectedOptionId, JsonElement? response)
        {
            if (IsSingleChoiceQuestion(question.Prompt.QuestionType))
                return selectedOptionId.HasValue && question.CorrectOptionIds.Contains(selectedOptionId.Value);
            if (question.Prompt.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase))
            {
                if (response is not { ValueKind: JsonValueKind.Array }) return false;
                var submitted = response.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _))
                    .Select(item => item.GetInt32()).Distinct().OrderBy(id => id).ToArray();
                return submitted.Length == response.Value.GetArrayLength() &&
                    submitted.SequenceEqual(question.CorrectOptionIds.OrderBy(id => id));
            }
            return IsStructuredResponseCorrect(question.AnswerKeyJson, question.Prompt.InteractionData, response);
        }

        private static JsonElement? ParseJsonElement(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        private static string? ResolvePassageText(string? passageText, JsonElement? interactionData)
        {
            if (!string.IsNullOrWhiteSpace(passageText)) return passageText;
            if (interactionData is not { ValueKind: JsonValueKind.Object } obj) return null;
            var parts = new List<string>(2);
            foreach (var key in new[] { "passage", "secondPassage" })
            {
                if (obj.TryGetProperty(key, out var node) && node.ValueKind == JsonValueKind.String)
                {
                    var text = node.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text)) parts.Add(text);
                }
            }
            return parts.Count > 0 ? string.Join("\n\n---\n\n", parts) : null;
        }

        private static string PublicOptionText(MockQuestionOption option, string? interactionData, int index)
        {
            if (JlptMockQuestionFormats.HasAudioOptions(ParseJsonElement(interactionData)))
                return (index + 1).ToString(CultureInfo.InvariantCulture);
            return RemoveAnswerSuffix(option.OptionText);
        }

        private static string RemoveAnswerSuffix(string text)
        {
            foreach (var suffix in new[] { " (Chính xác)", " (Correct)" })
                if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return text[..^suffix.Length];
            return text;
        }

        private static StartAttemptQuestionResponse SanitizeExamPrompt(StartAttemptQuestionResponse prompt)
        {
            foreach (var option in prompt.Options)
                option.Content = RemoveAnswerSuffix(option.Content);
            return prompt;
        }

        private static bool HasAnswerMetadata(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Array)
                return node.EnumerateArray().Any(HasAnswerMetadata);
            if (node.ValueKind != JsonValueKind.Object) return false;
            foreach (var property in node.EnumerateObject())
            {
                if (property.Name.Contains("answer", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("correct", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("explanation", StringComparison.OrdinalIgnoreCase) ||
                    HasAnswerMetadata(property.Value)) return true;
            }
            return false;
        }

        private static bool IsLearnerReady(MockTest test)
        {
            if (test.IsDeleted || test.TimeLimitMinutes <= 0 || test.CertificateLevel is null ||
                test.CertificateLevel.IsDeleted || JlptCurriculum.Rank(test.CertificateLevel.Code) == 0)
                return false;

            var sections = (test.Sections ?? []).Where(section => !section.IsDeleted).ToList();
            if (sections.Count == 0) return false;
            foreach (var section in sections)
            {
                var questions = (section.Questions ?? []).Where(question => !question.IsDeleted).ToList();
                if (questions.Count == 0) return false;
                foreach (var question in questions)
                {
                    // Legacy sample rows announce the answer in their option text. Never serve them to learners.
                    if (string.IsNullOrWhiteSpace(question.QuestionText) ||
                        question.QuestionText.Contains("Câu hỏi mẫu #", StringComparison.OrdinalIgnoreCase) ||
                        question.QuestionText.Contains("(Chính xác)", StringComparison.OrdinalIgnoreCase) ||
                        (JlptSkillAssessmentPolicy.Classify(section.LanguageSkill?.Name ?? section.Title) == JlptStudySkill.Listening &&
                         string.IsNullOrWhiteSpace(question.AudioUrl ?? question.Passage?.AudioUrl)))
                        return false;

                    if (!string.IsNullOrWhiteSpace(question.InteractionDataJson))
                    {
                        try
                        {
                            using var metadata = JsonDocument.Parse(question.InteractionDataJson);
                            if (HasAnswerMetadata(metadata.RootElement)) return false;
                        }
                        catch (JsonException)
                        {
                            return false;
                        }
                    }

                    if (!IsSingleChoiceQuestion(question.QuestionType) &&
                        !question.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(question.AnswerKeyJson)) return false;
                        continue;
                    }

                    var options = (question.Options ?? []).Where(option => !option.IsDeleted).ToList();
                    if (options.Count < 2 || options.Any(option =>
                            string.IsNullOrWhiteSpace(option.OptionText) ||
                            option.OptionText.Contains("(Chính xác)", StringComparison.OrdinalIgnoreCase) ||
                            option.OptionText.Contains("(Correct)", StringComparison.OrdinalIgnoreCase)) ||
                        options.Select(option => option.OptionText.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count ||
                        (IsSingleChoiceQuestion(question.QuestionType)
                            ? options.Count(option => option.IsCorrect) != 1
                            : !options.Any(option => option.IsCorrect)))
                        return false;
                }
            }
            return true;
        }

        private static TestSummaryResponse ToTestSummary(MockTest test)
        {
            List<string> includedSkills = [];
            if (!string.IsNullOrWhiteSpace(test.IncludedSkillsJson))
            {
                try { includedSkills = JsonSerializer.Deserialize<List<string>>(test.IncludedSkillsJson, MockSnapshotJson) ?? []; }
                catch (JsonException) { }
            }
            if (includedSkills.Count == 0)
            {
                includedSkills = (test.Sections ?? []).Where(s => !s.IsDeleted)
                    .Select(s => s.LanguageSkill?.Name ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return new TestSummaryResponse
            {
                MockTestId = test.Id,
                CertificateLevelId = test.CertificateLevelId,
                Title = test.Title,
                Description = test.Description ?? string.Empty,
                JlptLevelName = test.CertificateLevel?.Code ?? string.Empty,
                ExamKind = test.ExamKind.ToString(),
                Version = test.Version,
                TimeLimitMinutes = test.TimeLimitMinutes,
                IncludedSkills = includedSkills,
                Sections = (test.Sections ?? []).Where(section => !section.IsDeleted)
                    .OrderBy(section => section.SortOrder).ThenBy(section => section.Id)
                    .Select(section => new TestSectionSummaryResponse
                    {
                        Title = section.Title,
                        SkillName = section.LanguageSkill?.Name ?? string.Empty,
                        DurationMinutes = section.DurationMinutes,
                        QuestionCount = (section.Questions ?? []).Count(question => !question.IsDeleted),
                        RequiresAudio = (section.Questions ?? []).Any(question => !question.IsDeleted &&
                            !string.IsNullOrWhiteSpace(question.AudioUrl ?? question.Passage?.AudioUrl)),
                    }).ToList(),
                QuestionCount = (test.Sections ?? []).Where(section => !section.IsDeleted)
                    .Sum(section => (section.Questions ?? []).Count(question => !question.IsDeleted)),
            };
        }

        private async Task<bool> IsApprovedForLearnersAsync(MockTest test, CancellationToken cancellationToken)
        {
            if (test.IsDeleted || test.IsArchivedVersion) return false;
            if (ContentAuthoring.JlptBlueprintPolicy.IsRightsBlockedForLegacyOrManaged(test.RightsStatus, test.IsManagedWorkflow))
                return false;
            if (test.PublishStatus is ContentStatus.PendingReview or ContentStatus.Rejected or ContentStatus.ChangesRequested or ContentStatus.Unpublished)
                return false;
            if (test.IsManagedWorkflow)
                return test.PublishStatus == ContentStatus.Published;
            if (test.PublishStatus == ContentStatus.Published)
                return true;

            return await unitOfWork.ContentApprovalRequests.Query().AsNoTracking().AnyAsync(request =>
                !request.IsDeleted && request.EntityType == ImportEntityType.MockTest &&
                request.Status == ContentApprovalStatus.Approved &&
                (request.TargetEntityId == test.Id ||
                 (request.TargetEntityId == null && request.RowKey == test.Title + " / " + test.CertificateLevel.Code)),
                cancellationToken);
        }

        private static bool IsStructuredResponseCorrect(MockQuestion question, JsonElement? response) =>
            IsStructuredResponseCorrect(question.AnswerKeyJson, ParseJsonElement(question.InteractionDataJson), response);

        private static bool IsStructuredResponseCorrect(string? answerKey, JsonElement? data, JsonElement? response)
        {
            if (response is null || string.IsNullOrWhiteSpace(answerKey)) return false;
            try
            {
                if (JlptMockQuestionFormats.IsSentenceComposition(data))
                {
                    return JlptMockQuestionFormats.StarPosition(data) is >= 1 and <= 4 and var position &&
                        JlptMockQuestionFormats.IsValidSentenceResponse(data, response) &&
                        SentenceOrdering.TryReadSequence(response, out var submitted) && submitted.Length == 4 &&
                        SentenceOrdering.TryReadSequence(ParseJsonElement(answerKey), out var correct) && correct.Length == 4 &&
                        submitted[position - 1] == correct[position - 1];
                }
                if (SentenceOrdering.TryReadSequence(ParseJsonElement(answerKey), out var expectedOrder) &&
                    SentenceOrdering.TryReadSequence(response, out var submittedOrder))
                {
                    return expectedOrder.SequenceEqual(submittedOrder);
                }

                var keyRoot = ParseJsonElement(answerKey);
                if (keyRoot is { ValueKind: JsonValueKind.Object } keyObj &&
                    keyObj.TryGetProperty("acceptedAnswers", out var acceptedArr) &&
                    acceptedArr.ValueKind == JsonValueKind.Array)
                {
                    var submittedText = response.Value.ValueKind == JsonValueKind.String
                        ? response.Value.GetString()
                        : (response.Value.ValueKind == JsonValueKind.Array && response.Value.GetArrayLength() == 1 && response.Value[0].ValueKind == JsonValueKind.String
                            ? response.Value[0].GetString()
                            : (response.Value.ValueKind == JsonValueKind.Object && response.Value.TryGetProperty("text", out var tEl)
                                ? tEl.GetString()
                                : (response.Value.ValueKind == JsonValueKind.Object && response.Value.TryGetProperty("answer", out var aEl) ? aEl.GetString() : null)));
                    if (string.IsNullOrWhiteSpace(submittedText)) return false;

                    var caseSensitive = data is { ValueKind: JsonValueKind.Object } dObj &&
                        dObj.TryGetProperty("caseSensitive", out var cs) && cs.ValueKind == JsonValueKind.True;
                    var normSubmitted = NormalizeFillBlankText(submittedText, caseSensitive);

                    foreach (var ansEl in acceptedArr.EnumerateArray())
                    {
                        var ans = ansEl.GetString();
                        if (!string.IsNullOrWhiteSpace(ans) && NormalizeFillBlankText(ans, caseSensitive) == normSubmitted)
                            return true;
                    }
                    return false;
                }

                return JsonNode.DeepEquals(JsonNode.Parse(answerKey), JsonNode.Parse(response.Value.GetRawText()));
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string NormalizeFillBlankText(string input, bool caseSensitive)
        {
            var collapsed = string.Join(" ", input.Replace('\u3000', ' ').Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return caseSensitive ? collapsed : collapsed.ToLowerInvariant();
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
                if (!IsLearnerReady(test) || !await IsApprovedForLearnersAsync(test, cancellationToken))
                    return ApiResponse<MockTestDetailResponse>.NotFound(
                        "Bài đánh giá năng lực chưa được duyệt nội dung.");

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
                                        .Select((option, index) => new MockQuestionOptionResponse
                                        {
                                            OptionId = option.Id,
                                            OptionText = PublicOptionText(option, question.InteractionDataJson, index),
                                            ImageUrl = option.ImageUrl,
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
                if (test is null || test.IsDeleted ||
                    (test.CertificateLevel is { } level && (level.IsDeleted || JlptCurriculum.Rank(level.Code) == 0)))
                    return ApiResponse<StartAttemptResponse>.NotFound(
                        $"Không tìm thấy MockTest có Id = {practiceTestId}.");

                if (!IsLearnerReady(test) || !await IsApprovedForLearnersAsync(test, cancellationToken))
                    return ApiResponse<StartAttemptResponse>.Fail(
                        "Đề thi chưa đạt điều kiện nội dung để bắt đầu.", HttpStatusCode.Conflict);

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
                if (test.TimeLimitMinutes <= 0)
                    return ApiResponse<StartAttemptResponse>.Fail(
                        "Đề thi chưa có thời lượng hợp lệ.", HttpStatusCode.BadRequest);

                var frozenQuestions = orderedQuestions.Select(item => new MockQuestionSnapshot
                {
                    Prompt = new StartAttemptQuestionResponse
                    {
                        QuestionId = item.Question.Id,
                        MockTestSectionId = item.Section.Id,
                        SectionTitle = item.Section.Title,
                        SectionOrder = item.Section.SortOrder,
                        Order = item.Question.SortOrder,
                        Content = item.Question.QuestionText,
                        QuestionType = item.Question.QuestionType,
                        InteractionData = ParseJsonElement(item.Question.InteractionDataJson),
                        AudioUrl = item.Question.AudioUrl ?? item.Question.Passage?.AudioUrl,
                        ImageUrl = item.Question.ImageUrl,
                        PassageId = item.Question.PassageId,
                        PassageTitle = item.Question.Passage?.Title,
                        PassageText = ResolvePassageText(item.Question.Passage?.PassageText, ParseJsonElement(item.Question.InteractionDataJson)),
                        PassageInstructions = item.Question.Passage?.Instructions,
                        PassageImageUrl = item.Question.Passage?.ImageUrl,
                        TemplateKind = item.Question.TemplateKind,
                        PlaybackPolicy = TryDeserializePlaybackPolicy(item.Question.PlaybackPolicyJson ?? item.Question.Passage?.PlaybackPolicyJson),
                        Options = (item.Question.Options ?? [])
                            .Where(option => !option.IsDeleted)
                            .OrderBy(option => option.SortOrder).ThenBy(option => option.Id)
                            .Select((option, index) => new StartAttemptOptionResponse
                            {
                                OptionId = option.Id,
                                Content = PublicOptionText(option, item.Question.InteractionDataJson, index),
                                ImageUrl = option.ImageUrl,
                                Order = option.SortOrder,
                            }).ToList(),
                    },
                    CorrectOptionIds = (item.Question.Options ?? [])
                        .Where(option => !option.IsDeleted && option.IsCorrect)
                        .Select(option => option.Id).ToList(),
                    AnswerKeyJson = item.Question.AnswerKeyJson,
                    Explanation = item.Question.Explanation,
                }).ToList();

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
                    await unitOfWork.MockTestAttempts.LockMockStartAsync(userId, transactionCancellationToken);
                    var active = await GetActiveMockAttemptAsync(practiceTestId, transactionCancellationToken);
                    if (!active.IsSuccess)
                    {
                        response = ApiResponse<StartAttemptResponse>.Fail(
                            "Không thể kiểm tra lượt thi đang làm.", HttpStatusCode.InternalServerError);
                        return;
                    }
                    if (active.Result is not null)
                    {
                        response = ApiResponse<StartAttemptResponse>.Success(active.Result);
                        return;
                    }

                    var startedAt = DateTime.UtcNow;
                    var attempt = new MockTestAttempt
                    {
                        UserId = userId,
                        AttemptKind = AttemptKind.MockTest,
                        StartedAt = startedAt,
                        ExpiresAt = test.TimeLimitMinutes > 0 ? startedAt.AddMinutes(test.TimeLimitMinutes) : null,
                        QuestionSnapshotJson = JsonSerializer.Serialize(new MockAttemptSnapshot
                        {
                            MockTestId = practiceTestId,
                            Title = test.Title,
                            JlptLevelName = test.CertificateLevel?.Code,
                            PreviouslyWrongQuestionIds = previouslyWrongQuestionIds,
                            Questions = frozenQuestions,
                        }),
                        SubmittedAt = null,
                        TotalScore = 0,
                        IsCompleted = false,
                    };

                    await unitOfWork.MockTestAttempts.AddAsync(attempt, transactionCancellationToken);
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    // Lưu cả nội dung/đáp án trong QuestionSnapshotJson và quan hệ câu hỏi trong các answer rỗng.
                    var snapshots = orderedQuestions.Select(item => new MockTestAnswer
                    {
                        MockTestAttemptId = attempt.Id,
                        MockQuestionId = item.Question.Id,
                        SelectedOptionId = null,
                        IsCorrect = false,
                    });
                    await unitOfWork.MockTestAnswers.AddRangeAsync(snapshots, transactionCancellationToken);
                    var snapshot = new StartAttemptResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = practiceTestId,
                        Title = test.Title,
                        JlptLevelName = test.CertificateLevel?.Code ?? string.Empty,
                        StartedAt = attempt.StartedAt,
                        ServerNow = DateTime.UtcNow,
                        ExpiresAt = attempt.ExpiresAt,
                        TimeLimitMinutes = test.TimeLimitMinutes,
                        TotalPreviouslyWrongQuestions = previouslyWrongQuestionIds.Count,
                        PreviouslyWrongQuestionIds = previouslyWrongQuestionIds,
                        Questions = frozenQuestions.Select(question => question.Prompt).ToList(),
                    };
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                    response = ApiResponse<StartAttemptResponse>.Created(snapshot);
                }, cancellationToken);

                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<StartAttemptResponse>.Fail(
                    "Không thể bắt đầu lượt làm bài.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<StartAttemptResponse>> GetAttemptSnapshotAsync(int attemptId, CancellationToken cancellationToken = default)
        {
            try
            {
                var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
                if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    return ApiResponse<StartAttemptResponse>.NotFound("Không tìm thấy lượt thi thử.");
                if (attempt.UserId != claimService.GetUserClaim().Id)
                    return ApiResponse<StartAttemptResponse>.Fail("Bạn không có quyền xem lượt thi này.", HttpStatusCode.Forbidden);
                if (attempt.IsCompleted)
                    return ApiResponse<StartAttemptResponse>.Fail("Lượt thi đã được nộp.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_COMPLETED");

                var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, cancellationToken);
                var frozen = ReadMockSnapshot(attempt);
                var snapshot = frozen is not null && answers.Count > 0
                    ? new StartAttemptResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = frozen.MockTestId,
                        Title = frozen.Title ?? answers[0].MockQuestion.MockTestSection.MockTest.Title,
                        JlptLevelName = frozen.JlptLevelName ??
                            answers[0].MockQuestion.MockTestSection.MockTest.CertificateLevel?.Code ?? string.Empty,
                        StartedAt = attempt.StartedAt,
                        TimeLimitMinutes = attempt.ExpiresAt is { } expiry
                            ? (int)Math.Ceiling((expiry - attempt.StartedAt).TotalMinutes)
                            : answers[0].MockQuestion.MockTestSection.MockTest.TimeLimitMinutes,
                        PreviouslyWrongQuestionIds = frozen.PreviouslyWrongQuestionIds,
                        TotalPreviouslyWrongQuestions = frozen.PreviouslyWrongQuestionIds.Count,
                        Questions = frozen.Questions.Select(question => question.Prompt).ToList(),
                    }
                    : attempt.QuestionSnapshotJson is { Length: > 0 } json
                        ? JsonSerializer.Deserialize<StartAttemptResponse>(json, MockSnapshotJson) : null;
                if (snapshot is null || snapshot.AttemptId != attemptId || snapshot.Questions.Count == 0)
                    return ApiResponse<StartAttemptResponse>.Fail("Lượt thi này cần được mở lại từ danh sách đề.", HttpStatusCode.Conflict);

                var byQuestion = answers.ToDictionary(answer => answer.MockQuestionId);
                snapshot.ServerNow = DateTime.UtcNow;
                if (attempt.ExpiresAt is { } expiresAt) snapshot.ExpiresAt = BusinessDateHelper.EnsureUtc(expiresAt);
                foreach (var question in snapshot.Questions)
                {
                    SanitizeExamPrompt(question);
                    var answer = byQuestion[question.QuestionId];
                    question.SelectedOptionId = answer.SelectedOptionId;
                    question.Response = ParseJsonElement(answer.ResponseJson);
                }
                return ApiResponse<StartAttemptResponse>.Success(snapshot);
            }
            catch (Exception)
            {
                return ApiResponse<StartAttemptResponse>.Fail("Không thể tải lượt thi thử.", HttpStatusCode.InternalServerError);
            }
        }

        public Task<ApiResponse<AttemptResultResponse>> SaveAttemptAnswersAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default) =>
            StoreAttemptAnswersAsync(attemptId, request, submit: false, cancellationToken);

        public Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken = default) =>
            StoreAttemptAnswersAsync(attemptId, request, submit: true, cancellationToken);

        private async Task<ApiResponse<AttemptResultResponse>> StoreAttemptAnswersAsync(
            int attemptId, SubmitAttemptRequest request, bool submit, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (request.Answers is null || (!submit && request.Answers.Count == 0))
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
                        response = submit
                            ? await GetAttemptResultAsync(attemptId, transactionCancellationToken)
                            : ApiResponse<AttemptResultResponse>.Fail(
                                "Lượt làm bài này đã được nộp trước đó.", HttpStatusCode.BadRequest,
                                errorCode: "MOCK_TEST_COMPLETED");
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
                    var frozenQuestionsById = ReadFrozenQuestions(attempt, snapshotAnswers);
                    var now = DateTime.UtcNow;
                    var deadline = attempt.ExpiresAt is { } expiresAt ? BusinessDateHelper.EnsureUtc(expiresAt)
                        : mockTest.TimeLimitMinutes > 0 ? BusinessDateHelper.EnsureUtc(attempt.StartedAt).AddMinutes(mockTest.TimeLimitMinutes) : (DateTime?)null;
                    var expired = deadline.HasValue && now >= deadline.Value;
                    if (expired && (!submit || !attempt.ExpiresAt.HasValue))
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Lượt làm bài đã hết thời gian.", HttpStatusCode.BadRequest, errorCode: "MOCK_TEST_EXPIRED");
                        return;
                    }

                    // At expiry, grade the answers already saved before the server deadline.
                    var submittedAnswers = expired ? [] : request.Answers;
                    var snapshotsByQuestionId = snapshotAnswers.ToDictionary(
                        answer => answer.MockQuestionId);
                    var invalidQuestionIds = submittedAnswers
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

                    foreach (var submittedAnswer in submittedAnswers)
                    {
                        var question = snapshotsByQuestionId[submittedAnswer.QuestionId].MockQuestion;
                        var frozenQuestion = frozenQuestionsById is not null &&
                            frozenQuestionsById.TryGetValue(submittedAnswer.QuestionId, out var foundQuestion)
                            ? foundQuestion : null;
                        var questionType = frozenQuestion?.Prompt.QuestionType ?? question.QuestionType;
                        var validOptionIds = frozenQuestion?.Prompt.Options.Select(option => option.OptionId).ToHashSet() ??
                            (question.Options ?? []).Where(option => !option.IsDeleted).Select(option => option.Id).ToHashSet();
                        if (IsSingleChoiceQuestion(questionType))
                        {
                            if (submittedAnswer.Response.HasValue)
                            {
                                response = ApiResponse<AttemptResultResponse>.Fail(
                                    $"Câu hỏi {submittedAnswer.QuestionId} yêu cầu chọn một đáp án.", HttpStatusCode.BadRequest);
                                return;
                            }
                        }
                        else if (questionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase))
                        {
                            if (submittedAnswer.SelectedOptionId is not null ||
                                submittedAnswer.Response is not { ValueKind: JsonValueKind.Array })
                            {
                                response = ApiResponse<AttemptResultResponse>.Fail(
                                    $"Câu hỏi {submittedAnswer.QuestionId} yêu cầu danh sách OptionId.", HttpStatusCode.BadRequest);
                                return;
                            }
                        }
                        else if (submittedAnswer.SelectedOptionId is not null || submittedAnswer.Response is null)
                        {
                            response = ApiResponse<AttemptResultResponse>.Fail(
                                $"Câu hỏi {submittedAnswer.QuestionId} yêu cầu câu trả lời theo dạng {questionType}.", HttpStatusCode.BadRequest);
                            return;
                        }
                        else if (SentenceOrdering.IsType(questionType))
                        {
                            var data = frozenQuestion?.Prompt.InteractionData ?? ParseJsonElement(question.InteractionDataJson);
                            if (JlptMockQuestionFormats.IsSentenceComposition(data) &&
                                !JlptMockQuestionFormats.IsValidSentenceResponse(data, submittedAnswer.Response))
                            {
                                response = ApiResponse<AttemptResultResponse>.Fail(
                                    $"Câu hỏi {submittedAnswer.QuestionId} chứa cụm từ không hợp lệ.", HttpStatusCode.BadRequest);
                                return;
                            }
                        }
                        // Option phải thuộc đúng Question để client không thể ghép ID tùy ý khi chấm điểm.
                        if (submittedAnswer.SelectedOptionId is not null &&
                            !validOptionIds.Contains(submittedAnswer.SelectedOptionId.Value))
                        {
                            response = ApiResponse<AttemptResultResponse>.Fail(
                                $"Lựa chọn không thuộc câu hỏi {submittedAnswer.QuestionId}.",
                                HttpStatusCode.BadRequest);
                            return;
                        }
                    }

                    foreach (var submittedAnswer in submittedAnswers)
                    {
                        var snapshot = snapshotsByQuestionId[submittedAnswer.QuestionId];
                        var question = snapshot.MockQuestion;
                        var frozenQuestion = frozenQuestionsById is not null &&
                            frozenQuestionsById.TryGetValue(submittedAnswer.QuestionId, out var foundQuestion)
                            ? foundQuestion : null;
                        var selectedOption = frozenQuestion is not null || submittedAnswer.SelectedOptionId is null
                            ? null
                            : snapshot.MockQuestion.Options?.FirstOrDefault(
                                option => option.Id == submittedAnswer.SelectedOptionId);
                        snapshot.SelectedOptionId = submittedAnswer.SelectedOptionId;
                        snapshot.ResponseJson = submittedAnswer.Response?.GetRawText();
                        snapshot.IsCorrect = frozenQuestion is not null
                            ? IsFrozenAnswerCorrect(frozenQuestion, submittedAnswer.SelectedOptionId, submittedAnswer.Response)
                            : IsSingleChoiceQuestion(question.QuestionType)
                            ? selectedOption?.IsCorrect == true
                            : question.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase)
                                ? IsMultipleChoiceCorrect(question, submittedAnswer.Response)
                                : IsStructuredResponseCorrect(question, submittedAnswer.Response);
                    }
                    if (!submit)
                    {
                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                        response = ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                        {
                            AttemptId = attempt.Id, MockTestId = mockTest.Id, Status = StatusInProgress,
                        });
                        return;
                    }

                    var correctByQuestion = snapshotAnswers.ToDictionary(answer => answer.MockQuestionId, answer => answer.IsCorrect);
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

        public async Task<ApiResponse<bool>> SaveAttemptAnswerAsync(
            int attemptId, AnswerSubmissionItem answer, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<bool>? response = null;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var attempt = await unitOfWork.MockTestAttempts.GetForSubmissionAsync(attemptId, ct);
                    if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    {
                        response = ApiResponse<bool>.NotFound("Không tìm thấy lượt thi thử.");
                        return;
                    }
                    if (attempt.UserId != userId)
                    {
                        response = ApiResponse<bool>.Fail("Bạn không có quyền sửa lượt thi này.", HttpStatusCode.Forbidden);
                        return;
                    }
                    var snapshots = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, ct);
                    var timeLimitMinutes = snapshots.FirstOrDefault()?.MockQuestion.MockTestSection.MockTest.TimeLimitMinutes ?? 0;
                    if (attempt.IsCompleted || (!attempt.ExpiresAt.HasValue && timeLimitMinutes <= 0) ||
                        DateTime.UtcNow >= (attempt.ExpiresAt ?? attempt.StartedAt.AddMinutes(timeLimitMinutes)))
                    {
                        response = ApiResponse<bool>.Fail("Lượt thi đã kết thúc hoặc hết thời gian.", HttpStatusCode.Conflict);
                        return;
                    }

                    var snapshot = snapshots.FirstOrDefault(item => item.MockQuestionId == answer.QuestionId);
                    if (snapshot is null)
                    {
                        response = ApiResponse<bool>.Fail("Câu hỏi không thuộc lượt thi này.", HttpStatusCode.BadRequest);
                        return;
                    }
                    var question = snapshot.MockQuestion;
                    var frozenQuestionsById = ReadFrozenQuestions(attempt, snapshots);
                    var frozen = frozenQuestionsById is not null &&
                        frozenQuestionsById.TryGetValue(answer.QuestionId, out var frozenQuestion)
                        ? frozenQuestion : null;
                    var questionType = frozen?.Prompt.QuestionType ?? question.QuestionType;
                    var validOptionIds = frozen?.Prompt.Options.Select(option => option.OptionId).ToHashSet() ??
                        (question.Options ?? []).Where(option => !option.IsDeleted).Select(option => option.Id).ToHashSet();
                    if (IsSingleChoiceQuestion(questionType))
                    {
                        if (answer.Response.HasValue || (answer.SelectedOptionId.HasValue &&
                            !validOptionIds.Contains(answer.SelectedOptionId.Value)))
                        {
                            response = ApiResponse<bool>.Fail("Đáp án không hợp lệ cho câu hỏi này.", HttpStatusCode.BadRequest);
                            return;
                        }
                    }
                    else if (questionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase))
                    {
                        if (answer.SelectedOptionId.HasValue || answer.Response is not { ValueKind: JsonValueKind.Array } ||
                            answer.Response.Value.EnumerateArray().Any(option => option.ValueKind != JsonValueKind.Number ||
                                !option.TryGetInt32(out var optionId) || !validOptionIds.Contains(optionId)))
                        {
                            response = ApiResponse<bool>.Fail("Danh sách đáp án không hợp lệ.", HttpStatusCode.BadRequest);
                            return;
                        }
                    }
                    else if (answer.SelectedOptionId.HasValue || answer.Response is null)
                    {
                        response = ApiResponse<bool>.Fail("Câu trả lời không đúng định dạng.", HttpStatusCode.BadRequest);
                        return;
                    }
                    else if (SentenceOrdering.IsType(questionType))
                    {
                        var data = frozen?.Prompt.InteractionData ?? ParseJsonElement(question.InteractionDataJson);
                        if (JlptMockQuestionFormats.IsSentenceComposition(data) &&
                            !JlptMockQuestionFormats.IsValidSentenceResponse(data, answer.Response))
                        {
                            response = ApiResponse<bool>.Fail("Câu trả lời chứa cụm từ không hợp lệ.", HttpStatusCode.BadRequest);
                            return;
                        }
                    }

                    snapshot.SelectedOptionId = answer.SelectedOptionId;
                    snapshot.ResponseJson = answer.Response?.GetRawText();
                    await unitOfWork.SaveChangesAsync(ct);
                    response = ApiResponse<bool>.Success(true);
                }, cancellationToken);
                return response!;
            }
            catch (Exception)
            {
                return ApiResponse<bool>.Fail("Không thể lưu câu trả lời.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<StartAttemptResponse?>> GetActiveMockAttemptAsync(
            int practiceTestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempts = await unitOfWork.MockTestAttempts.GetByUserAsync(userId);
                foreach (var attempt in attempts.Where(item => !item.IsCompleted && !item.IsDeleted))
                {
                    var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attempt.Id, cancellationToken);
                    if (answers.Count == 0) continue;
                    var frozenQuestions = ReadFrozenQuestions(attempt, answers);
                    var frozen = frozenQuestions is null ? null : ReadMockSnapshot(attempt);
                    if (frozen is not null
                            ? frozen.MockTestId != practiceTestId
                            : answers.Any(answer => answer.MockQuestion.MockTestSection.MockTestId != practiceTestId))
                        continue;

                    var test = answers[0].MockQuestion.MockTestSection.MockTest;
                    return ApiResponse<StartAttemptResponse?>.Success(new StartAttemptResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = practiceTestId,
                        Title = frozen?.Title ?? test.Title,
                        JlptLevelName = frozen?.JlptLevelName ?? test.CertificateLevel?.Code ?? string.Empty,
                        StartedAt = BusinessDateHelper.EnsureUtc(attempt.StartedAt),
                        ServerNow = DateTime.UtcNow,
                        ExpiresAt = attempt.ExpiresAt is { } expiry ? BusinessDateHelper.EnsureUtc(expiry) : null,
                        PreviouslyWrongQuestionIds = frozen?.PreviouslyWrongQuestionIds ?? [],
                        TotalPreviouslyWrongQuestions = frozen?.PreviouslyWrongQuestionIds.Count ?? 0,
                        TimeLimitMinutes = attempt.ExpiresAt.HasValue
                            ? (int)Math.Ceiling((attempt.ExpiresAt.Value - attempt.StartedAt).TotalMinutes)
                            : test.TimeLimitMinutes,
                        Questions = answers.Select(answer =>
                        {
                            var frozenQuestion = frozenQuestions is not null &&
                                frozenQuestions.TryGetValue(answer.MockQuestionId, out var foundQuestion)
                                ? foundQuestion : null;
                            if (frozenQuestion is not null)
                            {
                                frozenQuestion.Prompt.SelectedOptionId = answer.SelectedOptionId;
                                frozenQuestion.Prompt.Response = ParseJsonElement(answer.ResponseJson);
                                return SanitizeExamPrompt(frozenQuestion.Prompt);
                            }
                            var question = answer.MockQuestion;
                            var section = question.MockTestSection;
                            return new StartAttemptQuestionResponse
                            {
                                QuestionId = question.Id,
                                MockTestSectionId = section.Id,
                                SectionTitle = section.Title,
                                SectionOrder = section.SortOrder,
                                Order = question.SortOrder,
                                SelectedOptionId = answer.SelectedOptionId,
                                Response = ParseJsonElement(answer.ResponseJson),
                                Content = question.QuestionText,
                                QuestionType = question.QuestionType,
                                InteractionData = ParseJsonElement(question.InteractionDataJson),
                                AudioUrl = question.AudioUrl,
                                ImageUrl = question.ImageUrl,
                                Options = (question.Options ?? [])
                                    .Where(option => !option.IsDeleted)
                                    .OrderBy(option => option.SortOrder).ThenBy(option => option.Id)
                                    .Select((option, index) => new StartAttemptOptionResponse
                                    {
                                        OptionId = option.Id,
                                        Content = PublicOptionText(option, question.InteractionDataJson, index),
                                        ImageUrl = option.ImageUrl,
                                        Order = option.SortOrder,
                                    }).ToList(),
                            };
                        }).OrderBy(question => question.SectionOrder)
                            .ThenBy(question => question.Order)
                            .ThenBy(question => question.QuestionId)
                            .ToList(),
                    });
                }

                return ApiResponse<StartAttemptResponse?>.Success(null);
            }
            catch (Exception)
            {
                return ApiResponse<StartAttemptResponse?>.Fail(
                    "Không thể khôi phục lượt thi thử.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<AttemptResultResponse>> FinalizeExpiredAttemptAsync(
            int attemptId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<AttemptResultResponse>? response = null;
                var alreadyCompleted = false;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var attempt = await unitOfWork.MockTestAttempts.GetForSubmissionAsync(attemptId, ct);
                    if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    {
                        response = ApiResponse<AttemptResultResponse>.NotFound("Không tìm thấy lượt thi thử.");
                        return;
                    }
                    if (attempt.UserId != userId)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail("Bạn không có quyền kết thúc lượt thi này.", HttpStatusCode.Forbidden);
                        return;
                    }
                    if (attempt.IsCompleted)
                    {
                        alreadyCompleted = true;
                        return;
                    }
                    var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, ct);
                    if (answers.Count == 0 || answers.Select(a => a.MockQuestion.MockTestSection.MockTestId).Distinct().Count() != 1)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail("Dữ liệu câu hỏi của lượt thi không hợp lệ.", HttpStatusCode.Conflict);
                        return;
                    }
                    var mockTest = answers[0].MockQuestion.MockTestSection.MockTest;
                    var deadline = attempt.ExpiresAt ?? attempt.StartedAt.AddMinutes(mockTest.TimeLimitMinutes);
                    if ((!attempt.ExpiresAt.HasValue && mockTest.TimeLimitMinutes <= 0) || DateTime.UtcNow < deadline)
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail("Lượt thi chưa hết thời gian.", HttpStatusCode.Conflict);
                        return;
                    }

                    var correctByQuestion = new Dictionary<int, bool>();
                    var frozenQuestionsById = ReadFrozenQuestions(attempt, answers);
                    foreach (var answer in answers)
                    {
                        var question = answer.MockQuestion;
                        var selected = (question.Options ?? []).FirstOrDefault(option => option.Id == answer.SelectedOptionId);
                        var structured = ParseJsonElement(answer.ResponseJson);
                        var frozenQuestion = frozenQuestionsById is not null &&
                            frozenQuestionsById.TryGetValue(answer.MockQuestionId, out var foundQuestion)
                            ? foundQuestion : null;
                        answer.IsCorrect = frozenQuestion is not null
                            ? IsFrozenAnswerCorrect(frozenQuestion, answer.SelectedOptionId, structured)
                            : IsSingleChoiceQuestion(question.QuestionType)
                            ? selected?.IsCorrect == true
                            : question.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase)
                                ? IsMultipleChoiceCorrect(question, structured)
                                : IsStructuredResponseCorrect(question, structured);
                        correctByQuestion[answer.MockQuestionId] = answer.IsCorrect;
                    }
                    var sections = BuildSectionResults(attemptId, answers.Select(a => a.MockQuestion).ToList(), correctByQuestion);
                    await unitOfWork.MockTestSectionResults.AddRangeAsync(sections.Entities, ct);
                    attempt.SubmittedAt = deadline;
                    attempt.IsCompleted = true;
                    attempt.TotalScore = sections.OverallScore;
                    unitOfWork.MockTestAttempts.Update(attempt);
                    await unitOfWork.SaveChangesAsync(ct);
                    response = ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = mockTest.Id,
                        Status = StatusCompleted,
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt,
                        TotalScore = sections.OverallScore,
                        WrongQuestionIds = correctByQuestion.Where(item => !item.Value).Select(item => item.Key).OrderBy(id => id).ToList(),
                        Sections = sections.Responses,
                    });
                }, cancellationToken);
                return alreadyCompleted ? await GetAttemptResultAsync(attemptId, cancellationToken) : response!;
            }
            catch (Exception)
            {
                return ApiResponse<AttemptResultResponse>.Fail("Không thể kết thúc lượt thi thử.", HttpStatusCode.InternalServerError);
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
                var tests = await unitOfWork.MockTests.Query().AsNoTracking()
                    .Where(test => !test.IsDeleted && test.CertificateLevelId == certificationLevelId
                        && test.ExamKind == ExamKind.FullMockTest
                        && !test.IsArchivedVersion)
                    .Include(test => test.CertificateLevel)
                    .Include(test => test.Sections.Where(section => !section.IsDeleted))
                        .ThenInclude(section => section.LanguageSkill)
                    .Include(test => test.Sections.Where(section => !section.IsDeleted))
                        .ThenInclude(section => section.Questions.Where(question => !question.IsDeleted))
                        .ThenInclude(question => question.Options.Where(option => !option.IsDeleted))
                    .Include(test => test.Sections.Where(section => !section.IsDeleted))
                        .ThenInclude(section => section.Questions.Where(question => !question.IsDeleted))
                        .ThenInclude(question => question.Passage)
                    .AsSplitQuery().ToListAsync(cancellationToken);

                var approvals = await unitOfWork.ContentApprovalRequests.Query().AsNoTracking()
                    .Where(request => !request.IsDeleted && request.EntityType == ImportEntityType.MockTest &&
                        request.Status == ContentApprovalStatus.Approved)
                    .Select(request => new { request.TargetEntityId, request.RowKey })
                    .ToListAsync(cancellationToken);
                var result = tests.Where(test => IsLearnerReady(test) &&
                        !ContentAuthoring.JlptBlueprintPolicy.IsRightsBlockedForLegacyOrManaged(test.RightsStatus, test.IsManagedWorkflow) &&
                        ((test.IsManagedWorkflow && test.PublishStatus == ContentStatus.Published) ||
                         (!test.IsManagedWorkflow && test.PublishStatus != ContentStatus.Unpublished &&
                          (test.PublishStatus == ContentStatus.Published ||
                           approvals.Any(request => request.TargetEntityId == test.Id ||
                               (request.TargetEntityId == null && request.RowKey == test.Title + " / " + test.CertificateLevel.Code))))))
                    .OrderBy(test => test.Title).ThenBy(test => test.Id)
                    .Select(ToTestSummary).ToList();

                return ApiResponse<List<TestSummaryResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail(
                    "Không thể tải danh sách đề thi.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TestSummaryResponse>>> GetPracticeSetsAsync(
            int? certificateLevelId = null, int? languageSkillId = null, string? examKind = null, string? keyword = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var query = unitOfWork.MockTests.Query().AsNoTracking()
                    .Where(t => !t.IsDeleted && !t.IsArchivedVersion &&
                                (t.ExamKind == ExamKind.PracticeSet || t.ExamKind == ExamKind.MixedPractice) &&
                                t.PublishStatus == ContentStatus.Published);

                if (certificateLevelId.HasValue)
                    query = query.Where(t => t.CertificateLevelId == certificateLevelId.Value);
                if (Enum.TryParse<ExamKind>(examKind, true, out var parsedKind) && parsedKind is ExamKind.PracticeSet or ExamKind.MixedPractice)
                    query = query.Where(t => t.ExamKind == parsedKind);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var kw = keyword.Trim().ToLower();
                    query = query.Where(t => t.Title.ToLower().Contains(kw) || (t.Description != null && t.Description.ToLower().Contains(kw)));
                }

                var tests = await query
                    .Include(t => t.CertificateLevel)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.LanguageSkill)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Passage)
                    .AsSplitQuery()
                    .OrderByDescending(t => t.PublishedAt ?? t.ModifiedDate ?? t.CreatedDate)
                    .ToListAsync(cancellationToken);

                if (languageSkillId.HasValue)
                {
                    tests = tests.Where(t =>
                        t.FocusSkillId == languageSkillId.Value ||
                        (t.Sections ?? []).Any(s => !s.IsDeleted && s.LanguageSkillId == languageSkillId.Value))
                        .ToList();
                }

                var result = tests.Where(test =>
                        IsLearnerReady(test) &&
                        !ContentAuthoring.JlptBlueprintPolicy.IsRightsBlockedForLegacyOrManaged(test.RightsStatus, test.IsManagedWorkflow))
                    .Select(ToTestSummary)
                    .ToList();
                return ApiResponse<List<TestSummaryResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail("Không thể tải danh sách bộ đề luyện tập.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TestSummaryResponse>>> GetFullMockTestsAsync(
            int? certificateLevelId = null, string? keyword = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = unitOfWork.MockTests.Query().AsNoTracking()
                    .Where(t => !t.IsDeleted && !t.IsArchivedVersion && t.ExamKind == ExamKind.FullMockTest);

                if (certificateLevelId.HasValue)
                    query = query.Where(t => t.CertificateLevelId == certificateLevelId.Value);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var kw = keyword.Trim().ToLower();
                    query = query.Where(t => t.Title.ToLower().Contains(kw) || (t.Description != null && t.Description.ToLower().Contains(kw)));
                }

                var tests = await query
                    .Include(t => t.CertificateLevel)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.LanguageSkill)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Passage)
                    .AsSplitQuery()
                    .OrderBy(t => t.CertificateLevelId).ThenByDescending(t => t.PublishedAt ?? t.CreatedDate)
                    .ToListAsync(cancellationToken);

                var ready = new List<TestSummaryResponse>();
                foreach (var test in tests)
                {
                    if (IsLearnerReady(test) && await IsApprovedForLearnersAsync(test, cancellationToken))
                        ready.Add(ToTestSummary(test));
                }
                return ApiResponse<List<TestSummaryResponse>>.Success(ready);
            }
            catch (Exception)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail("Không thể tải danh sách đề thi thử JLPT.", HttpStatusCode.InternalServerError);
            }
        }

        private static DTOs.ContentAuthoring.ListeningPlaybackPolicyDto? TryDeserializePlaybackPolicy(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<DTOs.ContentAuthoring.ListeningPlaybackPolicyDto>(json, MockSnapshotJson); }
            catch (JsonException) { return null; }
        }

        public async Task<ApiResponse<TestSummaryResponse>> GetTestInfoAsync(int practiceTestId,
            CancellationToken cancellationToken = default)
        {
            var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(practiceTestId, cancellationToken);
            return test is not null && IsLearnerReady(test) &&
                await IsApprovedForLearnersAsync(test, cancellationToken)
                ? ApiResponse<TestSummaryResponse>.Success(ToTestSummary(test))
                : ApiResponse<TestSummaryResponse>.NotFound("Đề thi chưa sẵn sàng.");
        }

        public async Task<ApiResponse<List<MockTestHistoryItem>>> GetHistoryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempts = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
                    .Where(attempt => attempt.UserId == userId && attempt.AttemptKind == AttemptKind.MockTest &&
                        attempt.IsCompleted && !attempt.IsDeleted && attempt.TotalScore.HasValue)
                    .OrderByDescending(attempt => attempt.SubmittedAt ?? attempt.StartedAt).ThenByDescending(attempt => attempt.Id)
                    .Select(attempt => new
                    {
                        attempt.Id,
                        Date = attempt.SubmittedAt ?? attempt.StartedAt,
                        Score = attempt.TotalScore!.Value,
                        attempt.QuestionSnapshotJson,
                        Test = attempt.SectionResults!.OrderBy(section => section.MockTestSectionId)
                            .Select(section => new
                            {
                                Id = section.MockTestSection.MockTestId,
                                section.MockTestSection.MockTest.Title,
                                Level = section.MockTestSection.MockTest.CertificateLevel.Code,
                            }).FirstOrDefault(),
                    }).ToListAsync(cancellationToken);
                return ApiResponse<List<MockTestHistoryItem>>.Success(attempts.Where(attempt => attempt.Test is not null)
                    .Select(attempt =>
                    {
                        MockAttemptSnapshot? frozen = null;
                        if (!string.IsNullOrWhiteSpace(attempt.QuestionSnapshotJson))
                        {
                            try { frozen = JsonSerializer.Deserialize<MockAttemptSnapshot>(attempt.QuestionSnapshotJson, MockSnapshotJson); }
                            catch (JsonException) { /* Preserve access to legacy history. */ }
                        }
                        return new MockTestHistoryItem(attempt.Id, attempt.Test!.Id,
                            frozen?.Title ?? attempt.Test.Title,
                            frozen?.JlptLevelName ?? attempt.Test.Level,
                            BusinessDateHelper.EnsureUtc(attempt.Date), attempt.Score);
                    }).ToList());
            }
            catch (Exception)
            {
                return ApiResponse<List<MockTestHistoryItem>>.Fail("Không thể tải lịch sử thi thử.", HttpStatusCode.InternalServerError);
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
                if (!IsLearnerReady(test) || !await IsApprovedForLearnersAsync(test, cancellationToken))
                    return ApiResponse<MockTestDetailResponse>.NotFound(
                        "Bài đánh giá năng lực chưa được duyệt nội dung.");

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
                                        .Select((option, index) => new MockQuestionOptionResponse
                                        {
                                            OptionId = option.Id,
                                            OptionText = PublicOptionText(option, question.InteractionDataJson, index),
                                            ImageUrl = option.ImageUrl,
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
                var frozenQuestionsById = ReadFrozenQuestions(attempt, answers);

                var questions = answers.Select(a =>
                {
                    var frozen = frozenQuestionsById?.GetValueOrDefault(a.MockQuestionId);
                    var question = a.MockQuestion;
                    return new QuestionReviewCard
                    {
                        QuestionId = a.MockQuestionId,
                        QuestionText = frozen?.Prompt.Content ?? question.QuestionText,
                        QuestionType = frozen?.Prompt.QuestionType ?? question.QuestionType,
                        InteractionData = frozen is null ? ParseJsonElement(question.InteractionDataJson) : frozen.Prompt.InteractionData,
                        ImageUrl = frozen is null ? question.ImageUrl : frozen.Prompt.ImageUrl,
                        AudioUrl = frozen is null ? question.AudioUrl : frozen.Prompt.AudioUrl,
                        AudioTranscript = frozen is null ||
                            (string.Equals(frozen.Prompt.AudioUrl, question.AudioUrl, StringComparison.Ordinal) &&
                             string.Equals(frozen.Prompt.InteractionData?.GetRawText(), question.InteractionDataJson, StringComparison.Ordinal))
                            ? question.AudioTranscript : null,
                        Response = ParseJsonElement(a.ResponseJson),
                        AnswerKey = ParseJsonElement(frozen is null ? question.AnswerKeyJson : frozen.AnswerKeyJson),
                        Explanation = frozen is null ? question.Explanation : frozen.Explanation,
                        PassageId = frozen?.Prompt.PassageId ?? question.PassageId,
                        PassageTitle = frozen?.Prompt.PassageTitle ?? question.Passage?.Title,
                        PassageText = ResolvePassageText(
                            frozen?.Prompt.PassageText ?? question.Passage?.PassageText,
                            frozen is null ? ParseJsonElement(question.InteractionDataJson) : frozen.Prompt.InteractionData),
                        ReviewTranscript = frozen is null ||
                            (string.Equals(frozen.Prompt.AudioUrl, question.AudioUrl, StringComparison.Ordinal))
                            ? (question.AudioTranscript ?? question.Passage?.AudioTranscript) : null,
                        SelectedOptionId = a.SelectedOptionId,
                        IsCorrect = a.IsCorrect,
                        WasPreviouslyIncorrect = !a.IsCorrect,
                        Options = frozen is not null
                            ? frozen.Prompt.Options.Select(option => new QuestionOptionReview
                            {
                                OptionId = option.OptionId,
                                OptionText = option.Content,
                                ImageUrl = option.ImageUrl,
                                IsCorrect = frozen.CorrectOptionIds.Contains(option.OptionId),
                            }).ToList()
                            : (question.Options ?? [])
                                .OrderBy(option => option.SortOrder).ThenBy(option => option.Id)
                                .Select(option => new QuestionOptionReview
                                {
                                    OptionId = option.Id,
                                    OptionText = option.OptionText,
                                    ImageUrl = option.ImageUrl,
                                    IsCorrect = option.IsCorrect,
                                }).ToList(),
                    };
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
                            ImageUrl = o.ImageUrl,
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
                List<MockQuestion> questions;
                int limitMinutes;

                if (request is not null && request.MockTestId > 0)
                {
                    var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(
                        request.MockTestId, cancellationToken);
                    if (test is null || test.IsDeleted)
                        return ApiResponse<PlacementAttemptSnapshotResponse>.NotFound(
                            $"Không tìm thấy bài đánh giá năng lực có Id = {request.MockTestId}.");
                    if (!IsLearnerReady(test) || !await IsApprovedForLearnersAsync(test, cancellationToken))
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                            "Bài đánh giá năng lực chưa được duyệt nội dung.", HttpStatusCode.Conflict);

                    // Lấy toàn bộ câu hỏi của đề theo đúng thứ tự section -> câu hỏi.
                    questions = (test.Sections ?? [])
                        .Where(s => !s.IsDeleted)
                        .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                        .SelectMany(s => (s.Questions ?? [])
                            .Where(q => !q.IsDeleted)
                            .OrderBy(q => q.SortOrder).ThenBy(q => q.Id))
                        .ToList();
                    if (questions.Count == 0)
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                            "Bài đánh giá chưa có câu hỏi.", HttpStatusCode.BadRequest);

                    limitMinutes = test.TimeLimitMinutes > 0 ? test.TimeLimitMinutes : DefaultPlacementMinutes;
                }
                else
                {
                    var allTests = await unitOfWork.MockTests.GetAllWithSectionsAndQuestionsAsync(cancellationToken);
                    var eligibleTests = new List<MockTest>();
                    foreach (var test in (allTests ?? [])
                        .Where(t => t.CertificateLevelId is >= 1 and <= 4 && IsLearnerReady(t))
                        .OrderBy(t => t.CertificateLevelId).ThenBy(t => t.Id))
                    {
                        if (await IsApprovedForLearnersAsync(test, cancellationToken)) eligibleTests.Add(test);
                    }

                    questions = eligibleTests
                        .GroupBy(t => t.CertificateLevelId)
                        .OrderBy(g => g.Key)
                        .SelectMany(g => g
                            .SelectMany(t => (t.Sections ?? [])
                                .Where(s => !s.IsDeleted)
                                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                                .SelectMany(s => (s.Questions ?? [])
                                    .Where(q => !q.IsDeleted && (q.Options ?? []).Any(o => !o.IsDeleted && o.IsCorrect))
                                    .OrderBy(q => q.SortOrder).ThenBy(q => q.Id)))
                            .Take(5))
                        .ToList();

                    if (questions.Count == 0)
                        return ApiResponse<PlacementAttemptSnapshotResponse>.Fail(
                            "Bài đánh giá chưa có câu hỏi.", HttpStatusCode.BadRequest);

                    limitMinutes = Math.Clamp(questions.Count * 2, 10, DefaultPlacementMinutes);
                }

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
                        if (!request.RestartExisting && existingAnswers.Count > 0 &&
                            existingAnswers.All(answer => answer.MockQuestion.MockTestSection.MockTestId == request.MockTestId) &&
                            !IsExpired(existing, existingAnswers))
                        {
                            response = ApiResponse<PlacementAttemptSnapshotResponse>.Success(
                                await BuildSnapshotAsync(existing, existingAnswers, isResumed: true, ct));
                            return;
                        }
                        // Giữ lịch sử lượt cũ; kết thúc thời gian để không khôi phục nhầm sau khi đổi đề.
                        existing.ExpiresAt = DateTime.UtcNow;
                        unitOfWork.MockTestAttempts.Update(existing);
                    }

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
            attempt.ExpiresAt ?? attempt.StartedAt.AddMinutes(GetTimeLimitMinutes(answers));

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
                ExpiresAt = ExpiresAt(attempt, answers),
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
                            .Select((o, index) => new PlacementTestOptionResponse
                            {
                                OptionId = o.Id,
                                Content = PublicOptionText(o, question.InteractionDataJson, index),
                                ImageUrl = o.ImageUrl,
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
