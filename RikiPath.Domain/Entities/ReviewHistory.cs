using System;

using RikiPath.Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>History of individual review sessions for a ReviewCard — drives SM-2 recalculation.</summary>
    public class ReviewHistory : Base
    {
        public int Id { get; set; }

        public int ReviewCardId { get; set; }
        public ReviewCard ReviewCard { get; set; }

        public ReviewRating Rating { get; set; }
        public int Quality { get; set; }
        public DateTime ReviewedAt { get; set; }
    }
}
