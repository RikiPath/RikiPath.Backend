namespace RikiPath.Domain.Enums;

// Vòng đời trạng thái xử lý khiếu nại và tranh chấp buổi tư vấn Mentor 1-1
public enum MentorDisputeStatus
{
    Submitted = 1,
    UnderReview = 2,
    AwaitingResponse = 3,
    Resolved = 4,
    Rejected = 5,
    Closed = 6
}
