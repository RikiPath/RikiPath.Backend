using System.Collections.Concurrent;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;

namespace RikiPath.Application.Services;

public sealed class MockTestExplanationService(IMockTestService mockTests, IClaimService claims,
    IAiMockTestExplanationClient ai, SubscriptionAccessService subscriptions, IMemoryCache cache,
    ILogger<MockTestExplanationService> logger, IContentAudioTranscriptionService? audioTranscriptions = null)
{
    public const string CreditFeature = "mock_test_explanation";
    private const int MaxPromptCharacters = 24_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly ConcurrentDictionary<ExplanationKey, byte> InProgress = new();
    private readonly record struct ExplanationKey(int UserId, int AttemptId, int QuestionId);

    public async Task<ApiResponse<MockQuestionExplanationResponse>> ExplainAsync(int attemptId, int questionId,
        CancellationToken cancellationToken)
    {
        var review = await mockTests.GetDetailedResultAsync(attemptId, cancellationToken);
        if (!review.IsSuccess || review.Result is null)
            return ApiResponse<MockQuestionExplanationResponse>.Fail(review.ErrorMessage ?? "Không tải được kết quả thi thử.",
                (HttpStatusCode)review.StatusCode);

        var question = review.Result.Questions.Find(item => item.QuestionId == questionId);
        if (question is null)
            return ApiResponse<MockQuestionExplanationResponse>.NotFound("Câu hỏi không thuộc lượt thi này.");
        if (question.Options.Count > 0 ? question.Options.All(option => !option.IsCorrect)
            : question.AnswerKey is null or { ValueKind: JsonValueKind.Null })
            return ApiResponse<MockQuestionExplanationResponse>.Fail("Câu hỏi chưa có đáp án chuẩn để AI giải thích.",
                HttpStatusCode.UnprocessableEntity, "MOCK_AI_ANSWER_KEY_MISSING");

        var key = new ExplanationKey(claims.GetUserClaim().Id, attemptId, questionId);
        if (cache.TryGetValue<MockQuestionExplanationResponse>(key, out var saved) && saved is not null)
            return ApiResponse<MockQuestionExplanationResponse>.Success(saved);
        if (!InProgress.TryAdd(key, 0))
            return ApiResponse<MockQuestionExplanationResponse>.Fail("AI đang giải thích câu hỏi này.",
                HttpStatusCode.Conflict, "MOCK_AI_IN_PROGRESS", retryable: true);

        try
        {
            if (cache.TryGetValue<MockQuestionExplanationResponse>(key, out saved) && saved is not null)
                return ApiResponse<MockQuestionExplanationResponse>.Success(saved);

            var selectedIds = ReadSelectedOptionIds(question);
            var images = new[] { question.ImageUrl }.Concat(question.Options.Select(option => option.ImageUrl))
                .OfType<string>().Where(url => !string.IsNullOrWhiteSpace(url)).ToArray();
            var transcript = question.AudioTranscript ?? JlptMockQuestionFormats.ReadString(question.InteractionData, "transcript");
            if (!string.IsNullOrWhiteSpace(question.AudioUrl) && string.IsNullOrWhiteSpace(transcript))
            {
                var audio = audioTranscriptions is null ? null : await audioTranscriptions.GetForGradingAsync(
                    question.AudioUrl, question.InteractionData, cancellationToken);
                if (audio is not { Status: "Completed", Transcript.Length: > 0 })
                    return ApiResponse<MockQuestionExplanationResponse>.Fail(
                        audio?.ErrorMessage ?? "Bản chép lời tiếng Nhật đang được xử lý. Vui lòng thử lại sau.",
                        HttpStatusCode.Conflict, audio?.ErrorCode ?? "AUDIO_TRANSCRIPTION_PENDING",
                        retryable: audio?.Status is "Pending" or "Processing");
                transcript = audio.Transcript;
            }
            var prompt = BuildPrompt(question, selectedIds, transcript);
            if (prompt.Length > MaxPromptCharacters)
                return ApiResponse<MockQuestionExplanationResponse>.Fail("Ngữ liệu của câu hỏi vượt giới hạn phân tích AI.",
                    HttpStatusCode.UnprocessableEntity, "MOCK_AI_CONTENT_TOO_LARGE");

            var result = await subscriptions.UseAiAsync(CreditFeature,
                () => GenerateAsync(key, question, selectedIds, prompt, images, cancellationToken),
                cancellationToken);
            if (result.IsSuccess && result.Result is not null)
                cache.Set(key, result.Result, TimeSpan.FromHours(24));
            return result;
        }
        finally
        {
            InProgress.TryRemove(key, out _);
        }
    }

    private async Task<ApiResponse<MockQuestionExplanationResponse>> GenerateAsync(ExplanationKey key,
        QuestionReviewCard question, HashSet<int> selectedIds, string prompt, string[] images, CancellationToken ct)
    {
        try
        {
            var raw = await ai.ExplainJsonAsync(key.UserId, prompt, images, null, ct);
            var result = raw is null ? null : JsonSerializer.Deserialize<ExplanationDraft>(raw, JsonOptions);
            if (result is null || result.EnoughEvidence == false)
                return ApiResponse<MockQuestionExplanationResponse>.Fail(
                    "AI chưa có đủ ngữ liệu đọc, ảnh hoặc audio để giải thích câu này.",
                    HttpStatusCode.UnprocessableEntity, "MOCK_AI_MEDIA_UNAVAILABLE");

            if (!TryBuildResponse(question, selectedIds, result, out var explanation))
                return ApiResponse<MockQuestionExplanationResponse>.Fail(
                    "AI chưa giải thích đầy đủ các lựa chọn. Bạn có thể thử lại.",
                    HttpStatusCode.BadGateway, "MOCK_AI_INCOMPLETE", retryable: true);

            return ApiResponse<MockQuestionExplanationResponse>.Success(explanation!);
        }
        catch (AiQuotaExceededException ex)
        {
            logger.LogWarning(ex, "Mock explanation quota exceeded. AttemptId={AttemptId} QuestionId={QuestionId}", key.AttemptId, key.QuestionId);
            return ApiResponse<MockQuestionExplanationResponse>.Fail("AI đã hết hạn mức sử dụng. Vui lòng thử lại sau.",
                HttpStatusCode.TooManyRequests, "MOCK_AI_QUOTA_EXCEEDED", retryable: true);
        }
        catch (Exception ex) when (ex is AiServiceException or HttpRequestException or JsonException)
        {
            logger.LogWarning(ex, "Mock explanation failed. AttemptId={AttemptId} QuestionId={QuestionId}", key.AttemptId, key.QuestionId);
            return ApiResponse<MockQuestionExplanationResponse>.Fail("Dịch vụ AI chưa phản hồi. Vui lòng thử lại.",
                HttpStatusCode.BadGateway, "MOCK_AI_UNAVAILABLE", retryable: true);
        }
    }

    private static string BuildPrompt(QuestionReviewCard question, HashSet<int> selectedIds, string? transcript) =>
        JsonSerializer.Serialize(new
        {
            question.QuestionText,
            question.QuestionType,
            JlptItemType = JlptMockQuestionFormats.ReadString(question.InteractionData, "jlptItemType"),
            Passage = JlptMockQuestionFormats.ReadString(question.InteractionData, "passage"),
            SecondPassage = JlptMockQuestionFormats.ReadString(question.InteractionData, "secondPassage"),
            Transcript = transcript,
            HasQuestionImage = !string.IsNullOrWhiteSpace(question.ImageUrl),
            OptionsInAudio = JlptMockQuestionFormats.HasAudioOptions(question.InteractionData),
            StarPosition = JlptMockQuestionFormats.StarPosition(question.InteractionData),
            Tokens = ReadInteractionField(question.InteractionData, "tokens"),
            Blanks = ReadInteractionField(question.InteractionData, "blanks"),
            Left = ReadInteractionField(question.InteractionData, "left"),
            Right = ReadInteractionField(question.InteractionData, "right"),
            question.Response,
            question.AnswerKey,
            question.IsCorrect,
            AuthorExplanation = question.Explanation,
            Options = question.Options.Select((option, index) => new
            {
                option.OptionId,
                Number = index + 1,
                option.OptionText,
                HasImage = !string.IsNullOrWhiteSpace(option.ImageUrl),
                Selected = selectedIds.Contains(option.OptionId),
                option.IsCorrect,
            }),
        }, JsonOptions);

    private static JsonElement? ReadInteractionField(JsonElement? data, string name) =>
        data is { ValueKind: JsonValueKind.Object } fields && fields.TryGetProperty(name, out var field) ? field : null;

    private static HashSet<int> ReadSelectedOptionIds(QuestionReviewCard question)
    {
        if (question.SelectedOptionId is int id) return [id];
        var selected = new HashSet<int>();
        if (question.QuestionType.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase) &&
            question.Response is { ValueKind: JsonValueKind.Array } values)
            foreach (var item in values.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var optionId)) selected.Add(optionId);
        return selected;
    }

    private static bool TryBuildResponse(QuestionReviewCard question, HashSet<int> selectedIds, ExplanationDraft draft,
        out MockQuestionExplanationResponse? response)
    {
        response = null;
        if (draft.EnoughEvidence != true || string.IsNullOrWhiteSpace(draft.Explanation) ||
            draft.Options is null || draft.Options.Count != question.Options.Count) return false;

        var reasons = new Dictionary<int, string>();
        foreach (var entry in draft.Options)
            if (entry is null || string.IsNullOrWhiteSpace(entry.Reason) || !reasons.TryAdd(entry.OptionId, entry.Reason.Trim()))
                return false;
        if (question.Options.Any(option => !reasons.ContainsKey(option.OptionId))) return false;

        response = new MockQuestionExplanationResponse(question.QuestionId, question.IsCorrect ? 1 : 0, 1,
            draft.Explanation.Trim(), question.Options.Select(option => new MockOptionExplanation(
                option.OptionId, selectedIds.Contains(option.OptionId), option.IsCorrect, reasons[option.OptionId])).ToList());
        return true;
    }

    private sealed record ExplanationDraft(bool? EnoughEvidence, string? Explanation, List<OptionDraft?>? Options);
    private sealed record OptionDraft(int OptionId, string? Reason);
}
