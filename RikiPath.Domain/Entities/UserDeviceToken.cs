using System;

namespace RikiPath.Domain.Entities
{
    /// <summary>
    /// Represents a registered device token for mobile push notifications (FCM HTTP v1 / APNs).
    /// Supports multi-device fan-out, token rotation, and cross-user isolation.
    /// </summary>
    public class UserDeviceToken : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; } = null!;

        /// <summary>
        /// Hardware/app-scoped device UUID or unique client identifier.
        /// </summary>
        public string DeviceIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Platform type: "Android", "iOS", "Web", etc.
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Firebase Cloud Messaging registration token.
        /// </summary>
        public string FcmToken { get; set; } = string.Empty;

        /// <summary>
        /// Whether this token is currently active and valid for push delivery.
        /// Deactivated on logout, token rotation, or FCM permanent rejection (404/410/Unregistered).
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastSeenAtUtc { get; set; }
    }
}
