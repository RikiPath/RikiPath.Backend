using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.IServices;

// // Tạo và kiểm tra khung giờ trong bộ nhớ. Không lưu, công bố hay bảo đảm giao dịch đặt lịch nguyên tử.
public interface IMentorSlotGenerator
{
    IReadOnlyList<MentorGeneratedSlot> GenerateDailySlots(
        int mentorId,
        DateOnly localDate,
        IReadOnlyCollection<MentorDaypart> enabledDayparts,
        DateTimeOffset nowUtc,
        int policyVersion = 3);

    MentorSlotValidationResult ValidateBooking(
        MentorBookingCandidate candidate,
        IReadOnlyCollection<ExistingMentorAppointment> existingAppointments,
        DateTimeOffset nowUtc);
}
