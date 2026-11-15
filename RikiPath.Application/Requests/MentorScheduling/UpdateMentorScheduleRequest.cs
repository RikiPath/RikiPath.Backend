using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorScheduling
{
    public sealed class UpdateMentorScheduleRequest
    {
        [MaxLength(64, ErrorMessage = "Tên múi giờ không được vượt quá 64 ký tự.")]
        public string TimeZoneId { get; init; } = "Asia/Ho_Chi_Minh";

        public bool AutoPublish { get; init; } = true;

        public DateOnly EffectiveFrom { get; init; }

        public List<MentorScheduleRuleRequest> Rules { get; init; } = [];
    }
}
