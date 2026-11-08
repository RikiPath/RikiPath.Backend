namespace RikiPath.Domain.Enums
{
    // // Quyết định của Quản trị viên khi xét duyệt hồ sơ Chuyên gia.
    public enum MentorVerificationDecision
    {
        // Phê duyệt hồ sơ chuyên gia.
        Approve = 1,

        // Từ chối hồ sơ kèm lý do bắt buộc.
        Reject = 2,

        // Yêu cầu chuyên gia bổ sung hoặc chỉnh sửa thông tin.
        RequestChanges = 3
    }
}
