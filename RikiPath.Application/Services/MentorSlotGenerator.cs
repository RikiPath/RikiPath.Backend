using Microsoft.Extensions.Options;
using RikiPath.Application.IServices;
using RikiPath.Application.Options;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services;

// // Tạo khung giờ theo quy tắc xác định, không thay đổi dữ liệu; chưa công bố lịch, đặt buổi hoặc xử lý tranh chấp giao dịch.
public sealed class MentorSlotGenerator : IMentorSlotGenerator
{
    private readonly MentorSlotPolicy _policy;

    public MentorSlotGenerator(IOptions<MentorSlotGenerationOptions> options)
        => _policy = options.Value.ToPolicy();

    public IReadOnlyList<MentorGeneratedSlot> GenerateDailySlots(
        int mentorId,
        DateOnly localDate,
        IReadOnlyCollection<MentorDaypart> enabledDayparts,
        DateTimeOffset nowUtc,
        int policyVersion = 3)
    {
        if (mentorId <= 0)
            throw new ArgumentOutOfRangeException(nameof(mentorId), "Cố vấn được chọn không hợp lệ.");
        if (enabledDayparts is null)
            throw new ArgumentNullException(nameof(enabledDayparts), "Danh sách ca làm việc không được để trống.");
        if (policyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(policyVersion), "Phiên bản chính sách phải lớn hơn 0.");
        ValidateScheduleDate(localDate, nowUtc);

        if (enabledDayparts.Count == 0)
            throw new ArgumentException("Vui lòng chọn ít nhất một ca làm việc.", nameof(enabledDayparts));
        if (enabledDayparts.Distinct().Count() != enabledDayparts.Count)
            throw new ArgumentException("Không được chọn trùng ca làm việc.", nameof(enabledDayparts));

        var selected = new List<MentorDaypartWindow>(enabledDayparts.Count);
        foreach (var daypart in enabledDayparts)
        {
            if (!_policy.TryGetDaypart(daypart, out var window))
                throw new ArgumentException($"Ca làm việc {daypart} chưa được cấu hình.", nameof(enabledDayparts));
            selected.Add(window);
        }

        var slots = new List<MentorGeneratedSlot>();
        DateTimeOffset? previousSlotEndUtc = null;
        var duration = TimeSpan.FromMinutes(MentorSlotPolicy.AppointmentDurationMinutes);
        var rest = TimeSpan.FromMinutes(MentorSlotPolicy.MinimumRestMinutes);

        foreach (var window in selected.OrderBy(x => x.StartLocal))
        {
            var windowStartUtc = _policy.ToUtc(localDate, window.StartLocal);
            var windowEndUtc = _policy.ToUtc(localDate, window.EndLocal);
            var candidateStartUtc = previousSlotEndUtc is { } previousEnd && previousEnd + rest > windowStartUtc
                ? previousEnd + rest
                : windowStartUtc;

            while (candidateStartUtc + duration <= windowEndUtc)
            {
                var startLocal = TimeZoneInfo.ConvertTime(candidateStartUtc, _policy.BusinessTimeZone).DateTime;
                var endUtc = candidateStartUtc + duration;
                var key = $"mentor-slot:p{policyVersion}:m{mentorId}:{localDate:yyyy-MM-dd}:{startLocal:HHmm}";
                slots.Add(new MentorGeneratedSlot(
                    mentorId,
                    localDate,
                    window.Daypart,
                    candidateStartUtc,
                    endUtc,
                    policyVersion,
                    key));

                previousSlotEndUtc = endUtc;
                candidateStartUtc = endUtc + rest;
            }
        }

        return slots.AsReadOnly();
    }

