using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Practice;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Practice;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RikiPath.Application.Services
{
    public class PracticeSubmissionService(
        IUnitOfWork unitOfWork,
        IAiEssayGradingClient aiClient,
        IClaimService claimService,
        ILogger<PracticeSubmissionService> logger) : IPracticeSubmissionService
    {
        private const int MaxAiAttempts = 2;
        private const int MaxTextContentLength = 4000;

        private const string EssayGradingSystemPrompt = """
            Bạn chấm kỹ năng viết tiếng Nhật (作文) theo tiêu chí luyện viết của RikiPath. Chỉ đánh giá bài làm
            theo đề và độ khó được cung cấp. Mã cấp độ chỉ dùng để điều chỉnh kỳ vọng về kỹ năng viết.
            Không quy đổi điểm sang điểm thi JLPT hoặc suy luận khả năng đỗ.
            Chấm theo 5 tiêu chí, mỗi tiêu chí thang điểm 0-10: "Nội dung & ý tưởng", "Ngữ pháp",
            "Từ vựng", "Cấu trúc & bố cục", "Chính tả & cách dùng Kanji".
            overallScore là điểm tổng quy đổi thang 0-100 (trung bình 5 tiêu chí nhân 10).
            Liệt kê cụ thể các lỗi tìm thấy trong corrections: original (câu/cụm gốc có lỗi),
            suggestion (cách sửa đề xuất), reason (giải thích ngắn gọn vì sao sai).
            Đưa ra nhận xét thẳng thắn, mang tính xây dựng, phù hợp trình độ người học.
            BẮT BUỘC: Toàn bộ nội dung (comment, strengths, improvements, reason...) viết 100% bằng
            TIẾNG VIỆT, không dùng tiếng Anh.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "overallScore": number, "criteria": [ { "name": string, "score": number, "comment": string } ],
              "strengths": string[], "improvements": string[],
              "corrections": [ { "original": string, "suggestion": string, "reason": string } ] }
            """;

        private const string SpeakingGradingSystemPrompt = """
            Bạn chấm kỹ năng nói tiếng Nhật theo tiêu chí luyện nói của RikiPath, dựa trên bản chép lời.
            Mã cấp độ chỉ dùng để điều chỉnh kỳ vọng về kỹ năng nói.
            Không quy đổi điểm sang điểm thi JLPT hoặc suy luận khả năng đỗ.
            Đối chiếu câu mẫu hoặc đề bài nếu được cung cấp. Chấm mỗi tiêu chí từ 0-10:
            Mức độ đáp ứng đề, Ngữ pháp, Từ vựng, Tính mạch lạc, Cách diễn đạt.
            overallScore là trung bình điểm 5 tiêu chí nhân 10, trong khoảng 0-100.
            Chỉ đánh giá nội dung thể hiện trong văn bản. Không suy đoán phát âm, ngữ điệu hoặc tốc độ nói.
            Viết phản hồi tiếng Việt và trả một JSON object đúng cấu trúc:
            { "overallScore": number, "criteria": [ { "name": string, "score": number, "comment": string } ],
              "strengths": string[], "improvements": string[],
              "corrections": [ { "original": string, "suggestion": string, "reason": string } ] }
            """;

        public async Task<ApiResponse<SubmitPracticeResponse>> SubmitAsync(
            SubmitPracticeRequest request, CancellationToken cancellationToken)
        {
            var validationError = ValidateSubmitRequest(request);
            if (validationError is not null)
                return ApiResponse<SubmitPracticeResponse>.Fail(validationError);

            try
            {
                var userId = claimService.GetUserClaim().Id;

                var level = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(request.CertificateLevelId);
                if (level is null)
                    return ApiResponse<SubmitPracticeResponse>.Fail(
                        "Cấp độ JLPT không hợp lệ.", HttpStatusCode.BadRequest);

                var submission = new PracticeSubmission
                {
                    UserId = userId,
                    Type = request.Type,
                    TextContent = request.TextContent?.Trim(),
                    ImageUrl = request.ImageUrl,
                    AudioUrl = request.AudioUrl,
                    CertificateLevelId = level.Id,
                    SubmittedAt = DateTime.UtcNow,
                };

                await unitOfWork.PracticeSubmissions.AddAsync(submission, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                if (!request.GradeWithAi || !SupportsAiGrading(submission))
                {
                    return ApiResponse<SubmitPracticeResponse>.Success(new SubmitPracticeResponse
                    {
                        SubmissionId = submission.Id,
                        Type = submission.Type,
                        SubmittedAt = submission.SubmittedAt,
                        Grading = null,
                        GradingNote = request.GradeWithAi
                            ? $"Loại bài {submission.Type} chưa có nội dung hỗ trợ chấm tự động."
                            : null,
                    });
                }

                var (grading, gradingNote) = await GradeAsync(
                    userId, submission.Type, submission.TextContent!, level.Code, request.Topic, cancellationToken);

                if (grading is not null)
                {
                    var gradingResult = new GradingResult
                    {
                        PracticeSubmissionId = submission.Id,
                        OverallScore = grading.OverallScore,
                        FeedbackJson = JsonSerializer.Serialize(grading),
                        GradedAt = DateTime.UtcNow,
                    };
                    await unitOfWork.GradingResults.AddAsync(gradingResult, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return ApiResponse<SubmitPracticeResponse>.Success(new SubmitPracticeResponse
                {
                    SubmissionId = submission.Id,
                    Type = submission.Type,
                    SubmittedAt = submission.SubmittedAt,
                    Transcript = submission.Type == SubmissionType.Speaking ? submission.TextContent : null,
                    Grading = grading,
                    GradingNote = gradingNote,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit practice.");
                return ApiResponse<SubmitPracticeResponse>.Fail(
                    "Không thể nộp bài lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PracticeSubmissionDetailResponse>> GetByIdAsync(
            int submissionId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var submission = await unitOfWork.PracticeSubmissions.Query()
                    .Include(s => s.CertificateLevel)
                    .Include(s => s.GradingResult)
                    .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

                if (submission is null)
                    return ApiResponse<PracticeSubmissionDetailResponse>.NotFound("Không tìm thấy bài nộp.");
                if (submission.UserId != userId)
                    return ApiResponse<PracticeSubmissionDetailResponse>.Fail(
                        "Bạn không có quyền xem bài nộp này.", HttpStatusCode.Forbidden);

                return ApiResponse<PracticeSubmissionDetailResponse>.Success(new PracticeSubmissionDetailResponse
                {
                    Id = submission.Id,
                    Type = submission.Type,
                    TextContent = submission.TextContent,
                    ImageUrl = submission.ImageUrl,
                    AudioUrl = submission.AudioUrl,
                    CertificateLevelCode = submission.CertificateLevel?.Code ?? string.Empty,
                    SubmittedAt = submission.SubmittedAt,
                    Grading = DeserializeGrading(submission.GradingResult),
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load practice submission. SubmissionId={Id}", submissionId);
                return ApiResponse<PracticeSubmissionDetailResponse>.Fail(
                    "Không thể tải bài nộp.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<PracticeSubmissionSummaryResponse>>> GetMyHistoryAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var submissions = await unitOfWork.PracticeSubmissions.Query()
                    .Where(s => s.UserId == userId)
                    .Include(s => s.CertificateLevel)
                    .Include(s => s.GradingResult)
                    .OrderByDescending(s => s.SubmittedAt)
                    .ToListAsync(cancellationToken);

                var history = submissions.Select(s => new PracticeSubmissionSummaryResponse
                {
                    Id = s.Id,
                    Type = s.Type,
                    CertificateLevelCode = s.CertificateLevel?.Code ?? string.Empty,
                    SubmittedAt = s.SubmittedAt,
                    IsGraded = s.GradingResult is not null,
                    OverallScore = s.GradingResult?.OverallScore,
                }).ToList();

                return ApiResponse<List<PracticeSubmissionSummaryResponse>>.Success(history);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load practice submission history.");
                return ApiResponse<List<PracticeSubmissionSummaryResponse>>.Fail(
                    "Không thể tải lịch sử nộp bài.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitPracticeResponse>> RegradeAsync(
            int submissionId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var submission = await unitOfWork.PracticeSubmissions.Query()
                    .Include(s => s.CertificateLevel)
                    .Include(s => s.GradingResult)
                    .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

                if (submission is null)
                    return ApiResponse<SubmitPracticeResponse>.NotFound("Không tìm thấy bài nộp.");
                if (submission.UserId != userId)
                    return ApiResponse<SubmitPracticeResponse>.Fail(
                        "Bạn không có quyền chấm lại bài này.", HttpStatusCode.Forbidden);
                if (!SupportsAiGrading(submission))
                    return ApiResponse<SubmitPracticeResponse>.Fail(
                        $"Loại bài {submission.Type} chưa hỗ trợ chấm tự động.", HttpStatusCode.BadRequest);

                var (grading, gradingNote) = await GradeAsync(
                    userId, submission.Type, submission.TextContent!, submission.CertificateLevel?.Code ?? string.Empty,
                    topic: null, cancellationToken);

                if (grading is not null)
                {
                    if (submission.GradingResult is not null)
                    {
                        submission.GradingResult.OverallScore = grading.OverallScore;
                        submission.GradingResult.FeedbackJson = JsonSerializer.Serialize(grading);
                        submission.GradingResult.GradedAt = DateTime.UtcNow;
                        unitOfWork.GradingResults.Update(submission.GradingResult);
                    }
                    else
                    {
                        await unitOfWork.GradingResults.AddAsync(new GradingResult
                        {
                            PracticeSubmissionId = submission.Id,
                            OverallScore = grading.OverallScore,
                            FeedbackJson = JsonSerializer.Serialize(grading),
                            GradedAt = DateTime.UtcNow,
                        }, cancellationToken);
                    }
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return ApiResponse<SubmitPracticeResponse>.Success(new SubmitPracticeResponse
                {
                    SubmissionId = submission.Id,
                    Type = submission.Type,
                    SubmittedAt = submission.SubmittedAt,
                    Transcript = submission.Type == SubmissionType.Speaking ? submission.TextContent : null,
                    Grading = grading,
                    GradingNote = gradingNote,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to regrade practice submission. SubmissionId={Id}", submissionId);
                return ApiResponse<SubmitPracticeResponse>.Fail(
                    "Không thể chấm lại lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================================================
        // AI GRADING PIPELINE
        // =====================================================================
        private async Task<(GradingResultResponse? Grading, string? Note)> GradeAsync(
            int userId, SubmissionType type, string essayText, string certificateLevelCode, string? topic,
            CancellationToken cancellationToken)
        {
            var systemPrompt = type == SubmissionType.Speaking
                ? SpeakingGradingSystemPrompt : EssayGradingSystemPrompt;
            var userPrompt = BuildGradingUserPrompt(essayText, certificateLevelCode, topic);
            string? lastError = null;

            for (var attempt = 1; attempt <= MaxAiAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPrompt = lastError is null
                    ? userPrompt
                    : $"{userPrompt}\n\n[LƯU Ý] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa lại.";

                string rawJson;
                try
                {
                    rawJson = StripCodeFence(await aiClient.GenerateGradingJsonAsync(
                        userId, systemPrompt, currentPrompt, cancellationToken));
                }
                catch (AiQuotaExceededException)
                {
                    return (null, "AI tạm thời hết hạn mức chấm bài; bài đã được lưu, vui lòng dùng chức năng chấm lại sau.");
                }
                catch (AiServiceException ex)
                {
                    logger.LogError(ex, "AI essay grading service failed. UserId={UserId}", userId);
                    return (null, "Dịch vụ AI hiện không phản hồi; bài đã được lưu, vui lòng dùng chức năng chấm lại sau.");
                }

                var parsed = TryParseGrading(rawJson, out lastError);
                if (parsed is not null)
                    return (parsed, null);

                logger.LogWarning("AI essay grading rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                    attempt, MaxAiAttempts, lastError, userId);
            }

            return (null, $"AI chấm bài trả về kết quả không hợp lệ ({lastError}); bài đã được lưu, vui lòng dùng chức năng chấm lại sau.");
        }

        private static string BuildGradingUserPrompt(string essayText, string certificateLevelCode, string? topic)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ĐỘ KHÓ THAM CHIẾU CỦA BÀI LUYỆN]: {certificateLevelCode}");
            if (!string.IsNullOrWhiteSpace(topic))
                sb.AppendLine($"[ĐỀ BÀI]: {topic.Trim()}");
            sb.AppendLine("\n[BÀI LÀM CỦA HỌC VIÊN]");
            sb.AppendLine(essayText);
            return sb.ToString();
        }

        private static bool SupportsAiGrading(PracticeSubmission submission) =>
            submission.Type is SubmissionType.Writing or SubmissionType.Speaking
            && !string.IsNullOrWhiteSpace(submission.TextContent);

        private static string? ValidateSubmitRequest(SubmitPracticeRequest request)
        {
            if (request.CertificateLevelId <= 0)
                return "Vui lòng chọn cấp độ JLPT.";

            switch (request.Type)
            {
                case SubmissionType.Writing:
                    if (string.IsNullOrWhiteSpace(request.TextContent))
                        return "Bài luận (Writing) cần có nội dung TextContent.";
                    if (request.TextContent.Trim().Length > MaxTextContentLength)
                        return $"Bài luận không được vượt quá {MaxTextContentLength} ký tự.";
                    break;
                case SubmissionType.Kanji:
                    if (string.IsNullOrWhiteSpace(request.ImageUrl))
                        return "Bài Kanji cần có ảnh (ImageUrl).";
                    break;
                case SubmissionType.Speaking:
                    if (string.IsNullOrWhiteSpace(request.AudioUrl) && string.IsNullOrWhiteSpace(request.TextContent))
                        return "Bài nói cần có bản ghi âm hoặc bản chép lời.";
                    if (request.TextContent?.Trim().Length > MaxTextContentLength)
                        return $"Bản chép lời không được vượt quá {MaxTextContentLength} ký tự.";
                    break;
            }
            return null;
        }

        private sealed record GradingAiResponse(
            double OverallScore,
            List<GradingCriterionResponse>? Criteria,
            List<string>? Strengths,
            List<string>? Improvements,
            List<EssayCorrectionResponse>? Corrections);

        private static readonly string[] EnglishTellSigns =
        {
            " the ", " and ", " you ", " your ", " is ", " are ", " with ", " this ", " that ",
            " for ", " have ", " has ", " will ", " should ", " grammar ", " vocabulary ",
        };

        private static bool LooksEnglish(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var padded = " " + text.ToLowerInvariant() + " ";
            return EnglishTellSigns.Count(sign => padded.Contains(sign)) >= 2;
        }

        private static GradingResultResponse? TryParseGrading(string rawJson, out string? error)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<GradingAiResponse>(rawJson, options);
                if (parsed is null || parsed.Criteria is null || parsed.Criteria.Count == 0)
                {
                    error = "thiếu danh sách tiêu chí chấm điểm";
                    return null;
                }
                if (parsed.OverallScore is < 0 or > 100)
                {
                    error = "overallScore phải trong khoảng 0-100";
                    return null;
                }
                if (parsed.Criteria.Any(c => c.Score is < 0 or > 10))
                {
                    error = "điểm từng tiêu chí phải trong khoảng 0-10";
                    return null;
                }
                var allTexts = parsed.Criteria.Select(c => c.Comment)
                    .Concat(parsed.Strengths ?? new List<string>())
                    .Concat(parsed.Improvements ?? new List<string>())
                    .Concat((parsed.Corrections ?? new List<EssayCorrectionResponse>()).Select(c => c.Reason));
                if (allTexts.Any(LooksEnglish))
                {
                    error = "nhận xét lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt";
                    return null;
                }

                error = null;
                return new GradingResultResponse
                {
                    OverallScore = parsed.OverallScore,
                    Criteria = parsed.Criteria,
                    Strengths = parsed.Strengths ?? new List<string>(),
                    Improvements = parsed.Improvements ?? new List<string>(),
                    Corrections = parsed.Corrections ?? new List<EssayCorrectionResponse>(),
                    GradedAt = DateTime.UtcNow,
                };
            }
            catch (JsonException)
            {
                error = "JSON sai định dạng (có thể bị ngắt giữa chừng)";
                return null;
            }
        }

        private static GradingResultResponse? DeserializeGrading(GradingResult? result)
        {
            if (result is null || string.IsNullOrWhiteSpace(result.FeedbackJson))
                return null;
            try
            {
                var parsed = JsonSerializer.Deserialize<GradingResultResponse>(
                    result.FeedbackJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                    parsed.GradedAt = result.GradedAt;
                return parsed;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string StripCodeFence(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.Trim();
            if (!s.StartsWith("```")) return s;

            var firstNewLine = s.IndexOf('\n');
            var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                return s[(firstNewLine + 1)..lastFence].Trim();
            if (firstNewLine >= 0)
                return s[(firstNewLine + 1)..].Trim();
            return s;
        }
    }
}
