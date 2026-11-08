using RikiPath.Domain.Enums;

namespace RikiPath.Application.Requests.Practice
{
    public sealed class SubmitPracticeRequest
    {
        public SubmissionType Type { get; set; } = SubmissionType.Writing;

        /// <summary>Bắt buộc khi Type = Writing (nội dung bài luận học viên đã viết).</summary>
        public string? TextContent { get; set; }

        /// <summary>Bắt buộc khi Type = Kanji (ảnh chữ viết tay/typed).</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Bắt buộc khi Type = Speaking (file ghi âm).</summary>
        public string? AudioUrl { get; set; }

        /// <summary>Cấp độ JLPT dùng làm rubric chấm (bắt buộc).</summary>
        public int CertificateLevelId { get; set; }

        /// <summary>Tùy chọn: đề bài/chủ đề học viên đã viết, giúp AI chấm đúng ngữ cảnh hơn
        /// (vd: "Hãy viết về kỳ nghỉ hè của bạn"). Không có cũng chấm được, chỉ là kém ngữ cảnh hơn.</summary>
        public string? Topic { get; set; }
    }
}