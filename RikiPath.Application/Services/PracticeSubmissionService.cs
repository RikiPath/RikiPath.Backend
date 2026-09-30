using RikiPath.Domain.Entities;
using RikiPath.Application.Common;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Practice;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Grading;
using RikiPath.Application.Responses.Practice;
using System.Text;
using System.Text.Json;

namespace RikiPath.Application.Services
{
    public class PracticeSubmissionService(
        IUnitOfWork unitOfWork,
        IAiGradingClient aiGradingClient,
        ISpeechToTextClient speechToTextClient,
        IClaimService claimService)
        : IPracticeSubmissionService
    {
        public async Task<ApiResponse<PracticeSubmissionResponse>> SubmitAsync(
            SubmitPracticeRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;

                if (string.IsNullOrWhiteSpace(request.TextContent) &&
                    string.IsNullOrWhiteSpace(request.ImageUrl) &&
                    string.IsNullOrWhiteSpace(request.AudioUrl))
                {
                    return ApiResponse<PracticeSubmissionResponse>.Fail(
                        "Bài nộp phải có nội dung văn bản, ảnh hoặc audio.");
                }

                var certificationLevel = await unitOfWork.CertificateLevels
                    .GetByIdAsync(request.JlptLevelId, cancellationToken);

                if (certificationLevel is null)
                {
                    return ApiResponse<PracticeSubmissionResponse>.Fail(
                        "Cấp độ chứng chỉ không hợp lệ.");
                }

                var submission = new PracticeSubmission
                {
                    UserId = userId,
                    Type = request.Type,
                    TextContent = request.TextContent,
                    ImageUrl = request.ImageUrl,
                    AudioUrl = request.AudioUrl,
                    CertificateLevelId = request.JlptLevelId,
                    SubmittedAt = DateTime.UtcNow
                };

                await unitOfWork.PracticeSubmissions.AddAsync(submission);
                await unitOfWork.SaveChangesAsync();

                await GradeInternalAsync(submission, certificationLevel.Code, cancellationToken);

                return ApiResponse<PracticeSubmissionResponse>.Success(
                    await MapToResponseAsync(submission, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApiResponse<PracticeSubmissionResponse>.Fail(
                    $"Không thể nộp bài: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PracticeSubmissionResponse>> GetByIdAsync(
            int submissionId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var submission = await unitOfWork.PracticeSubmissions.GetByIdAsync(submissionId);

                if (submission is null || submission.UserId != userId)
                {
                    return ApiResponse<PracticeSubmissionResponse>.Fail("Không tìm thấy bài nộp.");
                }

                return ApiResponse<PracticeSubmissionResponse>.Success(
                    await MapToResponseAsync(submission, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApiResponse<PracticeSubmissionResponse>.Fail(
                    $"Lỗi khi lấy bài nộp: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<PracticeSubmissionResponse>>> GetMyHistoryAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var submissions = await unitOfWork.PracticeSubmissions.FindAsync(
                    s => s.UserId == userId);

                var result = new List<PracticeSubmissionResponse>();

                foreach (var submission in submissions.OrderByDescending(s => s.SubmittedAt))
                {
                    result.Add(await MapToResponseAsync(submission, cancellationToken));
                }

                return ApiResponse<List<PracticeSubmissionResponse>>.Success(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PracticeSubmissionResponse>>.Fail(
                    $"Lỗi khi lấy lịch sử: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PracticeSubmissionResponse>> RegradeAsync(
            int submissionId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var submission = await unitOfWork.PracticeSubmissions.GetByIdAsync(submissionId);

                if (submission is null || submission.UserId != userId)
                {
                    return ApiResponse<PracticeSubmissionResponse>.Fail("Không tìm thấy bài nộp.");
                }

                var certificationLevel = await unitOfWork.CertificateLevels
                    .GetByIdAsync(submission.CertificateLevelId, cancellationToken);

                if (certificationLevel is null)
                {
                    return ApiResponse<PracticeSubmissionResponse>.Fail(
                        "Cấp độ chứng chỉ của bài nộp không còn hợp lệ.");
                }

                await GradeInternalAsync(submission, certificationLevel.Code, cancellationToken);

                return ApiResponse<PracticeSubmissionResponse>.Success(
                    await MapToResponseAsync(submission, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApiResponse<PracticeSubmissionResponse>.Fail(
                    $"Không thể chấm lại bài: {ex.Message}");
            }
        }

        private async Task GradeInternalAsync(
            PracticeSubmission submission,
            string certificationLevelCode,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(submission.AudioUrl))
                {
                    var transcription = await speechToTextClient.TranscribeFromUrlAsync(
                        submission.AudioUrl,
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(transcription.TranscribedText))
                    {
                        // Tránh nối lặp bản phiên âm mỗi lần gọi RegradeAsync.
                        submission.TextContent = transcription.TranscribedText;
                        unitOfWork.PracticeSubmissions.Update(submission);
                        await unitOfWork.SaveChangesAsync();
                    }
                }

                var promptText = BuildGradingPrompt(submission, certificationLevelCode);

                var rawJson = await aiGradingClient.GradeSubmissionJsonAsync(
                    submission.UserId,
                    promptText,
                    cancellationToken);

                var existing = (await unitOfWork.GradingResults.FindAsync(
                    g => g.PracticeSubmissionId == submission.Id,
                    cancellationToken)).FirstOrDefault();

                if (existing is null)
                {
                    await unitOfWork.GradingResults.AddAsync(new GradingResult
                    {
                        PracticeSubmissionId = submission.Id,
                        OverallScore = ExtractOverallScore(rawJson),
                        FeedbackJson = rawJson,
                        GradedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.OverallScore = ExtractOverallScore(rawJson);
                    existing.FeedbackJson = rawJson;
                    existing.GradedAt = DateTime.UtcNow;
                    unitOfWork.GradingResults.Update(existing);
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (AiQuotaExceededException)
            {
                // Bài nộp vẫn được lưu; có thể thử chấm lại khi quota được làm mới.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Bài nộp vẫn được lưu; có thể gọi RegradeAsync để thử lại.
            }
        }

        private static string BuildGradingPrompt(
            PracticeSubmission submission,
            string certificationLevelCode)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine(
                $"Bạn là giám khảo chấm bài luyện tập tiếng Nhật trình độ {certificationLevelCode}.");
            prompt.AppendLine($"Loại bài luyện tập: {submission.Type}.");

            if (!string.IsNullOrWhiteSpace(submission.TextContent))
            {
                prompt.AppendLine($"Nội dung bài làm: {submission.TextContent}");
            }

            if (!string.IsNullOrWhiteSpace(submission.ImageUrl))
            {
                prompt.AppendLine($"Ảnh bài làm (URL): {submission.ImageUrl}");
            }

            if (!string.IsNullOrWhiteSpace(submission.AudioUrl))
            {
                prompt.AppendLine(
                    $"Audio bài làm (URL; nội dung đã phiên âm nếu phiên âm thành công): {submission.AudioUrl}");
            }

            prompt.AppendLine();
            prompt.AppendLine(
                "Chấm điểm và chỉ trả về một JSON object hợp lệ theo schema sau:");
            prompt.AppendLine(
                "{ \"overallScore\": number (0-100), \"criteria\": [ { \"name\": string, \"score\": number, \"comment\": string } ], \"generalComment\": string }");

            return prompt.ToString();
        }

        private static double ExtractOverallScore(string rawJson)
        {
            try
            {
                using var document = JsonDocument.Parse(rawJson);

                if (document.RootElement.TryGetProperty("overallScore", out var scoreProperty) &&
                    scoreProperty.TryGetDouble(out var score))
                {
                    return score;
                }
            }
            catch (JsonException)
            {
                // Giữ nguyên phản hồi gốc trong FeedbackJson nếu JSON không hợp lệ.
            }

            return 0;
        }

        private async Task<PracticeSubmissionResponse> MapToResponseAsync(
            PracticeSubmission submission,
            CancellationToken cancellationToken)
        {
            var certificationLevel = await unitOfWork.CertificateLevels
                .GetByIdAsync(submission.CertificateLevelId, cancellationToken);

            var grading = (await unitOfWork.GradingResults.FindAsync(
                g => g.PracticeSubmissionId == submission.Id,
                cancellationToken)).FirstOrDefault();

            return new PracticeSubmissionResponse
            {
                Id = submission.Id,
                Type = submission.Type,
                TextContent = submission.TextContent,
                ImageUrl = submission.ImageUrl,
                AudioUrl = submission.AudioUrl,
                JlptLevelId = submission.CertificateLevelId,
                JlptLevelName = certificationLevel?.Code ?? "N/A",
                SubmittedAt = submission.SubmittedAt,
                Grading = grading is null
                    ? null
                    : new GradingResultResponse
                    {
                        Score = grading.OverallScore,
                        OverallFeedback = grading.FeedbackJson,
                        GradedAt = grading.GradedAt
                    }
            };
        }
    }
}