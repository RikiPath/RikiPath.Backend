using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Homework;
using RikiPath.Application.Responses.Lessons;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;

namespace RikiPath.Application.Services;

public sealed partial class HomeworkService(IUnitOfWork unit, IClaimService claims, ILessonProgressService lessons,
    IFileStorageService? storage = null, IAiEssayGradingClient? grader = null, IAiOcrClient? ocr = null)
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly string[] SupportedLevels = ["N5", "N4", "N3", "N2"];

    internal static IQueryable<MentorBooking> ScheduledBookings(IUnitOfWork unit) => unit.MentorBookings.Query()
        .Where(booking => !booking.IsDeleted && !booking.UserSubscription.IsDeleted &&
            booking.UserSubscription.PaymentStatus == PaymentStatus.Paid &&
            (booking.Status == MentorStatus.Assigned || booking.Status == MentorStatus.Accepted ||
             booking.Status == MentorStatus.Completed) &&
            booking.MentorAvailability != null && !booking.MentorAvailability.IsDeleted &&
            booking.MentorAvailability.IsApproved && booking.MentorAvailability.RejectedAt == null);

    public async Task<List<HomeworkLearner>> GetLearnersAsync(CancellationToken ct, bool includeAllBookings = false)
    {
        var mentorId = claims.GetUserClaim().Id;
        var bookings = await ScheduledBookings(unit)
            .Where(booking => booking.MentorAvailability!.MentorId == mentorId &&
                !booking.UserSubscription.UserAccount.IsDeleted)
            .OrderByDescending(booking => booking.MentorAvailability!.StartTime).ThenByDescending(booking => booking.Id)
            .Select(booking => new HomeworkLearner(booking.UserSubscription.UserId,
                (booking.UserSubscription.UserAccount.FirstName + " " + booking.UserSubscription.UserAccount.LastName).Trim(),
                booking.Id, booking.MentorAvailability!.StartTime))
            .ToListAsync(ct);
        return (includeAllBookings ? bookings : bookings.DistinctBy(booking => booking.LearnerId))
            .OrderBy(booking => booking.LearnerName).ToList();
    }

    public async Task<List<HomeworkCatalogItem>> GetCatalogAsync(string? level, string? search, int page, CancellationToken ct)
    {
        var query = VisibleLessons();
        if (!string.IsNullOrWhiteSpace(level))
        {
            var code = level.Trim().ToUpperInvariant();
            query = query.Where(lesson => lesson.CertificateLevel.Code == code);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(lesson => lesson.Title.ToLower().Contains(term));
        }

        var pageSize = 20;
        var items = await query.OrderBy(lesson => lesson.CertificateLevel.SortOrder)
            .ThenBy(lesson => lesson.SortOrder).ThenBy(lesson => lesson.Id)
            .Skip((Math.Clamp(page, 1, 10000) - 1) * pageSize).Take(pageSize)
            .Select(lesson => new HomeworkLesson(lesson.Id, lesson.Title, lesson.CertificateLevel.Code,
                lesson.LanguageSkill.Name)).ToListAsync(ct);
        var lessonIds = items.Select(lesson => lesson.Id).ToArray();
        var exercises = await UsableExercises().Where(exercise => lessonIds.Contains(exercise.LessonId))
            .OrderBy(exercise => exercise.SortOrder).ThenBy(exercise => exercise.Id)
            .Select(exercise => new { exercise.LessonId, Item = new HomeworkExercise(exercise.Id,
                exercise.Title, exercise.Description, exercise.Questions!.Count(question => !question.IsDeleted), null) })
            .ToListAsync(ct);
        var byLesson = exercises.ToLookup(exercise => exercise.LessonId, exercise => exercise.Item);
        return items.Select(lesson => new HomeworkCatalogItem(lesson, byLesson[lesson.Id].ToList())).ToList();
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateHomeworkRequest request, CancellationToken ct)
    {
        var mentorId = claims.GetUserClaim().Id;
        var booking = await ScheduledBookings(unit).Where(booking => booking.Id == request.BookingId &&
            booking.MentorAvailability!.MentorId == mentorId && !booking.UserSubscription.UserAccount.IsDeleted)
            .Select(booking => new { LearnerId = booking.UserSubscription.UserId }).SingleOrDefaultAsync(ct);
        if (booking is null)
            return ApiResponse<int>.NotFound("Không tìm thấy lịch ôn luyện hợp lệ với người học này.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResponse<int>.Fail("Hãy nhập tiêu đề bài tập.");
        if (request.DueAt is { } dueAt && dueAt.UtcDateTime <= DateTime.UtcNow)
            return ApiResponse<int>.Fail("Hạn nộp phải ở sau thời điểm giao bài.");

        var lessonIds = (request.LessonIds ?? []).Distinct().ToList();
        var exerciseIds = (request.PracticeExerciseIds ?? []).Distinct().ToArray();
        var selectedExercises = await UsableExercises().Where(exercise => exerciseIds.Contains(exercise.Id))
            .Select(exercise => new { exercise.Id, exercise.LessonId }).ToListAsync(ct);
        if (selectedExercises.Count != exerciseIds.Length)
            return ApiResponse<int>.Fail("Có bài luyện tập chưa sẵn sàng hoặc không tồn tại.");
        lessonIds = lessonIds.Union(selectedExercises.Select(exercise => exercise.LessonId)).ToList();
        if (await VisibleLessons().CountAsync(lesson => lessonIds.Contains(lesson.Id), ct) != lessonIds.Count)
            return ApiResponse<int>.Fail("Có bài học không còn khả dụng.");
        if (lessonIds.Count == 0 && string.IsNullOrWhiteSpace(request.Instructions))
            return ApiResponse<int>.Fail("Hãy nhập yêu cầu làm bài hoặc đính kèm bài học.");

        var assignment = new HomeworkAssignment
        {
            MentorBookingId = request.BookingId,
            LearnerId = booking.LearnerId,
            MentorId = mentorId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions?.Trim(),
            DueAt = request.DueAt?.UtcDateTime,
            HomeworkAssignmentLessons = lessonIds.Select(id => new HomeworkAssignmentLesson { LessonId = id }).ToList(),
            Exercises = exerciseIds.Select(id => new HomeworkAssignmentExercise { PracticeExerciseId = id }).ToList()
        };
        await unit.HomeworkAssignments.AddAsync(assignment, ct);
        await unit.SaveChangesAsync(ct);
        return ApiResponse<int>.Created(assignment.Id);
    }

    public async Task<List<HomeworkItem>> GetAssignmentsAsync(int? learnerId, CancellationToken ct, int? bookingId = null)
    {
        var user = claims.GetUserClaim();
        var rows = await AssignmentsFor(user, learnerId).Where(item => !bookingId.HasValue || item.MentorBookingId == bookingId)
            .AsNoTracking().OrderByDescending(item => item.CreatedDate)
            .ThenByDescending(item => item.Id)
            .Include(item => item.Learner).Include(item => item.Mentor)
            .Include(item => item.HomeworkAssignmentLessons).ThenInclude(link => link.Lesson).ThenInclude(lesson => lesson.CertificateLevel)
            .Include(item => item.HomeworkAssignmentLessons).ThenInclude(link => link.Lesson).ThenInclude(lesson => lesson.LanguageSkill)
            .Include(item => item.Exercises).ThenInclude(link => link.PracticeExercise)
            .Include(item => item.Submission).ThenInclude(submission => submission!.GradingResult).AsSplitQuery().ToListAsync(ct);
        var ids = rows.Select(item => item.Id).ToArray();
        var scores = await unit.PracticeExerciseAttempts.Query()
            .Where(attempt => attempt.HomeworkAssignmentId.HasValue && ids.Contains(attempt.HomeworkAssignmentId.Value) &&
                attempt.IsCompleted && !attempt.IsDeleted)
            .Select(attempt => new { attempt.HomeworkAssignmentId, attempt.PracticeExerciseId, attempt.ScorePercent })
            .ToDictionaryAsync(attempt => (attempt.HomeworkAssignmentId!.Value, attempt.PracticeExerciseId),
                attempt => attempt.ScorePercent, ct);
        var exerciseIds = rows.SelectMany(item => item.Exercises).Select(link => link.PracticeExerciseId).Distinct().ToArray();
        var questionCounts = await unit.PracticeQuestions.Query()
            .Where(question => !question.IsDeleted && exerciseIds.Contains(question.PracticeExerciseId))
            .GroupBy(question => question.PracticeExerciseId)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Id, group => group.Count, ct);

        return rows.Select(item => new HomeworkItem(item.Id, item.LearnerId,
            FullName(item.Learner), FullName(item.Mentor), item.Title, item.Instructions, item.CreatedDate, item.DueAt,
            item.Status, item.HomeworkAssignmentLessons.OrderBy(link => link.Lesson.SortOrder).ThenBy(link => link.LessonId)
                .Select(link => new HomeworkLesson(link.LessonId, link.Lesson.Title, link.Lesson.CertificateLevel.Code,
                    link.Lesson.LanguageSkill.Name)).ToList(),
            item.Exercises.OrderBy(link => link.PracticeExercise.SortOrder).ThenBy(link => link.PracticeExerciseId)
                .Select(link => new HomeworkExercise(link.PracticeExerciseId, link.PracticeExercise.Title,
                    link.PracticeExercise.Description, questionCounts.GetValueOrDefault(link.PracticeExerciseId),
                    scores.GetValueOrDefault((item.Id, link.PracticeExerciseId)))).ToList(),
            item.Submission is { IsDeleted: false } submission ? ToSubmission(submission) : null, item.MentorBookingId)).ToList();
    }

    public async Task<ApiResponse<LessonDetailResponse>> GetAssignedLessonAsync(int assignmentId, int lessonId, CancellationToken ct)
    {
        if (!await AssignmentsFor(claims.GetUserClaim(), null).AnyAsync(item => item.Id == assignmentId &&
            item.HomeworkAssignmentLessons.Any(link => link.LessonId == lessonId), ct))
            return ApiResponse<LessonDetailResponse>.NotFound("Không tìm thấy bài học được giao.");
        return await lessons.GetLessonDetailAsync(lessonId, ct);
    }

    public Task<ApiResponse<HomeworkSubmissionDetail>> SubmitAsync(int assignmentId, SubmitHomeworkRequest request, CancellationToken ct)
        => SubmitInternalAsync(assignmentId, request, null, ct);

    private async Task<ApiResponse<HomeworkSubmissionDetail>> SubmitInternalAsync(int assignmentId, SubmitHomeworkRequest request,
        IFormFile? scan, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var response = ApiResponse<HomeworkSubmissionDetail>.NotFound("Không tìm thấy bài tập được giao.");
        var text = request.TextContent?.Trim();
        var link = request.FileUrl?.Trim();
        if (!string.IsNullOrEmpty(link) &&
            (!Uri.TryCreate(link, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
             string.IsNullOrEmpty(url.Host) || !string.IsNullOrEmpty(url.UserInfo)))
            return ApiResponse<HomeworkSubmissionDetail>.Fail("Đường dẫn bài nộp phải là liên kết HTTPS hợp lệ.");

        await unit.ExecuteInTransactionAsync(async token =>
        {
            response = ApiResponse<HomeworkSubmissionDetail>.NotFound("Không tìm thấy bài tập được giao.");
            if (await unit.UserAccounts.GetByIdForUpdateAsync(userId, token) is not { IsDeleted: false })
                return;
            var assignment = await unit.HomeworkAssignments.Query().AsTracking()
                .Include(item => item.Submission).ThenInclude(submission => submission!.GradingResult)
                .Include(item => item.Exercises).Include(item => item.HomeworkAssignmentLessons)
                .SingleOrDefaultAsync(item => item.Id == assignmentId && item.LearnerId == userId && !item.IsDeleted, token);
            if (assignment is null) return;
            if (assignment.Submission is { IsDeleted: false } previous)
            {
                response = ApiResponse<HomeworkSubmissionDetail>.Success(ToSubmission(previous));
                return;
            }
            if (assignment.Status != "Assigned")
            {
                response = ApiResponse<HomeworkSubmissionDetail>.Fail("Bài tập đã đóng.", HttpStatusCode.Conflict, "HOMEWORK_CLOSED");
                return;
            }

            var exerciseIds = assignment.Exercises.Select(item => item.PracticeExerciseId).ToArray();
            var completed = await unit.PracticeExerciseAttempts.Query().CountAsync(attempt =>
                attempt.HomeworkAssignmentId == assignmentId && attempt.UserId == userId &&
                attempt.IsCompleted && !attempt.IsDeleted && exerciseIds.Contains(attempt.PracticeExerciseId), token);
            if (completed != exerciseIds.Length)
            {
                response = ApiResponse<HomeworkSubmissionDetail>.Fail("Hãy làm hết các bài luyện tập trước khi nộp.",
                    HttpStatusCode.Conflict, "HOMEWORK_EXERCISES_INCOMPLETE");
                return;
            }
            if (completed == 0 && string.IsNullOrEmpty(text) && string.IsNullOrEmpty(link) && scan is null)
            {
                response = ApiResponse<HomeworkSubmissionDetail>.Fail("Hãy nhập nội dung hoặc đường dẫn bài nộp.");
                return;
            }

            string? privateScanReference = null;
            if (scan is not null)
            {
                await using var image = scan.OpenReadStream();
                var stored = await storage!.UploadPrivateVerificationAsync(image, scan.ContentType, userId, token);
                privateScanReference = stored.Url;
            }

            var submission = new HomeworkSubmission
            {
                HomeworkAssignmentId = assignmentId,
                LearnerId = userId,
                LessonId = assignment.HomeworkAssignmentLessons.Select(item => (int?)item.LessonId).Order().FirstOrDefault(),
                TextContent = string.IsNullOrEmpty(text) ? null : text,
                FileUrl = string.IsNullOrEmpty(link) ? null : link,
                PrivateScanReference = privateScanReference,
                SubmittedAt = DateTime.UtcNow
            };
            await unit.HomeworkSubmissions.AddAsync(submission, token);
            assignment.Status = "Submitted";
            await unit.SaveChangesAsync(token);
            response = ApiResponse<HomeworkSubmissionDetail>.Created(ToSubmission(submission));
        }, ct);
        return response;
    }

    public async Task<ApiResponse<HomeworkPeriodReport>> GetReportAsync(DateOnly from, DateOnly to, int? learnerId, CancellationToken ct)
    {
        if (from >= DateOnly.MaxValue || to >= DateOnly.MaxValue || from < new DateOnly(2000, 1, 1) ||
            from > to || to.DayNumber - from.DayNumber > 365)
            return ApiResponse<HomeworkPeriodReport>.Fail("Chọn khoảng thời gian hợp lệ, tối đa 366 ngày.");

        var user = claims.GetUserClaim();
        var sessions = ScheduledBookings(unit);
        var assignments = AssignmentsFor(user, learnerId);
        if (user.Role == Role.Mentor)
        {
            if (learnerId is null)
                return ApiResponse<HomeworkPeriodReport>.Fail("Hãy chọn người học để xem báo cáo.");
            sessions = sessions.Where(booking => booking.MentorAvailability!.MentorId == user.Id &&
                booking.UserSubscription.UserId == learnerId);
            if (!await sessions.AnyAsync(ct) && !await assignments.AnyAsync(ct))
                return ApiResponse<HomeworkPeriodReport>.NotFound("Không tìm thấy người học của bạn.");
        }
        else
        {
            sessions = sessions.Where(booking => booking.UserSubscription.UserId == user.Id);
        }

        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;
        var observedUntil = end < DateTime.UtcNow ? end : DateTime.UtcNow;
        var homework = await assignments.Where(item => item.CreatedDate >= start && item.CreatedDate < end)
            .Select(item => item.Submission != null && !item.Submission.IsDeleted &&
                item.Submission.SubmittedAt < end).ToListAsync(ct);
        var meetings = await sessions.Where(booking => booking.MentorAvailability!.StartTime >= start &&
                booking.MentorAvailability.StartTime < end && booking.MentorAvailability.EndTime <= observedUntil)
            .GroupBy(booking => booking.MentorAvailabilityId)
            .Select(group => new
            {
                Tracked = group.Any(booking => booking.MentorAvailability!.AttendanceTrackingEnabled),
                Attended = group.Any(booking => booking.AttendedAt != null)
            }).ToListAsync(ct);
        var submitted = homework.Count(value => value);
        var tracked = meetings.Count(meeting => meeting.Tracked);
        var attended = meetings.Count(meeting => meeting.Tracked && meeting.Attended);
        return ApiResponse<HomeworkPeriodReport>.Success(new HomeworkPeriodReport(from, to,
            homework.Count, submitted, Percent(submitted, homework.Count),
            tracked, attended, Percent(attended, tracked), meetings.Count - tracked));
    }

    private IQueryable<HomeworkAssignment> AssignmentsFor(ClaimDTO user, int? learnerId) =>
        unit.HomeworkAssignments.Query().Where(item => !item.IsDeleted && (user.Role == Role.Admin
            ? (!learnerId.HasValue || item.LearnerId == learnerId) : user.Role == Role.Mentor
            ? item.MentorId == user.Id && (!learnerId.HasValue || item.LearnerId == learnerId)
            : item.LearnerId == user.Id));

    private IQueryable<Lesson> VisibleLessons() => unit.Lessons.Query().Where(lesson => !lesson.IsDeleted &&
        !lesson.LanguageSkill.IsDeleted && !lesson.CertificateLevel.IsDeleted &&
        SupportedLevels.Contains(lesson.CertificateLevel.Code) && lesson.CertificateLevel.CertificateType.Name == "JLPT" &&
        lesson.CertificateLevel.CertificateType.IsActive && !lesson.CertificateLevel.CertificateType.IsDeleted);

    private IQueryable<PracticeExercise> UsableExercises() => unit.PracticeExercises.Query().Where(exercise =>
        !exercise.IsDeleted && exercise.Questions!.Any(question => !question.IsDeleted) &&
        !exercise.Questions!.Any(question => !question.IsDeleted &&
            (question.Options!.Count(option => !option.IsDeleted) < 2 ||
             !question.Options!.Any(option => !option.IsDeleted && option.IsCorrect))));

    private static HomeworkSubmissionDetail ToSubmission(HomeworkSubmission submission) =>
        new(submission.Id, submission.TextContent, submission.FileUrl, submission.SubmittedAt,
            submission.PrivateScanReference is not null, PublishedGrade(submission.GradingResult));

    private static string FullName(UserAccount user) => $"{user.FirstName} {user.LastName}".Trim();
    private static double? Percent(int count, int total) => total == 0 ? null : Math.Round(count * 100d / total, 2);
}
