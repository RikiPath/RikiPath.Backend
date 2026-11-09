using RikiPath.Domain.Enums;

namespace RikiPath.Application.Responses.Practice
{
    /// <summary>Điểm + nhận xét cho MỘT tiêu chí chấm (vd: Ngữ pháp, Từ vựng, Nội dung...).</summary>
    public sealed class GradingCriterionResponse
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Thang điểm 0-10 cho tiêu chí này.</summary>
        public double Score { get; set; }

        public string Comment { get; set; } = string.Empty;
    }

    /// <summary>Một lỗi cụ thể AI tìm thấy trong bài, kèm câu gốc + đề xuất sửa.</summary>
    public sealed class EssayCorrectionResponse
    {
        public string Original { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class GradingResultResponse
    {
        /// <summary>Thang điểm 0-100, quy đổi từ các tiêu chí.</summary>
        public double OverallScore { get; set; }

        public List<GradingCriterionResponse> Criteria { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Improvements { get; set; } = new();
        public List<EssayCorrectionResponse> Corrections { get; set; } = new();
        public DateTime GradedAt { get; set; }
    }

    public sealed class SubmitPracticeResponse
    {
        public int SubmissionId { get; set; }
        public SubmissionType Type { get; set; }
        public DateTime SubmittedAt { get; set; }

        /// <summary>null nếu loại bài này chưa hỗ trợ chấm tự động, hoặc AI chấm thất bại.</summary>
        public GradingResultResponse? Grading { get; set; }

        /// <summary>Lý do chưa có Grading (vd: "Loại bài này chưa hỗ trợ chấm tự động." hoặc lỗi AI tạm thời).
        /// Bài vẫn được lưu — học viên có thể gọi Regrade để chấm lại sau.</summary>
        public string? GradingNote { get; set; }
    }

    public sealed class PracticeSubmissionDetailResponse
    {
        public int Id { get; set; }
        public SubmissionType Type { get; set; }
        public string? TextContent { get; set; }
        public string? ImageUrl { get; set; }
        public string? AudioUrl { get; set; }
        public string CertificateLevelCode { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public GradingResultResponse? Grading { get; set; }
    }

    public sealed class PracticeSubmissionSummaryResponse
    {
        public int Id { get; set; }
        public SubmissionType Type { get; set; }
        public string CertificateLevelCode { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public bool IsGraded { get; set; }
        public double? OverallScore { get; set; }
    }
}