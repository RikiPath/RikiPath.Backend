using RikiPath.Application.Common;
using RikiPath.Application.Requests.Consultations;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Consultations;

namespace RikiPath.Application.IServices
{
    public interface IConsultationService
    {
        // ---- Learner ----
        Task<ApiResponse<ConsultationRequestResponse>> BookMeetingAsync(
            int learnerId, BookMeetingRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<ConsultationRequestResponse>> SubmitTicketAsync(
            int learnerId, SubmitTicketRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<ConsultationRequestResponse>>> GetMyRequestsAsync(
            int learnerId, CancellationToken cancellationToken);

        // ---- Consultant ----
        Task<ApiResponse<ConsultantQueueResponse>> GetConsultantQueueAsync(
            int consultantId, CancellationToken cancellationToken);

        // Nhận 1 ticket đang chờ (chưa có consultant nào claim) vào hàng đợi của mình
        Task<ApiResponse<ConsultationRequestResponse>> ClaimTicketAsync(
            int consultantId, int requestId, CancellationToken cancellationToken);

        Task<ApiResponse<ConsultationRequestResponse>> SubmitAnswerAsync(
            int consultantId, int requestId, SubmitAnswerRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<ConsultationRequestResponse>> LogMeetingNoteAsync(
            int consultantId, int requestId, LogMeetingNoteRequest request, CancellationToken cancellationToken);
    }
}
