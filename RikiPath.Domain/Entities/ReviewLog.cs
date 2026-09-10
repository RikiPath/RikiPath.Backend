using System;

using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    /// <summary>History of individual review sessions for a ReviewItem — drives SM-2 recalculation.</summary>
    public class ReviewLog : Base
    {
        public int Id { get; set; }

        public int ReviewItemId { get; set; }
        public ReviewItem ReviewItem { get; set; }

        public ReviewRating Rating { get; set; }
        public int Quality { get; set; }
        public DateTime ReviewedAt { get; set; }
    }
}
