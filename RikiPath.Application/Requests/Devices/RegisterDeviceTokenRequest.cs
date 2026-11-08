namespace RikiPath.Application.Requests.Devices
{
    public class RegisterDeviceTokenRequest
    {
        public string DeviceIdentifier { get; set; } = string.Empty;
        public string Platform { get; set; } = "Android"; // "Android" | "iOS" | "Web"
        public string FcmToken { get; set; } = string.Empty;
    }

    public class UnregisterDeviceTokenRequest
    {
        public string DeviceIdentifier { get; set; } = string.Empty;
        public string? FcmToken { get; set; }
    }
}
