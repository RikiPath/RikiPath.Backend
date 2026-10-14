namespace RikiPath.Application.Responses.Content
{
    public sealed class ContentApprovalDetailResponse : ContentApprovalListItemResponse
    {
        /// <summary>true nếu yêu cầu "cập nhật" nhưng bản ghi gốc không còn trong DB.</summary>
        public bool TargetMissing { get; set; }
        /// <summary>Các trường (kể cả không đổi) để Admin xem cạnh nhau: cũ -> mới.</summary>
        public List<ApprovalFieldDiffResponse> Fields { get; set; } = new();
        /// <summary>Cảnh báo cho Admin: dữ liệu gốc đã đổi từ lúc gửi, thêm mới nhưng đã tồn tại, payload lỗi...</summary>
        public List<string> Warnings { get; set; } = new();
    }
}