    public MentorSlotValidationResult ValidateBooking(
        MentorBookingCandidate candidate,
        IReadOnlyCollection<ExistingMentorAppointment> existingAppointments,
        DateTimeOffset nowUtc)
    {
        if (candidate is null)
            throw new ArgumentNullException(nameof(candidate), "Thông tin đặt lịch không được để trống.");
        if (existingAppointments is null)
            throw new ArgumentNullException(nameof(existingAppointments), "Danh sách buổi hỗ trợ hiện có không được để trống.");

        if (candidate.MentorId <= 0 || candidate.PolicyVersion <= 0)
            return MentorSlotValidationResult.Invalid("INVALID_BOOKING", "Thông tin cố vấn hoặc chính sách lịch tư vấn không hợp lệ.");
        if (candidate.EndUtc <= candidate.StartUtc)
            return MentorSlotValidationResult.Invalid("INVALID_INTERVAL", "Giờ kết thúc buổi hỗ trợ phải sau giờ bắt đầu.");
        if (candidate.EndUtc - candidate.StartUtc != TimeSpan.FromMinutes(MentorSlotPolicy.AppointmentDurationMinutes))
            return MentorSlotValidationResult.Invalid("INVALID_DURATION", "Buổi hỗ trợ theo chính sách này phải kéo dài đúng 90 phút.");

        var startLocal = TimeZoneInfo.ConvertTime(candidate.StartUtc, _policy.BusinessTimeZone);
        var endLocal = TimeZoneInfo.ConvertTime(candidate.EndUtc, _policy.BusinessTimeZone);
        if (DateOnly.FromDateTime(startLocal.DateTime) != DateOnly.FromDateTime(endLocal.DateTime)
            || !_policy.TryGetDaypart(candidate.Daypart, out var window)
            || TimeOnly.FromDateTime(startLocal.DateTime) < window.StartLocal
            || TimeOnly.FromDateTime(endLocal.DateTime) > window.EndLocal)
            return MentorSlotValidationResult.Invalid("OUTSIDE_DAYPART", "Toàn bộ buổi hỗ trợ phải nằm trong ca làm việc đã cấu hình.");

        var now = nowUtc.ToUniversalTime();
        if (candidate.StartUtc.ToUniversalTime() < now.AddHours(MentorSlotPolicy.MinimumBookingLeadHours))
            return MentorSlotValidationResult.Invalid("LEAD_TIME", "Vui lòng đặt lịch trước giờ bắt đầu ít nhất 2 giờ.");

        var todayLocal = _policy.GetBusinessDate(now);
        var bookingLocalDate = DateOnly.FromDateTime(startLocal.DateTime);
        if (bookingLocalDate < todayLocal
            || bookingLocalDate > todayLocal.AddDays(MentorSlotPolicy.MaximumScheduleHorizonDays))
            return MentorSlotValidationResult.Invalid("HORIZON", "Chỉ có thể đặt lịch trong vòng 30 ngày tới theo giờ Việt Nam.");

        var relevant = existingAppointments
            .Where(x => x.MentorId == candidate.MentorId && !x.IsCancelled)
            .ToArray();
        if (relevant.Any(x => candidate.StartUtc < x.EndUtc && x.StartUtc < candidate.EndUtc))
            return MentorSlotValidationResult.Invalid("APPOINTMENT_OVERLAP", "Cố vấn đã có buổi tư vấn trùng thời gian này.");

        var rest = TimeSpan.FromMinutes(MentorSlotPolicy.MinimumRestMinutes);
        foreach (var existing in relevant.Where(x => x.UsesNinetyMinutePolicy))
        {
            var gap = existing.EndUtc <= candidate.StartUtc
                ? candidate.StartUtc - existing.EndUtc
                : candidate.EndUtc <= existing.StartUtc
                    ? existing.StartUtc - candidate.EndUtc
                    : TimeSpan.MaxValue;

            // Buổi trùng giờ đã bị từ chối. Chỉ áp dụng thời gian nghỉ giữa các buổi theo chính sách 90 phút;
            // quy tắc nghỉ giữa buổi lịch sử và buổi mới vẫn chờ quyết định nghiệp vụ.
            if (gap < rest)
                return MentorSlotValidationResult.Invalid("REST_BUFFER", "Hai buổi hỗ trợ theo chính sách 90 phút phải cách nhau ít nhất 15 phút.");
        }

        var currentPolicyBookingCount = relevant.Count(x => x.UsesNinetyMinutePolicy && _policy.GetBusinessDate(x.StartUtc) == bookingLocalDate);
        if (currentPolicyBookingCount >= _policy.DailyBookingCap)
            return MentorSlotValidationResult.Invalid("DAILY_CAP", $"Cố vấn đã đạt giới hạn {_policy.DailyBookingCap} buổi tư vấn trong ngày.");

        return MentorSlotValidationResult.Valid();
    }

    private void ValidateScheduleDate(DateOnly localDate, DateTimeOffset nowUtc)
    {
        var todayLocal = _policy.GetBusinessDate(nowUtc);
        if (localDate < todayLocal || localDate > todayLocal.AddDays(MentorSlotPolicy.MaximumScheduleHorizonDays))
            throw new ArgumentOutOfRangeException(nameof(localDate), "Chỉ có thể tạo lịch cho 30 ngày tới theo giờ Việt Nam.");
    }
}
