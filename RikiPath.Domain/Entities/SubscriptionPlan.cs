namespace RikiPath.Domain.Entities
{
    public class SubscriptionPlan : Base
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int DurationDays { get; set; }
        public decimal Price { get; set; }

        /// <summary>Số lượt chấm AI Writing &amp; Speaking đi kèm gói, trong suốt DurationDays.</summary>
        public int AiGradingQuota { get; set; }

        /// <summary>Number of mentor meeting sessions included. Zero means this is not a meeting package.</summary>
        public int MeetingSessionCount { get; set; }

        /// <summary>Cấp độ JLPT (N5-N2) gắn với gói tư vấn 1-1; null đối với gói tự học hoặc gói lịch sử.</summary>
        public int? CertificateLevelId { get; set; }
        public CertificateLevel? CertificateLevel { get; set; }

        /// <summary>Phân loại gói học để tránh phụ thuộc ID cứng: self_study, speaking, writing, ai_grading, learning_path, mentor_legacy.</summary>
        public string PlanCategory { get; set; } = "self_study";

        /// <summary>Đánh dấu gói "phổ biến nhất" để FE highlight — hiện tại là gói 1 tháng.</summary>
        public bool IsPopular { get; set; } = false;
        public bool IsTrial { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public int Revision { get; set; } = 1;

        public List<UserSubscription>? UserSubscriptions { get; set; }
        public List<Feature> Features { get; set; } = [];
    }
}
