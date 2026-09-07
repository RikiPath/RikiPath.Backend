using Domain.Enums;

namespace RikiPath.Domain.Entities
{
    public interface IReviewableContent
    {
        int Id { get; }
        int ContentAuthorId { get; }
        ContentStatus Status { get; set; }
        string? ReviewNote { get; set; }
        DateTime? ReviewedDate { get; set; }
        int? ReviewedById { get; set; }
    }
}
