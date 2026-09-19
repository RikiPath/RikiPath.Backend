using RikiPath.Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IServices;

namespace RikiPath.Application.Services
{
    public class ClaimService(IHttpContextAccessor httpContextAccessor) : IClaimService
    {
        public ClaimDTO GetUserClaim()
        {
            var user = httpContextAccessor.HttpContext?.User;
            var tokenUserId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                              ?? user?.FindFirst("sub")
                              ?? user?.FindFirst("userId")
                              ?? user?.FindFirst("id");

            var tokenUserRole = user?.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? user?.FindFirst("role");

            if (tokenUserId == null || string.IsNullOrWhiteSpace(tokenUserId.Value))
            {
                throw new ArgumentNullException("UserId can not be found in JWT claims.");
            }

            if (!int.TryParse(tokenUserId.Value, out var userId))
                throw new FormatException("UserId claim is not a valid integer.");

            Role userRole = tokenUserRole is null
                ? Role.Learner
                : Enum.Parse<Role>(tokenUserRole.Value, ignoreCase: true);

            return new ClaimDTO { Role = userRole, Id = userId };
        }
    }

    public class ClaimDTO
    {
        public int Id { get; set; }
        public Role Role { get; set; }
    }
}