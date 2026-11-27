using RikiPath.Application.Helpers;
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
    public class MockTestService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        SubscriptionAccessService? subscriptionAccess = null) : IMockTestService
    {
        private const string StatusInProgress = "in_progress";
        private const string StatusCompleted = "completed";
        private const string StatusExpired = "expired";
        private const string StatusAbandoned = "abandoned";
        private const string StatusGrading = "grading";
        private const string StatusFailed = "failed";
        private static readonly JsonSerializerOptions MockSnapshotJson = new(JsonSerializerDefaults.Web);

        private static bool IsSingleChoiceQuestion(string type) =>
            JlptMockQuestionFormats.IsSingleChoice(type);

        private static bool IsPracticeExamKind(ExamKind kind) =>
            kind is ExamKind.PracticeSet or ExamKind.MixedPractice;

        private static bool IsPracticeExamKind(string? kind) =>
            string.Equals(kind, nameof(ExamKind.PracticeSet), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, nameof(ExamKind.MixedPractice), StringComparison.OrdinalIgnoreCase);

        private static bool IsAttemptAbandoned(MockAttemptSnapshot? snap) =>
            string.Equals(snap?.AttemptStatus, StatusAbandoned, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(snap?.AttemptStatus, "superseded", StringComparison.OrdinalIgnoreCase) ||
            snap?.AbandonedAt is not null;

        private static string ResolveAttemptStatus(MockTestAttempt attempt, MockAttemptSnapshot? snap)
        {
            if (IsAttemptAbandoned(snap)) return StatusAbandoned;
            if (!string.IsNullOrWhiteSpace(snap?.AttemptStatus) &&
                !string.Equals(snap.AttemptStatus, StatusInProgress, StringComparison.OrdinalIgnoreCase))
            {
                return snap.AttemptStatus!;
            }
            if (attempt.IsCompleted)
            {
                return StatusCompleted;
            }
            return StatusInProgress;
        }

        private static bool HasAnsweredValue(MockTestAnswer answer) =>
            answer.SelectedOptionId.HasValue || !string.IsNullOrWhiteSpace(answer.ResponseJson);

        private async Task<(bool Allowed, HttpStatusCode StatusCode, string? ErrorCode, string? Message)> EvaluateMockFeatureAsync(
            CancellationToken cancellationToken)
        {
            if (subscriptionAccess is null) return (true, HttpStatusCode.OK, null, null);
            var decision = await subscriptionAccess.EvaluateFeatureAsync(FeatureCodes.MockTests, cancellationToken);
            return (decision.Allowed, decision.StatusCode, decision.ErrorCode, decision.Message);
        }

        private static string ResolveEffectiveExamKind(
            string? snapExamKind,
            ExamKind? testExamKind,
            AttemptKind attemptKind = AttemptKind.MockTest,
            bool isPlacementAssessment = false)
        {
            if (attemptKind == AttemptKind.Placement || isPlacementAssessment || testExamKind == ExamKind.Placement)
                return nameof(ExamKind.Placement);
            if (testExamKind is ExamKind.PracticeSet or ExamKind.MixedPractice)
                return testExamKind.Value.ToString();
            if (!string.IsNullOrWhiteSpace(snapExamKind))
                return snapExamKind;
            return testExamKind?.ToString() ?? nameof(ExamKind.FullMockTest);
        }

        private static bool CanReuseSnapshotScoring(MockAttemptScoringSummary? snapScoring, string effectiveExamKind)
        {
            if (snapScoring is null) return false;
            if (IsPracticeExamKind(effectiveExamKind))
                return string.Equals(snapScoring.ScoreType, MockTestScoringPolicy.ScoreTypePractice10, StringComparison.OrdinalIgnoreCase);
            if (string.Equals(effectiveExamKind, nameof(ExamKind.FullMockTest), StringComparison.OrdinalIgnoreCase))
                return string.Equals(snapScoring.ScoreType, MockTestScoringPolicy.ScoreTypeMock180, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private sealed class MockAttemptSnapshot
        {
            public MockAttemptSnapshot() { }
            public int MockTestId { get; set; }
            public string? Title { get; set; }
            public string? JlptLevelName { get; set; }
            public string? ExamKind { get; set; }
            public string AttemptStatus { get; set; } = StatusInProgress;
            public DateTime? AbandonedAt { get; set; }
            public int? SupersededByAttemptId { get; set; }
            public DateTime? LastSavedAt { get; set; }
            public long LatestAnswerSequence { get; set; }
            public Dictionary<int, long> QuestionSequences { get; set; } = [];
            public int ElapsedSeconds { get; set; }
            public MockAttemptScoringSummary? Scoring { get; set; }
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

        private static MockAttemptSnapshot? TryReadMockSnapshotSafe(MockTestAttempt attempt)
        {
            if (string.IsNullOrWhiteSpace(attempt.QuestionSnapshotJson)) return null;
            try { return JsonSerializer.Deserialize<MockAttemptSnapshot>(attempt.QuestionSnapshotJson, MockSnapshotJson); }
            catch (JsonException) { return null; }
        }

        private async Task AutoFinalizeExpiredMockAttemptsInternalAsync(
            int userId, int? filterMockTestId, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var active = await unitOfWork.MockTestAttempts.Query()
                .AsNoTracking()
                .Where(a => a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest && !a.IsCompleted && a.ExpiresAt.HasValue && a.ExpiresAt.Value <= nowUtc)
                .OrderByDescending(a => a.Id)
                .ToListAsync(cancellationToken);

            foreach (var cand in active)
            {
                var snap = TryReadMockSnapshotSafe(cand);
                if (IsAttemptAbandoned(snap)) continue;
                if (IsPracticeExamKind(snap?.ExamKind)) continue;
                if (filterMockTestId.HasValue && snap is not null && snap.MockTestId != filterMockTestId.Value) continue;
                await FinalizeExpiredAttemptAsync(cand.Id, cancellationToken);
            }
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
                EnforceStrictTimer = !IsPracticeExamKind(test.ExamKind),
                PublishedAt = test.PublishedAt ?? test.ReviewedAt ?? test.CreatedDate,
                IncludedSkills = includedSkills,
                Sections = (test.Sections ?? []).Where(section => !section.IsDeleted)
                    .OrderBy(section => section.SortOrder).ThenBy(section => section.Id)
                    .Select(section => new TestSectionSummaryResponse
                    {
                        SectionId = section.Id,
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

        public Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(int practiceTestId, CancellationToken cancellationToken = default) =>
            StartAttemptAsync(practiceTestId, forceNew: false, cancellationToken);

        public async Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(
            int practiceTestId, bool forceNew, CancellationToken cancellationToken = default)
        {
            try
            {
                var featureGate = await EvaluateMockFeatureAsync(cancellationToken);
                if (!featureGate.Allowed)
                    return ApiResponse<StartAttemptResponse>.Fail(
                        featureGate.Message ?? "Tính năng Luyện đề & Thi thử hiện không khả dụng cho tài khoản của bạn.",
                        featureGate.StatusCode,
                        errorCode: featureGate.ErrorCode ?? "FEATURE_ACCESS_DENIED");

                var userId = claimService.GetUserClaim().Id;
                var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(practiceTestId, cancellationToken);
                if (test is null || test.IsDeleted ||
                    (test.CertificateLevel is { } level && (level.IsDeleted || JlptCurriculum.Rank(level.Code) == 0)))
                    return ApiResponse<StartAttemptResponse>.NotFound(
                        $"Không tìm thấy MockTest có Id = {practiceTestId}.");

                if (test.ExamKind is not (ExamKind.PracticeSet or ExamKind.MixedPractice or ExamKind.FullMockTest))
                    return ApiResponse<StartAttemptResponse>.Fail(
                        "Không thể mở đề đánh giá năng lực dưới dạng bài luyện tập hoặc thi thử.", HttpStatusCode.BadRequest);

                var isPractice = IsPracticeExamKind(test.ExamKind);
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
                if (!isPractice && test.TimeLimitMinutes <= 0)
                    return ApiResponse<StartAttemptResponse>.Fail(
                        "Đề thi chưa có thời lượng hợp lệ.", HttpStatusCode.BadRequest);

                var frozenQuestions = orderedQuestions.Select(item => new MockQuestionSnapshot
                {
                    Prompt = new StartAttemptQuestionResponse
                    {
                        QuestionId = item.Question.Id,
                        MockTestSectionId = item.Section.Id,
                        SectionTitle = item.Section.Title,
                        LanguageSkillName = item.Section.LanguageSkill?.Name,
                        QuestionGroupTitle = item.Question.QuestionGroupTitle,
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
                    await AutoFinalizeExpiredMockAttemptsInternalAsync(userId, practiceTestId, transactionCancellationToken);
                    var active = await GetActiveMockAttemptAsync(practiceTestId, transactionCancellationToken);
                    if (!active.IsSuccess)
                    {
                        response = ApiResponse<StartAttemptResponse>.Fail(
                            "Không thể kiểm tra lượt thi đang làm.", HttpStatusCode.InternalServerError);
                        return;
                    }
                    if (active.Result is not null)
                    {
                        if (!forceNew)
                        {
                            active.Result.IsResumed = true;
                            response = ApiResponse<StartAttemptResponse>.Success(active.Result);
                            return;
                        }

                        if (!isPractice)
                        {
                            response = ApiResponse<StartAttemptResponse>.Fail(
                                "Bạn đang có một lượt thi thử JLPT chưa hoàn thành. Vui lòng tiếp tục hoặc nộp lượt hiện tại.",
                                HttpStatusCode.Conflict,
                                errorCode: "MOCK_TEST_CANNOT_RESTART_ACTIVE");
                            return;
                        }

                        // Prepare to abandon previous PracticeSet attempt atomically only after new attempt succeeds
                        var oldAttempt = await unitOfWork.MockTestAttempts.GetForSubmissionAsync(active.Result.AttemptId, transactionCancellationToken);
                        var nowUtc = DateTime.UtcNow;
                        var newAttempt = new MockTestAttempt
                        {
                            UserId = userId,
                            AttemptKind = AttemptKind.MockTest,
                            StartedAt = nowUtc,
                            ExpiresAt = null,
                            QuestionSnapshotJson = JsonSerializer.Serialize(new MockAttemptSnapshot
                            {
                                MockTestId = practiceTestId,
                                Title = test.Title,
                                JlptLevelName = test.CertificateLevel?.Code,
                                ExamKind = test.ExamKind.ToString(),
                                AttemptStatus = StatusInProgress,
                                LastSavedAt = nowUtc,
                                PreviouslyWrongQuestionIds = previouslyWrongQuestionIds,
                                Questions = frozenQuestions,
                            }, MockSnapshotJson),
                            SubmittedAt = null,
                            TotalScore = 0,
                            IsCompleted = false,
                        };

                        await unitOfWork.MockTestAttempts.AddAsync(newAttempt, transactionCancellationToken);
                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                        if (oldAttempt is not null && !oldAttempt.IsCompleted)
                        {
                            var oldSnap = TryReadMockSnapshotSafe(oldAttempt) ?? new MockAttemptSnapshot { MockTestId = practiceTestId };
                            oldSnap.AttemptStatus = StatusAbandoned;
                            oldSnap.AbandonedAt = nowUtc;
                            oldSnap.SupersededByAttemptId = newAttempt.Id;
                            oldAttempt.QuestionSnapshotJson = JsonSerializer.Serialize(oldSnap, MockSnapshotJson);
                            oldAttempt.IsCompleted = false;
                            oldAttempt.TotalScore = null;
                            oldAttempt.SubmittedAt = nowUtc;
                            unitOfWork.MockTestAttempts.Update(oldAttempt);
                        }

                        var newSnapshots = orderedQuestions.Select(item => new MockTestAnswer
                        {
                            MockTestAttemptId = newAttempt.Id,
                            MockQuestionId = item.Question.Id,
                            SelectedOptionId = null,
                            IsCorrect = false,
                        });
                        await unitOfWork.MockTestAnswers.AddRangeAsync(newSnapshots, transactionCancellationToken);
                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                        response = ApiResponse<StartAttemptResponse>.Created(new StartAttemptResponse
                        {
                            AttemptId = newAttempt.Id,
                            MockTestId = practiceTestId,
                            Title = test.Title,
                            JlptLevelName = test.CertificateLevel?.Code ?? string.Empty,
                            ExamKind = test.ExamKind.ToString(),
                            EnforceStrictTimer = false,
                            IsResumed = false,
                            AttemptStatus = StatusInProgress,
                            StartedAt = newAttempt.StartedAt,
                            ServerNow = DateTime.UtcNow,
                            LastSavedAt = nowUtc,
                            ExpiresAt = null,
                            TimeLimitMinutes = test.TimeLimitMinutes,
                            TotalPreviouslyWrongQuestions = previouslyWrongQuestionIds.Count,
                            PreviouslyWrongQuestionIds = previouslyWrongQuestionIds,
                            Questions = frozenQuestions.Select(question => question.Prompt).ToList(),
                        });
                        return;
                    }

                    var startedAt = DateTime.UtcNow;
                    var attempt = new MockTestAttempt
                    {
                        UserId = userId,
                        AttemptKind = AttemptKind.MockTest,
                        StartedAt = startedAt,
                        ExpiresAt = !isPractice && test.TimeLimitMinutes > 0 ? startedAt.AddMinutes(test.TimeLimitMinutes) : null,
                        QuestionSnapshotJson = JsonSerializer.Serialize(new MockAttemptSnapshot
                        {
                            MockTestId = practiceTestId,
                            Title = test.Title,
                            JlptLevelName = test.CertificateLevel?.Code,
                            ExamKind = test.ExamKind.ToString(),
                            AttemptStatus = StatusInProgress,
                            LastSavedAt = startedAt,
                            PreviouslyWrongQuestionIds = previouslyWrongQuestionIds,
                            Questions = frozenQuestions,
                        }, MockSnapshotJson),
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
                        ExamKind = test.ExamKind.ToString(),
                        EnforceStrictTimer = !isPractice,
                        IsResumed = false,
                        AttemptStatus = StatusInProgress,
                        StartedAt = attempt.StartedAt,
                        ServerNow = DateTime.UtcNow,
                        LastSavedAt = startedAt,
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
                var userId = claimService.GetUserClaim().Id;
                var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
                if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    return ApiResponse<StartAttemptResponse>.NotFound("Không tìm thấy lượt làm bài.");
                if (attempt.UserId != userId)
                    return ApiResponse<StartAttemptResponse>.Fail("Bạn không có quyền xem lượt làm bài này.", HttpStatusCode.Forbidden);

                var frozen = ReadMockSnapshot(attempt);
                if (IsAttemptAbandoned(frozen))
                    return ApiResponse<StartAttemptResponse>.Fail(
                        "Lượt làm bài này đã kết thúc do bạn chọn bắt đầu lượt mới.",
                        HttpStatusCode.Conflict,
                        errorCode: "MOCK_TEST_ABANDONED");

                if (attempt.IsCompleted)
                    return ApiResponse<StartAttemptResponse>.Fail("Lượt làm bài đã được nộp.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_COMPLETED");

                var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, cancellationToken);
                var mockTest = answers.FirstOrDefault()?.MockQuestion?.MockTestSection?.MockTest;
                var resolvedExamKind = ResolveEffectiveExamKind(
                    frozen?.ExamKind,
                    mockTest?.ExamKind,
                    attempt.AttemptKind,
                    attempt.IsPlacementAssessment);
                var isPractice = IsPracticeExamKind(resolvedExamKind);

                if (!isPractice)
                {
                    var deadline = attempt.ExpiresAt is { } exp
                        ? BusinessDateHelper.EnsureUtc(exp)
                        : mockTest is { TimeLimitMinutes: > 0 }
                            ? BusinessDateHelper.EnsureUtc(attempt.StartedAt).AddMinutes(mockTest.TimeLimitMinutes)
                            : (DateTime?)null;
                    if (deadline.HasValue && DateTime.UtcNow >= deadline.Value)
                    {
                        await FinalizeExpiredAttemptAsync(attemptId, cancellationToken);
                        return ApiResponse<StartAttemptResponse>.Fail(
                            "Bài thi thử JLPT đã hết thời gian và đã được hệ thống chấm tự động theo các đáp án đã lưu.",
                            HttpStatusCode.Conflict,
                            errorCode: "MOCK_TEST_EXPIRED");
                    }
                }

                var snapshot = frozen is not null && answers.Count > 0
                    ? new StartAttemptResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = frozen.MockTestId,
                        Title = frozen.Title ?? mockTest?.Title ?? string.Empty,
                        JlptLevelName = frozen.JlptLevelName ?? mockTest?.CertificateLevel?.Code ?? string.Empty,
                        ExamKind = resolvedExamKind,
                        EnforceStrictTimer = !isPractice,
                        IsResumed = true,
                        AttemptStatus = StatusInProgress,
                        StartedAt = BusinessDateHelper.EnsureUtc(attempt.StartedAt),
                        LastSavedAt = frozen.LastSavedAt ?? BusinessDateHelper.EnsureUtc(attempt.ModifiedDate ?? attempt.StartedAt),
                        LatestAnswerSequence = frozen.LatestAnswerSequence,
                        ElapsedSeconds = frozen.ElapsedSeconds,
                        TimeLimitMinutes = !isPractice && attempt.ExpiresAt is { } expiry
                            ? (int)Math.Ceiling((expiry - attempt.StartedAt).TotalMinutes)
                            : mockTest?.TimeLimitMinutes ?? 0,
                        PreviouslyWrongQuestionIds = frozen.PreviouslyWrongQuestionIds,
                        TotalPreviouslyWrongQuestions = frozen.PreviouslyWrongQuestionIds.Count,
                        Questions = frozen.Questions.Select(question => question.Prompt).ToList(),
                    }
                    : attempt.QuestionSnapshotJson is { Length: > 0 } json
                        ? JsonSerializer.Deserialize<StartAttemptResponse>(json, MockSnapshotJson) : null;
                if (snapshot is null || snapshot.AttemptId != attemptId || snapshot.Questions.Count == 0)
                    return ApiResponse<StartAttemptResponse>.Fail("Lượt làm bài này cần được mở lại từ danh sách đề.", HttpStatusCode.Conflict);

                var byQuestion = answers.ToDictionary(answer => answer.MockQuestionId);
                snapshot.ServerNow = DateTime.UtcNow;
                snapshot.ExamKind = resolvedExamKind;
                snapshot.EnforceStrictTimer = !isPractice;
                snapshot.IsResumed = true;
                snapshot.AttemptStatus = StatusInProgress;
                if (!isPractice && attempt.ExpiresAt is { } expiresAt) snapshot.ExpiresAt = BusinessDateHelper.EnsureUtc(expiresAt);
                else snapshot.ExpiresAt = null;
                foreach (var question in snapshot.Questions)
                {
                    SanitizeExamPrompt(question);
                    if (byQuestion.TryGetValue(question.QuestionId, out var answer))
                    {
                        question.SelectedOptionId = answer.SelectedOptionId;
                        question.Response = ParseJsonElement(answer.ResponseJson);
                    }
                }
                return ApiResponse<StartAttemptResponse>.Success(snapshot);
            }
            catch (Exception)
            {
                return ApiResponse<StartAttemptResponse>.Fail("Không thể tải lượt làm bài.", HttpStatusCode.InternalServerError);
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

                    var existingSnapshot = TryReadMockSnapshotSafe(attempt);
                    if (IsAttemptAbandoned(existingSnapshot))
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Lượt làm bài này đã kết thúc do bạn chọn bắt đầu lượt mới.",
                            HttpStatusCode.Conflict,
                            errorCode: "MOCK_TEST_ABANDONED");
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
                    var isPractice = IsPracticeExamKind(mockTest.ExamKind) || IsPracticeExamKind(existingSnapshot?.ExamKind);
                    var frozenQuestionsById = ReadFrozenQuestions(attempt, snapshotAnswers);
                    var now = DateTime.UtcNow;
                    var deadline = isPractice
                        ? (DateTime?)null
                        : attempt.ExpiresAt is { } expiresAt
                            ? BusinessDateHelper.EnsureUtc(expiresAt)
                            : mockTest.TimeLimitMinutes > 0
                                ? BusinessDateHelper.EnsureUtc(attempt.StartedAt).AddMinutes(mockTest.TimeLimitMinutes)
                                : (DateTime?)null;
                    var expired = !isPractice && deadline.HasValue && now >= deadline.Value;
                    if (expired && (!submit || !attempt.ExpiresAt.HasValue))
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail(
                            "Lượt làm bài đã hết thời gian.", HttpStatusCode.BadRequest, errorCode: "MOCK_TEST_EXPIRED");
                        return;
                    }

                    var isStaleSequence = !submit && request.ClientSequence.HasValue && existingSnapshot is not null &&
                        request.ClientSequence.Value < existingSnapshot.LatestAnswerSequence;
                    // At expiry, grade the answers already saved before the server deadline.
                    var submittedAnswers = expired || isStaleSequence ? [] : request.Answers;
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
                        if (existingSnapshot is not null && submittedAnswer.ClientSequence.HasValue &&
                            existingSnapshot.QuestionSequences.TryGetValue(submittedAnswer.QuestionId, out var prevQSeq) &&
                            submittedAnswer.ClientSequence.Value < prevQSeq)
                        {
                            continue;
                        }

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

                        if (existingSnapshot is not null && submittedAnswer.ClientSequence.HasValue)
                            existingSnapshot.QuestionSequences[submittedAnswer.QuestionId] = submittedAnswer.ClientSequence.Value;
                    }

                    if (!submit)
                    {
                        if (existingSnapshot is not null)
                        {
                            existingSnapshot.LastSavedAt = now;
                            if (request.ClientSequence.HasValue && request.ClientSequence.Value > existingSnapshot.LatestAnswerSequence)
                                existingSnapshot.LatestAnswerSequence = request.ClientSequence.Value;
                            if (isPractice && request.ActiveElapsedSeconds is > 0)
                                existingSnapshot.ElapsedSeconds = Math.Clamp(Math.Max(existingSnapshot.ElapsedSeconds, request.ActiveElapsedSeconds.Value), 0, 86400);
                            existingSnapshot.ExamKind = mockTest.ExamKind.ToString();
                            existingSnapshot.AttemptStatus = StatusInProgress;
                            attempt.QuestionSnapshotJson = JsonSerializer.Serialize(existingSnapshot, MockSnapshotJson);
                        }
                        attempt.ModifiedDate = now;
                        unitOfWork.MockTestAttempts.Update(attempt);
                        await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                        response = ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                        {
                            AttemptId = attempt.Id,
                            MockTestId = mockTest.Id,
                            Title = mockTest.Title,
                            JlptLevelName = mockTest.CertificateLevel?.Code ?? existingSnapshot?.JlptLevelName ?? string.Empty,
                            ExamKind = mockTest.ExamKind.ToString(),
                            EnforceStrictTimer = !isPractice,
                            Status = StatusInProgress,
                            StartedAt = attempt.StartedAt,
                            TotalQuestions = snapshotAnswers.Count,
                        });
                        return;
                    }

                    var correctByQuestion = snapshotAnswers.ToDictionary(answer => answer.MockQuestionId, answer => answer.IsCorrect);
                    var questions = snapshotAnswers.Select(answer => answer.MockQuestion).ToList();
                    var levelCode = mockTest.CertificateLevel?.Code ?? existingSnapshot?.JlptLevelName ?? string.Empty;
                    var evaluated = MockTestScoringPolicy.BuildEvaluatedAttempt(
                        attemptId, mockTest.ExamKind.ToString(), levelCode, questions, correctByQuestion);

                    if (existingSnapshot is not null)
                    {
                        existingSnapshot.LastSavedAt = now;
                        if (request.ClientSequence.HasValue && request.ClientSequence.Value > existingSnapshot.LatestAnswerSequence)
                            existingSnapshot.LatestAnswerSequence = request.ClientSequence.Value;
                        if (isPractice && request.ActiveElapsedSeconds is > 0)
                            existingSnapshot.ElapsedSeconds = Math.Clamp(Math.Max(existingSnapshot.ElapsedSeconds, request.ActiveElapsedSeconds.Value), 0, 86400);
                        existingSnapshot.ExamKind = mockTest.ExamKind.ToString();
                        existingSnapshot.JlptLevelName = levelCode;
                        existingSnapshot.AttemptStatus = expired ? StatusExpired : StatusCompleted;
                        existingSnapshot.Scoring = evaluated.Scoring;
                        attempt.QuestionSnapshotJson = JsonSerializer.Serialize(existingSnapshot, MockSnapshotJson);
                    }

                    await unitOfWork.MockTestSectionResults.AddRangeAsync(
                        evaluated.Entities, transactionCancellationToken);

                    attempt.SubmittedAt = expired && deadline.HasValue ? deadline.Value : now;
                    attempt.IsCompleted = true;
                    attempt.TotalScore = evaluated.OverallAccuracyPercent;
                    unitOfWork.MockTestAttempts.Update(attempt);

                    // Answers, section results và trạng thái attempt cùng commit trong một transaction.
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    var totalCount = snapshotAnswers.Count;
                    var answeredCount = snapshotAnswers.Count(HasAnsweredValue);
                    var correctCount = snapshotAnswers.Count(a => a.IsCorrect);
                    var blankCount = Math.Max(0, totalCount - answeredCount);
                    var wrongCount = Math.Max(0, answeredCount - correctCount);
                    var finalStatus = expired ? StatusExpired : StatusCompleted;
                    var durationSec = isPractice && existingSnapshot is { ElapsedSeconds: > 0 }
                        ? existingSnapshot.ElapsedSeconds
                        : Math.Max(0, (int)(attempt.SubmittedAt.Value - BusinessDateHelper.EnsureUtc(attempt.StartedAt)).TotalSeconds);

                    response = ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = mockTest.Id,
                        Title = mockTest.Title,
                        JlptLevelName = levelCode,
                        ExamKind = mockTest.ExamKind.ToString(),
                        EnforceStrictTimer = !isPractice,
                        Status = finalStatus,
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt,
                        TotalScore = attempt.TotalScore.Value,
                        RawScore = evaluated.RawScore,
                        MaxRawScore = evaluated.MaxRawScore,
                        AccuracyPercent = evaluated.OverallAccuracyPercent,
                        Scoring = evaluated.Scoring,
                        CorrectCount = correctCount,
                        WrongCount = wrongCount,
                        BlankCount = blankCount,
                        TotalQuestions = totalCount,
                        DurationSeconds = durationSec,
                        WrongQuestionIds = correctByQuestion
                            .Where(item => !item.Value)
                            .Select(item => item.Key)
                            .OrderBy(id => id)
                            .ToList(),
                        Sections = evaluated.Responses,
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

        public async Task<ApiResponse<AttemptResultResponse>> PauseAttemptAsync(
            int attemptId, SubmitAttemptRequest? request = null, CancellationToken cancellationToken = default)
        {
            if (request is { Answers.Count: > 0 })
            {
                return await StoreAttemptAnswersAsync(attemptId, request, submit: false, cancellationToken);
            }

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var attempt = await unitOfWork.MockTestAttempts.GetByIdAsync(attemptId, cancellationToken);
                if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
                    return ApiResponse<AttemptResultResponse>.NotFound("Không tìm thấy lượt làm bài.");
                if (attempt.UserId != userId)
                    return ApiResponse<AttemptResultResponse>.Fail("Bạn không có quyền thao tác lượt làm bài này.", HttpStatusCode.Forbidden);

                var snap = TryReadMockSnapshotSafe(attempt);
                if (IsAttemptAbandoned(snap))
                    return ApiResponse<AttemptResultResponse>.Fail("Lượt làm bài đã kết thúc.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_ABANDONED");
                if (attempt.IsCompleted)
                    return await GetAttemptResultAsync(attemptId, cancellationToken);

                var now = DateTime.UtcNow;
                if (snap is not null)
                {
                    if (request?.ActiveElapsedSeconds is > 0)
                    {
                        var clampedElapsed = Math.Clamp(request.ActiveElapsedSeconds.Value, 0, 86_400);
                        snap.ElapsedSeconds = Math.Max(snap.ElapsedSeconds, clampedElapsed);
                    }
                    snap.LastSavedAt = now;
                    attempt.QuestionSnapshotJson = JsonSerializer.Serialize(snap, MockSnapshotJson);
                }
                attempt.ModifiedDate = now;
                unitOfWork.MockTestAttempts.Update(attempt);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var pauseExamKind = ResolveEffectiveExamKind(snap?.ExamKind, null, attempt.AttemptKind, attempt.IsPlacementAssessment);

                return ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    MockTestId = snap?.MockTestId ?? 0,
                    Title = snap?.Title ?? string.Empty,
                    JlptLevelName = snap?.JlptLevelName ?? string.Empty,
                    ExamKind = pauseExamKind,
                    EnforceStrictTimer = !IsPracticeExamKind(pauseExamKind),
                    DurationSeconds = snap?.ElapsedSeconds ?? 0,
                    Status = snap?.AttemptStatus ?? StatusInProgress,
                    StartedAt = attempt.StartedAt,
                });
            }
            catch (Exception)
            {
                return ApiResponse<AttemptResultResponse>.Fail("Chưa lưu được trạng thái tạm dừng.", HttpStatusCode.InternalServerError);
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
                    var existingSnap = TryReadMockSnapshotSafe(attempt);
                    if (IsAttemptAbandoned(existingSnap))
                    {
                        response = ApiResponse<bool>.Fail("Lượt làm bài đã kết thúc.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_ABANDONED");
                        return;
                    }
                    var snapshots = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attemptId, ct);
                    var mockTest = snapshots.FirstOrDefault()?.MockQuestion.MockTestSection.MockTest;
                    var timeLimitMinutes = mockTest?.TimeLimitMinutes ?? 0;
                    var isPractice = (mockTest is not null && IsPracticeExamKind(mockTest.ExamKind)) || IsPracticeExamKind(existingSnap?.ExamKind);
                    if (attempt.IsCompleted)
                    {
                        response = ApiResponse<bool>.Fail("Lượt làm bài đã được nộp.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_COMPLETED");
                        return;
                    }
                    if (!isPractice)
                    {
                        var hardDeadline = attempt.ExpiresAt ?? (timeLimitMinutes > 0 ? attempt.StartedAt.AddMinutes(timeLimitMinutes) : (DateTime?)null);
                        if (!hardDeadline.HasValue || DateTime.UtcNow >= hardDeadline.Value)
                        {
                            response = ApiResponse<bool>.Fail("Bài thi thử đã hết thời gian.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_EXPIRED");
                            return;
                        }
                    }

                    if (existingSnap is not null && answer.ClientSequence.HasValue &&
                        existingSnap.QuestionSequences.TryGetValue(answer.QuestionId, out var prevSeq) &&
                        answer.ClientSequence.Value < prevSeq)
                    {
                        response = ApiResponse<bool>.Success(true);
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
                    var saveUtc = DateTime.UtcNow;
                    if (existingSnap is not null)
                    {
                        existingSnap.LastSavedAt = saveUtc;
                        if (answer.ClientSequence.HasValue)
                        {
                            existingSnap.QuestionSequences[answer.QuestionId] = answer.ClientSequence.Value;
                            if (answer.ClientSequence.Value > existingSnap.LatestAnswerSequence)
                                existingSnap.LatestAnswerSequence = answer.ClientSequence.Value;
                        }
                        attempt.QuestionSnapshotJson = JsonSerializer.Serialize(existingSnap, MockSnapshotJson);
                    }
                    attempt.ModifiedDate = saveUtc;
                    unitOfWork.MockTestAttempts.Update(attempt);
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
                var nowUtc = DateTime.UtcNow;
                var attempts = await unitOfWork.MockTestAttempts.GetByUserAsync(userId);
                foreach (var attempt in attempts.Where(item => !item.IsCompleted && !item.IsDeleted))
                {
                    var frozenRaw = TryReadMockSnapshotSafe(attempt);
                    if (IsAttemptAbandoned(frozenRaw)) continue;

                    var answers = await unitOfWork.MockTestAnswers.GetByAttemptIdAsync(attempt.Id, cancellationToken);
                    if (answers.Count == 0) continue;
                    var frozenQuestions = ReadFrozenQuestions(attempt, answers);
                    var frozen = frozenQuestions is null ? frozenRaw : ReadMockSnapshot(attempt);
                    if (frozen is not null
                            ? frozen.MockTestId != practiceTestId
                            : answers.Any(answer => answer.MockQuestion.MockTestSection.MockTestId != practiceTestId))
                        continue;

                    var test = answers[0].MockQuestion.MockTestSection.MockTest;
                    var isPractice = IsPracticeExamKind(test.ExamKind) || IsPracticeExamKind(frozen?.ExamKind);
                    if (!isPractice)
                    {
                        var deadline = attempt.ExpiresAt is { } exp
                            ? BusinessDateHelper.EnsureUtc(exp)
                            : test.TimeLimitMinutes > 0
                                ? BusinessDateHelper.EnsureUtc(attempt.StartedAt).AddMinutes(test.TimeLimitMinutes)
                                : (DateTime?)null;
                        if (deadline.HasValue && nowUtc >= deadline.Value)
                        {
                            // Auto-finalize timed out FullMockTest so it never falsely resumes past deadline
                            await FinalizeExpiredAttemptAsync(attempt.Id, cancellationToken);
                            continue;
                        }
                    }

                    return ApiResponse<StartAttemptResponse?>.Success(new StartAttemptResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = practiceTestId,
                        Title = frozen?.Title ?? test.Title,
                        JlptLevelName = frozen?.JlptLevelName ?? test.CertificateLevel?.Code ?? string.Empty,
                        ExamKind = test.ExamKind.ToString(),
                        EnforceStrictTimer = !isPractice,
                        IsResumed = true,
                        AttemptStatus = StatusInProgress,
                        StartedAt = BusinessDateHelper.EnsureUtc(attempt.StartedAt),
                        ServerNow = nowUtc,
                        LastSavedAt = frozen?.LastSavedAt ?? BusinessDateHelper.EnsureUtc(attempt.ModifiedDate ?? attempt.StartedAt),
                        LatestAnswerSequence = frozen?.LatestAnswerSequence ?? 0,
                        ElapsedSeconds = frozen?.ElapsedSeconds ?? 0,
                        ExpiresAt = !isPractice && attempt.ExpiresAt is { } expiry ? BusinessDateHelper.EnsureUtc(expiry) : null,
                        PreviouslyWrongQuestionIds = frozen?.PreviouslyWrongQuestionIds ?? [],
                        TotalPreviouslyWrongQuestions = frozen?.PreviouslyWrongQuestionIds.Count ?? 0,
                        TimeLimitMinutes = !isPractice && attempt.ExpiresAt.HasValue
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
                                LanguageSkillName = section.LanguageSkill?.Name,
                                QuestionGroupTitle = question.QuestionGroupTitle,
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
                    "Không thể khôi phục lượt làm bài.", HttpStatusCode.InternalServerError);
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
                    var existingSnap = TryReadMockSnapshotSafe(attempt);
                    if (IsAttemptAbandoned(existingSnap))
                    {
                        response = ApiResponse<AttemptResultResponse>.Fail("Lượt làm bài đã kết thúc.", HttpStatusCode.Conflict, errorCode: "MOCK_TEST_ABANDONED");
                        return;
                    }
                    var levelCode = existingSnap?.JlptLevelName ?? mockTest.CertificateLevel?.Code ?? string.Empty;
                    var evaluated = MockTestScoringPolicy.BuildEvaluatedAttempt(
                        attemptId, mockTest.ExamKind.ToString(), levelCode, answers.Select(a => a.MockQuestion).ToList(), correctByQuestion);
                    await unitOfWork.MockTestSectionResults.AddRangeAsync(evaluated.Entities, ct);
                    attempt.SubmittedAt = deadline;
                    attempt.IsCompleted = true;
                    attempt.TotalScore = evaluated.OverallAccuracyPercent;
                    if (existingSnap is not null)
                    {
                        existingSnap.AttemptStatus = StatusExpired;
                        existingSnap.JlptLevelName = levelCode;
                        existingSnap.Scoring = evaluated.Scoring;
                        attempt.QuestionSnapshotJson = JsonSerializer.Serialize(existingSnap, MockSnapshotJson);
                    }
                    unitOfWork.MockTestAttempts.Update(attempt);
                    await unitOfWork.SaveChangesAsync(ct);

                    var totalCount = answers.Count;
                    var answeredCount = answers.Count(HasAnsweredValue);
                    var correctCount = answers.Count(a => a.IsCorrect);
                    var blankCount = Math.Max(0, totalCount - answeredCount);
                    var wrongCount = Math.Max(0, answeredCount - correctCount);
                    response = ApiResponse<AttemptResultResponse>.Success(new AttemptResultResponse
                    {
                        AttemptId = attempt.Id,
                        MockTestId = mockTest.Id,
                        Title = existingSnap?.Title ?? mockTest.Title,
                        JlptLevelName = levelCode,
                        ExamKind = mockTest.ExamKind.ToString(),
                        EnforceStrictTimer = !IsPracticeExamKind(mockTest.ExamKind),
                        Status = StatusExpired,
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt,
                        TotalScore = evaluated.OverallAccuracyPercent,
                        RawScore = evaluated.RawScore,
                        MaxRawScore = evaluated.MaxRawScore,
                        AccuracyPercent = evaluated.OverallAccuracyPercent,
                        Scoring = evaluated.Scoring,
                        CorrectCount = correctCount,
                        WrongCount = wrongCount,
                        BlankCount = blankCount,
                        TotalQuestions = totalCount,
                        DurationSeconds = Math.Max(0, (int)(deadline - BusinessDateHelper.EnsureUtc(attempt.StartedAt)).TotalSeconds),
                        WrongQuestionIds = correctByQuestion.Where(item => !item.Value).Select(item => item.Key).OrderBy(id => id).ToList(),
                        Sections = evaluated.Responses,
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
                var snap = TryReadMockSnapshotSafe(attempt);
                var mockTest = answers.FirstOrDefault()?.MockQuestion?.MockTestSection?.MockTest
                    ?? sectionResults.FirstOrDefault()?.MockTestSection?.MockTest;
                var examKind = ResolveEffectiveExamKind(
                    snap?.ExamKind,
                    mockTest?.ExamKind,
                    attempt.AttemptKind,
                    attempt.IsPlacementAssessment);
                var totalCount = answers?.Count ?? sectionResults.Sum(s => s.TotalCount);
                var answeredCount = answers?.Count(HasAnsweredValue) ?? totalCount;
                var correctCount = answers?.Count(a => a.IsCorrect) ?? sectionResults.Sum(s => s.CorrectCount);
                var blankCount = Math.Max(0, totalCount - answeredCount);
                var wrongCount = Math.Max(0, answeredCount - correctCount);
                var status = ResolveAttemptStatus(attempt, snap);
                var submittedUtc = attempt.SubmittedAt.HasValue ? BusinessDateHelper.EnsureUtc(attempt.SubmittedAt.Value) : DateTime.UtcNow;
                var durationSec = IsPracticeExamKind(examKind) && snap is { ElapsedSeconds: > 0 }
                    ? snap.ElapsedSeconds
                    : Math.Max(0, (int)(submittedUtc - BusinessDateHelper.EnsureUtc(attempt.StartedAt)).TotalSeconds);
                var levelCode = snap?.JlptLevelName ?? mockTest?.CertificateLevel?.Code ?? sectionResults.FirstOrDefault()?.MockTestSection?.MockTest?.CertificateLevel?.Code ?? string.Empty;
                var scoring = CanReuseSnapshotScoring(snap?.Scoring, examKind)
                    ? snap!.Scoring!
                    : MockTestScoringPolicy.EvaluateFromSectionResults(
                        examKind, levelCode, sectionResults, correctCount, totalCount, attempt.TotalScore);

                var result = new AttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    MockTestId = sectionResults
                        .Select(section => section.MockTestSection.MockTestId)
                        .FirstOrDefault(),
                    Title = snap?.Title ?? mockTest?.Title ?? string.Empty,
                    JlptLevelName = levelCode,
                    ExamKind = examKind,
                    EnforceStrictTimer = !IsPracticeExamKind(examKind),
                    Status = status,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    TotalScore = attempt.TotalScore.GetValueOrDefault(),
                    RawScore = scoring.RawScore,
                    MaxRawScore = scoring.MaxRawScore,
                    AccuracyPercent = scoring.AccuracyPercent,
                    Scoring = scoring,
                    CorrectCount = correctCount,
                    WrongCount = wrongCount,
                    BlankCount = blankCount,
                    TotalQuestions = totalCount,
                    DurationSeconds = durationSec,
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
                        LanguageSkillCode = r.MockTestSection.LanguageSkill?.Code,
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

                await EnrichSummariesForLearnerAsync(result, cancellationToken);
                return ApiResponse<List<TestSummaryResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail(
                    "Không thể tải danh sách đề thi.", HttpStatusCode.InternalServerError);
            }
        }

        public Task<ApiResponse<List<TestSummaryResponse>>> GetPracticeSetsAsync(
            int? certificateLevelId = null, int? languageSkillId = null, string? examKind = null, string? keyword = null,
            CancellationToken cancellationToken = default) =>
            GetPracticeSetsAsync(certificateLevelId, languageSkillId, examKind, keyword, status: null, sort: null, cancellationToken);

        public async Task<ApiResponse<List<TestSummaryResponse>>> GetPracticeSetsAsync(
            int? certificateLevelId, int? languageSkillId, string? examKind, string? keyword,
            string? status, string? sort, CancellationToken cancellationToken = default)
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

                await EnrichSummariesForLearnerAsync(result, cancellationToken);
                result = FilterAndSortSummaries(result, status, sort);
                return ApiResponse<List<TestSummaryResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<TestSummaryResponse>>.Fail("Không thể tải danh sách bộ đề luyện tập.", HttpStatusCode.InternalServerError);
            }
        }

        public Task<ApiResponse<List<TestSummaryResponse>>> GetFullMockTestsAsync(
            int? certificateLevelId = null, string? keyword = null, CancellationToken cancellationToken = default) =>
            GetFullMockTestsAsync(certificateLevelId, keyword, status: null, sort: null, cancellationToken);

        public async Task<ApiResponse<List<TestSummaryResponse>>> GetFullMockTestsAsync(
            int? certificateLevelId, string? keyword, string? status, string? sort, CancellationToken cancellationToken = default)
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

                await EnrichSummariesForLearnerAsync(ready, cancellationToken);
                ready = FilterAndSortSummaries(ready, status, sort);
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
            if (test is null || !IsLearnerReady(test) || !await IsApprovedForLearnersAsync(test, cancellationToken))
                return ApiResponse<TestSummaryResponse>.NotFound("Đề thi chưa sẵn sàng.");
            var summary = ToTestSummary(test);
            await EnrichSummariesForLearnerAsync([summary], cancellationToken);
            return ApiResponse<TestSummaryResponse>.Success(summary);
        }

        public async Task<ApiResponse<PracticeSetPreviewResponse>> GetPracticeSetPreviewAsync(
            int practiceTestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var test = await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(practiceTestId, cancellationToken);
                if (test is null || test.IsDeleted || test.IsArchivedVersion)
                    return ApiResponse<PracticeSetPreviewResponse>.NotFound("Không tìm thấy bộ đề luyện tập.");

                if (test.ExamKind is not (ExamKind.PracticeSet or ExamKind.MixedPractice))
                    return ApiResponse<PracticeSetPreviewResponse>.Fail(
                        "Đề thi thử JLPT không cho phép xem trước danh sách câu hỏi.",
                        HttpStatusCode.Forbidden,
                        errorCode: "MOCK_TEST_PREVIEW_FORBIDDEN");

                if (!IsLearnerReady(test) || !await IsApprovedForLearnersAsync(test, cancellationToken))
                    return ApiResponse<PracticeSetPreviewResponse>.Fail(
                        "Bộ đề hiện chưa khả dụng hoặc đang chờ cập nhật.",
                        HttpStatusCode.Conflict,
                        errorCode: "PRACTICE_SET_UNAVAILABLE");

                var summary = ToTestSummary(test);
                await EnrichSummariesForLearnerAsync([summary], cancellationToken);

                // Prevent accidental leak of shared FullMockTest questions unless not shared with published FullMockTest
                var sections = (test.Sections ?? []).Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                    .ToList();
                var bankIds = sections.SelectMany(s => s.Questions ?? [])
                    .Where(q => !q.IsDeleted && q.QuestionBankItemId.HasValue)
                    .Select(q => q.QuestionBankItemId!.Value)
                    .Distinct()
                    .ToList();
                HashSet<int> confidentialMockBankIds = [];
                if (bankIds.Count > 0)
                {
                    confidentialMockBankIds = (await unitOfWork.MockTests.Query().AsNoTracking()
                        .Where(m => !m.IsDeleted && !m.IsArchivedVersion && m.ExamKind == ExamKind.FullMockTest && m.PublishStatus == ContentStatus.Published)
                        .SelectMany(m => m.Sections!.Where(s => !s.IsDeleted))
                        .SelectMany(s => s.Questions!.Where(q => !q.IsDeleted && q.QuestionBankItemId.HasValue))
                        .Where(q => bankIds.Contains(q.QuestionBankItemId!.Value))
                        .Select(q => q.QuestionBankItemId!.Value)
                        .Distinct()
                        .ToListAsync(cancellationToken)).ToHashSet();
                }

                var qOrder = 0;
                var previewSections = sections.Select(section =>
                {
                    var orderedQs = (section.Questions ?? []).Where(q => !q.IsDeleted)
                        .OrderBy(q => q.SortOrder).ThenBy(q => q.Id)
                        .ToList();
                    return new PracticeSetPreviewSectionResponse
                    {
                        SectionId = section.Id,
                        Title = section.Title,
                        SkillName = section.LanguageSkill?.Name ?? string.Empty,
                        DurationMinutes = section.DurationMinutes,
                        QuestionCount = orderedQs.Count,
                        RequiresAudio = orderedQs.Any(q => !string.IsNullOrWhiteSpace(q.AudioUrl ?? q.Passage?.AudioUrl)),
                        Questions = orderedQs.Select(question =>
                        {
                            qOrder++;
                            var isConfidential = question.QuestionBankItemId.HasValue && confidentialMockBankIds.Contains(question.QuestionBankItemId.Value);
                            if (isConfidential)
                            {
                                return new PracticeSetPreviewQuestionResponse
                                {
                                    QuestionId = question.Id,
                                    Order = qOrder,
                                    QuestionText = "Câu hỏi thuộc phạm vi dùng chung với ngân hàng Thi thử JLPT — nội dung chi tiết chỉ hiển thị khi làm bài.",
                                    QuestionType = question.QuestionType,
                                    SkillName = section.LanguageSkill?.Name ?? string.Empty,
                                    QuestionGroupTitle = question.QuestionGroupTitle,
                                    HasAudio = !string.IsNullOrWhiteSpace(question.AudioUrl ?? question.Passage?.AudioUrl),
                                    HasImage = !string.IsNullOrWhiteSpace(question.ImageUrl ?? question.Passage?.ImageUrl),
                                    IsConfidentialRedacted = true,
                                    Options = [],
                                };
                            }

                            var interactionData = ParseJsonElement(question.InteractionDataJson);
                            return new PracticeSetPreviewQuestionResponse
                            {
                                QuestionId = question.Id,
                                Order = qOrder,
                                QuestionText = question.QuestionText,
                                QuestionType = question.QuestionType,
                                SkillName = section.LanguageSkill?.Name ?? string.Empty,
                                QuestionGroupTitle = question.QuestionGroupTitle,
                                PassageId = question.PassageId,
                                PassageTitle = question.Passage?.Title,
                                PassageText = ResolvePassageText(question.Passage?.PassageText, interactionData),
                                PassageInstructions = question.Passage?.Instructions,
                                HasAudio = !string.IsNullOrWhiteSpace(question.AudioUrl ?? question.Passage?.AudioUrl),
                                HasImage = !string.IsNullOrWhiteSpace(question.ImageUrl ?? question.Passage?.ImageUrl),
                                IsConfidentialRedacted = false,
                                // NEVER return IsCorrect, AnswerKey, Explanation, or AudioTranscript!
                                Options = (question.Options ?? []).Where(o => !o.IsDeleted)
                                    .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                                    .Select((o, idx) => new PracticeSetPreviewOptionResponse
                                    {
                                        Order = idx + 1,
                                        OptionText = PublicOptionText(o, question.InteractionDataJson, idx),
                                        HasImage = !string.IsNullOrWhiteSpace(o.ImageUrl),
                                    }).ToList(),
                            };
                        }).ToList(),
                    };
                }).ToList();

                return ApiResponse<PracticeSetPreviewResponse>.Success(new PracticeSetPreviewResponse
                {
                    MockTestId = summary.MockTestId,
                    Title = summary.Title,
                    Description = summary.Description,
                    JlptLevelName = summary.JlptLevelName,
                    ExamKind = summary.ExamKind,
                    Version = summary.Version,
                    TimeLimitMinutes = summary.TimeLimitMinutes,
                    QuestionCount = summary.QuestionCount,
                    IncludedSkills = summary.IncludedSkills,
                    UserStatus = summary.UserStatus,
                    ActiveAttemptId = summary.ActiveAttemptId,
                    ActiveAnsweredCount = summary.ActiveAnsweredCount,
                    ActiveTotalQuestions = summary.ActiveTotalQuestions,
                    ActiveProgressPercent = summary.ActiveProgressPercent,
                    ActiveLastSavedAt = summary.ActiveLastSavedAt,
                    CanStart = summary.CanStart,
                    CanDownloadPdf = summary.CanDownloadPdf,
                    UnavailableReason = summary.UnavailableReason,
                    Sections = previewSections,
                });
            }
            catch (Exception)
            {
                return ApiResponse<PracticeSetPreviewResponse>.Fail("Không thể tải thông tin bộ đề luyện tập.", HttpStatusCode.InternalServerError);
            }
        }

        private async Task EnrichSummariesForLearnerAsync(List<TestSummaryResponse> items, CancellationToken cancellationToken)
        {
            if (items.Count == 0) return;

            var featureGate = await EvaluateMockFeatureAsync(cancellationToken);
            int userId;
            try { userId = claimService.GetUserClaim().Id; }
            catch { return; }

            await AutoFinalizeExpiredMockAttemptsInternalAsync(userId, null, cancellationToken);

            var attempts = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
                .Where(a => a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.MockTest)
                .OrderByDescending(a => a.StartedAt).ThenByDescending(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.StartedAt,
                    a.SubmittedAt,
                    a.ModifiedDate,
                    a.ExpiresAt,
                    a.IsCompleted,
                    a.TotalScore,
                    a.QuestionSnapshotJson,
                    TotalAnswers = a.Answers!.Count,
                    AnsweredCount = a.Answers!.Count(ans => ans.SelectedOptionId != null || ans.ResponseJson != null),
                    CorrectCount = a.Answers!.Count(ans => ans.IsCorrect),
                    FallbackTestId = a.SectionResults!.Select(sr => (int?)sr.MockTestSection.MockTestId).FirstOrDefault()
                        ?? a.Answers!.Select(ans => (int?)ans.MockQuestion.MockTestSection.MockTestId).FirstOrDefault(),
                    SectionRows = a.SectionResults!.Select(sr => new ProjectedSectionScoreRow(
                        sr.MockTestSectionId,
                        sr.MockTestSection.Title,
                        sr.MockTestSection.LanguageSkill.Code,
                        sr.MockTestSection.LanguageSkill.Name,
                        sr.CorrectCount,
                        sr.TotalCount,
                        sr.ScorePercent)).ToList(),
                })
                .ToListAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow;
            var byTest = new Dictionary<int, List<(
                int Id, DateTime StartedAt, DateTime? SubmittedAt, DateTime? ModifiedDate, DateTime? ExpiresAt,
                bool IsCompleted, double? TotalScore, int TotalAnswers, int AnsweredCount, int CorrectCount, MockAttemptSnapshot? Snap, List<ProjectedSectionScoreRow> SectionRows)>>();

            foreach (var a in attempts)
            {
                MockAttemptSnapshot? snap = null;
                if (!string.IsNullOrWhiteSpace(a.QuestionSnapshotJson))
                {
                    try { snap = JsonSerializer.Deserialize<MockAttemptSnapshot>(a.QuestionSnapshotJson, MockSnapshotJson); }
                    catch (JsonException) { }
                }
                var testId = snap?.MockTestId > 0 ? snap.MockTestId : (a.FallbackTestId ?? 0);
                if (testId <= 0) continue;
                if (!byTest.TryGetValue(testId, out var list))
                {
                    list = [];
                    byTest[testId] = list;
                }
                list.Add((a.Id, a.StartedAt, a.SubmittedAt, a.ModifiedDate, a.ExpiresAt, a.IsCompleted, a.TotalScore, a.TotalAnswers, a.AnsweredCount, a.CorrectCount, snap, a.SectionRows));
            }

            foreach (var item in items)
            {
                var isPractice = IsPracticeExamKind(item.ExamKind);
                byTest.TryGetValue(item.MockTestId, out var list);
                list ??= [];

                var active = list.FirstOrDefault(a =>
                    !a.IsCompleted &&
                    !IsAttemptAbandoned(a.Snap) &&
                    (isPractice || !a.ExpiresAt.HasValue || BusinessDateHelper.EnsureUtc(a.ExpiresAt.Value) > nowUtc));

                var completed = list
                    .Where(a => a.IsCompleted && !IsAttemptAbandoned(a.Snap) && a.TotalScore.HasValue)
                    .OrderByDescending(a => a.SubmittedAt ?? a.StartedAt).ThenByDescending(a => a.Id)
                    .ToList();
                var latestCompleted = completed.FirstOrDefault();

                item.AttemptsCompletedCount = completed.Count;
                item.EnforceStrictTimer = !isPractice;

                if (latestCompleted.Id > 0)
                {
                    var latestTotalQ = latestCompleted.TotalAnswers > 0 ? latestCompleted.TotalAnswers : item.QuestionCount;
                    item.LatestCompletedAttemptId = latestCompleted.Id;
                    item.LatestScore = latestCompleted.TotalScore;
                    item.LatestSubmittedAt = latestCompleted.SubmittedAt.HasValue
                        ? BusinessDateHelper.EnsureUtc(latestCompleted.SubmittedAt.Value)
                        : BusinessDateHelper.EnsureUtc(latestCompleted.StartedAt);
                    item.LatestCorrectCount = latestCompleted.CorrectCount;
                    item.LatestTotalQuestions = latestTotalQ;
                    item.LatestScoring = latestCompleted.Snap?.Scoring ?? BuildScoringFromProjectedRows(
                        item.ExamKind, item.JlptLevelName, latestCompleted.SectionRows, latestCompleted.CorrectCount, latestTotalQ, latestCompleted.TotalScore);
                }

                if (active.Id > 0)
                {
                    var totalQ = active.TotalAnswers > 0 ? active.TotalAnswers : item.QuestionCount;
                    var pct = totalQ > 0 ? (int)Math.Round(active.AnsweredCount * 100d / totalQ) : 0;
                    item.UserStatus = "IN_PROGRESS";
                    item.ActiveAttemptId = active.Id;
                    item.ActiveAnsweredCount = active.AnsweredCount;
                    item.ActiveTotalQuestions = totalQ;
                    item.ActiveProgressPercent = pct;
                    item.ActiveLastSavedAt = active.Snap?.LastSavedAt ?? BusinessDateHelper.EnsureUtc(active.ModifiedDate ?? active.StartedAt);
                    item.ActiveElapsedSeconds = active.Snap?.ElapsedSeconds ?? 0;
                    if (!isPractice && active.ExpiresAt.HasValue)
                    {
                        var exp = BusinessDateHelper.EnsureUtc(active.ExpiresAt.Value);
                        item.ActiveExpiresAt = exp;
                        item.ActiveRemainingSeconds = Math.Max(0, (int)Math.Ceiling((exp - nowUtc).TotalSeconds));
                    }
                    item.CanResume = true;
                    item.CanRestart = isPractice && featureGate.Allowed;
                    item.CanStart = featureGate.Allowed;
                    if (!featureGate.Allowed) item.UnavailableReason = featureGate.Message;
                }
                else if (!featureGate.Allowed)
                {
                    item.UserStatus = "UNAVAILABLE";
                    item.CanStart = false;
                    item.CanResume = false;
                    item.CanRestart = false;
                    item.UnavailableReason = featureGate.Message;
                }
                else if (latestCompleted.Id > 0)
                {
                    var latestExpired = string.Equals(latestCompleted.Snap?.AttemptStatus, StatusExpired, StringComparison.OrdinalIgnoreCase);
                    item.UserStatus = latestExpired ? "EXPIRED" : "COMPLETED";
                    item.CanStart = true;
                }
                else
                {
                    item.UserStatus = "NOT_STARTED";
                    item.CanStart = true;
                }

                item.CanDownloadPdf = featureGate.Allowed && (isPractice || completed.Count > 0);
            }
        }

        private static List<TestSummaryResponse> FilterAndSortSummaries(
            List<TestSummaryResponse> items, string? status, string? sort)
        {
            IEnumerable<TestSummaryResponse> q = items;
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var st = status.Trim().ToUpperInvariant();
                q = st switch
                {
                    "COMPLETED" => q.Where(i => i.UserStatus is "COMPLETED" or "EXPIRED"),
                    "IN_PROGRESS" => q.Where(i => i.UserStatus == "IN_PROGRESS"),
                    "NOT_STARTED" => q.Where(i => i.UserStatus == "NOT_STARTED"),
                    "EXPIRED" => q.Where(i => i.UserStatus == "EXPIRED"),
                    "UNAVAILABLE" => q.Where(i => i.UserStatus == "UNAVAILABLE"),
                    _ => q,
                };
            }

            q = (sort ?? "default").Trim().ToLowerInvariant() switch
            {
                "title" => q.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase),
                "level" => q.OrderBy(i => i.CertificateLevelId).ThenBy(i => i.Title),
                "progress" => q.OrderByDescending(i => i.UserStatus == "IN_PROGRESS" ? 2 : i.UserStatus is "COMPLETED" or "EXPIRED" ? 1 : 0)
                    .ThenByDescending(i => i.ActiveProgressPercent ?? 0)
                    .ThenByDescending(i => i.PublishedAt ?? DateTime.MinValue),
                "newest" => q.OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue).ThenByDescending(i => i.MockTestId),
                _ => q,
            };

            return q.ToList();
        }

        public Task<ApiResponse<List<MockTestHistoryItem>>> GetHistoryAsync(CancellationToken cancellationToken = default) =>
            GetHistoryAsync(kind: null, status: null, cancellationToken);

        public async Task<ApiResponse<List<MockTestHistoryItem>>> GetHistoryAsync(
            string? kind, string? status, CancellationToken cancellationToken = default)
        {
            try
            {
                var (items, _) = await BuildHistoryInternalAsync(kind, status, null, null, null, cancellationToken);
                return ApiResponse<List<MockTestHistoryItem>>.Success(items);
            }
            catch (Exception)
            {
                return ApiResponse<List<MockTestHistoryItem>>.Fail("Không thể tải lịch sử làm bài.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PagedMockTestHistoryResponse>> GetPagedHistoryAsync(
            string? kind,
            string? status,
            string? jlptLevel,
            int? days,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var (items, stats) = await BuildHistoryInternalAsync(kind, status, jlptLevel, days, search, cancellationToken);
                var safePage = Math.Max(1, page);
                var safeSize = Math.Clamp(pageSize, 1, 50);
                var pageItems = items.Skip((safePage - 1) * safeSize).Take(safeSize).ToList();
                return ApiResponse<PagedMockTestHistoryResponse>.Success(
                    new PagedMockTestHistoryResponse(
                        pageItems,
                        items.Count,
                        safePage,
                        safeSize,
                        safePage * safeSize < items.Count,
                        stats));
            }
            catch (Exception)
            {
                return ApiResponse<PagedMockTestHistoryResponse>.Fail("Không thể tải lịch sử làm bài.", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<(List<MockTestHistoryItem> Items, ExamHistoryStatsResponse Stats)> BuildHistoryInternalAsync(
            string? kind,
            string? status,
            string? jlptLevel,
            int? days,
            string? search,
            CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            await AutoFinalizeExpiredMockAttemptsInternalAsync(userId, null, cancellationToken);

            var attempts = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
                .Where(attempt => attempt.UserId == userId
                    && !attempt.IsDeleted
                    && (attempt.AttemptKind == AttemptKind.MockTest || attempt.AttemptKind == AttemptKind.Placement))
                .OrderByDescending(attempt => attempt.SubmittedAt ?? attempt.ModifiedDate ?? attempt.StartedAt)
                .ThenByDescending(attempt => attempt.Id)
                .Select(attempt => new
                {
                    attempt.Id,
                    attempt.AttemptKind,
                    attempt.IsPlacementAssessment,
                    attempt.StartedAt,
                    attempt.SubmittedAt,
                    attempt.ModifiedDate,
                    attempt.ExpiresAt,
                    attempt.IsCompleted,
                    Score = attempt.TotalScore,
                    attempt.QuestionSnapshotJson,
                    TotalQuestions = attempt.Answers!.Count,
                    AnsweredCount = attempt.Answers!.Count(ans => ans.SelectedOptionId != null || ans.ResponseJson != null),
                    CorrectCount = attempt.Answers!.Count(ans => ans.IsCorrect),
                    SectionRows = attempt.SectionResults!.Select(sr => new ProjectedSectionScoreRow(
                        sr.MockTestSectionId,
                        sr.MockTestSection.Title,
                        sr.MockTestSection.LanguageSkill.Code,
                        sr.MockTestSection.LanguageSkill.Name,
                        sr.CorrectCount,
                        sr.TotalCount,
                        sr.ScorePercent)).ToList(),
                    Test = attempt.SectionResults!.OrderBy(section => section.MockTestSectionId)
                        .Select(section => new
                        {
                            Id = section.MockTestSection.MockTestId,
                            section.MockTestSection.MockTest.Title,
                            Level = section.MockTestSection.MockTest.CertificateLevel.Code,
                            ExamKind = section.MockTestSection.MockTest.ExamKind,
                        }).FirstOrDefault()
                        ?? attempt.Answers!.OrderBy(ans => ans.Id)
                            .Select(ans => new
                            {
                                Id = ans.MockQuestion.MockTestSection.MockTestId,
                                ans.MockQuestion.MockTestSection.MockTest.Title,
                                Level = ans.MockQuestion.MockTestSection.MockTest.CertificateLevel.Code,
                                ExamKind = ans.MockQuestion.MockTestSection.MockTest.ExamKind,
                            }).FirstOrDefault(),
                }).ToListAsync(cancellationToken);

            var baselineForStats = new List<MockTestHistoryItem>();
            var filtered = new List<MockTestHistoryItem>();
            var cutoffUtc = days is > 0 ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;
            var levelNorm = string.IsNullOrWhiteSpace(jlptLevel) || string.Equals(jlptLevel, "ALL", StringComparison.OrdinalIgnoreCase)
                ? null : jlptLevel.Trim().ToUpperInvariant();
            var keyword = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            foreach (var attempt in attempts)
            {
                var isPlacementRow = attempt.AttemptKind == AttemptKind.Placement
                    || attempt.IsPlacementAssessment
                    || attempt.Test?.ExamKind == ExamKind.Placement;

                if (!isPlacementRow && attempt.Test is null) continue;

                if (attempt.AttemptKind == AttemptKind.Placement)
                {
                    // Skip onboarding skip-markers that have no questions and no snapshot
                    if (string.IsNullOrWhiteSpace(attempt.QuestionSnapshotJson)
                        && attempt.TotalQuestions == 0
                        && attempt.SectionRows.Count == 0)
                        continue;

                    var completed = PracticeTestService.TryGetCompletedResultFromSnapshot(attempt.QuestionSnapshotJson);
                    var pStatus = attempt.IsCompleted
                        ? StatusCompleted
                        : (attempt.ExpiresAt is { } exp && BusinessDateHelper.EnsureUtc(exp) <= DateTime.UtcNow ? StatusExpired : StatusInProgress);

                    var includeInDefaultStatus = pStatus != StatusAbandoned
                        && (attempt.IsCompleted || string.Equals(status, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase));

                    var pDate = BusinessDateHelper.EnsureUtc(completed?.SubmittedAt ?? attempt.SubmittedAt ?? attempt.ModifiedDate ?? attempt.StartedAt);
                    var pStarted = BusinessDateHelper.EnsureUtc(completed?.StartedAt ?? attempt.StartedAt);
                    var pDuration = completed?.DurationSeconds > 0
                        ? completed.DurationSeconds
                        : Math.Max(0, (int)(pDate - pStarted).TotalSeconds);
                    var pCorrect = completed?.CorrectCount ?? attempt.CorrectCount;
                    var pTotalQ = completed?.TotalQuestions > 0 ? completed.TotalQuestions : attempt.TotalQuestions;
                    var pBlank = Math.Max(0, pTotalQ - Math.Max(attempt.AnsweredCount, pCorrect));
                    var pWrong = Math.Max(0, Math.Max(attempt.AnsweredCount, pCorrect) - pCorrect);
                    var pAccuracy = Math.Round(completed?.TotalScore ?? (attempt.Score ?? 0d), 1);
                    var pLevel = completed?.ScreeningLevelName
                        ?? completed?.AssessedJlptLevelName
                        ?? completed?.RecommendedTargetJlptLevelName
                        ?? attempt.Test?.Level
                        ?? string.Empty;
                    var pTitle = completed?.IsComprehensiveAssessment == true
                        ? "Đánh giá năng lực JLPT toàn diện"
                        : !string.IsNullOrWhiteSpace(pLevel)
                            ? $"Đánh giá năng lực {pLevel}"
                            : "Đánh giá năng lực JLPT";

                    var pScoring = new MockAttemptScoringSummary
                    {
                        ScoreType = "PLACEMENT_ASSESSMENT",
                        JlptLevelCode = pLevel,
                        AccuracyPercent = pAccuracy,
                        NormalizedScore = pAccuracy,
                        NormalizedMaxScore = 100,
                        IsPassStatusApplicable = false,
                        IsScoringValid = attempt.IsCompleted && completed is not null,
                    };

                    var pItem = new MockTestHistoryItem(
                        attempt.Id,
                        attempt.Test?.Id ?? 0,
                        pTitle,
                        pLevel,
                        pDate,
                        pAccuracy,
                        nameof(ExamKind.Placement),
                        pStatus,
                        pCorrect,
                        pWrong,
                        pBlank,
                        pTotalQ,
                        pDuration,
                        pStarted,
                        CanReview: false,
                        Scoring: pScoring,
                        AttemptKind: nameof(AttemptKind.Placement),
                        AssessedJlptLevelName: completed?.AssessedJlptLevelName,
                        RecommendedTargetJlptLevelName: completed?.RecommendedTargetJlptLevelName);

                    if (pStatus != StatusAbandoned) baselineForStats.Add(pItem);
                    if (!includeInDefaultStatus) continue;
                    if (!MatchesHistoryFilters(pItem, kind, status, levelNorm, cutoffUtc, keyword)) continue;
                    filtered.Add(pItem);
                    continue;
                }

                MockAttemptSnapshot? frozen = null;
                if (!string.IsNullOrWhiteSpace(attempt.QuestionSnapshotJson))
                {
                    try { frozen = JsonSerializer.Deserialize<MockAttemptSnapshot>(attempt.QuestionSnapshotJson, MockSnapshotJson); }
                    catch (JsonException) { /* Preserve access to legacy history. */ }
                }

                var attemptStatus = ResolveAttemptStatus(new MockTestAttempt
                {
                    IsCompleted = attempt.IsCompleted,
                }, frozen);

                var examKind = ResolveEffectiveExamKind(
                    frozen?.ExamKind,
                    attempt.Test?.ExamKind,
                    attempt.AttemptKind,
                    attempt.IsPlacementAssessment);
                var isPlacementLegacy = string.Equals(examKind, nameof(ExamKind.Placement), StringComparison.OrdinalIgnoreCase);
                var totalQ = attempt.TotalQuestions;
                var correctC = attempt.CorrectCount;
                var blankC = Math.Max(0, totalQ - attempt.AnsweredCount);
                var wrongC = Math.Max(0, attempt.AnsweredCount - correctC);
                var date = BusinessDateHelper.EnsureUtc(attempt.SubmittedAt ?? attempt.ModifiedDate ?? attempt.StartedAt);
                var started = BusinessDateHelper.EnsureUtc(attempt.StartedAt);
                var durationSec = IsPracticeExamKind(examKind) && frozen is { ElapsedSeconds: > 0 }
                    ? frozen.ElapsedSeconds
                    : Math.Max(0, (int)(date - started).TotalSeconds);

                var levelCode = frozen?.JlptLevelName ?? attempt.Test?.Level ?? string.Empty;
                var historyScoring = attempt.IsCompleted && attemptStatus != StatusAbandoned && attempt.Score.HasValue
                    ? (CanReuseSnapshotScoring(frozen?.Scoring, examKind)
                        ? frozen!.Scoring!
                        : BuildScoringFromProjectedRows(
                            examKind, levelCode, attempt.SectionRows, correctC, totalQ, attempt.Score))
                    : null;

                var item = new MockTestHistoryItem(
                    attempt.Id,
                    attempt.Test?.Id ?? frozen?.MockTestId ?? 0,
                    frozen?.Title ?? attempt.Test?.Title ?? string.Empty,
                    levelCode,
                    date,
                    attempt.Score ?? 0d,
                    examKind,
                    attemptStatus,
                    correctC,
                    wrongC,
                    blankC,
                    totalQ,
                    durationSec,
                    started,
                    attempt.IsCompleted && attempt.Score.HasValue && !isPlacementLegacy,
                    historyScoring,
                    isPlacementLegacy ? nameof(AttemptKind.Placement) : nameof(AttemptKind.MockTest));

                if (attemptStatus != StatusAbandoned) baselineForStats.Add(item);

                if (!attempt.IsCompleted && attemptStatus == StatusInProgress && !IsAttemptAbandoned(frozen))
                {
                    if (!string.Equals(status, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                if (attemptStatus == StatusAbandoned && string.IsNullOrWhiteSpace(status))
                {
                    continue;
                }

                if (!MatchesHistoryFilters(item, kind, status, levelNorm, cutoffUtc, keyword)) continue;
                filtered.Add(item);
            }

            var stats = BuildHistoryStats(baselineForStats);
            return (filtered, stats);
        }

        private static bool MatchesHistoryFilters(
            MockTestHistoryItem item,
            string? kind,
            string? status,
            string? levelNorm,
            DateTime? cutoffUtc,
            string? keyword)
        {
            if (!string.IsNullOrWhiteSpace(kind) && !string.Equals(kind, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var kNorm = kind.Trim().ToUpperInvariant();
                var isPlacement = string.Equals(item.ExamKind, nameof(ExamKind.Placement), StringComparison.OrdinalIgnoreCase);
                var isPractice = IsPracticeExamKind(item.ExamKind);
                if (kNorm == "PRACTICE" && !isPractice) return false;
                if (kNorm == "MOCK" && (isPractice || isPlacement)) return false;
                if (kNorm == "PLACEMENT" && !isPlacement) return false;
            }
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(status, item.AttemptStatus, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (!string.IsNullOrWhiteSpace(levelNorm))
            {
                if (!string.Equals(item.JlptLevelName?.Trim(), levelNorm, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (cutoffUtc.HasValue && item.SubmittedAt < cutoffUtc.Value)
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var hitTitle = item.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
                var hitLevel = item.JlptLevelName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
                if (!hitTitle && !hitLevel) return false;
            }
            return true;
        }

        private static ExamHistoryStatsResponse BuildHistoryStats(List<MockTestHistoryItem> items)
        {
            var practiceCount = items.Count(i => IsPracticeExamKind(i.ExamKind));
            var placementCount = items.Count(i => string.Equals(i.ExamKind, nameof(ExamKind.Placement), StringComparison.OrdinalIgnoreCase));
            var mockCount = items.Count(i => !IsPracticeExamKind(i.ExamKind) && !string.Equals(i.ExamKind, nameof(ExamKind.Placement), StringComparison.OrdinalIgnoreCase));
            var completedCount = items.Count(i => string.Equals(i.AttemptStatus, StatusCompleted, StringComparison.OrdinalIgnoreCase) || string.Equals(i.AttemptStatus, StatusExpired, StringComparison.OrdinalIgnoreCase));

            var practiceScores = items
                .Where(i => IsPracticeExamKind(i.ExamKind)
                    && (i.AttemptStatus == StatusCompleted || i.AttemptStatus == StatusExpired)
                    && i.Scoring is { IsScoringValid: true, NormalizedMaxScore: 10 })
                .Select(i => i.Scoring!.NormalizedScore)
                .ToList();

            var validMockScores = items
                .Where(i => !IsPracticeExamKind(i.ExamKind)
                    && !string.Equals(i.ExamKind, nameof(ExamKind.Placement), StringComparison.OrdinalIgnoreCase)
                    && (i.AttemptStatus == StatusCompleted || i.AttemptStatus == StatusExpired)
                    && i.Scoring is { IsScoringValid: true, NormalizedMaxScore: 180 })
                .Select(i => i.Scoring!.NormalizedScore)
                .ToList();

            return new ExamHistoryStatsResponse(
                TotalAttempts: items.Count,
                PracticeCount: practiceCount,
                MockCount: mockCount,
                PlacementCount: placementCount,
                CompletedCount: completedCount,
                AveragePracticeScore10: practiceScores.Count > 0 ? Math.Round(practiceScores.Average(), 2) : null,
                AverageMockScore180: validMockScores.Count > 0 ? Math.Round(validMockScores.Average(), 1) : null,
                BestMockScore180: validMockScores.Count > 0 ? Math.Round(validMockScores.Max(), 1) : null,
                CompletionRatePercent: items.Count > 0 ? (int)Math.Round(completedCount * 100.0 / items.Count) : null);
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
                        SectionTitle = frozen?.Prompt.SectionTitle ?? question.MockTestSection?.Title,
                        SkillName = question.MockTestSection?.LanguageSkill?.Name ?? question.MockTestSection?.Title,
                        IsAnswered = HasAnsweredValue(a),
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

                var snap = TryReadMockSnapshotSafe(attempt);
                var mockTest = answers.FirstOrDefault()?.MockQuestion?.MockTestSection?.MockTest;
                var totalCount = answers.Count;
                var answeredCount = answers.Count(HasAnsweredValue);
                var correctCount = answers.Count(a => a.IsCorrect);
                var blankCount = Math.Max(0, totalCount - answeredCount);
                var wrongCount = Math.Max(0, answeredCount - correctCount);
                var detailExamKind = ResolveEffectiveExamKind(
                    snap?.ExamKind,
                    mockTest?.ExamKind,
                    attempt.AttemptKind,
                    attempt.IsPlacementAssessment);
                var detailLevelCode = snap?.JlptLevelName ?? mockTest?.CertificateLevel?.Code ?? string.Empty;
                var detailScoring = CanReuseSnapshotScoring(snap?.Scoring, detailExamKind) ? snap!.Scoring : null;
                if (detailScoring is null && attempt.IsCompleted)
                {
                    var sectionRows = await unitOfWork.MockTestSectionResults.GetByAttemptIdAsync(attemptId, cancellationToken);
                    detailScoring = MockTestScoringPolicy.EvaluateFromSectionResults(
                        detailExamKind, detailLevelCode, sectionRows, correctCount, totalCount, attempt.TotalScore);
                }
                var detailSubmittedUtc = attempt.SubmittedAt.HasValue ? BusinessDateHelper.EnsureUtc(attempt.SubmittedAt.Value) : (DateTime?)null;
                var detailDurationSec = IsPracticeExamKind(detailExamKind) && snap is { ElapsedSeconds: > 0 }
                    ? snap.ElapsedSeconds
                    : detailSubmittedUtc.HasValue
                        ? Math.Max(0, (int)(detailSubmittedUtc.Value - BusinessDateHelper.EnsureUtc(attempt.StartedAt)).TotalSeconds)
                        : 0;

                return ApiResponse<DetailedAttemptResultResponse>.Success(new DetailedAttemptResultResponse
                {
                    AttemptId = attempt.Id,
                    MockTestId = answers
                        .Select(answer => answer.MockQuestion.MockTestSection.MockTestId)
                        .FirstOrDefault(),
                    Title = snap?.Title ?? mockTest?.Title ?? string.Empty,
                    JlptLevelName = detailLevelCode,
                    ExamKind = detailExamKind,
                    AttemptStatus = ResolveAttemptStatus(attempt, snap),
                    TotalScore = attempt.TotalScore.GetValueOrDefault(),
                    AccuracyPercent = detailScoring?.AccuracyPercent ?? attempt.TotalScore.GetValueOrDefault(),
                    Scoring = detailScoring,
                    CorrectCount = correctCount,
                    WrongCount = wrongCount,
                    BlankCount = blankCount,
                    TotalQuestions = totalCount,
                    StartedAt = BusinessDateHelper.EnsureUtc(attempt.StartedAt),
                    SubmittedAt = detailSubmittedUtc,
                    DurationSeconds = detailDurationSec,
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

                var serverNow = DateTime.UtcNow;
                var targetLevelId = user.TargetCertificateLevelId;
                string? targetLevelName = null;
                if (targetLevelId is not null)
                {
                    var levels = await LoadLevelsAsync([targetLevelId.Value], cancellationToken);
                    status.TargetJlptLevelId = targetLevelId;
                    targetLevelName = levels.GetValueOrDefault(targetLevelId.Value)?.Name;
                    status.TargetJlptLevelName = targetLevelName;
                }
                status.IsGoalConfirmed = targetLevelId.HasValue && user.DailyStudyMinutes > 0;

                var compatibility = PracticeTestService.EvaluatePlacementCompatibility(
                    attempts, targetLevelId, targetLevelName);
                var matchedCompleted = compatibility.CompatibleAttempt ?? compatibility.LatestAttempt;
                var matchedRecovered = compatibility.CompatibleResult ?? compatibility.LatestResult;

                if (matchedCompleted is not null && matchedRecovered is not null)
                {
                    status.HasCompletedPlacementTest = true;
                    status.LatestCompletedAttemptId = matchedCompleted.Id;
                    status.LatestSubmittedAt = matchedCompleted.SubmittedAt;
                    status.LatestScorePercent = matchedCompleted.TotalScore;
                    status.AssessedJlptLevelId = matchedRecovered.AssessedJlptLevelId > 0 ? matchedRecovered.AssessedJlptLevelId : null;
                    status.AssessedJlptLevelName = matchedRecovered.AssessedJlptLevelName;
                    status.RecommendedTargetJlptLevelId = matchedRecovered.RecommendedTargetJlptLevelId;
                    status.RecommendedTargetJlptLevelName = matchedRecovered.RecommendedTargetJlptLevelName;
                    status.LatestAssessmentLevelId = matchedRecovered.AssessmentTargetLevelId;
                    status.LatestAssessmentLevelName = matchedRecovered.ScreeningLevelName ?? compatibility.CoveredLevelName;
                    status.LatestAssessmentIsComprehensive = matchedRecovered.IsComprehensiveAssessment;
                }
                else
                {
                    status.HasCompletedPlacementTest = false;
                    status.LatestCompletedAttemptId = null;
                    status.LatestSubmittedAt = null;
                    status.LatestScorePercent = null;
                    status.AssessedJlptLevelId = user.AssessedCertificateLevelId;
                    status.AssessedJlptLevelName = null;
                    status.RecommendedTargetJlptLevelId = null;
                    status.RecommendedTargetJlptLevelName = null;
                }

                status.PlacementCompatibilityStatus = compatibility.Status;
                status.PlacementCompatibilityReason = compatibility.Reason;

                // Always initialize quota allowance so new users never see false quota exhausted messages.
                var startedCount = attempts.Count(a => PracticeTestService.TryDeserializeSnapshot(a.QuestionSnapshotJson, out var snap)
                    && !snap.ParentAssessmentAttemptId.HasValue
                    && snap.AssessmentTargetLevelId == targetLevelId);
                var pendingExpired = attempts.FirstOrDefault(a => !a.IsDeleted && !a.IsCompleted
                    && a.ExpiresAt.HasValue && BusinessDateHelper.EnsureUtc(a.ExpiresAt.Value) <= serverNow);
                var limit = 2;
                var remaining = Math.Max(0, limit - startedCount);
                status.QuotaInitialized = true;
                status.AttemptAllowance = new PlacementAttemptAllowance(limit, startedCount, remaining, pendingExpired?.Id);
                if (status.HasActiveAttempt)
                {
                    status.CanStartNewAttempt = false;
                    status.CannotStartReason = "ACTIVE_ATTEMPT_EXISTS";
                }
                else if (pendingExpired is not null)
                {
                    status.CanStartNewAttempt = false;
                    status.CannotStartReason = "PENDING_SUBMISSION";
                }
                else if (remaining <= 0)
                {
                    status.CanStartNewAttempt = false;
                    status.CannotStartReason = "PLACEMENT_ATTEMPT_LIMIT_REACHED";
                }
                else
                {
                    status.CanStartNewAttempt = true;
                    status.CannotStartReason = null;
                }

                var hasValidCompatiblePlacement = compatibility.Status == "COMPATIBLE"
                    || (!status.IsGoalConfirmed && compatibility.HasAnyCompletedPlacement);
                status.PlacementDecision = hasValidCompatiblePlacement
                    ? "COMPLETED"
                    : status.IsGoalConfirmed && user.PlacementDecision == PlacementDecision.Skipped ? "SKIPPED" : "NOT_TAKEN";
                status.IsReadyForLearningPath =
                    status.IsGoalConfirmed && compatibility.Status == "COMPATIBLE";

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
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<PlacementTestStatusResponse>.NotFound("Không tìm thấy người dùng.");

                var targetCode = user.TargetCertificateLevel?.Code?.Trim().ToUpperInvariant();
                if (!user.TargetCertificateLevelId.HasValue || string.IsNullOrWhiteSpace(targetCode) ||
                    !JlptCurriculum.IsSupportedStudyLevel(targetCode) || user.DailyStudyMinutes <= 0)
                    return ApiResponse<PlacementTestStatusResponse>.Fail(
                        "Vui lòng thiết lập mục tiêu JLPT (N5–N2) trước khi chọn bỏ qua bài đánh giá năng lực.",
                        HttpStatusCode.BadRequest, errorCode: "LEARNING_GOAL_REQUIRED");

                // Chỉ chuyển sang Skipped nếu chưa có kết quả đánh giá hợp lệ và tương thích cho mục tiêu hiện tại.
                var attempts = (await unitOfWork.MockTestAttempts.FindAsync(a =>
                    a.UserId == userId && !a.IsDeleted && a.AttemptKind == AttemptKind.Placement, cancellationToken))
                    .ToList();
                var compatibility = PracticeTestService.EvaluatePlacementCompatibility(
                    attempts, user.TargetCertificateLevelId, targetCode);
                if (compatibility.Status != "COMPATIBLE" && user.PlacementDecision != PlacementDecision.Skipped)
                {
                    // Sequential flow: a new roadmap needs a real assessment result, so skipping is no longer
                    // accepted. Learners who skipped earlier keep their existing roadmap (see LearnerSetupStateService).
                    return ApiResponse<PlacementTestStatusResponse>.Fail(
                        "Hãy hoàn thành bài đánh giá năng lực để tạo lộ trình học phù hợp với bạn.",
                        HttpStatusCode.Conflict, errorCode: "PLACEMENT_SKIP_DISABLED");
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

        private sealed record ProjectedSectionScoreRow(
            int SectionId,
            string Title,
            string? SkillCode,
            string? SkillName,
            int CorrectCount,
            int TotalCount,
            double ScorePercent);

        private static MockAttemptScoringSummary BuildScoringFromProjectedRows(
            string? examKind,
            string? jlptLevelCode,
            IReadOnlyList<ProjectedSectionScoreRow> rows,
            int totalCorrect,
            int totalQuestions,
            double? fallbackAccuracy)
        {
            var inputs = rows.Select(r =>
            {
                var total = Math.Max(0, r.TotalCount);
                var earned = total > 0 && r.ScorePercent is >= 0 and <= 100
                    ? Math.Round(r.ScorePercent * total / 100d, 4, MidpointRounding.AwayFromZero)
                    : r.CorrectCount;
                return new MockTestScoringPolicy.TestSectionScoreInput(
                    r.SectionId, r.Title, r.SkillCode, r.SkillName, r.CorrectCount, total, earned, total);
            }).ToList();

            return MockTestScoringPolicy.EvaluateFromSections(
                examKind, jlptLevelCode, inputs, totalCorrect, totalQuestions, fallbackAccuracy);
        }
    }
}
