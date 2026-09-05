using RikiPath.Application.Services;

namespace RikiPath.Application.IServices
{
    public interface IClaimService
    {
        ClaimDTO GetUserClaim();
    }
}
