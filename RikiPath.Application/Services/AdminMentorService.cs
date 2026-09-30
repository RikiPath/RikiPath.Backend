using RikiPath.Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminMentor;
using RikiPath.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace RikiPath.Application.Services
{
    public class AdminMentorService(IUnitOfWork unitOfWork) : IAdminMentorService
    {
        public async Task<ApiResponse<MentorResponse>> CreateMentorAsync(
            CreateMentorRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return ApiResponse<MentorResponse>.Fail("Email không được để trống.");

                if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                    return ApiResponse<MentorResponse>.Fail("Mật khẩu phải có ít nhất 6 ký tự.");

                var normalizedEmail = request.Email.Trim().ToLowerInvariant();

                var existing = (await unitOfWork.UserAccounts
                    .FindAsync(u => u.Email.ToLower() == normalizedEmail)).FirstOrDefault();
                if (existing is not null)
                    return ApiResponse<MentorResponse>.Fail("Email này đã được sử dụng.");

                CreatePasswordHash(request.Password, out var hash, out var salt);

                var mentor = new UserAccount
                {
                    Email = normalizedEmail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    Role = Role.Mentor,
                    IsEmailVerified = true, // do Admin tạo trực tiếp, coi như đã xác thực
                    CreatedDate = DateTime.UtcNow
                };

                await unitOfWork.UserAccounts.AddAsync(mentor);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<MentorResponse>.Success(MapToMentorResponse(mentor));
            }
            catch (Exception ex)
            {
                return ApiResponse<MentorResponse>.Fail($"Không thể tạo mentor: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<MentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var mentors = await unitOfWork.UserAccounts.FindAsync(u => u.Role == Role.Mentor && !u.IsDeleted);
                var result = mentors
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .Select(MapToMentorResponse)
                    .ToList();

                return ApiResponse<List<MentorResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MentorResponse>>.Fail($"Không thể tải danh sách mentor: {ex.Message}");
            }
        }

        public async Task<ApiResponse<MentorResponse>> SetMentorActiveAsync(
            int mentorId, bool isActive, CancellationToken cancellationToken)
        {
            try
            {
                var mentor = await unitOfWork.UserAccounts.GetByIdAsync(mentorId);
                if (mentor is null || mentor.Role != Role.Mentor)
                    return ApiResponse<MentorResponse>.Fail("Không tìm thấy mentor.");

                mentor.IsDeleted = !isActive;
                mentor.ModifiedDate = DateTime.UtcNow;

                unitOfWork.UserAccounts.Update(mentor);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<MentorResponse>.Success(
                    MapToMentorResponse(mentor), message:
                    isActive ? "Đã kích hoạt mentor." : "Đã khóa mentor.");
            }
            catch (Exception ex)
            {
                return ApiResponse<MentorResponse>.Fail($"Không thể cập nhật trạng thái mentor: {ex.Message}");
            }
        }

        private static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
        {
            using var hmac = new HMACSHA512();
            salt = hmac.Key;
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private static MentorResponse MapToMentorResponse(UserAccount u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            IsEmailVerified = u.IsEmailVerified,
            CreatedDate = u.CreatedDate
        };
    }

}
