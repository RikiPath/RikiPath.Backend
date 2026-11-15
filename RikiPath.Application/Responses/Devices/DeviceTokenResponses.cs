using System;

namespace RikiPath.Application.Responses.Devices
{
    public class DeviceTokenResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DeviceIdentifier { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string FcmToken { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
