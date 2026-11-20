using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Feedback;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Services;

public sealed class LearnerFeedbackService(
    AppDbContext db, IClaimService claims)
{
    public sealed record Entry
    {
        public int Id { get; init; }
        public int LearnerId { get; init; }
        public required string LearnerName { get; init; }
        public required string TargetType { get; init; }
        public int TargetId { get; init; }
        public required string TargetTitle { get; init; }
        public int Rating { get; init; }
        public string? Comment { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }

        public Entry WithUtcDates() => this with
        {
            CreatedAtUtc = BusinessDateHelper.EnsureUtc(CreatedAtUtc),
            UpdatedAtUtc = BusinessDateHelper.EnsureUtc(UpdatedAtUtc)
        };
    }

    public sealed record Page(List<Entry> Items, bool HasMore);

    private const int PageSize = 20;

    private static bool ValidTarget(string type, int id) => type switch
    {
        "general" or "speaking" or "writing" => id == 0,
        "lesson" or "exercise" or "mock-test" or "placement" or "homework" or "vocabulary" or "kanji" or "grammar" => id > 0,
        _ => false
    };

    private IQueryable<Entry> ContentEntries() => db.LearnerFeedbacks.AsNoTracking().Select(x => new Entry
    {
        Id = x.Id,
        LearnerId = x.LearnerId,
        LearnerName = (x.Learner.FirstName + " " + x.Learner.LastName).Trim(),
        TargetType = x.TargetType,
        TargetId = x.TargetId,
        TargetTitle = x.TargetTitle,
        Rating = x.Rating,
        Comment = x.Comment,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    });

    public async Task<ApiResponse<Page>> ListAsync(bool forAdmin, string? type, int page, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != (forAdmin ? Role.Admin : Role.Learner))
            return ApiResponse<Page>.Fail("Bạn không có quyền xem phản hồi.", HttpStatusCode.Forbidden);

        if (page is < 1 or > 100_000 || (type is not null && !ValidTarget(type, 0) && !ValidTarget(type, 1)))
            return ApiResponse<Page>.Fail("Bộ lọc phản hồi không hợp lệ.");

        var content = ContentEntries();
        if (!forAdmin)
        {
            content = content.Where(x => x.LearnerId == actor.Id);
        }

        var entries = type switch
        {
            null => content,
            _ => content.Where(x => x.TargetType == type)
        };
        var items = await entries.OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id).ThenBy(x => x.TargetType)
            .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(ct);

        var hasMore = items.Count > PageSize;
        if (hasMore) items.RemoveAt(PageSize);
        return ApiResponse<Page>.Success(new Page(items.Select(x => x.WithUtcDates()).ToList(), hasMore));
    }

    public async Task<ApiResponse<Entry?>> GetMineAsync(string type, int id, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Learner)
            return ApiResponse<Entry?>.Fail("Bạn không có quyền xem phản hồi.", HttpStatusCode.Forbidden);
        if (!ValidTarget(type, id))
            return ApiResponse<Entry?>.Fail("Nội dung phản hồi không hợp lệ.");

        var entries = ContentEntries();
        var entry = await entries.SingleOrDefaultAsync(x =>
            x.LearnerId == actor.Id && x.TargetType == type && x.TargetId == id, ct);
        return ApiResponse<Entry?>.Success(entry?.WithUtcDates());
    }

    public async Task<ApiResponse<Entry?>> SaveMineAsync(
        string type, int id, SaveLearnerFeedbackRequest request, CancellationToken ct)
    {
        var current = await GetMineAsync(type, id, ct);
        if (!current.IsSuccess) return current;
        if (request.Rating is < 1 or > 5 || request.Comment?.Length > 1000)
            return ApiResponse<Entry?>.Fail("Chọn từ 1 đến 5 sao và nhập tối đa 1000 ký tự.");

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var learnerId = claims.GetUserClaim().Id;
        var title = current.Result?.TargetTitle ?? await ResolveTitleAsync(type, id, learnerId, ct);
        if (title is null) return ApiResponse<Entry?>.NotFound("Không tìm thấy nội dung để phản hồi.");

        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LearnerFeedback" ("LearnerId", "TargetType", "TargetId", "TargetTitle",
                "Rating", "Comment", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({learnerId}, {type}, {id}, {title}, {request.Rating}, {comment}, {now}, {now})
            ON CONFLICT ("LearnerId", "TargetType", "TargetId") DO UPDATE
            SET "Rating" = EXCLUDED."Rating", "Comment" = EXCLUDED."Comment", "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";
            """, ct);
        return await GetMineAsync(type, id, ct);
    }

    private async Task<string?> ResolveTitleAsync(string type, int id, int learnerId, CancellationToken ct)
    {
        switch (type)
        {
            case "general": return "Hệ thống RikiPath";
            case "speaking": return "Luyện nói";
            case "writing": return "Luyện viết";
            case "lesson":
                return await db.Lessons.Where(x => x.Id == id && !x.IsDeleted).Select(x => x.Title).SingleOrDefaultAsync(ct);
            case "exercise":
                return await db.PracticeExercises.Where(x => x.Id == id && !x.IsDeleted && !x.Lesson.IsDeleted &&
                        x.Attempts!.Any(a => a.UserId == learnerId && !a.IsDeleted && a.IsCompleted))
                    .Select(x => x.Title).SingleOrDefaultAsync(ct);
            case "homework":
                return await db.HomeworkAssignments.Where(x => x.Id == id && x.LearnerId == learnerId && !x.IsDeleted)
                    .Select(x => x.Title).SingleOrDefaultAsync(ct);
            case "vocabulary":
                return await db.Vocabularies.Where(x => x.Id == id && !x.IsDeleted).Select(x => x.Word).SingleOrDefaultAsync(ct);
            case "kanji":
                return await db.Kanjis.Where(x => x.Id == id && !x.IsDeleted).Select(x => x.Character).SingleOrDefaultAsync(ct);
            case "grammar":
                return await db.GrammarPatterns.Where(x => x.Id == id && !x.IsDeleted).Select(x => x.Title).SingleOrDefaultAsync(ct);
            case "mock-test":
            case "placement":
                var attempt = await db.MockTestAttempts.Where(x => x.Id == id && x.UserId == learnerId && !x.IsDeleted
                        && x.IsCompleted && (type == "placement"
                            ? x.AttemptKind == AttemptKind.Placement || x.IsPlacementAssessment
                            : x.AttemptKind == AttemptKind.MockTest && !x.IsPlacementAssessment))
                    .Select(x => new
                    {
                        Title = x.SectionResults!.OrderBy(r => r.Id)
                        .Select(r => r.MockTestSection.MockTest.Title).FirstOrDefault()
                    }).SingleOrDefaultAsync(ct);
                if (attempt is null) return null;
                return type == "placement" ? $"Bài đánh giá năng lực · Lượt #{id}" : attempt.Title ?? $"Đề thi thử · Lượt #{id}";
            default: return null;
        }
    }
}
