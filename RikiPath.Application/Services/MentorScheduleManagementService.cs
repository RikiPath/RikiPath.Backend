using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Application.Services
{
    // // Triển khai dịch vụ quản lý mẫu lịch làm việc và tích hợp bộ sinh khung giờ (Slot Generation Integration).
    public sealed class MentorScheduleManagementService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        IMentorProfileService mentorProfileService,
        IMentorSlotGenerator slotGenerator,
        ILogger<MentorScheduleManagementService> logger) : IMentorScheduleManagementService
    {
        private const int DefaultPolicyVersion = 3;

        public async Task<ApiResponse<MentorScheduleResponse>> GetMyScheduleAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Mentor)
                    return ApiResponse<MentorScheduleResponse>.Fail("Chỉ cố vấn mới có quyền truy cập lịch tư vấn.", HttpStatusCode.Forbidden);

                var schedule = await unitOfWork.MentorSchedules.GetByMentorIdWithRulesAsync(claim.Id, DefaultPolicyVersion, cancellationToken);
                if (schedule is null)
                {
                    var user = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, cancellationToken);
                    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                    var defaultResponse = new MentorScheduleResponse
                    {
                        ScheduleId = 0,
                        MentorId = claim.Id,
                        MentorName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : string.Empty,
                        PolicyVersion = DefaultPolicyVersion,
                        TimeZoneId = MentorSlotPolicy.BusinessTimeZoneId,
                        AutoPublish = true,
                        EffectiveFrom = today,
                        GeneratedThrough = null,
                        CreatedAtUtc = DateTime.UtcNow,
                        Rules = []
                    };
                    return ApiResponse<MentorScheduleResponse>.Success(defaultResponse);
                }

                return ApiResponse<MentorScheduleResponse>.Success(MapToMentorScheduleResponse(schedule));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi lấy mẫu lịch làm việc của chuyên gia.");
                return ApiResponse<MentorScheduleResponse>.Fail($"Không thể tải lịch làm việc: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorScheduleResponse>> UpdateMyScheduleAsync(
            UpdateMentorScheduleRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Mentor)
                    return ApiResponse<MentorScheduleResponse>.Fail("Chỉ cố vấn mới có quyền cấu hình lịch tư vấn.", HttpStatusCode.Forbidden);

                var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                    ? MentorSlotPolicy.BusinessTimeZoneId
                    : request.TimeZoneId.Trim();

                TimeZoneInfo timeZoneInfo;
                try
                {
                    timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (Exception)
                {
                    return ApiResponse<MentorScheduleResponse>.Fail($"Múi giờ '{timeZoneId}' không hợp lệ hoặc không được hỗ trợ bởi hệ điều hành.", HttpStatusCode.BadRequest);
                }

                // Kiểm tra hợp lệ từng quy tắc
                var validationError = ValidateRules(request.Rules);
                if (validationError is not null)
                    return ApiResponse<MentorScheduleResponse>.Fail(validationError, HttpStatusCode.BadRequest);

                var schedule = await unitOfWork.MentorSchedules.GetByMentorIdWithRulesAsync(claim.Id, DefaultPolicyVersion, cancellationToken);
                var isNew = false;

                if (schedule is null)
                {
                    isNew = true;
                    schedule = new MentorSchedule
                    {
                        MentorId = claim.Id,
                        PolicyVersion = DefaultPolicyVersion,
                        TimeZoneId = timeZoneInfo.Id,
                        AutoPublish = request.AutoPublish,
                        EffectiveFrom = request.EffectiveFrom,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    await unitOfWork.MentorSchedules.AddAsync(schedule, cancellationToken);
                }
                else
                {
                    schedule.TimeZoneId = timeZoneInfo.Id;
                    schedule.AutoPublish = request.AutoPublish;
                    schedule.EffectiveFrom = request.EffectiveFrom;
                }

                // Đồng bộ danh sách rules
                SyncRules(schedule, request.Rules);

                if (!isNew)
                {
                    unitOfWork.MentorSchedules.Update(schedule);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);

                // Nạp lại kèm thông tin mentor
                var refreshed = await unitOfWork.MentorSchedules.GetByMentorIdWithRulesAsync(claim.Id, DefaultPolicyVersion, cancellationToken);
                return ApiResponse<MentorScheduleResponse>.Success(
                    MapToMentorScheduleResponse(refreshed ?? schedule),
                    message: "Cập nhật mẫu lịch làm việc và quy tắc ca thành công.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi cập nhật mẫu lịch làm việc của chuyên gia.");
                return ApiResponse<MentorScheduleResponse>.Fail($"Không thể cập nhật lịch làm việc: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<MentorScheduleRuleResponse>>> GetMyScheduleRulesAsync(CancellationToken cancellationToken = default)
        {
            var scheduleResult = await GetMyScheduleAsync(cancellationToken);
            if (!scheduleResult.IsSuccess || scheduleResult.Result is null)
                return ApiResponse<List<MentorScheduleRuleResponse>>.Fail(scheduleResult.ErrorMessage ?? "Không thể lấy quy tắc lịch.", (HttpStatusCode)scheduleResult.StatusCode);

            return ApiResponse<List<MentorScheduleRuleResponse>>.Success(scheduleResult.Result.Rules);
        }

        public async Task<ApiResponse<List<MentorScheduleRuleResponse>>> UpdateMyScheduleRulesAsync(
            List<MentorScheduleRuleRequest> rules, CancellationToken cancellationToken = default)
        {
            var currentSchedule = await GetMyScheduleAsync(cancellationToken);
            if (!currentSchedule.IsSuccess || currentSchedule.Result is null)
                return ApiResponse<List<MentorScheduleRuleResponse>>.Fail(currentSchedule.ErrorMessage ?? "Không thể lấy lịch hiện tại.", (HttpStatusCode)currentSchedule.StatusCode);

            var updateRequest = new UpdateMentorScheduleRequest
            {
                TimeZoneId = currentSchedule.Result.TimeZoneId,
                AutoPublish = currentSchedule.Result.AutoPublish,
                EffectiveFrom = currentSchedule.Result.EffectiveFrom,
                Rules = rules
            };

            var updateResult = await UpdateMyScheduleAsync(updateRequest, cancellationToken);
            if (!updateResult.IsSuccess || updateResult.Result is null)
                return ApiResponse<List<MentorScheduleRuleResponse>>.Fail(updateResult.ErrorMessage ?? "Không thể cập nhật quy tắc.", (HttpStatusCode)updateResult.StatusCode);

            return ApiResponse<List<MentorScheduleRuleResponse>>.Success(updateResult.Result.Rules);
        }

        public async Task<ApiResponse<SlotPreviewResponse>> PreviewSlotsAsync(
            PreviewSlotsRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Mentor)
                    return ApiResponse<SlotPreviewResponse>.Fail("Chỉ cố vấn mới có quyền xem trước khung giờ tư vấn.", HttpStatusCode.Forbidden);

                // GATE BẢO MẬT: Chuyên gia phải ĐÃ ĐƯỢC PHÊ DUYỆT (Approved)
                var isEligible = await mentorProfileService.IsMentorEligibleForSchedulingAsync(claim.Id, cancellationToken);
                if (!isEligible)
                {
                    return ApiResponse<SlotPreviewResponse>.Fail(
                        "Hồ sơ của bạn chưa được Quản trị viên phê duyệt (Approved). Bạn không thể tạo hoặc xem trước lịch hẹn khả dụng.",
                        HttpStatusCode.Forbidden);
                }

                if (request.StartDate > request.EndDate)
                    return ApiResponse<SlotPreviewResponse>.Fail("Ngày bắt đầu không được lớn hơn ngày kết thúc.", HttpStatusCode.BadRequest);

                var schedule = await unitOfWork.MentorSchedules.GetByMentorIdWithRulesAsync(claim.Id, DefaultPolicyVersion, cancellationToken);
                if (schedule is null || schedule.Rules.Count == 0)
                {
                    return ApiResponse<SlotPreviewResponse>.Fail(
                        "Bạn chưa cấu hình mẫu lịch làm việc hoặc chưa có ca làm việc nào. Vui lòng cập nhật lịch làm việc trước.",
                        HttpStatusCode.BadRequest);
                }

                var tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
                var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
                var todayLocal = DateOnly.FromDateTime(nowLocal.DateTime);

                if (request.StartDate < todayLocal)
                    return ApiResponse<SlotPreviewResponse>.Fail("Không thể xem trước khung giờ cho các ngày trong quá khứ.", HttpStatusCode.BadRequest);

                if (request.EndDate > todayLocal.AddDays(MentorSlotPolicy.MaximumScheduleHorizonDays))
                    return ApiResponse<SlotPreviewResponse>.Fail($"Chỉ được xem trước khung giờ trong phạm vi {MentorSlotPolicy.MaximumScheduleHorizonDays} ngày tới.", HttpStatusCode.BadRequest);

                var previewItems = new List<SlotPreviewItemResponse>();

                // Nạp tất cả slot hiện có của mentor trong khoảng thời gian để kiểm tra trùng
                var startRangeUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartDate.ToDateTime(TimeOnly.MinValue), tz);
                var endRangeUtc = TimeZoneInfo.ConvertTimeToUtc(request.EndDate.ToDateTime(TimeOnly.MaxValue), tz);
                var existingSlots = await unitOfWork.MentorAvailabilities.GetSlotsInRangeAsync(claim.Id, startRangeUtc, endRangeUtc, cancellationToken);
                var existingKeySet = new HashSet<string>(
                    existingSlots.Where(x => !string.IsNullOrEmpty(x.GenerationKey)).Select(x => x.GenerationKey!),
                    StringComparer.OrdinalIgnoreCase);

                var currentDate = request.StartDate;
                while (currentDate <= request.EndDate)
                {
                    var weekday = (int)currentDate.DayOfWeek;
                    var activeRulesForDay = schedule.Rules
                        .Where(r => r.Weekday == weekday && r.EffectiveFrom <= currentDate && (r.EffectiveTo == null || currentDate <= r.EffectiveTo.Value))
                        .ToList();

                    if (activeRulesForDay.Count > 0)
                    {
                        var dayparts = new List<MentorDaypart>();
                        foreach (var rule in activeRulesForDay)
                        {
                            if (Enum.TryParse<MentorDaypart>(rule.DaypartCode, true, out var dp) && !dayparts.Contains(dp))
                                dayparts.Add(dp);
                        }

                        if (dayparts.Count > 0)
                        {
                            var dailySlots = slotGenerator.GenerateDailySlots(
                                claim.Id, currentDate, dayparts, DateTimeOffset.UtcNow, DefaultPolicyVersion);

                            foreach (var s in dailySlots)
                            {
                                var isExisting = existingKeySet.Contains(s.GenerationKey);
                                var hasConflict = false;
                                string? conflictReason = null;

                                // Kiểm tra xung đột với slot khác (khác GenerationKey)
                                var overlapping = existingSlots.FirstOrDefault(x =>
                                    x.GenerationKey != s.GenerationKey &&
                                    x.StartTime < s.EndUtc.UtcDateTime &&
                                    s.StartUtc.UtcDateTime < x.EndTime);

                                if (overlapping is not null)
                                {
                                    hasConflict = true;
                                    conflictReason = overlapping.BookedCount > 0
                                        ? "Trùng với khung giờ đã có học viên đặt lịch."
                                        : "Trùng với khung giờ làm việc khác đã tạo trước đó.";
                                }

                                var localStartStr = TimeZoneInfo.ConvertTime(s.StartUtc, tz).ToString("HH:mm");
                                var localEndStr = TimeZoneInfo.ConvertTime(s.EndUtc, tz).ToString("HH:mm");

                                previewItems.Add(new SlotPreviewItemResponse
                                {
                                    LocalDate = s.LocalDate,
                                    DaypartCode = s.Daypart.ToString(),
                                    StartUtc = s.StartUtc.UtcDateTime,
                                    EndUtc = s.EndUtc.UtcDateTime,
                                    LocalStart = localStartStr,
                                    LocalEnd = localEndStr,
                                    GenerationKey = s.GenerationKey,
                                    IsExisting = isExisting,
                                    HasConflict = hasConflict,
                                    ConflictReason = conflictReason
                                });
                            }
                        }
                    }

                    currentDate = currentDate.AddDays(1);
                }

                var response = new SlotPreviewResponse
                {
                    TotalCandidateSlots = previewItems.Count,
                    ExistingSlotsCount = previewItems.Count(x => x.IsExisting),
                    NewSlotsCount = previewItems.Count(x => !x.IsExisting && !x.HasConflict),
                    ConflictSlotsCount = previewItems.Count(x => x.HasConflict),
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Slots = previewItems
                };

                return ApiResponse<SlotPreviewResponse>.Success(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi xem trước khung giờ sinh lịch của chuyên gia.");
                return ApiResponse<SlotPreviewResponse>.Fail($"Không thể xem trước khung giờ: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PublishSlotsResponse>> PublishSlotsAsync(
            PublishSlotsRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Mentor)
                    return ApiResponse<PublishSlotsResponse>.Fail("Chỉ cố vấn mới có quyền công bố khung giờ tư vấn.", HttpStatusCode.Forbidden);

                // GATE BẢO MẬT: Chuyên gia phải ĐÃ ĐƯỢC PHÊ DUYỆT (Approved)
                var isEligible = await mentorProfileService.IsMentorEligibleForSchedulingAsync(claim.Id, cancellationToken);
                if (!isEligible)
                {
                    return ApiResponse<PublishSlotsResponse>.Fail(
                        "Hồ sơ của bạn chưa được Quản trị viên phê duyệt (Approved). Bạn không được phép công bố hoặc tạo khung giờ đặt lịch mới.",
                        HttpStatusCode.Forbidden);
                }

                if (request.StartDate > request.EndDate)
                    return ApiResponse<PublishSlotsResponse>.Fail("Ngày bắt đầu không được lớn hơn ngày kết thúc.", HttpStatusCode.BadRequest);

                var schedule = await unitOfWork.MentorSchedules.GetByMentorIdWithRulesAsync(claim.Id, DefaultPolicyVersion, cancellationToken);
                if (schedule is null || schedule.Rules.Count == 0)
                {
                    return ApiResponse<PublishSlotsResponse>.Fail(
                        "Bạn chưa cấu hình mẫu lịch làm việc hoặc chưa có ca làm việc nào. Vui lòng cập nhật lịch làm việc trước.",
                        HttpStatusCode.BadRequest);
                }

                var tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
                var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
                var todayLocal = DateOnly.FromDateTime(nowLocal.DateTime);

                if (request.StartDate < todayLocal)
                    return ApiResponse<PublishSlotsResponse>.Fail("Không thể công bố khung giờ cho các ngày trong quá khứ.", HttpStatusCode.BadRequest);

                if (request.EndDate > todayLocal.AddDays(MentorSlotPolicy.MaximumScheduleHorizonDays))
                    return ApiResponse<PublishSlotsResponse>.Fail($"Chỉ được công bố khung giờ trong phạm vi {MentorSlotPolicy.MaximumScheduleHorizonDays} ngày tới.", HttpStatusCode.BadRequest);

                var createdSlots = new List<PublishedSlotItemResponse>();
                var createdCount = 0;
                var skippedCount = 0;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var startRangeUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartDate.ToDateTime(TimeOnly.MinValue), tz);
                    var endRangeUtc = TimeZoneInfo.ConvertTimeToUtc(request.EndDate.ToDateTime(TimeOnly.MaxValue), tz);

                    // Nạp tất cả slot hiện có trong range để kiểm tra idempotent và tránh ghi đè booking
                    var existingSlots = await unitOfWork.MentorAvailabilities.GetSlotsInRangeAsync(claim.Id, startRangeUtc, endRangeUtc, token);
                    var existingByKey = existingSlots
                        .Where(x => !string.IsNullOrEmpty(x.GenerationKey))
                        .ToDictionary(x => x.GenerationKey!, StringComparer.OrdinalIgnoreCase);

                    var currentDate = request.StartDate;
                    while (currentDate <= request.EndDate)
                    {
                        var weekday = (int)currentDate.DayOfWeek;
                        var activeRulesForDay = schedule.Rules
                            .Where(r => r.Weekday == weekday && r.EffectiveFrom <= currentDate && (r.EffectiveTo == null || currentDate <= r.EffectiveTo.Value))
                            .ToList();

                        if (activeRulesForDay.Count > 0)
                        {
                            var dayparts = new List<MentorDaypart>();
                            foreach (var rule in activeRulesForDay)
                            {
                                if (Enum.TryParse<MentorDaypart>(rule.DaypartCode, true, out var dp) && !dayparts.Contains(dp))
                                    dayparts.Add(dp);
                            }

                            if (dayparts.Count > 0)
                            {
                                var dailySlots = slotGenerator.GenerateDailySlots(
                                    claim.Id, currentDate, dayparts, DateTimeOffset.UtcNow, DefaultPolicyVersion);

                                foreach (var s in dailySlots)
                                {
                                    // 1. Kiểm tra nếu GenerationKey đã tồn tại
                                    if (existingByKey.TryGetValue(s.GenerationKey, out var existingSlot))
                                    {
                                        if (!existingSlot.IsDeleted)
                                        {
                                            skippedCount++;
                                            continue; // Idempotent: Bỏ qua slot đã công bố và đang active
                                        }

                                        // Nếu bị soft-delete và chưa từng có booking -> Kích hoạt lại
                                        if (existingSlot.BookedCount == 0 && (existingSlot.MentorBookings == null || existingSlot.MentorBookings.Count == 0))
                                        {
                                            existingSlot.IsDeleted = false;
                                            existingSlot.IsApproved = true;
                                            existingSlot.PublishedAt = DateTime.UtcNow;
                                            existingSlot.ModifiedDate = DateTime.UtcNow;
                                            unitOfWork.MentorAvailabilities.Update(existingSlot);
                                            createdCount++;

                                            createdSlots.Add(new PublishedSlotItemResponse
                                            {
                                                AvailabilityId = existingSlot.Id,
                                                LocalDate = s.LocalDate,
                                                DaypartCode = s.Daypart.ToString(),
                                                StartTimeUtc = existingSlot.StartTime,
                                                EndTimeUtc = existingSlot.EndTime,
                                                LocalStart = TimeZoneInfo.ConvertTime(s.StartUtc, tz).ToString("HH:mm"),
                                                LocalEnd = TimeZoneInfo.ConvertTime(s.EndUtc, tz).ToString("HH:mm"),
                                                GenerationKey = s.GenerationKey,
                                                IsApproved = existingSlot.IsApproved
                                            });
                                            continue;
                                        }

                                        // Nếu đã từng có booking trong lịch sử, không ghi đè
                                        skippedCount++;
                                        continue;
                                    }

                                    // 2. Kiểm tra xung đột với các slot khác (khác GenerationKey) mà có booking
                                    var hasOverlapWithBooked = existingSlots.Any(x =>
                                        x.GenerationKey != s.GenerationKey &&
                                        x.BookedCount > 0 &&
                                        x.StartTime < s.EndUtc.UtcDateTime &&
                                        s.StartUtc.UtcDateTime < x.EndTime);

                                    if (hasOverlapWithBooked)
                                    {
                                        logger.LogWarning("Bỏ qua sinh slot {GenKey} vì xung đột với buổi hẹn lịch sử đã có booking.", s.GenerationKey);
                                        skippedCount++;
                                        continue;
                                    }

                                    // Tìm quy tắc tương ứng
                                    var matchedRule = activeRulesForDay.FirstOrDefault(r =>
                                        r.DaypartCode.Equals(s.Daypart.ToString(), StringComparison.OrdinalIgnoreCase));

                                    var newAvailability = new MentorAvailability
                                    {
                                        MentorId = claim.Id,
                                        MentorScheduleRuleId = matchedRule?.Id,
                                        GenerationKey = s.GenerationKey,
                                        PolicyVersion = DefaultPolicyVersion,
                                        LocalBusinessDate = s.LocalDate,
                                        StartTime = s.StartUtc.UtcDateTime,
                                        EndTime = s.EndUtc.UtcDateTime,
                                        RoomId = Guid.NewGuid(),
                                        MaxLearners = 1,
                                        BookedCount = 0,
                                        IsApproved = true,
                                        PublishedAt = DateTime.UtcNow,
                                        IsDeleted = false,
                                        CreatedDate = DateTime.UtcNow
                                    };

                                    await unitOfWork.MentorAvailabilities.AddAsync(newAvailability, token);
                                    createdCount++;

                                    createdSlots.Add(new PublishedSlotItemResponse
                                    {
                                        AvailabilityId = newAvailability.Id,
                                        LocalDate = s.LocalDate,
                                        DaypartCode = s.Daypart.ToString(),
                                        StartTimeUtc = newAvailability.StartTime,
                                        EndTimeUtc = newAvailability.EndTime,
                                        LocalStart = TimeZoneInfo.ConvertTime(s.StartUtc, tz).ToString("HH:mm"),
                                        LocalEnd = TimeZoneInfo.ConvertTime(s.EndUtc, tz).ToString("HH:mm"),
                                        GenerationKey = s.GenerationKey,
                                        IsApproved = newAvailability.IsApproved
                                    });
                                }
                            }
                        }

                        currentDate = currentDate.AddDays(1);
                    }

                    schedule.GeneratedThrough = request.EndDate;
                    unitOfWork.MentorSchedules.Update(schedule);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                var response = new PublishSlotsResponse
                {
                    CreatedCount = createdCount,
                    SkippedExistingCount = skippedCount,
                    TotalSlotsProcessed = createdCount + skippedCount,
                    GeneratedThrough = request.EndDate,
                    PublishedSlots = createdSlots
                };

                return ApiResponse<PublishSlotsResponse>.Success(
                    response,
                    message: $"Công bố thành công {createdCount} khung giờ làm việc mới ({skippedCount} khung giờ đã tồn tại trước đó được giữ nguyên).");
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Xảy ra xung đột dữ liệu (DbUpdateException) khi công bố khung giờ làm việc đồng thời.");
                return ApiResponse<PublishSlotsResponse>.Fail(
                    "Đã xảy ra xung đột khi công bố khung giờ làm việc do có thao tác đồng thời. Vui lòng làm mới danh sách.",
                    HttpStatusCode.Conflict);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi công bố khung giờ làm việc của chuyên gia.");
                return ApiResponse<PublishSlotsResponse>.Fail($"Không thể công bố khung giờ: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DisableSlotAsync(
            int slotId, DisableSlotRequest? request = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                var slot = await unitOfWork.MentorAvailabilities.Query()
                    .Include(x => x.MentorBookings)
                    .FirstOrDefaultAsync(x => x.Id == slotId && !x.IsDeleted, cancellationToken);

                if (slot is null)
                    return ApiResponse<bool>.Fail("Không tìm thấy khung giờ làm việc hoặc khung giờ đã bị xóa.", HttpStatusCode.NotFound);

                // Kiểm tra quyền sở hữu
                if (claim.Role != Role.Admin && slot.MentorId != claim.Id)
                    return ApiResponse<bool>.Fail("Bạn không có quyền can thiệp vào khung giờ làm việc của chuyên gia khác.", HttpStatusCode.Forbidden);

                // RÀNG BUỘC TOÀN VẸN: Không được xóa hoặc hủy slot đã có học viên đặt lịch
                if (slot.BookedCount > 0 || (slot.MentorBookings != null && slot.MentorBookings.Any(b => !b.IsDeleted && b.Status != MentorStatus.Cancelled)))
                {
                    return ApiResponse<bool>.Fail(
                        "Không thể hủy khung giờ đã có học viên đặt lịch. Vui lòng liên hệ Quản trị viên để được hỗ trợ điều phối lịch.",
                        HttpStatusCode.BadRequest);
                }

                // Không xóa khung giờ trong quá khứ
                if (slot.StartTime <= DateTime.UtcNow)
                {
                    return ApiResponse<bool>.Fail(
                        "Không thể hủy khung giờ đã bắt đầu hoặc nằm trong quá khứ.",
                        HttpStatusCode.BadRequest);
                }

                slot.IsDeleted = true;
                slot.CancelReason = request?.Reason?.Trim();
                slot.CancelRequestedAt = DateTime.UtcNow;
                slot.ModifiedDate = DateTime.UtcNow;

                unitOfWork.MentorAvailabilities.Update(slot);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<bool>.Success(true, message: "Hủy khung giờ làm việc thành công.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi hủy khung giờ {SlotId}.", slotId);
                return ApiResponse<bool>.Fail($"Không thể hủy khung giờ: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorScheduleResponse>> GetMentorScheduleByIdForAdminAsync(
            int mentorId, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Admin)
                    return ApiResponse<MentorScheduleResponse>.Fail("Chỉ Quản trị viên mới có quyền xem lịch làm việc của chuyên gia khác.", HttpStatusCode.Forbidden);

                var schedule = await unitOfWork.MentorSchedules.GetByMentorIdWithRulesAsync(mentorId, DefaultPolicyVersion, cancellationToken);
                if (schedule is null)
                    return ApiResponse<MentorScheduleResponse>.Fail("Chuyên gia chưa cấu hình mẫu lịch làm việc.", HttpStatusCode.NotFound);

                return ApiResponse<MentorScheduleResponse>.Success(MapToMentorScheduleResponse(schedule));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi Quản trị viên lấy lịch làm việc của mentor {MentorId}.", mentorId);
                return ApiResponse<MentorScheduleResponse>.Fail($"Không thể tải lịch làm việc: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        private static string? ValidateRules(IReadOnlyList<MentorScheduleRuleRequest>? rules)
        {
            if (rules is null || rules.Count == 0) return null;

            for (var i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r.Weekday is < 0 or > 6)
                    return $"Quy tắc #{i + 1}: Thứ trong tuần phải nằm trong khoảng từ 0 (Chủ nhật) đến 6 (Thứ bảy).";

                if (r.LocalStart >= r.LocalEnd)
                    return $"Quy tắc #{i + 1}: Giờ kết thúc ca làm việc ({r.LocalEnd:HH\\:mm}) phải sau giờ bắt đầu ({r.LocalStart:HH\\:mm}).";

                if (r.EffectiveTo.HasValue && r.EffectiveTo.Value < r.EffectiveFrom)
                    return $"Quy tắc #{i + 1}: Ngày kết thúc áp dụng ({r.EffectiveTo.Value:yyyy-MM-dd}) phải sau ngày bắt đầu ({r.EffectiveFrom:yyyy-MM-dd}).";
            }

            // Kiểm tra trùng lặp exact (Weekday, DaypartCode, EffectiveFrom)
            var duplicateKey = rules
                .GroupBy(r => new { r.Weekday, Daypart = r.DaypartCode.ToUpperInvariant(), r.EffectiveFrom })
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateKey is not null)
                return $"Trùng lặp quy tắc ca làm việc cho Thứ {duplicateKey.Key.Weekday}, ca {duplicateKey.Key.Daypart} từ ngày {duplicateKey.Key.EffectiveFrom:yyyy-MM-dd}.";

            // Kiểm tra trùng lặp khoảng thời gian trong cùng thứ
            var byWeekday = rules.GroupBy(r => r.Weekday);
            foreach (var group in byWeekday)
            {
                var sorted = group.OrderBy(x => x.LocalStart).ToList();
                for (var i = 1; i < sorted.Count; i++)
                {
                    if (sorted[i - 1].LocalEnd > sorted[i].LocalStart)
                    {
                        return $"Xung đột thời gian trong Thứ {group.Key}: Ca {sorted[i - 1].DaypartCode} ({sorted[i - 1].LocalStart:HH\\:mm}-{sorted[i - 1].LocalEnd:HH\\:mm}) giao nhau với ca {sorted[i].DaypartCode} ({sorted[i].LocalStart:HH\\:mm}-{sorted[i].LocalEnd:HH\\:mm}).";
                    }
                }
            }

            return null;
        }

        private static void SyncRules(MentorSchedule schedule, List<MentorScheduleRuleRequest> requestRules)
        {
            var existingRules = schedule.Rules.ToList();

            // Cập nhật hoặc thêm mới
            foreach (var req in requestRules)
            {
                var matched = existingRules.FirstOrDefault(r =>
                    r.Weekday == req.Weekday &&
                    r.DaypartCode.Equals(req.DaypartCode, StringComparison.OrdinalIgnoreCase) &&
                    r.EffectiveFrom == req.EffectiveFrom);

                if (matched is not null)
                {
                    matched.LocalStart = req.LocalStart;
                    matched.LocalEnd = req.LocalEnd;
                    matched.EffectiveTo = req.EffectiveTo;
                }
                else
                {
                    schedule.Rules.Add(new MentorScheduleRule
                    {
                        ScheduleId = schedule.Id,
                        Weekday = req.Weekday,
                        DaypartCode = req.DaypartCode,
                        LocalStart = req.LocalStart,
                        LocalEnd = req.LocalEnd,
                        EffectiveFrom = req.EffectiveFrom,
                        EffectiveTo = req.EffectiveTo
                    });
                }
            }

            // Xử lý các rule cũ không còn trong request
            foreach (var oldRule in existingRules)
            {
                var stillPresent = requestRules.Any(r =>
                    r.Weekday == oldRule.Weekday &&
                    r.DaypartCode.Equals(oldRule.DaypartCode, StringComparison.OrdinalIgnoreCase) &&
                    r.EffectiveFrom == oldRule.EffectiveFrom);

                if (!stillPresent)
                {
                    // Nếu rule đã sinh ra slot trong quá khứ, đặt EffectiveTo = Hôm qua thay vì xóa cứng
                    if (oldRule.Id > 0)
                    {
                        oldRule.EffectiveTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                    }
                    else
                    {
                        schedule.Rules.Remove(oldRule);
                    }
                }
            }
        }

        private static MentorScheduleResponse MapToMentorScheduleResponse(MentorSchedule s) => new()
        {
            ScheduleId = s.Id,
            MentorId = s.MentorId,
            MentorName = s.Mentor != null ? $"{s.Mentor.FirstName} {s.Mentor.LastName}".Trim() : string.Empty,
            PolicyVersion = s.PolicyVersion,
            TimeZoneId = s.TimeZoneId,
            AutoPublish = s.AutoPublish,
            EffectiveFrom = s.EffectiveFrom,
            GeneratedThrough = s.GeneratedThrough,
            CreatedAtUtc = s.CreatedAtUtc,
            Rules = s.Rules.Select(r => new MentorScheduleRuleResponse
            {
                Id = r.Id,
                ScheduleId = r.ScheduleId,
                Weekday = r.Weekday,
                WeekdayName = GetWeekdayName(r.Weekday),
                DaypartCode = r.DaypartCode,
                LocalStart = r.LocalStart.ToString("HH\\:mm"),
                LocalEnd = r.LocalEnd.ToString("HH\\:mm"),
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo
            }).OrderBy(r => r.Weekday).ThenBy(r => r.LocalStart).ToList()
        };

        private static string GetWeekdayName(int weekday) => weekday switch
        {
            0 => "Chủ nhật",
            1 => "Thứ hai",
            2 => "Thứ ba",
            3 => "Thứ tư",
            4 => "Thứ năm",
            5 => "Thứ sáu",
            6 => "Thứ bảy",
            _ => $"Thứ {weekday}"
        };
    }
}
