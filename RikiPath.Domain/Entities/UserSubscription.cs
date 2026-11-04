using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public class UserSubscription : Base
    {
        public int Id { get; set; }
        public uint Version { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; }
        public int SubscriptionPlanId { get; set; }
        public SubscriptionPlan SubscriptionPlan { get; set; }
        public int? MentorId { get; set; }
        public UserAccount? Mentor { get; set; }

        /// <summary>Cấp độ JLPT (N5-N2) được chốt tại thời điểm mua gói tư vấn; null đối với gói tự học hoặc gói lịch sử.</summary>
        public int? CertificateLevelId { get; set; }
        public CertificateLevel? CertificateLevel { get; set; }

        public decimal AmountPaid { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public string? PaymentTransactionId { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int AiGradingQuota { get; set; }
        public int AiGradingUsedCount { get; set; } = 0;

        public int MeetingSessionsIncluded { get; set; }
        public int MeetingSessionsUsed { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }

        public DateTime PurchasedAt { get; set; }

        public List<MentorBooking> MentorBookings { get; set; } = [];
    }
}
