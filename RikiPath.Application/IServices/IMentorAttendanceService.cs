using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorAttendanceService
{
    // Ghi nhận hoặc khôi phục sự hiện diện khi người tham gia kết nối vào phòng họp
    Task<ApiResponse<MentorAttendanceResponse>> RecordJoinAsync(
        Guid roomId, int userId, string connectionId, CancellationToken cancellationToken = default);

    // Ghi nhận khi người tham gia chủ động rời khỏi phòng họp
    Task<ApiResponse<MentorAttendanceResponse>> RecordLeaveAsync(
        Guid roomId, int userId, string connectionId, CancellationToken cancellationToken = default);

    // Ghi nhận khi kết nối SignalR bị ngắt (phát hiện ngắt tạm thời hoặc hoàn toàn)
    Task<ApiResponse<MentorAttendanceResponse?>> RecordDisconnectAsync(
        string connectionId, CancellationToken cancellationToken = default);

    // Lấy danh sách hồ sơ điểm danh bền vững của phòng họp
    Task<ApiResponse<List<MentorAttendanceResponse>>> GetRoomAttendanceAsync(
        Guid roomId, CancellationToken cancellationToken = default);

    // Lấy lịch sử điểm danh theo mã lịch hẹn tư vấn
    Task<ApiResponse<List<MentorAttendanceResponse>>> GetBookingAttendanceAsync(
        int bookingId, CancellationToken cancellationToken = default);

    // Lấy trạng thái hiện diện trực tuyến theo thời gian thực của phòng họp
    Task<ApiResponse<RoomPresenceResponse>> GetRoomPresenceAsync(
        Guid roomId, CancellationToken cancellationToken = default);

    // Kiểm tra quyền truy cập vào phòng họp của người dùng
    Task<bool> CanAccessRoomAsync(
        Guid roomId, int userId, CancellationToken cancellationToken = default);

    // Kiểm tra xem người dùng hiện có kết nối trực tuyến nào trong phòng hay không
    bool IsUserOnlineInRoom(Guid roomId, int userId);

    // Kiểm tra xem phòng họp đã đủ điều kiện bắt đầu (có cả Mentor và ít nhất 1 Learner trực tuyến)
    bool IsRoomReady(Guid roomId);

    // Kiểm tra quyền vào phòng họp và trạng thái sẵn sàng của phiên tư vấn
    Task<ApiResponse<RoomAccessCheckResponse>> CheckRoomAccessAsync(
        Guid roomId, CancellationToken cancellationToken = default);
}
