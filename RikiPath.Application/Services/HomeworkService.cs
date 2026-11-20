using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

// PR #70: partial (HomeworkGradingService.cs, MentorHomeworkProgressService.cs của teammate). Chấm AI dùng
// IAiEssayGradingClient/IAiOcrClient thật (teammate); IHomeworkAiDraftGenerator giả lập cũ không còn được dùng.
public sealed partial class HomeworkService(
    IUnitOfWork unit,
    IClaimService claims,
    ILessonProgressService lessons,
    IFileStorageService? storage = null,
    IAiEssayGradingClient? grader = null,
    IAiOcrClient? ocr = null,
    IMentorGroupPolicyService? groupPolicies = null)
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly string[] SupportedLevels = ["N5", "N4", "N3", "N2", "N1"];

    public async Task<List<HomeworkLearner>> GetLearnersAsync(CancellationToken ct, bool includeAllBookings = false)
    {
        var mentorId = claims.GetUserClaim().Id;
        var enrollments = await unit.StudyCourses.Query()
            .Where(c => c.CohortId != null && c.PaymentStatus == "Paid" && c.Status == "Active"
                && c.Cohort!.MentorId == mentorId && !c.Learner.IsDeleted)
            .OrderByDescending(c => c.StartsAt ?? c.Cohort!.StartsAt)
            .ThenByDescending(c => c.Id)
            .Select(c => new HomeworkLearner(
                c.LearnerId,
                (c.Learner.FirstName + " " + c.Learner.LastName).Trim(),
                c.CohortId!.Value,
                c.StartsAt ?? c.Cohort!.StartsAt))
            .ToListAsync(ct);
        return (includeAllBookings ? enrollments : enrollments.DistinctBy(c => c.LearnerId))
            .OrderBy(c => c.LearnerName).ToList();
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
        if (!request.CohortId.HasValue)
            return ApiResponse<int>.Fail("Vui lòng chọn lớp học nhóm (Cohort) để giao bài tập.", HttpStatusCode.BadRequest, "COHORT_REQUIRED");

        var batch = await CreateCohortAssignmentsAsync(request.CohortId.Value, request, ct);
        return batch.IsSuccess && batch.Result is { Count: > 0 } ids
            ? ApiResponse<int>.Created(ids[0])
            : ApiResponse<int>.Fail(batch.ErrorMessage ?? "Không thể giao bài tập cho lớp nhóm.", (HttpStatusCode)batch.StatusCode, batch.ErrorCode, errors: batch.Errors);
    }

    public async Task<ApiResponse<List<int>>> CreateCohortAssignmentsAsync(int cohortId, CreateHomeworkRequest request, CancellationToken ct)
    {
        var user = claims.GetUserClaim();
        if (user.Role != Role.Mentor)
            return ApiResponse<List<int>>.Fail("Chỉ cố vấn mới được giao bài tập.", HttpStatusCode.Forbidden);

        var cohort = await unit.StudyCohorts.Query().AsNoTracking()
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == cohortId, ct);
        if (cohort is null)
            return ApiResponse<List<int>>.NotFound("Không tìm thấy lớp nhóm.");
        if (cohort.MentorId != user.Id)
            return ApiResponse<List<int>>.Fail("Bạn chỉ được giao bài tập cho lớp nhóm do mình phụ trách.", HttpStatusCode.Forbidden);

        var activeEnrollments = cohort.Enrollments
            .Where(e => e.PaymentStatus == "Paid" && e.Status == "Active")
            .ToList();
        if (activeEnrollments.Count == 0)
            return ApiResponse<List<int>>.Fail("Lớp nhóm chưa có học viên đang theo học.", HttpStatusCode.Conflict);

        List<(int LearnerId, int CourseId)> recipients;
        string scope;
        Guid? groupKey = null;
        if (request.TargetLearnerIds is { Count: > 0 })
        {
            var distinctTargets = request.TargetLearnerIds.Distinct().ToList();
            var byLearner = activeEnrollments.ToDictionary(e => e.LearnerId, e => e.Id);
            if (distinctTargets.Any(id => !byLearner.ContainsKey(id)))
                return ApiResponse<List<int>>.Fail("Chỉ được giao bài cho học viên thuộc lớp nhóm hiện tại.", HttpStatusCode.Forbidden);
            recipients = distinctTargets.Select(id => (id, byLearner[id])).ToList();
            scope = "CohortIndividual";
        }
        else
        {
            recipients = activeEnrollments.Select(e => (e.LearnerId, e.Id)).ToList();
            scope = "CohortCommon";
            groupKey = Guid.NewGuid();
        }

        var defaultDeadlineDays = groupPolicies is null
            ? 7
            : (await groupPolicies.GetEffectiveAsync(DateTime.UtcNow, ct))?.HomeworkDeadlineDays ?? 7;
        var validated = await ValidateAssignmentContentAsync(request, DateTime.UtcNow.AddDays(defaultDeadlineDays), ct);
        if (validated.Error is not null)
            return ApiResponse<List<int>>.Fail(validated.Error.ErrorMessage ?? "Yêu cầu giao bài không hợp lệ.", (HttpStatusCode)validated.Error.StatusCode);

        var created = new List<HomeworkAssignment>();
        foreach (var (learnerId, courseId) in recipients)
        {
            var assignment = BuildAssignment(user.Id, learnerId, null, cohortId, courseId, groupKey, scope, request, validated.DueAt, validated.LessonIds, validated.ExerciseIds);
            await unit.HomeworkAssignments.AddAsync(assignment, ct);
            created.Add(assignment);
        }
        await unit.SaveChangesAsync(ct);
        return ApiResponse<List<int>>.Created(created.Select(a => a.Id).ToList());
    }

    private async Task<(ApiResponse<int>? Error, DateTime? DueAt, List<int> LessonIds, int[] ExerciseIds)> ValidateAssignmentContentAsync(
        CreateHomeworkRequest request, DateTime? fallbackDueAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return (ApiResponse<int>.Fail("Hãy nhập tiêu đề bài tập."), null, [], []);
        var dueAt = request.DueAt?.UtcDateTime ?? fallbackDueAtUtc;
        if (dueAt.HasValue && dueAt.Value <= DateTime.UtcNow)
            return (ApiResponse<int>.Fail("Hạn nộp phải ở sau thời điểm giao bài."), null, [], []);

        var lessonIds = (request.LessonIds ?? []).Distinct().ToList();
        var exerciseIds = (request.PracticeExerciseIds ?? []).Distinct().ToArray();
        var selectedExercises = await UsableExercises().Where(exercise => exerciseIds.Contains(exercise.Id))
            .Select(exercise => new { exercise.Id, exercise.LessonId }).ToListAsync(ct);
        if (selectedExercises.Count != exerciseIds.Length)
            return (ApiResponse<int>.Fail("Có bài luyện tập chưa sẵn sàng hoặc không tồn tại."), null, [], []);
        lessonIds = lessonIds.Union(selectedExercises.Select(exercise => exercise.LessonId)).ToList();
        if (await VisibleLessons().CountAsync(lesson => lessonIds.Contains(lesson.Id), ct) != lessonIds.Count)
            return (ApiResponse<int>.Fail("Có bài học không còn khả dụng."), null, [], []);
        if (lessonIds.Count == 0 && string.IsNullOrWhiteSpace(request.Instructions))
            return (ApiResponse<int>.Fail("Hãy nhập yêu cầu làm bài hoặc đính kèm bài học."), null, [], []);

        return (null, dueAt, lessonIds, exerciseIds);
    }

    private static HomeworkAssignment BuildAssignment(
        int mentorId,
        int learnerId,
        int? bookingId,
        int? cohortId,
        int? studyCourseId,
        Guid? cohortGroupKey,
        string scope,
        CreateHomeworkRequest request,
        DateTime? dueAt,
        List<int> lessonIds,
        int[] exerciseIds) => new()
        {
            MentorBookingId = bookingId,
            CohortId = cohortId,
            StudyCourseId = studyCourseId,
            CohortAssignmentGroupKey = cohortGroupKey,
            AssignmentScope = scope,
            LearnerId = learnerId,
            MentorId = mentorId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions?.Trim(),
            DueAt = dueAt,
            HomeworkAssignmentLessons = lessonIds.Select(id => new HomeworkAssignmentLesson { LessonId = id }).ToList(),
            Exercises = exerciseIds.Select(id => new HomeworkAssignmentExercise { PracticeExerciseId = id }).ToList()
        };

    public async Task<List<HomeworkItem>> GetAssignmentsAsync(int? learnerId, CancellationToken ct, int? cohortId = null)
    {
        var user = claims.GetUserClaim();
        var query = AssignmentsFor(user, learnerId);
        if (cohortId.HasValue)
            query = query.Where(item => item.CohortId == cohortId.Value);
        var rows = await query.AsNoTracking().OrderByDescending(item => item.CreatedDate)
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
            item.Submission is { IsDeleted: false } submission ? ToSubmission(submission) : null,
            item.CohortId,
            item.AssignmentScope,
            item.CohortAssignmentGroupKey,
            user.Role is Role.Mentor or Role.Admin && (item.AiDraftFeedback is not null || item.AiDraftGeneratedAtUtc.HasValue || item.AiDraftUnavailableReason is not null)
                ? new HomeworkAiDraftView(item.AiDraftFeedback, item.AiDraftScore, item.AiDraftGeneratedAtUtc, item.AiDraftUnavailableReason)
                : null,
            item.PublishedFeedbackAtUtc.HasValue
                ? new HomeworkPublishedGradeView(item.Passed, item.MentorScore, item.MentorFeedback, item.PublishedFeedbackAtUtc.Value, item.PublishedByMentorId)
                : null,
            item.MentorBookingId)).ToList();
    }

    public async Task<ApiResponse<LessonDetailResponse>> GetAssignedLessonAsync(int assignmentId, int lessonId, CancellationToken ct)
    {
        if (!await AssignmentsFor(claims.GetUserClaim(), null).AnyAsync(item => item.Id == assignmentId &&
            item.HomeworkAssignmentLessons.Any(link => link.LessonId == lessonId), ct))
            return ApiResponse<LessonDetailResponse>.NotFound("Không tìm thấy bài học được giao.");
        return await lessons.GetLessonDetailAsync(lessonId, ct);
    }

    // PR #70 hợp nhất: một writer duy nhất cho bài nộp (văn bản/liên kết/ảnh-PDF). Ảnh lưu ở PrivateScanReference (teammate);
    // Task 02 giữ quy tắc được sửa bài trước khi chấm (RevisionCount) và xóa nháp AI cũ khi nộp lại.
    public Task<ApiResponse<HomeworkSubmissionDetail>> SubmitAsync(int assignmentId, SubmitHomeworkRequest request, CancellationToken ct)
        => SubmitInternalAsync(assignmentId, request, null, ct);

    // Alias tương thích Task 02 (POST {id}/submit-scan): chuyển về SubmitScanAsync canonical.
    public Task<ApiResponse<HomeworkSubmissionDetail>> SubmitWithScanAsync(
        int assignmentId, string? textContent, IFormFile? scan, CancellationToken ct)
        => scan is null
            ? SubmitInternalAsync(assignmentId, new SubmitHomeworkRequest(textContent, null), null, ct)
            : SubmitScanAsync(assignmentId, scan, ct, textContent);

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
            if (assignment.Status == "Graded" || assignment.PublishedFeedbackAtUtc.HasValue
                || assignment.Submission?.GradingResult is { IsDeleted: false })
            {
                response = ApiResponse<HomeworkSubmissionDetail>.Fail(
                    "Bài tập đã được chấm và công bố kết quả; không thể chỉnh sửa bài nộp.",
                    HttpStatusCode.Conflict,
                    "HOMEWORK_ALREADY_GRADED");
                return;
            }
            if (assignment.Status is not ("Assigned" or "Submitted"))
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
            var existing = assignment.Submission is { IsDeleted: false } current ? current : null;
            var hasStoredScan = existing is not null && ScanReference(existing) is not null;
            if (completed == 0 && string.IsNullOrEmpty(text) && string.IsNullOrEmpty(link) && scan is null && !hasStoredScan)
            {
                response = ApiResponse<HomeworkSubmissionDetail>.Fail("Hãy nhập nội dung, đường dẫn hoặc đính kèm ảnh/PDF bài làm.");
                return;
            }

            string? privateScanReference = null;
            if (scan is not null)
            {
                await using var image = scan.OpenReadStream();
                var stored = await storage!.UploadPrivateVerificationAsync(image, scan.ContentType, userId, token);
                privateScanReference = stored.Url;
            }

            var nowUtc = DateTime.UtcNow;
            if (existing is not null)
            {
                existing.TextContent = string.IsNullOrEmpty(text) ? null : text;
                existing.FileUrl = string.IsNullOrEmpty(link) ? null : link;
                if (privateScanReference is not null) existing.PrivateScanReference = privateScanReference;
                existing.SubmittedAt = nowUtc;
                existing.LastRevisedAtUtc = nowUtc;
                existing.RevisionCount++;
                // Xóa nháp AI cũ vì học viên đã nộp phiên bản mới.
                assignment.AiDraftFeedback = null;
                assignment.AiDraftScore = null;
                assignment.AiDraftGeneratedAtUtc = null;
                assignment.AiDraftUnavailableReason = null;
                assignment.Status = "Submitted";
                await unit.SaveChangesAsync(token);
                response = ApiResponse<HomeworkSubmissionDetail>.Success(ToSubmission(existing), HttpStatusCode.OK, "Cập nhật bài nộp thành công.");
                return;
            }

            var submission = new HomeworkSubmission
            {
                HomeworkAssignmentId = assignmentId,
                LearnerId = userId,
                LessonId = assignment.HomeworkAssignmentLessons.Select(item => (int?)item.LessonId).Order().FirstOrDefault(),
                TextContent = string.IsNullOrEmpty(text) ? null : text,
                FileUrl = string.IsNullOrEmpty(link) ? null : link,
                PrivateScanReference = privateScanReference,
                SubmittedAt = nowUtc,
                RevisionCount = 1
            };
            await unit.HomeworkSubmissions.AddAsync(submission, token);
            assignment.Status = "Submitted";
            await unit.SaveChangesAsync(token);
            response = ApiResponse<HomeworkSubmissionDetail>.Created(ToSubmission(submission));
        }, ct);
        return response;
    }

    // Alias tương thích Task 02 (GET {id}/submission-scan) → ReadScanAsync canonical.
    public Task<PrivateVerificationReadResult?> OpenSubmissionScanAsync(int assignmentId, CancellationToken ct)
        => ReadScanAsync(assignmentId, ct);

    // Alias tương thích Task 02 (POST {id}/grade): ánh xạ GradeHomeworkAssignmentRequest về PublishGradeAsync canonical
    // (một writer duy nhất: GradingResult + bản sao trên HomeworkAssignment cho báo cáo Cohort).
    public async Task<ApiResponse<HomeworkPublishedGradeView>> GradeAndPublishAsync(
        int assignmentId, GradeHomeworkAssignmentRequest request, CancellationToken ct)
    {
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Mentor)
            return ApiResponse<HomeworkPublishedGradeView>.Fail("Chỉ cố vấn mới được chấm và công bố kết quả bài tập.", HttpStatusCode.Forbidden);
        var target = await AssignmentsFor(actor, null).AsNoTracking().Where(a => a.Id == assignmentId)
            .Select(a => new
            {
                SubmissionId = a.Submission != null && !a.Submission.IsDeleted ? (int?)a.Submission.Id : null,
                a.AiDraftFeedback,
                a.AiDraftScore
            }).SingleOrDefaultAsync(ct);
        if (target is null)
            return await unit.HomeworkAssignments.Query().AnyAsync(a => a.Id == assignmentId && !a.IsDeleted, ct)
                ? ApiResponse<HomeworkPublishedGradeView>.Fail("Bạn chỉ được chấm bài tập do mình giao hoặc của lớp mình đang phụ trách.", HttpStatusCode.Forbidden)
                : ApiResponse<HomeworkPublishedGradeView>.NotFound("Không tìm thấy bài tập.");
        if (target.SubmissionId is not int submissionId)
            return ApiResponse<HomeworkPublishedGradeView>.Fail("Học viên chưa nộp bài.", HttpStatusCode.Conflict);
        var feedback = string.IsNullOrWhiteSpace(request.Feedback) && request.PublishAiDraft ? target.AiDraftFeedback : request.Feedback;
        var score = request.Score ?? (request.PublishAiDraft ? target.AiDraftScore : null);
        if (string.IsNullOrWhiteSpace(feedback))
            return ApiResponse<HomeworkPublishedGradeView>.Fail("Vui lòng nhập nhận xét trước khi công bố cho học viên.");
        if (score is null)
            return ApiResponse<HomeworkPublishedGradeView>.Fail("Nhập điểm từ 0 đến 100 trước khi công bố.");
        var published = await PublishGradeCoreAsync(assignmentId,
            new PublishHomeworkGradeRequest(submissionId, (decimal)score.Value, feedback), request.Passed, ct);
        return published.IsSuccess
            ? ApiResponse<HomeworkPublishedGradeView>.Success(new HomeworkPublishedGradeView(request.Passed, published.Result!.Score,
                published.Result.Feedback, published.Result.PublishedAt, actor.Id))
            : ApiResponse<HomeworkPublishedGradeView>.Fail(published.ErrorMessage ?? "Không công bố được kết quả.",
                (HttpStatusCode)published.StatusCode, published.ErrorCode);
    }

    public async Task<ApiResponse<HomeworkPeriodReport>> GetReportAsync(DateOnly from, DateOnly to, int? learnerId, CancellationToken ct)
    {
        if (from >= DateOnly.MaxValue || to >= DateOnly.MaxValue || from < new DateOnly(2000, 1, 1) ||
            from > to || to.DayNumber - from.DayNumber > 365)
            return ApiResponse<HomeworkPeriodReport>.Fail("Chọn khoảng thời gian hợp lệ, tối đa 366 ngày.");

        var user = claims.GetUserClaim();
        var courses = unit.StudyCourses.Query()
            .Where(c => c.CohortId != null && c.PaymentStatus == "Paid" && c.Status != "Cancelled");
        var assignments = AssignmentsFor(user, learnerId);
        if (user.Role == Role.Mentor)
        {
            if (learnerId is null)
                return ApiResponse<HomeworkPeriodReport>.Fail("Hãy chọn người học để xem báo cáo.");
            courses = courses.Where(c => (c.MentorId == user.Id || (c.Cohort != null && c.Cohort.MentorId == user.Id)) &&
                c.LearnerId == learnerId);
            if (!await courses.AnyAsync(ct) && !await assignments.AnyAsync(ct))
                return ApiResponse<HomeworkPeriodReport>.NotFound("Không tìm thấy người học của bạn.");
        }
        else
        {
            courses = courses.Where(c => c.LearnerId == user.Id);
        }

        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;
        var observedUntil = end < DateTime.UtcNow ? end : DateTime.UtcNow;
        var homework = await assignments.Where(item => item.CreatedDate >= start && item.CreatedDate < end)
            .Select(item => item.Submission != null && !item.Submission.IsDeleted &&
                item.Submission.SubmittedAt < end).ToListAsync(ct);
        var meetings = await courses
            .SelectMany(c => c.Sessions.Where(s => s.ScheduledAt >= start && s.ScheduledAt < end
                && s.ScheduledAt.AddMinutes(c.SessionMinutes) <= observedUntil && s.Status != "CancelledByMentor"))
            .Select(s => new
            {
                Tracked = true,
                Attended = s.Status == "Present"
            }).ToListAsync(ct);
        var submitted = homework.Count(value => value);
        var tracked = meetings.Count(meeting => meeting.Tracked);
        var attended = meetings.Count(meeting => meeting.Tracked && meeting.Attended);
        return ApiResponse<HomeworkPeriodReport>.Success(new HomeworkPeriodReport(from, to,
            homework.Count, submitted, Percent(submitted, homework.Count),
            tracked, attended, Percent(attended, tracked), meetings.Count - tracked));
    }

    // PR #70: Admin xem mọi bài (teammate). Mentor: bài mình giao, hoặc (Task 02/05) bài Cohort của học viên đang học
    // trong lớp mà mentor hiện là cố vấn chính (sau khi Admin thay cố vấn). Mentor khác lớp không truy cập được.
    private IQueryable<HomeworkAssignment> AssignmentsFor(ClaimDTO user, int? learnerId) =>
        unit.HomeworkAssignments.Query().Where(item => !item.IsDeleted && (user.Role == Role.Admin
            ? (!learnerId.HasValue || item.LearnerId == learnerId)
            : user.Role == Role.Mentor
            ? (item.MentorId == user.Id
                || (item.CohortId != null && item.StudyCourseId != null && unit.StudyCourses.Query().Any(c => c.Id == item.StudyCourseId
                    && c.CohortId == item.CohortId && c.Status == "Active" && c.Cohort != null && c.Cohort.MentorId == user.Id)))
              && (!learnerId.HasValue || item.LearnerId == learnerId)
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

    // Ảnh mới ở PrivateScanReference (teammate); ảnh lịch sử Task 02 ở ScanStorageUrl vẫn đọc được.
    private static string? ScanReference(HomeworkSubmission submission)
        => !string.IsNullOrWhiteSpace(submission.PrivateScanReference) ? submission.PrivateScanReference
            : !string.IsNullOrWhiteSpace(submission.ScanStorageUrl) ? submission.ScanStorageUrl : null;

    private static HomeworkSubmissionDetail ToSubmission(HomeworkSubmission submission) =>
        new(submission.Id, submission.TextContent, submission.FileUrl, submission.SubmittedAt, submission.RevisionCount,
            submission.LastRevisedAtUtc, ScanReference(submission) is not null, PublishedGrade(submission.GradingResult));

    private static string FullName(UserAccount user) => $"{user.FirstName} {user.LastName}".Trim();
    private static double? Percent(int count, int total) => total == 0 ? null : Math.Round(count * 100d / total, 2);
}
