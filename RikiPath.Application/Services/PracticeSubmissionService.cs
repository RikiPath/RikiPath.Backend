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
    public class PracticeSubmissionService(IUnitOfWork unitOfWork, IAiGradingClient aiGradingClient)
        : IPracticeSubmissionService
    {
        public async Task<ApiResponse<PracticeSubmissionResponse>> SubmitAsync(
            int userId, SubmitPracticeRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.TextContent) && string.IsNullOrWhiteSpace(request.ImageUrl))
                    return ApiResponse<PracticeSubmissionResponse>.Fail("Bài nộp phải có nội dung văn bản hoặc ảnh.");

                var jlptLevel = await unitOfWork.JlptLevels.GetByIdAsync(request.JlptLevelId);
                if (jlptLevel is null)
                    return ApiResponse<PracticeSubmissionResponse>.Fail("Cấp độ JLPT không hợp lệ.");

                var submission = new PracticeSubmission
                {
                    UserId = userId,
                    Type = request.Type,
                    TextContent = request.TextContent,
                    ImageUrl = request.ImageUrl,
                    JlptLevelId = request.JlptLevelId,
                    SubmittedAt = DateTime.UtcNow
                };

                await unitOfWork.PracticeSubmissions.AddAsync(submission);
                await unitOfWork.SaveChangesAsync();

                // Chấm điểm đồng bộ ngay sau khi lưu bài nộp. Nếu muốn phản hồi nhanh hơn cho learner,
                // tách phần này ra background job/queue và để FE poll trạng thái (Grading == null).
                await GradeInternalAsync(submission, jlptLevel.Name, cancellationToken);

                return ApiResponse<PracticeSubmissionResponse>.Success(
                    await MapToResponseAsync(submission, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<PracticeSubmissionResponse>.Fail($"Không thể nộp bài: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PracticeSubmissionResponse>> GetByIdAsync(
            int userId, int submissionId, CancellationToken cancellationToken)
        {
            try
            {
                var submission = await unitOfWork.PracticeSubmissions.GetByIdAsync(submissionId);
                if (submission is null || submission.UserId != userId)
                    return ApiResponse<PracticeSubmissionResponse>.Fail("Không tìm thấy bài nộp.");

                return ApiResponse<PracticeSubmissionResponse>.Success(await MapToResponseAsync(submission, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<PracticeSubmissionResponse>.Fail($"Lỗi khi lấy bài nộp: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<PracticeSubmissionResponse>>> GetMyHistoryAsync(
            int userId, CancellationToken cancellationToken)
        {
            try
            {
                var submissions = await unitOfWork.PracticeSubmissions.FindAsync(s => s.UserId == userId);

                var result = new List<PracticeSubmissionResponse>();
                foreach (var s in submissions.OrderByDescending(s => s.SubmittedAt))
                    result.Add(await MapToResponseAsync(s, cancellationToken));

                return ApiResponse<List<PracticeSubmissionResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PracticeSubmissionResponse>>.Fail($"Lỗi khi lấy lịch sử: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PracticeSubmissionResponse>> RegradeAsync(
            int userId, int submissionId, CancellationToken cancellationToken)
        {
            try
            {
                var submission = await unitOfWork.PracticeSubmissions.GetByIdAsync(submissionId);
                if (submission is null || submission.UserId != userId)
                    return ApiResponse<PracticeSubmissionResponse>.Fail("Không tìm thấy bài nộp.");

                var jlptLevel = await unitOfWork.JlptLevels.GetByIdAsync(submission.JlptLevelId);
                await GradeInternalAsync(submission, jlptLevel?.Name ?? "N3", cancellationToken);

                return ApiResponse<PracticeSubmissionResponse>.Success(
                    await MapToResponseAsync(submission, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<PracticeSubmissionResponse>.Fail($"Không thể chấm lại bài: {ex.Message}");
            }
        }

        private async Task GradeInternalAsync(PracticeSubmission submission, string jlptLevelName, CancellationToken cancellationToken)
        {
            try
            {
                var promptText = BuildGradingPrompt(submission, jlptLevelName);

                // submission.UserId: chỉnh lại tên property này nếu entity PracticeSubmission của bạn
                // đặt tên khác (vd LearnerId) - đây là user cần tính quota, không phải id của bài nộp.
                var rawJson = await aiGradingClient.GradeSubmissionJsonAsync(submission.UserId, promptText, cancellationToken);

                var overallScore = ExtractOverallScore(rawJson);

                var existing = (await unitOfWork.GradingResults
                    .FindAsync(g => g.PracticeSubmissionId == submission.Id)).FirstOrDefault();

                if (existing is null)
                {
                    await unitOfWork.GradingResults.AddAsync(new GradingResult
                    {
                        PracticeSubmissionId = submission.Id,
                        OverallScore = overallScore,
                        FeedbackJson = rawJson,
                        GradedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.OverallScore = overallScore;
                    existing.FeedbackJson = rawJson;
                    existing.GradedAt = DateTime.UtcNow;
                    unitOfWork.GradingResults.Update(existing);
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (AiQuotaExceededException)
            {
                // Vượt quota AI/ngày: khác bản chất với lỗi provider (mạng/AI lỗi tạm thời) - retry ngay
                // trong hôm nay chắc chắn sẽ fail lại vì quota chỉ reset qua ngày. Vẫn không throw ra ngoài
                // (giữ đúng hành vi cũ: bài nộp không hỏng), nhưng nên cân nhắc ghi log riêng ở đây (khác
                // log lỗi AI thường) để phân biệt khi debug, vì generic catch bên dưới sẽ không phân biệt được.
            }
            catch
            {
                // Không throw ra ngoài: bài nộp vẫn được lưu, learner có thể gọi lại RegradeAsync sau.
                // GradingResult vẫn null -> FE hiểu là "đang chờ chấm / chấm lỗi".
            }
        }

        // Ghép prompt text gửi cho AI. Yêu cầu model trả JSON đúng shape mô tả ở đầu file
        // (bắt buộc có "overallScore" ở top-level).
        private static string BuildGradingPrompt(PracticeSubmission submission, string jlptLevelName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Bạn là giám khảo chấm bài luyện tập tiếng Nhật trình độ JLPT {jlptLevelName}.");
            sb.AppendLine($"Loại bài luyện tập: {submission.Type}.");

            if (!string.IsNullOrWhiteSpace(submission.TextContent))
                sb.AppendLine($"Nội dung bài làm (văn bản): {submission.TextContent}");

            if (!string.IsNullOrWhiteSpace(submission.ImageUrl))
                sb.AppendLine($"Ảnh bài làm (URL): {submission.ImageUrl}");

            sb.AppendLine();
            sb.AppendLine("Hãy chấm điểm và trả lời CHỈ MỘT đối tượng JSON hợp lệ, không kèm text nào khác, " +
                           "đúng theo shape sau:");
            sb.AppendLine("{ \"overallScore\": number (0-100), \"criteria\": [ { \"name\": string, " +
                           "\"score\": number, \"comment\": string } ], \"generalComment\": string }");

            return sb.ToString();
        }

        // Parse điểm tổng từ raw JSON model trả về. Không throw nếu JSON sai định dạng hoặc thiếu
        // field - trả về 0 và để nguyên rawJson trong FeedbackJson để không mất dữ liệu.
        private static double ExtractOverallScore(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("overallScore", out var scoreProp) &&
                    scoreProp.TryGetDouble(out var score))
                {
                    return score;
                }
            }
            catch (JsonException)
            {
                // rawJson không phải JSON hợp lệ (model trả kèm text thừa, markdown code fence,...)
            }

            return 0;
        }

        private async Task<PracticeSubmissionResponse> MapToResponseAsync(PracticeSubmission s, CancellationToken cancellationToken)
        {
            var jlptLevel = await unitOfWork.JlptLevels.GetByIdAsync(s.JlptLevelId);
            var grading = (await unitOfWork.GradingResults
                .FindAsync(g => g.PracticeSubmissionId == s.Id)).FirstOrDefault();

            return new PracticeSubmissionResponse
            {
                Id = s.Id,
                Type = s.Type,
                TextContent = s.TextContent,
                ImageUrl = s.ImageUrl,
                JlptLevelId = s.JlptLevelId,
                JlptLevelName = jlptLevel?.Name ?? "N/A",
                SubmittedAt = s.SubmittedAt,
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