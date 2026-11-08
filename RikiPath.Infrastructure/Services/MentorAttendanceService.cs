using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorAttendanceService : IMentorAttendanceService
{
    private readonly AppDbContext _context;
    private readonly IClaimService _claimService;
    private readonly ILogger<MentorAttendanceService> _logger;

    // Quản lý kết nối thời gian thực theo phiên bộ nhớ (hỗ trợ đa thiết bị / đa tab cho cùng 1 người dùng)
    private sealed record ConnectionInfo(string ConnectionId, Guid RoomId, int UserId, string Role, string Name);

    private static readonly ConcurrentDictionary<string, ConnectionInfo> Connections = new();
    private static readonly ConcurrentDictionary<(Guid RoomId, int UserId), ConcurrentDictionary<string, byte>> UserConnections = new();
    private static readonly ConcurrentDictionary<Guid, int> RoomMentors = new();

    public MentorAttendanceService(
        AppDbContext context,
        IClaimService claimService,
        ILogger<MentorAttendanceService> logger)
    {
        _context = context;
        _claimService = claimService;
        _logger = logger;
    }

    public async Task<bool> CanAccessRoomAsync(Guid roomId, int userId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || userId <= 0) return false;

        var nowUtc = DateTime.UtcNow;

        var slot = await _context.MentorAvailabilities
            .Include(a => a.Mentor)
            .Include(a => a.MentorBookings)
                .ThenInclude(b => b.UserSubscription)
            .FirstOrDefaultAsync(a => a.RoomId == roomId && !a.IsDeleted && a.IsApproved && a.RejectedAt == null, cancellationToken);

        if (slot is null) return false;

        // Cho phép vào phòng từ 15 phút trước giờ bắt đầu đến 30 phút sau giờ kết thúc
        if (nowUtc < slot.StartTime.AddMinutes(-15) || nowUtc > slot.EndTime.AddMinutes(30))
            return false;

        // Nếu là Mentor phụ trách
        if (slot.MentorId == userId)
        {
            RoomMentors[roomId] = userId;
            return true;
        }

        // Nếu là Learner có booking hợp lệ đã thanh toán và chưa bị hủy
        var hasValidBooking = slot.MentorBookings != null && slot.MentorBookings.Any(b =>
            !b.IsDeleted
            && b.UserSubscription.UserId == userId
            && b.UserSubscription.PaymentStatus == PaymentStatus.Paid
            && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted));

        if (hasValidBooking)
            return true;

        // Quản trị viên (Admin) luôn có quyền giám sát
        var claim = _claimService.GetUserClaim();
        return claim.Role == Role.Admin;
    }

    public async Task<ApiResponse<MentorAttendanceResponse>> RecordJoinAsync(
        Guid roomId, int userId, string connectionId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || userId <= 0 || string.IsNullOrWhiteSpace(connectionId))
            return ApiResponse<MentorAttendanceResponse>.Fail("Thông tin kết nối phòng họp không hợp lệ.", HttpStatusCode.BadRequest);

        try
        {
            var nowUtc = DateTime.UtcNow;

            // Tìm khung giờ và thông tin người dùng
            var slot = await _context.MentorAvailabilities
                .Include(a => a.Mentor)
                .Include(a => a.MentorBookings)
                    .ThenInclude(b => b.UserSubscription)
                        .ThenInclude(s => s.UserAccount)
                .FirstOrDefaultAsync(a => a.RoomId == roomId && !a.IsDeleted && a.IsApproved && a.RejectedAt == null, cancellationToken);

            if (slot is null)
                return ApiResponse<MentorAttendanceResponse>.Fail("Không tìm thấy phòng họp hợp lệ.", HttpStatusCode.NotFound);

            var isMentor = slot.MentorId == userId;
            MentorBooking? booking = null;
            string participantName = string.Empty;
            string role = isMentor ? "Mentor" : "Learner";

            if (isMentor)
            {
                participantName = slot.Mentor != null ? $"{slot.Mentor.FirstName} {slot.Mentor.LastName}".Trim() : "Mentor";
                RoomMentors[roomId] = userId;
                booking = slot.MentorBookings?.FirstOrDefault(b => !b.IsDeleted && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted));
            }
            else
            {
                booking = slot.MentorBookings?.FirstOrDefault(b =>
                    !b.IsDeleted
                    && b.UserSubscription.UserId == userId
                    && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted));

                if (booking is null)
                    return ApiResponse<MentorAttendanceResponse>.Fail("Bạn không có lịch hẹn hợp lệ cho phòng họp này.", HttpStatusCode.Forbidden);

                var learnerAccount = booking.UserSubscription?.UserAccount;
                participantName = learnerAccount != null ? $"{learnerAccount.FirstName} {learnerAccount.LastName}".Trim() : $"User #{userId}";
            }

            if (booking is null)
                return ApiResponse<MentorAttendanceResponse>.Fail("Chưa có lịch hẹn nào được gắn với khung giờ này.", HttpStatusCode.Conflict);

            // 1. Quản lý trạng thái hiện diện đa thiết bị trong bộ nhớ
            var userConnMap = UserConnections.GetOrAdd((roomId, userId), _ => new ConcurrentDictionary<string, byte>());
            var isFirstConnection = userConnMap.IsEmpty;
            userConnMap[connectionId] = 1;

            Connections[connectionId] = new ConnectionInfo(connectionId, roomId, userId, role, participantName);

            // 2. Cập nhật hoặc khởi tạo bản ghi điểm danh trong Database (bảo đảm bền vững)
            var record = await _context.MentorAttendanceRecords
                .FirstOrDefaultAsync(r => r.MentorBookingId == booking.Id && r.UserId == userId, cancellationToken);

            if (record is null)
            {
                record = new MentorAttendanceRecord
                {
                    MentorBookingId = booking.Id,
                    RoomId = roomId,
                    UserId = userId,
                    Role = role,
                    Status = MentorAttendanceStatus.Joined,
                    FirstJoinedAtUtc = nowUtc,
                    LastJoinedAtUtc = nowUtc,
                    LastSeenAtUtc = nowUtc,
                    LeftAtUtc = null,
                    ReconnectionCount = 0,
                    TotalDurationSeconds = 0,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                };
                _context.MentorAttendanceRecords.Add(record);
            }
            else
            {
                if (record.Status == MentorAttendanceStatus.TemporarilyDisconnected || record.Status == MentorAttendanceStatus.Left)
                {
                    // Đánh dấu là đã kết nối lại (Reconnected)
                    record.ReconnectionCount++;
                }

                record.Status = MentorAttendanceStatus.Joined;
                record.LastJoinedAtUtc = nowUtc;
                record.LastSeenAtUtc = nowUtc;
                record.LeftAtUtc = null;
                record.UpdatedAtUtc = nowUtc;
                _context.MentorAttendanceRecords.Update(record);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var response = MapAttendanceResponse(record, participantName, true);
            return ApiResponse<MentorAttendanceResponse>.Success(response, HttpStatusCode.OK, "Ghi nhận tham gia thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi nhận tham gia phòng {RoomId} cho user {UserId}: {Message}", roomId, userId, ex.Message);
            return ApiResponse<MentorAttendanceResponse>.Fail("Không thể ghi nhận tham gia phòng họp.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorAttendanceResponse>> RecordLeaveAsync(
        Guid roomId, int userId, string connectionId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || userId <= 0)
            return ApiResponse<MentorAttendanceResponse>.Fail("Thông tin phòng hoặc người dùng không hợp lệ.", HttpStatusCode.BadRequest);

        try
        {
            var nowUtc = DateTime.UtcNow;

            // Xóa connection khỏi bộ nhớ
            Connections.TryRemove(connectionId, out _);

            if (UserConnections.TryGetValue((roomId, userId), out var userConns))
            {
                userConns.TryRemove(connectionId, out _);
            }

            // Tìm bản ghi điểm danh tương ứng trong DB
            var record = await _context.MentorAttendanceRecords
                .Include(r => r.UserAccount)
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.UserId == userId, cancellationToken);

            if (record is null)
                return ApiResponse<MentorAttendanceResponse>.Fail("Không tìm thấy bản ghi điểm danh.", HttpStatusCode.NotFound);

            var isStillOnline = IsUserOnlineInRoom(roomId, userId);

            if (!isStillOnline)
            {
                // Nếu người dùng không còn kết nối nào khác trong phòng, tính toán thời lượng và chuyển trạng thái Left
                if (record.LastJoinedAtUtc.HasValue)
                {
                    var elapsed = (int)(nowUtc - record.LastJoinedAtUtc.Value).TotalSeconds;
                    if (elapsed > 0) record.TotalDurationSeconds += elapsed;
                }

                record.Status = MentorAttendanceStatus.Left;
                record.LeftAtUtc = nowUtc;
                record.LastSeenAtUtc = nowUtc;
                record.UpdatedAtUtc = nowUtc;
                _context.MentorAttendanceRecords.Update(record);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var name = record.UserAccount != null ? $"{record.UserAccount.FirstName} {record.UserAccount.LastName}".Trim() : $"User #{userId}";
            return ApiResponse<MentorAttendanceResponse>.Success(MapAttendanceResponse(record, name, isStillOnline), HttpStatusCode.OK, "Rời phòng họp thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi nhận rời phòng {RoomId} cho user {UserId}: {Message}", roomId, userId, ex.Message);
            return ApiResponse<MentorAttendanceResponse>.Fail("Không thể ghi nhận rời phòng họp.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<MentorAttendanceResponse?>> RecordDisconnectAsync(
        string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return ApiResponse<MentorAttendanceResponse?>.Success(null);

        if (!Connections.TryRemove(connectionId, out var conn))
            return ApiResponse<MentorAttendanceResponse?>.Success(null);

        try
        {
            var nowUtc = DateTime.UtcNow;

            if (UserConnections.TryGetValue((conn.RoomId, conn.UserId), out var userConns))
            {
                userConns.TryRemove(connectionId, out _);
            }

            var isStillOnline = IsUserOnlineInRoom(conn.RoomId, conn.UserId);

            var record = await _context.MentorAttendanceRecords
                .Include(r => r.UserAccount)
                .FirstOrDefaultAsync(r => r.RoomId == conn.RoomId && r.UserId == conn.UserId, cancellationToken);

            if (record is null)
                return ApiResponse<MentorAttendanceResponse?>.Success(null);

            if (!isStillOnline)
            {
                // Khi toàn bộ kết nối của người dùng bị ngắt, đánh dấu tạm thời mất kết nối (TemporarilyDisconnected)
                if (record.LastJoinedAtUtc.HasValue)
                {
                    var elapsed = (int)(nowUtc - record.LastJoinedAtUtc.Value).TotalSeconds;
                    if (elapsed > 0) record.TotalDurationSeconds += elapsed;
                }

                record.Status = MentorAttendanceStatus.TemporarilyDisconnected;
                record.LastSeenAtUtc = nowUtc;
                record.UpdatedAtUtc = nowUtc;
                _context.MentorAttendanceRecords.Update(record);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var name = record.UserAccount != null ? $"{record.UserAccount.FirstName} {record.UserAccount.LastName}".Trim() : conn.Name;
            return ApiResponse<MentorAttendanceResponse?>.Success(MapAttendanceResponse(record, name, isStillOnline));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi nhận disconnect connection {ConnectionId}: {Message}", connectionId, ex.Message);
            return ApiResponse<MentorAttendanceResponse?>.Success(null);
        }
    }

    public async Task<ApiResponse<RoomAccessCheckResponse>> CheckRoomAccessAsync(
        Guid roomId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty)
            return ApiResponse<RoomAccessCheckResponse>.Fail("Mã phòng không hợp lệ.", HttpStatusCode.BadRequest);

        var claim = _claimService.GetUserClaim();
        if (claim.Id <= 0)
            return ApiResponse<RoomAccessCheckResponse>.Fail("Yêu cầu xác thực tài khoản.", HttpStatusCode.Unauthorized);

        var canAccess = await CanAccessRoomAsync(roomId, claim.Id, cancellationToken);
        var isReady = IsRoomReady(roomId);

        var response = new RoomAccessCheckResponse
        {
            RoomId = roomId,
            CanAccess = canAccess,
            IsRoomReady = isReady,
            Reason = canAccess ? null : "Bạn không có quyền tham gia phòng tư vấn này hoặc thời gian chưa hợp lệ."
        };

        if (!canAccess)
        {
            return ApiResponse<RoomAccessCheckResponse>.Fail(response.Reason ?? "Truy cập bị từ chối.", HttpStatusCode.Forbidden);
        }

        return ApiResponse<RoomAccessCheckResponse>.Success(response, HttpStatusCode.OK, "Được phép tham gia phòng họp.");
    }

    public async Task<ApiResponse<List<MentorAttendanceResponse>>> GetRoomAttendanceAsync(
        Guid roomId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty)
            return ApiResponse<List<MentorAttendanceResponse>>.Fail("Mã phòng họp không hợp lệ.");

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
            {
                var canAccess = await CanAccessRoomAsync(roomId, claim.Id, cancellationToken);
                if (!canAccess)
                {
                    return ApiResponse<List<MentorAttendanceResponse>>.Fail(
                        "Bạn không có quyền xem thông tin điểm danh phòng họp này.", HttpStatusCode.Forbidden);
                }
            }

            var records = await _context.MentorAttendanceRecords
                .Include(r => r.UserAccount)
                .Where(r => r.RoomId == roomId)
                .OrderBy(r => r.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var result = records.Select(r =>
            {
                var name = r.UserAccount != null ? $"{r.UserAccount.FirstName} {r.UserAccount.LastName}".Trim() : $"User #{r.UserId}";
                var isOnline = IsUserOnlineInRoom(roomId, r.UserId);
                return MapAttendanceResponse(r, name, isOnline);
            }).ToList();

            return ApiResponse<List<MentorAttendanceResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách điểm danh phòng {RoomId}: {Message}", roomId, ex.Message);
            return ApiResponse<List<MentorAttendanceResponse>>.Fail("Không thể tải danh sách điểm danh.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<List<MentorAttendanceResponse>>> GetBookingAttendanceAsync(
        int bookingId, CancellationToken cancellationToken = default)
    {
        if (bookingId <= 0)
            return ApiResponse<List<MentorAttendanceResponse>>.Fail("Mã lịch hẹn phải lớn hơn 0.");

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
            {
                var booking = await _context.MentorBookings
                    .Include(b => b.UserSubscription)
                    .Include(b => b.MentorAvailability)
                    .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted, cancellationToken);

                if (booking is null)
                {
                    return ApiResponse<List<MentorAttendanceResponse>>.Fail(
                        "Không tìm thấy thông tin lịch hẹn tư vấn.", HttpStatusCode.NotFound);
                }

                var isOwnerLearner = booking.UserSubscription != null && booking.UserSubscription.UserId == claim.Id;
                var isAssignedMentor = booking.MentorAvailability != null && booking.MentorAvailability.MentorId == claim.Id;
                if (!isOwnerLearner && !isAssignedMentor)
                {
                    return ApiResponse<List<MentorAttendanceResponse>>.Fail(
                        "Bạn không có quyền truy cập lịch sử điểm danh của lịch hẹn này.", HttpStatusCode.Forbidden);
                }
            }

            var records = await _context.MentorAttendanceRecords
                .Include(r => r.UserAccount)
                .Where(r => r.MentorBookingId == bookingId)
                .OrderBy(r => r.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var result = records.Select(r =>
            {
                var name = r.UserAccount != null ? $"{r.UserAccount.FirstName} {r.UserAccount.LastName}".Trim() : $"User #{r.UserId}";
                var isOnline = IsUserOnlineInRoom(r.RoomId, r.UserId);
                return MapAttendanceResponse(r, name, isOnline);
            }).ToList();

            return ApiResponse<List<MentorAttendanceResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách điểm danh booking {BookingId}: {Message}", bookingId, ex.Message);
            return ApiResponse<List<MentorAttendanceResponse>>.Fail("Không thể tải danh sách điểm danh.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<RoomPresenceResponse>> GetRoomPresenceAsync(
        Guid roomId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty)
            return ApiResponse<RoomPresenceResponse>.Fail("Mã phòng không hợp lệ.");

        try
        {
            var claim = _claimService.GetUserClaim();
            if (claim.Role != Role.Admin)
            {
                var canAccess = await CanAccessRoomAsync(roomId, claim.Id, cancellationToken);
                if (!canAccess)
                {
                    return ApiResponse<RoomPresenceResponse>.Fail(
                        "Bạn không có quyền theo dõi hiện diện phòng họp này.", HttpStatusCode.Forbidden);
                }
            }

            var records = await _context.MentorAttendanceRecords
                .Include(r => r.UserAccount)
                .Where(r => r.RoomId == roomId)
                .ToListAsync(cancellationToken);

            var participants = records.Select(r =>
            {
                var name = r.UserAccount != null ? $"{r.UserAccount.FirstName} {r.UserAccount.LastName}".Trim() : $"User #{r.UserId}";
                var connCount = UserConnections.TryGetValue((roomId, r.UserId), out var map) ? map.Count : 0;
                return new ParticipantPresenceResponse
                {
                    UserId = r.UserId,
                    Name = name,
                    Role = r.Role,
                    IsOnline = connCount > 0,
                    ActiveConnections = connCount,
                    LastSeenAtUtc = r.LastSeenAtUtc ?? r.UpdatedAtUtc
                };
            }).ToList();

            var response = new RoomPresenceResponse
            {
                RoomId = roomId,
                TotalOnlineParticipants = participants.Count(p => p.IsOnline),
                Participants = participants
            };

            return ApiResponse<RoomPresenceResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thông tin hiện diện phòng {RoomId}: {Message}", roomId, ex.Message);
            return ApiResponse<RoomPresenceResponse>.Fail("Không thể tải thông tin hiện diện.", HttpStatusCode.InternalServerError);
        }
    }

    public bool IsUserOnlineInRoom(Guid roomId, int userId)
    {
        return UserConnections.TryGetValue((roomId, userId), out var conns) && !conns.IsEmpty;
    }

    public bool IsRoomReady(Guid roomId)
    {
        if (!RoomMentors.TryGetValue(roomId, out var mentorId))
            return false;

        var isMentorOnline = IsUserOnlineInRoom(roomId, mentorId);
        if (!isMentorOnline) return false;

        // Kiểm tra có ít nhất 1 Learner trực tuyến
        return UserConnections.Any(kvp => kvp.Key.RoomId == roomId && kvp.Key.UserId != mentorId && !kvp.Value.IsEmpty);
    }

    private static MentorAttendanceResponse MapAttendanceResponse(MentorAttendanceRecord r, string name, bool isOnline)
    {
        return new MentorAttendanceResponse
        {
            Id = r.Id,
            MentorBookingId = r.MentorBookingId,
            RoomId = r.RoomId,
            UserId = r.UserId,
            ParticipantName = name,
            Role = r.Role,
            Status = r.Status,
            FirstJoinedAtUtc = r.FirstJoinedAtUtc,
            LastJoinedAtUtc = r.LastJoinedAtUtc,
            LastSeenAtUtc = r.LastSeenAtUtc,
            LeftAtUtc = r.LeftAtUtc,
            ReconnectionCount = r.ReconnectionCount,
            TotalDurationSeconds = r.TotalDurationSeconds,
            IsOnline = isOnline
        };
    }
}
