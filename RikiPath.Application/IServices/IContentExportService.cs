using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.Application.IServices;

public sealed record ExportFileResult(byte[] Bytes, string ContentType, string FileName);

public interface IContentExportService
{
    Task<ApiResponse<ExportFileResult>> ExportExamAsync(
        int examId, string format, bool includeAnswers, int actorUserId, bool isAuthorOrAdmin, CancellationToken ct);

    Task<ApiResponse<ExportFileResult>> ExportQuestionBankAsync(
        QuestionBankQueryFilter filter, string format, bool includeAnswers, int actorUserId, bool isAuthorOrAdmin, CancellationToken ct);

    Task<ApiResponse<RoundTripVerificationResponse>> VerifyExamRoundTripAsync(
        int examId, string format, int actorUserId, bool isAuthorOrAdmin, CancellationToken ct);
}
