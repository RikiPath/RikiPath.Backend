using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.MentorScheduling
{
    public sealed class PreviewSlotsRequest
    {
        [Required(ErrorMessage = "Ngày bắt đầu không được để trống.")]
        public DateOnly StartDate { get; init; }

        [Required(ErrorMessage = "Ngày kết thúc không được để trống.")]
        public DateOnly EndDate { get; init; }
    }

    public sealed class PublishSlotsRequest
    {
        [Required(ErrorMessage = "Ngày bắt đầu không được để trống.")]
        public DateOnly StartDate { get; init; }

        [Required(ErrorMessage = "Ngày kết thúc không được để trống.")]
        public DateOnly EndDate { get; init; }
    }

    public sealed class DisableSlotRequest
    {
        [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
        public string? Reason { get; init; }
    }
}
