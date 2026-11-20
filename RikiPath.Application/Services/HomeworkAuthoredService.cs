using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Homework;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed partial class HomeworkService
{
    private const string MultipleChoiceHomework = "MultipleChoice";
    private const string WritingHomework = "Writing";
    private const string SpeakingHomework = "Speaking";
    private const int AudioLimit = 5 * 1024 * 1024;

    private sealed record AuthoredDefinition(string Type, string Level, HomeworkAuthoredQuestion[] Questions);

    private static AuthoredDefinition? ReadAuthoredDefinition(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<AuthoredDefinition>(json, HomeworkJson);

    private static int[]? ReadSelectedOptions(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<int[]>(json, HomeworkJson);

    private static string? ValidateAuthoredDefinition(HomeworkContentRequest request, out AuthoredDefinition? definition)
    {
        definition = null;
        if (request.AssignmentType is null && request.JlptLevel is null && request.Questions is not { Length: > 0 })
            return null;

        var type = request.AssignmentType?.Trim();
        var level = request.JlptLevel?.Trim().ToUpperInvariant();
        if (type is not (MultipleChoiceHomework or WritingHomework or SpeakingHomework))
            return "Chọn dạng Trắc nghiệm, Viết hoặc Nói.";
        if (level is null || !SupportedLevels.Contains(level))
            return "Chọn cấp JLPT N5, N4, N3 hoặc N2.";

        var questions = new List<HomeworkAuthoredQuestion>();
        if (type == MultipleChoiceHomework)
        {
            if (request.Questions is not { Length: >= 1 and <= 20 })
                return "Đề trắc nghiệm cần từ 1 đến 20 câu hỏi.";

            foreach (var item in request.Questions)
            {
                if (item?.Question?.Trim() is not { Length: > 0 and <= 1000 } question ||
                    item.Options is not { Length: >= 2 and <= 4 } options ||
                    options.Any(option => option?.Trim() is not { Length: > 0 and <= 300 }) ||
                    item.CorrectOptionIndex is not int index || index < 0 || index >= options.Length ||
                    item.Explanation?.Trim().Length > 800)
                    return $"Câu {questions.Count + 1} cần nội dung, 2–4 lựa chọn và một đáp án đúng.";

                var normalizedOptions = options.Select(option => option.Trim()).ToArray();
                if (normalizedOptions.Distinct(StringComparer.Ordinal).Count() != normalizedOptions.Length)
                    return $"Các lựa chọn ở câu {questions.Count + 1} phải khác nhau.";
                questions.Add(new HomeworkAuthoredQuestion(question, normalizedOptions, index, item.Explanation?.Trim()));
            }
        }
        else if (request.Questions is { Length: > 0 } || string.IsNullOrWhiteSpace(request.Instructions))
        {
            return "Nhập đề bài viết hoặc nói trong Yêu cầu làm bài.";
        }

        definition = new AuthoredDefinition(type, level, questions.ToArray());
        return null;
    }

    private static string? ValidateAuthoredSubmission(AuthoredDefinition? definition, SubmitHomeworkRequest request,
        bool hasScan, bool hasAudio)
    {
        if (definition?.Type == MultipleChoiceHomework)
        {
            if (hasScan || hasAudio || !string.IsNullOrWhiteSpace(request.TextContent) || !string.IsNullOrWhiteSpace(request.FileUrl))
                return "Hãy chọn đáp án ngay trên hệ thống cho bài trắc nghiệm này.";
            if (!HasValidSelections(definition, request.SelectedOptions))
                return "Hãy chọn một đáp án hợp lệ cho từng câu trắc nghiệm.";
        }
        else if (request.SelectedOptions is not null)
        {
            return "Bài tập này không nhận đáp án trắc nghiệm.";
        }

        if (definition?.Type == SpeakingHomework && !hasAudio)
            return "Hãy nộp bản thu âm cho bài nói.";
        if (hasAudio && definition?.Type != SpeakingHomework)
            return "Chỉ bài tập nói mới nhận bản thu âm.";
        if (definition?.Type == WritingHomework && !hasScan && string.IsNullOrWhiteSpace(request.TextContent))
            return "Hãy nhập bài viết hoặc nộp ảnh bài viết tay.";
        return null;
    }

    private static bool HasValidSelections(AuthoredDefinition definition, int[]? selected) =>
        selected is not null && selected.Length == definition.Questions.Length &&
        selected.Select((option, index) => option >= 0 && option < definition.Questions[index].Options.Length).All(valid => valid);

    private static HomeworkItem WithAuthoredDefinition(HomeworkItem item, HomeworkAssignment assignment, Role role)
    {
        if (ReadAuthoredDefinition(assignment.AuthoredDefinitionJson) is not { } definition)
            return item;
        var questions = role is Role.Mentor or Role.Admin
            ? definition.Questions
            : definition.Questions.Select(question => new HomeworkAuthoredQuestion(question.Question, question.Options)).ToArray();
        return item with { AssignmentType = definition.Type, JlptLevel = definition.Level, Questions = questions };
    }

    public async Task<ApiResponse<HomeworkSubmissionDetail>> SubmitAudioAsync(int id, IFormFile? file, CancellationToken ct)
    {
        if (storage is null)
            return ApiResponse<HomeworkSubmissionDetail>.Fail("Kho bản thu âm chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
        if (file is not { Length: >= 12 and <= AudioLimit })
            return ApiResponse<HomeworkSubmissionDetail>.Fail("Chọn bản thu WAV, MP3, M4A hoặc WEBM tối đa 5 MB.");
        try
        {
            await using var stream = file.OpenReadStream();
            var signature = new byte[12];
            await stream.ReadExactlyAsync(signature, ct);
            var contentType = HomeworkAudioFormat.Detect(signature);
            if (contentType is null)
                return ApiResponse<HomeworkSubmissionDetail>.Fail("Định dạng bản thu không hợp lệ.");
            return await SubmitInternalAsync(id, new(null, null), null, ct, (file, contentType));
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or ArgumentException or InvalidOperationException)
        {
            return ApiResponse<HomeworkSubmissionDetail>.Fail("Chưa lưu được bản thu âm. Hãy thử lại.", HttpStatusCode.BadGateway);
        }
    }

    public async Task<PrivateVerificationReadResult?> ReadAudioAsync(int id, CancellationToken ct)
    {
        var reference = await AssignmentsFor(claims.GetUserClaim(), null).AsNoTracking()
            .Where(a => a.Id == id && a.Submission != null && !a.Submission.IsDeleted)
            .Select(a => a.Submission!.PrivateAudioReference).SingleOrDefaultAsync(ct);
        return reference is null || storage is null ? null : await storage.OpenPrivateVerificationAsync(reference, ct);
    }
}
