using System.Net;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.CertificateSections;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateSections;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services
{
    public sealed class AdminCertificateSectionService(
        IUnitOfWork unitOfWork,
        ILogger<AdminCertificateSectionService> logger) : IAdminCertificateSectionService
    {
        public async Task<ApiResponse<List<CertificateSectionResponse>>> GetAllAsync(
            int? certificateLevelId,
            bool includeArchived,
            CancellationToken cancellationToken)
        {
            try
            {
                if (certificateLevelId is <= 0)
                    return ApiResponse<List<CertificateSectionResponse>>.Fail(
                        "CertificateLevelId phải là số nguyên dương.");

                var sections = await unitOfWork.CertificateSections.GetForManagementAsync(
                    certificateLevelId,
                    includeArchived,
                    cancellationToken);

                return ApiResponse<List<CertificateSectionResponse>>.Success(
                    sections.Select(section => Map(section)).ToList());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể tải danh sách CertificateSection.");
                return ApiResponse<List<CertificateSectionResponse>>.Fail(
                    "Không thể tải danh sách CertificateSection.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<CertificateSectionResponse>> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var section = await unitOfWork.CertificateSections.GetForManagementByIdAsync(
                    id,
                    cancellationToken);
                return section is null
                    ? ApiResponse<CertificateSectionResponse>.NotFound("Không tìm thấy CertificateSection.")
                    : ApiResponse<CertificateSectionResponse>.Success(Map(section));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể tải CertificateSection {CertificateSectionId}.", id);
                return ApiResponse<CertificateSectionResponse>.Fail(
                    "Không thể tải CertificateSection.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<CertificateSectionResponse>> CreateAsync(
            CreateCertificateSectionRequest request,
            CancellationToken cancellationToken)
        {
            var validationError = Validate(
                request.CertificateLevelId,
                request.Name,
                request.Code,
                request.Description,
                request.DurationMinutes,
                request.MaxScore,
                request.PassingScore,
                request.SortOrder);
            if (validationError is not null)
                return ApiResponse<CertificateSectionResponse>.Fail(validationError);

            try
            {
                // Kiểm tra CertificateLevel JLPT hợp lệ trước khi tạo section để tránh dữ liệu mồ côi.
                var level = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(
                    request.CertificateLevelId,
                    cancellationToken);
                if (level is null)
                    return ApiResponse<CertificateSectionResponse>.Fail(
                        "CertificateLevel JLPT không tồn tại hoặc không khả dụng.");

                var now = DateTime.UtcNow;
                var section = new CertificateSection
                {
                    CertificateLevelId = level.Id,
                    Name = request.Name.Trim(),
                    Code = request.Code.Trim(),
                    Description = NormalizeOptional(request.Description),
                    DurationMinutes = request.DurationMinutes,
                    MaxScore = request.MaxScore,
                    PassingScore = request.PassingScore,
                    SortOrder = request.SortOrder,
                    CreatedDate = now,
                    IsDeleted = false,
                };

                await unitOfWork.CertificateSections.AddAsync(section, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<CertificateSectionResponse>.Created(Map(section, level.Code));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể tạo CertificateSection.");
                return ApiResponse<CertificateSectionResponse>.Fail(
                    "Không thể tạo CertificateSection.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<CertificateSectionResponse>> UpdateAsync(
            int id,
            UpdateCertificateSectionRequest request,
            CancellationToken cancellationToken)
        {
            var validationError = Validate(
                request.CertificateLevelId,
                request.Name,
                request.Code,
                request.Description,
                request.DurationMinutes,
                request.MaxScore,
                request.PassingScore,
                request.SortOrder);
            if (validationError is not null)
                return ApiResponse<CertificateSectionResponse>.Fail(validationError);

            try
            {
                var section = await unitOfWork.CertificateSections.GetByIdAsync(id, cancellationToken);
                if (section is null || section.IsDeleted)
                    return ApiResponse<CertificateSectionResponse>.NotFound("Không tìm thấy CertificateSection đang hoạt động.");

                var level = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(
                    request.CertificateLevelId,
                    cancellationToken);
                if (level is null)
                    return ApiResponse<CertificateSectionResponse>.Fail(
                        "CertificateLevel JLPT không tồn tại hoặc không khả dụng.");

                section.CertificateLevelId = level.Id;
                section.Name = request.Name.Trim();
                section.Code = request.Code.Trim();
                section.Description = NormalizeOptional(request.Description);
                section.DurationMinutes = request.DurationMinutes;
                section.MaxScore = request.MaxScore;
                section.PassingScore = request.PassingScore;
                section.SortOrder = request.SortOrder;
                section.ModifiedDate = DateTime.UtcNow;

                unitOfWork.CertificateSections.Update(section);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<CertificateSectionResponse>.Success(Map(section, level.Code));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể cập nhật CertificateSection {CertificateSectionId}.", id);
                return ApiResponse<CertificateSectionResponse>.Fail(
                    "Không thể cập nhật CertificateSection.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<CertificateSectionResponse>> ArchiveAsync(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var section = await unitOfWork.CertificateSections.GetByIdAsync(id, cancellationToken);
                if (section is null)
                    return ApiResponse<CertificateSectionResponse>.NotFound("Không tìm thấy CertificateSection.");

                if (!section.IsDeleted)
                {
                    // Chỉ lưu trữ mềm để giữ nguyên FK của nội dung Mock Test đang tham chiếu section.
                    section.IsDeleted = true;
                    section.ModifiedDate = DateTime.UtcNow;
                    unitOfWork.CertificateSections.Update(section);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                var archived = await unitOfWork.CertificateSections.GetForManagementByIdAsync(
                    section.Id,
                    cancellationToken);
                return ApiResponse<CertificateSectionResponse>.Success(Map(archived ?? section));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể lưu trữ CertificateSection {CertificateSectionId}.", id);
                return ApiResponse<CertificateSectionResponse>.Fail(
                    "Không thể lưu trữ CertificateSection.",
                    HttpStatusCode.InternalServerError);
            }
        }

        private static string? Validate(
            int certificateLevelId,
            string name,
            string code,
            string? description,
            int? durationMinutes,
            int maxScore,
            int passingScore,
            int sortOrder)
        {
            if (certificateLevelId <= 0)
                return "CertificateLevelId phải là số nguyên dương.";
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
                return "Name là bắt buộc và không được vượt quá 200 ký tự.";
            if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50)
                return "Code là bắt buộc và không được vượt quá 50 ký tự.";
            if (description?.Trim().Length > 1000)
                return "Description không được vượt quá 1000 ký tự.";
            if (durationMinutes is <= 0)
                return "DurationMinutes phải lớn hơn 0 khi được cung cấp.";
            if (maxScore <= 0)
                return "MaxScore phải lớn hơn 0.";
            if (passingScore < 0 || passingScore > maxScore)
                return "PassingScore phải nằm trong khoảng từ 0 đến MaxScore.";
            if (sortOrder <= 0)
                return "SortOrder phải lớn hơn 0.";

            return null;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static CertificateSectionResponse Map(
            CertificateSection section,
            string? certificateLevelCode = null) => new()
        {
            Id = section.Id,
            CertificateLevelId = section.CertificateLevelId,
            CertificateLevelCode = certificateLevelCode
                ?? section.CertificateLevel?.Code
                ?? string.Empty,
            Name = section.Name,
            Code = section.Code,
            Description = section.Description,
            DurationMinutes = section.DurationMinutes,
            MaxScore = section.MaxScore,
            PassingScore = section.PassingScore,
            SortOrder = section.SortOrder,
            IsArchived = section.IsDeleted,
            CreatedDate = section.CreatedDate,
            ModifiedDate = section.ModifiedDate,
        };
    }
}
