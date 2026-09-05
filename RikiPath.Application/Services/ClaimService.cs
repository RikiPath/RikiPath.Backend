using Domain.Entities;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IServices;

namespace RikiPath.Application.Services
{
    public class ClaimService(IHttpContextAccessor httpContextAccessor) : IClaimService
    {
        public ClaimDTO GetUserClaim()
        {
            var tokenUserId = httpContextAccessor.HttpContext!.User.FindFirst("UserId");
            var tokenUserRole = httpContextAccessor.HttpContext!.User.FindFirst("Role");
            if (tokenUserId == null)
            {
                throw new ArgumentNullException("UserId can not be found!");
            }
            var userId = Int32.Parse(tokenUserId?.Value.ToString()!);
            Role userRole = Enum.Parse<Role>(tokenUserRole?.Value.ToString()!);
            var userClaim = new ClaimDTO
            {
                Role = userRole,
                Id = userId
            };

            return userClaim;
        }
    }

    public class ClaimDTO
    {
        public int Id { get; set; }
        public Role Role { get; set; }
    }
}