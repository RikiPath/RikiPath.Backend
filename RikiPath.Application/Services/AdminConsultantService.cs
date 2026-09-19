using Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminConsultant;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminConsultant;
using RikiPath.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace RikiPath.Application.Services
{
    public class AdminConsultantService(IUnitOfWork unitOfWork) : IAdminConsultantService
    {
        public async Task<ApiResponse<ConsultantResponse>> CreateConsultantAsync(
            CreateConsultantRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return ApiResponse<ConsultantResponse>.Fail("Email không được để trống.");

                if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                    return ApiResponse<ConsultantResponse>.Fail("Mật khẩu phải có ít nhất 6 ký tự.");

                var normalizedEmail = request.Email.Trim().ToLowerInvariant();

                var existing = (await unitOfWork.UserAccounts
                    .FindAsync(u => u.Email.ToLower() == normalizedEmail)).FirstOrDefault();
                if (existing is not null)
                    return ApiResponse<ConsultantResponse>.Fail("Email này đã được sử dụng.");

                CreatePasswordHash(request.Password, out var hash, out var salt);

                var consultant = new UserAccount
                {
                    Email = normalizedEmail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    Role = Role.Consultant,
                    IsEmailVerified = true, // do Admin tạo trực tiếp, coi như đã xác thực
                    CreatedDate = DateTime.UtcNow
                };

                await unitOfWork.UserAccounts.AddAsync(consultant);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ConsultantResponse>.Success(MapToConsultantResponse(consultant));
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultantResponse>.Fail($"Không thể tạo consultant: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ConsultantResponse>>> GetConsultantsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var consultants = await unitOfWork.UserAccounts.FindAsync(u => u.Role == Role.Consultant && !u.IsDeleted);
                var result = consultants
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .Select(MapToConsultantResponse)
                    .ToList();

                return ApiResponse<List<ConsultantResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ConsultantResponse>>.Fail($"Không thể tải danh sách consultant: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultantResponse>> SetConsultantActiveAsync(
            int consultantId, bool isActive, CancellationToken cancellationToken)
        {
            try
            {
                var consultant = await unitOfWork.UserAccounts.GetByIdAsync(consultantId);
                if (consultant is null || consultant.Role != Role.Consultant)
                    return ApiResponse<ConsultantResponse>.Fail("Không tìm thấy consultant.");

                consultant.IsDeleted = !isActive;
                consultant.ModifiedDate = DateTime.UtcNow;

                unitOfWork.UserAccounts.Update(consultant);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ConsultantResponse>.Success(
                    MapToConsultantResponse(consultant), message:
                    isActive ? "Đã kích hoạt consultant." : "Đã khóa consultant.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultantResponse>.Fail($"Không thể cập nhật trạng thái consultant: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationPackageResponse>> CreateConsultationPackageAsync(
            CreateConsultationPackageRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<ConsultationPackageResponse>.Fail("Tên gói không được để trống.");

                if (request.Price < 0)
                    return ApiResponse<ConsultationPackageResponse>.Fail("Giá gói không được âm.");

                if (request.DurationMinutes <= 0)
                    return ApiResponse<ConsultationPackageResponse>.Fail("Thời lượng gói phải lớn hơn 0.");

                var package = new ConsultationPackage
                {
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    Type = request.Type,
                    Price = request.Price,
                    DurationMinutes = request.DurationMinutes,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                await unitOfWork.ConsultationPackages.AddAsync(package);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ConsultationPackageResponse>.Success(MapToPackageResponse(package), message: "Tạo gói tư vấn thành công.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationPackageResponse>.Fail($"Không thể tạo gói tư vấn: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationPackageResponse>> UpdateConsultationPackageAsync(
            int packageId, UpdateConsultationPackageRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var package = await unitOfWork.ConsultationPackages.GetByIdAsync(packageId);
                if (package is null)
                    return ApiResponse<ConsultationPackageResponse>.Fail("Không tìm thấy gói tư vấn.");

                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<ConsultationPackageResponse>.Fail("Tên gói không được để trống.");

                if (request.Price < 0)
                    return ApiResponse<ConsultationPackageResponse>.Fail("Giá gói không được âm.");

                if (request.DurationMinutes <= 0)
                    return ApiResponse<ConsultationPackageResponse>.Fail("Thời lượng gói phải lớn hơn 0.");

                // Không cho đổi Type (Meeting/WrittenAnswer) sau khi tạo để tránh phá vỡ các
                // ConsultationPurchase/ConsultationRequest đã gắn với gói theo đúng hình thức cũ.
                package.Name = request.Name.Trim();
                package.Description = request.Description;
                package.Price = request.Price;
                package.DurationMinutes = request.DurationMinutes;
                package.ModifiedDate = DateTime.UtcNow;

                unitOfWork.ConsultationPackages.Update(package);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ConsultationPackageResponse>.Success(MapToPackageResponse(package), message: "Cập nhật gói tư vấn thành công.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationPackageResponse>.Fail($"Không thể cập nhật gói tư vấn: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ConsultationPackageResponse>>> GetConsultationPackagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var packages = await unitOfWork.ConsultationPackages.GetAllAsync();
                var result = packages.OrderBy(p => p.Name).Select(MapToPackageResponse).ToList();
                return ApiResponse<List<ConsultationPackageResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ConsultationPackageResponse>>.Fail($"Không thể tải danh sách gói tư vấn: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationPackageResponse>> SetConsultationPackageActiveAsync(
            int packageId, bool isActive, CancellationToken cancellationToken)
        {
            try
            {
                var package = await unitOfWork.ConsultationPackages.GetByIdAsync(packageId);
                if (package is null)
                    return ApiResponse<ConsultationPackageResponse>.Fail("Không tìm thấy gói tư vấn.");

                package.IsActive = isActive;
                package.ModifiedDate = DateTime.UtcNow;

                unitOfWork.ConsultationPackages.Update(package);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ConsultationPackageResponse>.Success(
                    MapToPackageResponse(package), message:
                    isActive ? "Đã mở bán gói tư vấn." : "Đã ngừng bán gói tư vấn.");
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationPackageResponse>.Fail($"Không thể cập nhật trạng thái gói: {ex.Message}");
            }
        }

        private static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
        {
            using var hmac = new HMACSHA512();
            salt = hmac.Key;
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private static ConsultantResponse MapToConsultantResponse(UserAccount u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            IsEmailVerified = u.IsEmailVerified,
            CreatedDate = u.CreatedDate
        };

        private static ConsultationPackageResponse MapToPackageResponse(ConsultationPackage p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Type = p.Type,
            Price = p.Price,
            DurationMinutes = p.DurationMinutes,
            IsActive = p.IsActive
        };
    }

}
