using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorScheduling
{
    public sealed class MentorScheduleRuleRequest
    {
        [Range(0, 6, ErrorMessage = "Thứ trong tuần phải nằm trong khoảng từ 0 (Chủ nhật) đến 6 (Thứ bảy).")]
        public int Weekday { get; init; }

        [Required(ErrorMessage = "Mã ca làm việc không được để trống.")]
        [RegularExpression("^(Morning|Noon|Afternoon|Evening)$", ErrorMessage = "Ca làm việc phải là một trong các giá trị: Morning, Noon, Afternoon, Evening.")]
        public string DaypartCode { get; init; } = "Morning";

        public TimeOnly LocalStart { get; init; }

        public TimeOnly LocalEnd { get; init; }

        public DateOnly EffectiveFrom { get; init; }

        public DateOnly? EffectiveTo { get; init; }
    }
}
