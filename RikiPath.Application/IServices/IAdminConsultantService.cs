using RikiPath.Application.Requests.AdminConsultant;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminConsultant;

namespace RikiPath.Application.IServices
{
    public interface IAdminConsultantService
    {
        Task<ApiResponse<ConsultantResponse>> CreateConsultantAsync(
            CreateConsultantRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<ConsultantResponse>>> GetConsultantsAsync(CancellationToken cancellationToken);

        Task<ApiResponse<ConsultantResponse>> SetConsultantActiveAsync(
            int consultantId, bool isActive, CancellationToken cancellationToken);

        // ---- Consultation package management ----
        Task<ApiResponse<ConsultationPackageResponse>> CreateConsultationPackageAsync(
            CreateConsultationPackageRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<ConsultationPackageResponse>> UpdateConsultationPackageAsync(
            int packageId, UpdateConsultationPackageRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<ConsultationPackageResponse>>> GetConsultationPackagesAsync(CancellationToken cancellationToken);

        Task<ApiResponse<ConsultationPackageResponse>> SetConsultationPackageActiveAsync(
            int packageId, bool isActive, CancellationToken cancellationToken);

    }
}
