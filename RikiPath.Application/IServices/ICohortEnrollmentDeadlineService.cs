//namespace RikiPath.Application.IServices;

//public sealed record CohortDeadlineProcessResult(
//    bool Processed,
//    string Outcome,
//    string? CancellationReason = null,
//    int ConfirmedCount = 0);

///// <summary>
///// Xử lý đóng tuyển sinh khi đến hạn (đồng bộ giữa Background Worker và kiểm tra trực tiếp trước khi vào lớp / thanh toán):
///// - Có từ 1 học viên ghi danh hợp lệ trở lên: đóng tuyển sinh, giữ nguyên lịch chính thức để chuẩn bị/bắt đầu giảng dạy.
///// - Không có học viên ghi danh hợp lệ (sau khi đối soát các giữ chỗ hết hạn): tự động hủy lớp, giải phóng toàn bộ khung giờ dạy của Giáo viên, và gửi thông báo idempotent.
///// </summary>
//public interface ICohortEnrollmentDeadlineService
//{
//    Task<CohortDeadlineProcessResult> ProcessCohortIfPastCutoffAsync(int cohortId, CancellationToken ct = default);
//    Task<int> ProcessAllPastCutoffCohortsAsync(CancellationToken ct = default);
//}
