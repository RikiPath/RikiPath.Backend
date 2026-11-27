using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Handlers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class ManualContentService(IUnitOfWork uow, IClaimService claims, IContentApprovalNotifierService? notifier = null,
    IContentAudioTranscriptionService? audioTranscriptions = null, ISidebarCounterNotifier? sidebarNotifier = null)
{
    private const string ManualSource = "Nhập tay";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ManualContentItem>> ListAsync(ImportEntityType? type, ContentApprovalStatus? status,
        CancellationToken ct, Guid? batchId = null)
    {
        var userId = claims.GetUserClaim().Id;
        var rows = await uow.ContentApprovalRequests.Query().AsNoTracking()
            .Where(x => !x.IsDeleted && x.SubmittedByUserId == userId && (!type.HasValue || x.EntityType == type.Value)
                && (!status.HasValue || x.Status == status.Value) && (!batchId.HasValue || x.BatchId == batchId.Value))
            .OrderByDescending(x => x.ModifiedDate ?? x.SubmittedAt)
            .Select(x => new
            {
                x.Id, x.EntityType, x.RowKey, x.Status, x.SourceFileName, x.SourceRowNumber, x.ReviewNote, x.PayloadJson,
                UpdatedAt = x.ModifiedDate ?? x.CreatedDate ?? x.SubmittedAt,
                x.SubmittedAt,
                x.ReviewedAt,
            })
            .ToListAsync(ct);

        var levelCodes = await uow.CertificateLevels.Query().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        return rows.Select(row =>
        {
            var info = ReadListInfo(row.PayloadJson);
            var levelCode = info?.CertificateLevelId is int lvlId && levelCodes.TryGetValue(lvlId, out var code) ? code : null;
            return new ManualContentItem(row.Id, row.EntityType, row.RowKey, row.Status,
                row.SourceFileName, row.SourceRowNumber, row.ReviewNote, info?.CertificateLevelId, info?.TopicTitle,
                row.UpdatedAt, row.SubmittedAt, row.ReviewedAt, levelCode);
        }).ToList();
    }

    public async Task<IReadOnlyList<AuthorImportBatch>> GetImportBatchesAsync(CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        return await uow.ContentApprovalRequests.Query().AsNoTracking()
            .Where(item => !item.IsDeleted && item.SubmittedByUserId == userId && item.BatchId != Guid.Empty &&
                (item.SourceFileName.ToLower().EndsWith(".xlsx") || item.SourceFileName.ToLower().EndsWith(".xls")))
            .GroupBy(item => new { item.BatchId, item.SourceFileName })
            .OrderByDescending(group => group.Min(item => item.CreatedDate ?? item.SubmittedAt))
            .ThenBy(group => group.Key.BatchId)
            .Select(group => new AuthorImportBatch(group.Key.BatchId, group.Key.SourceFileName,
                group.Min(item => item.CreatedDate ?? item.SubmittedAt), group.Count(),
                group.Count(item => item.Status == ContentApprovalStatus.Draft),
                group.Count(item => item.Status == ContentApprovalStatus.PendingReview),
                group.Count(item => item.Status == ContentApprovalStatus.Approved),
                group.Count(item => item.Status == ContentApprovalStatus.Rejected)))
            .ToListAsync(ct);
    }

    public async Task<ApiResponse<JsonElement>> GetAsync(int id, CancellationToken ct)
    {
        var item = await FindMineAsync(id, ct);
        if (item is null) return ApiResponse<JsonElement>.NotFound("Không tìm thấy nội dung.");

        var input = JsonSerializer.Deserialize<JsonElement>(item.PayloadJson);
        if (item.EntityType != ImportEntityType.Lesson || item.Status != ContentApprovalStatus.Approved)
            return ApiResponse<JsonElement>.Success(input);

        var lesson = Read<LessonImportPayloadResponse>(input);
        var published = await uow.Lessons.Query()
            .Where(x => !x.IsDeleted && (item.TargetEntityId.HasValue
                ? x.Id == item.TargetEntityId : x.Title == lesson.Title && x.CertificateLevelId == lesson.CertificateLevelId))
            .OrderBy(x => x.Id).Select(x => new
            {
                x.AudioUrl,
                x.ImageUrl,
                TopicId = x.Topic != null && !x.Topic.IsDeleted ? x.TopicId : null,
                TopicTitle = x.Topic != null && !x.Topic.IsDeleted ? x.Topic.Title : null,
            }).FirstOrDefaultAsync(ct);
        lesson.TopicId = published?.TopicId;
        lesson.TopicTitle = published?.TopicTitle;
        lesson.AudioUrl = published?.AudioUrl;
        lesson.ImageUrl = published?.ImageUrl;
        return ApiResponse<JsonElement>.Success(JsonSerializer.SerializeToElement(lesson));
    }

    public async Task<ApiResponse<int>> SaveAsync(ImportEntityType type, int? id, JsonElement input, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var response = ApiResponse<int>.Fail("Không thể lưu bản nháp.");
        await uow.ExecuteContentAuthorWriteAsync(userId, async token =>
            response = await SaveDraftAsync(type, id, input, userId, token), ct);
        if (sidebarNotifier is not null && response.IsSuccess)
            await sidebarNotifier.NotifyUsersAsync([userId], "manual_content_draft_saved", ct);
        return response;
    }

    private async Task<ApiResponse<int>> SaveDraftAsync(ImportEntityType type, int? id, JsonElement input,
        int userId, CancellationToken ct)
    {
        try
        {
            var source = id is null ? null : await FindMineAsync(id.Value, ct);
            if (id.HasValue && (source is null || source.EntityType != type))
                return ApiResponse<int>.NotFound("Không tìm thấy nội dung.");
            if (source?.Status == ContentApprovalStatus.PendingReview)
                return ApiResponse<int>.Fail("Nội dung đang chờ duyệt.", HttpStatusCode.Conflict);

            var (payload, rowKey) = await ValidateAsync(type, input, ct);
            if (rowKey.Length > 500)
                return ApiResponse<int>.Fail("Tên hoặc khóa nội dung quá dài.");
            var targetId = source?.TargetEntityId is > 0 ? source.TargetEntityId : null;
            if (source?.Status == ContentApprovalStatus.Approved && targetId is null)
            {
                targetId = await ResolveTargetAsync(type, JsonSerializer.Deserialize<JsonElement>(source.PayloadJson), ct);
                if (targetId is null)
                    return ApiResponse<int>.Fail("Không xác định được bản đã xuất bản để cập nhật.", HttpStatusCode.Conflict);
            }
            var existingId = await ResolveTargetAsync(type, JsonSerializer.SerializeToElement(payload), ct);
            if (existingId.HasValue && existingId != targetId)
                return ApiResponse<int>.Fail("Đã có nội dung với thông tin này.", HttpStatusCode.Conflict);

            var open = await uow.ContentApprovalRequests.Query().Where(x => !x.IsDeleted && x.EntityType == type
                && x.Id != id && (x.Status == ContentApprovalStatus.Draft || x.Status == ContentApprovalStatus.PendingReview)
                && (x.SubmittedByUserId == userId || x.Status == ContentApprovalStatus.PendingReview)
                && (x.RowKey == rowKey || (targetId.HasValue && x.TargetEntityId == targetId)))
                .Select(x => new { x.TargetEntityId, x.PayloadJson }).ToListAsync(ct);
            if (open.Any(item => type != ImportEntityType.PracticeExercise ||
                (targetId.HasValue && item.TargetEntityId == targetId) ||
                ReadListInfo(item.PayloadJson)?.CertificateLevelId == ((PracticeExerciseImportPayloadResponse)payload).CertificateLevelId))
                return ApiResponse<int>.Fail("Nội dung này đã có bản nháp hoặc yêu cầu đang chờ duyệt.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            var item = source;
            if (item is null || item.Status == ContentApprovalStatus.Approved)
            {
                item = new ContentApprovalRequest
                {
                    EntityType = type,
                    BatchId = Guid.NewGuid(),
                    SubmittedByUserId = userId,
                    SourceFileName = ManualSource,
                    TargetEntityId = targetId,
                    Action = targetId.HasValue ? ImportRowAction.Modified : ImportRowAction.New,
                    SubmittedAt = now,
                };
                await uow.ContentApprovalRequests.AddAsync(item, ct);
            }
            else if (type == ImportEntityType.Lesson && source!.SourceRowNumber > 0 && source.RowKey != rowKey)
            {
                // The import workflow resolves lesson links by the original title and level.
                return ApiResponse<int>.Fail("Hãy giữ tên và cấp độ của bài học trong bản nháp Excel để giữ liên kết với nội dung đi kèm.");
            }

            item.RowKey = rowKey;
            item.PayloadJson = JsonSerializer.Serialize(payload);
            item.DiffSummaryJson = payload is LessonImportPayloadResponse { TopicTitle: not null } lesson && targetId.HasValue
                ? await DescribeTopicChangeAsync(targetId.Value, lesson.TopicTitle, ct) : null;
            item.Status = ContentApprovalStatus.Draft;
            item.ModifiedDate = now;
            item.ReviewedAt = null;
            item.ReviewedByName = null;
            item.ReviewNote = null;
            if (audioTranscriptions is not null)
                await audioTranscriptions.QueuePayloadAsync(JsonSerializer.SerializeToElement(payload), userId, ct);
            await uow.SaveChangesAsync(ct);
            return ApiResponse<int>.Success(item.Id);
        }
        catch (JsonException) { return ApiResponse<int>.Fail("Dữ liệu biểu mẫu không hợp lệ."); }
        catch (ArgumentException ex) { return ApiResponse<int>.Fail(ex.Message); }
    }

    public async Task<ApiResponse<bool>> SubmitAsync(int id, CancellationToken ct)
    {
        var result = await SubmitDraftsAsync(id, null, ct);
        return result.IsSuccess ? ApiResponse<bool>.Success(true) : ApiResponse<bool>.Fail(
            result.ErrorMessage ?? "Không thể gửi duyệt.", (HttpStatusCode)result.StatusCode);
    }

    public async Task<ApiResponse<bool>> DeleteDraftAsync(int id, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var result = ApiResponse<bool>.NotFound("Không tìm thấy bản nháp.");
        await uow.ExecuteContentAuthorWriteAsync(userId, async token =>
        {
            var item = await FindMineAsync(id, token);
            if (item is null) return;
            if (item.Status is not (ContentApprovalStatus.Draft or ContentApprovalStatus.Rejected))
            {
                result = ApiResponse<bool>.Fail(
                    "Chỉ có thể xóa bản nháp hoặc nội dung bị từ chối.",
                    HttpStatusCode.Conflict);
                return;
            }
            item.IsDeleted = true;
            item.ModifiedDate = DateTime.UtcNow;
            await uow.SaveChangesAsync(token);
            result = ApiResponse<bool>.Success(true);
        }, ct);
        if (sidebarNotifier is not null && result.IsSuccess)
            await sidebarNotifier.NotifyUsersAsync([userId], "manual_content_draft_deleted", ct);
        return result;
    }

    public Task<ApiResponse<int>> SubmitBatchAsync(Guid batchId, CancellationToken ct)
        => batchId == Guid.Empty ? Task.FromResult(ApiResponse<int>.NotFound("Không tìm thấy lô import."))
            : SubmitDraftsAsync(null, batchId, ct);

    private async Task<ApiResponse<int>> SubmitDraftsAsync(int? id, Guid? batchId, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        var result = ApiResponse<int>.NotFound("Không tìm thấy bản nháp hoặc lô import.");
        var batchName = string.Empty;
        await uow.ExecuteContentAuthorWriteAsync(userId, async token =>
        {
            var items = await uow.ContentApprovalRequests.Query().AsNoTracking()
                .Where(item => !item.IsDeleted && item.SubmittedByUserId == userId &&
                    (id.HasValue ? item.Id == id : item.BatchId == batchId))
                .OrderBy(item => item.SourceRowNumber).ThenBy(item => item.Id).ToListAsync(token);
            result = await SubmitLoadedDraftsAsync(items, id.HasValue, token);
            batchName = items.FirstOrDefault()?.SourceFileName ?? string.Empty;
        }, ct);

        if (notifier is not null && result.IsSuccess && result.Result > 0)
            await notifier.NotifyAdminsOnRequestCreatedAsync(result.Result, batchName, ct);
        if (sidebarNotifier is not null && result.IsSuccess && result.Result > 0)
        {
            await sidebarNotifier.NotifyAdminsAsync("manual_content_submitted", ct);
            await sidebarNotifier.NotifyUsersAsync([userId], "manual_content_submitted", ct);
        }
        return result;
    }

    private async Task<ApiResponse<int>> SubmitLoadedDraftsAsync(List<ContentApprovalRequest> items, bool single, CancellationToken ct)
    {
        if (items.Count == 0) return ApiResponse<int>.NotFound("Không tìm thấy bản nháp hoặc lô import.");
        var drafts = items.Where(item => item.Status == ContentApprovalStatus.Draft).ToList();
        if (drafts.Count == 0)
            return single && items[0].Status != ContentApprovalStatus.PendingReview
                ? ApiResponse<int>.Fail("Hãy lưu bản nháp trước khi gửi duyệt.", HttpStatusCode.Conflict)
                : ApiResponse<int>.Success(0);

        var lessonDraftInfos = drafts.Where(item => item.EntityType == ImportEntityType.Lesson)
            .Select(item => ReadListInfo(item.PayloadJson))
            .ToList();
        var lessonKeys = lessonDraftInfos
            .Select(info => (info?.Title, info?.CertificateLevelId))
            .ToHashSet();
        var topicKeys = lessonDraftInfos
            .Where(info => !string.IsNullOrWhiteSpace(info?.TopicTitle))
            .Select(info => (TopicTitle: info!.TopicTitle!.Trim(), info.CertificateLevelId))
            .ToHashSet();
        if (drafts.Any(item => item.EntityType == ImportEntityType.PracticeExercise))
        {
            var pendingLessons = await uow.ContentApprovalRequests.Query()
                .Where(item => !item.IsDeleted && item.SubmittedByUserId == drafts[0].SubmittedByUserId &&
                    item.EntityType == ImportEntityType.Lesson && item.Status == ContentApprovalStatus.PendingReview)
                .Select(item => item.PayloadJson).ToListAsync(ct);
            var pendingInfos = pendingLessons.Select(ReadListInfo).ToList();
            lessonKeys.UnionWith(pendingInfos.Select(info => (info?.Title, info?.CertificateLevelId)));
            topicKeys.UnionWith(pendingInfos
                .Where(info => !string.IsNullOrWhiteSpace(info?.TopicTitle))
                .Select(info => (TopicTitle: info!.TopicTitle!.Trim(), info.CertificateLevelId)));
        }

        foreach (var item in drafts)
        {
            try
            {
                // Newly created Excel rows have already passed the import validator before they
                // are persisted as Drafts, except PracticeExercises which still validate
                // batch/DB Lesson or Topic references below before submission.
                if (IsExcelImportDraft(item) && item.Action == ImportRowAction.New && item.EntityType != ImportEntityType.PracticeExercise)
                {
                    ValidateStoredImportPayload(item);
                    continue;
                }

                var input = JsonSerializer.Deserialize<JsonElement>(item.PayloadJson);
                var (payload, _) = await ValidateAsync(item.EntityType, input, ct);
                if (item.SourceRowNumber > 0)
                {
                    var targetId = await ResolveTargetAsync(item.EntityType, input, ct);
                    var originalTargetId = item.TargetEntityId is > 0 ? item.TargetEntityId : null;
                    if (targetId != originalTargetId)
                        throw new ArgumentException("Học liệu gốc đã thay đổi. Hãy kiểm tra lại bản nháp trước khi gửi.");
                }
                if (payload is PracticeExerciseImportPayloadResponse exercise)
                {
                    var scope = Domain.Enums.PracticeExerciseScopes.Normalize(exercise.Scope);
                    if (scope == Domain.Enums.PracticeExerciseScopes.LessonQuickCheck)
                    {
                        if (!lessonKeys.Contains((exercise.LessonTitle, exercise.CertificateLevelId)) &&
                            !await uow.Lessons.Query().AnyAsync(lesson => !lesson.IsDeleted &&
                                lesson.Title == exercise.LessonTitle && lesson.CertificateLevelId == exercise.CertificateLevelId, ct))
                        {
                            throw new ArgumentException($"Hãy gửi bài học '{exercise.LessonTitle}' cùng lô hoặc trước bài luyện tập.");
                        }
                    }
                    else if (scope == Domain.Enums.PracticeExerciseScopes.GroupQuiz)
                    {
                        var tt = exercise.TopicTitle?.Trim();
                        var topicExistsInBatch = !string.IsNullOrWhiteSpace(tt) && topicKeys.Contains((tt, (int?)exercise.CertificateLevelId));
                        var topicExistsInDb = !topicExistsInBatch && !string.IsNullOrWhiteSpace(tt) &&
                            await uow.Topics.Query().AnyAsync(t => !t.IsDeleted && t.Title == tt
                                && (!t.CertificateLevelId.HasValue || t.CertificateLevelId == exercise.CertificateLevelId), ct);
                        var topicIdExists = !topicExistsInBatch && !topicExistsInDb && exercise.TopicId.HasValue &&
                            await uow.Topics.Query().AnyAsync(t => !t.IsDeleted && t.Id == exercise.TopicId.Value, ct);
                        if (!topicExistsInBatch && !topicExistsInDb && !topicIdExists)
                            throw new ArgumentException($"Hãy gửi bài học thuộc chặng '{exercise.TopicTitle}' cùng lô hoặc trước GroupQuiz.");
                    }
                    else if (scope == Domain.Enums.PracticeExerciseScopes.StageAssessment && exercise.CertificateLevelId <= 0)
                    {
                        throw new ArgumentException("StageAssessment cần cấp độ JLPT hợp lệ.");
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                return ApiResponse<int>.Fail($"Không thể gửi '{item.RowKey}': {ex.Message}");
            }
        }

        // The batch can contain thousands of Excel rows. A tracked SaveChanges
        // would issue one UPDATE per row and commonly exceed the browser/proxy
        // timeout. Keep the operation atomic, but update all draft statuses with
        // one parameterized SQL statement.
        var now = DateTime.UtcNow;
        var draftIds = drafts.Select(item => item.Id).ToArray();
        var updatedCount = await uow.ContentApprovalRequests.Query()
            .Where(item => draftIds.Contains(item.Id) && !item.IsDeleted &&
                item.Status == ContentApprovalStatus.Draft)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ContentApprovalStatus.PendingReview)
                .SetProperty(item => item.SubmittedAt, now), ct);

        return updatedCount == drafts.Count
            ? ApiResponse<int>.Success(updatedCount)
            : ApiResponse<int>.Fail("Một số bản nháp vừa được thao tác bởi yêu cầu khác. Vui lòng tải lại lô import.",
                HttpStatusCode.Conflict);
    }

    private static bool IsExcelImportDraft(ContentApprovalRequest item) =>
        item.SourceRowNumber > 0 &&
        (item.SourceFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
         item.SourceFileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));

    private static void ValidateStoredImportPayload(ContentApprovalRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.PayloadJson))
            throw new ArgumentException("Payload bản nháp bị rỗng.");

        try
        {
            using var document = JsonDocument.Parse(item.PayloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Payload bản nháp không hợp lệ.");

            var info = ReadListInfo(item.PayloadJson);
            if (info?.CertificateLevelId is not > 0)
                throw new ArgumentException("Payload bản nháp thiếu cấp độ JLPT.");

            if (item.EntityType == ImportEntityType.MockTest)
            {
                var hasSections = false;
                var sections = default(JsonElement);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "sections", StringComparison.OrdinalIgnoreCase))
                        continue;

                    sections = property.Value;
                    hasSections = true;
                    break;
                }

                if (!hasSections || sections.ValueKind != JsonValueKind.Array || sections.GetArrayLength() == 0)
                    throw new ArgumentException("Đề thi phải có ít nhất một phần thi.");
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Payload bản nháp không hợp lệ.", ex);
        }
    }

    private Task<ContentApprovalRequest?> FindMineAsync(int id, CancellationToken ct)
    {
        var userId = claims.GetUserClaim().Id;
        return uow.ContentApprovalRequests.Query()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.SubmittedByUserId == userId, ct);
    }

    private async Task<(object Payload, string RowKey)> ValidateAsync(ImportEntityType type, JsonElement input, CancellationToken ct)
    {
        var levelId = Read<LessonImportPayloadResponse>(input).CertificateLevelId;
        var level = await uow.CertificateLevels.Query().SingleOrDefaultAsync(x => x.Id == levelId && !x.IsDeleted
            && x.CertificateType.IsActive && !x.CertificateType.IsDeleted, ct)
            ?? throw new ArgumentException("Hãy chọn cấp độ hợp lệ.");

        switch (type)
        {
            case ImportEntityType.Lesson:
                var lesson = Read<LessonImportPayloadResponse>(input);
                if (lesson.ImageUrl is not null) lesson.ImageUrl = MediaUrl(lesson.ImageUrl, "URL ảnh") ?? string.Empty;
                lesson.Title = Required(lesson.Title, "Tên bài học");
                if (lesson.AudioUrl is not null)
                    lesson.AudioUrl = MediaUrl(lesson.AudioUrl, "URL âm thanh") ?? string.Empty;
                lesson.CertificateLevelCode = level.Code;
                await RequireSkillAsync(lesson.LanguageSkillId, ct);
                lesson.TopicTitle = lesson.TopicId is int topicId
                    ? await uow.Topics.Query().Where(x => x.Id == topicId && !x.IsDeleted)
                        .Select(x => x.Title).SingleOrDefaultAsync(ct)
                        ?? throw new ArgumentException("Chủ đề đã chọn không còn tồn tại.")
                    : NormalizeTopicTitle(lesson.TopicTitle);
                return (lesson, $"{lesson.Title} / {level.Code}");
            case ImportEntityType.Vocabulary:
                var vocabulary = Read<VocabularyImportPayloadResponse>(input);
                if (vocabulary.ImageUrl is not null) vocabulary.ImageUrl = MediaUrl(vocabulary.ImageUrl, "URL ảnh") ?? string.Empty;
                vocabulary.Word = Required(vocabulary.Word, "Từ vựng");
                vocabulary.Reading = Required(vocabulary.Reading, "Cách đọc");
                vocabulary.Meaning = Required(vocabulary.Meaning, "Nghĩa");
                vocabulary.AudioUrl = MediaUrl(vocabulary.AudioUrl, "URL âm thanh");
                vocabulary.CertificateLevelCode = level.Code;
                return (vocabulary, $"{vocabulary.Word} / {vocabulary.Reading}");
            case ImportEntityType.Kanji:
                var kanji = Read<KanjiImportPayloadResponse>(input);
                if (kanji.ImageUrl is not null) kanji.ImageUrl = MediaUrl(kanji.ImageUrl, "URL ảnh") ?? string.Empty;
                kanji.Character = Required(kanji.Character, "Hán tự");
                kanji.Meaning = Required(kanji.Meaning, "Nghĩa");
                if (kanji.StrokeCount <= 0) throw new ArgumentException("Số nét phải lớn hơn 0.");
                kanji.AudioUrl = MediaUrl(kanji.AudioUrl, "URL âm thanh");
                kanji.StrokeOrderImageUrl = MediaUrl(kanji.StrokeOrderImageUrl, "URL ảnh thứ tự nét");
                kanji.CertificateLevelCode = level.Code;
                return (kanji, kanji.Character);
            case ImportEntityType.Grammar:
                var grammar = Read<GrammarImportPayloadResponse>(input);
                if (grammar.ImageUrl is not null) grammar.ImageUrl = MediaUrl(grammar.ImageUrl, "URL ảnh") ?? string.Empty;
                grammar.Title = Required(grammar.Title, "Tên mẫu ngữ pháp");
                grammar.Structure = Required(grammar.Structure, "Cấu trúc");
                grammar.CertificateLevelCode = level.Code;
                return (grammar, grammar.Title);
            case ImportEntityType.MockTest:
                var test = Read<MockTestImportPayloadResponse>(input);
                await ValidateMockTestAsync(test, level, ct);
                return (test, $"{test.Title} / {level.Code}");
            case ImportEntityType.PracticeExercise:
                var exercise = Read<PracticeExerciseImportPayloadResponse>(input);
                await ValidateExerciseAsync(exercise, ct);
                var exTarget = !string.IsNullOrEmpty(exercise.LessonTitle)
                    ? exercise.LessonTitle
                    : (!string.IsNullOrEmpty(exercise.TopicTitle) ? exercise.TopicTitle : level.Code);
                return (exercise, $"{exercise.Title} / {exTarget}");
            default:
                throw new ArgumentException("Loại nội dung không được hỗ trợ.");
        }
    }

    public async Task<string?> ValidateImportPayloadAsync(ImportEntityType type, string payloadJson, CancellationToken ct)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            await ValidateAsync(type, document.RootElement, ct);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return ex.Message;
        }
    }

    private async Task ValidateMockTestAsync(MockTestImportPayloadResponse test, CertificateLevel level, CancellationToken ct)
    {
        test.Title = Required(test.Title, "Tên đề");
        test.CertificateLevelCode = level.Code;
        if (test.TimeLimitMinutes <= 0) throw new ArgumentException("Thời lượng phải lớn hơn 0.");

        var skills = await uow.LanguageSkills.Query().Where(x => !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var codes = await uow.CertificateSections.Query().Where(x => !x.IsDeleted && x.CertificateLevelId == level.Id)
            .Select(x => x.Code).ToListAsync(ct);
        if (codes.Count == 0)
            throw new ArgumentException($"Cấp độ {level.Code} chưa có phần thi nào được quản trị viên cấu hình.");
        if (test.Sections is not { Count: > 0 }) throw new ArgumentException("Hãy thêm phần thi.");
        var questionOrder = 0;
        for (var i = 0; i < test.Sections.Count; i++)
        {
            var section = test.Sections[i] ?? throw new ArgumentException("Phần thi không hợp lệ.");
            section.Title = Required(section.Title, "Tên phần thi");
            section.SortOrder = i + 1;
            if (!skills.TryGetValue(section.LanguageSkillId, out var skill))
                throw new ArgumentException($"Phần '{section.Title}' chưa có kỹ năng hợp lệ.");
            section.LanguageSkillName = skill;
            if (!codes.Contains(section.CertificateSectionCode))
                throw new ArgumentException($"Hãy chọn mục thi của cấp độ {level.Code} cho phần '{section.Title}'.");
            if (section.Questions is not { Count: > 0 })
                throw new ArgumentException($"Phần '{section.Title}' chưa có câu hỏi.");

            foreach (var question in section.Questions)
            {
                if (question is null) throw new ArgumentException("Câu hỏi không hợp lệ.");
                question.SortOrder = ++questionOrder;
                question.AudioUrl = MediaUrl(question.AudioUrl, $"URL âm thanh câu {questionOrder}");
                question.ImageUrl = MediaUrl(question.ImageUrl, $"URL ảnh câu {questionOrder}");
                question.QuestionText = question.ImageUrl is null
                    ? Required(question.QuestionText, $"Nội dung câu {questionOrder}")
                    : question.QuestionText?.Trim() ?? string.Empty;
                SetOptionOrder(question.Options, allowImage: true);
            }
        }
        var error = ExcelContentImportService.ValidateMockTestPayload(JsonSerializer.Serialize(test), skills,
            new Dictionary<int, string> { [level.Id] = level.Code });
        if (error is not null) throw new ArgumentException(error);
    }

    private async Task ValidateExerciseAsync(PracticeExerciseImportPayloadResponse exercise, CancellationToken ct)
    {
        exercise.Title = Required(exercise.Title, "Tên bài luyện tập");
        exercise.Scope = Domain.Enums.PracticeExerciseScopes.Normalize(exercise.Scope);
        if (exercise.PassingScorePercent <= 0 || exercise.PassingScorePercent > 100)
            exercise.PassingScorePercent = 70;
        if (exercise.ImageUrl is not null) exercise.ImageUrl = MediaUrl(exercise.ImageUrl, "URL ảnh bài tập") ?? string.Empty;
        exercise.LanguageSkillName = await RequireSkillAsync(exercise.LanguageSkillId, ct);
        if (exercise.LessonId.HasValue)
        {
            exercise.LessonTitle = await uow.Lessons.Query().Where(x => x.Id == exercise.LessonId
                && !x.IsDeleted && x.CertificateLevelId == exercise.CertificateLevelId)
                .Select(x => x.Title).SingleOrDefaultAsync(ct)
                ?? throw new ArgumentException("Bài học không thuộc cấp độ đã chọn.");
        }
        if (exercise.Scope == Domain.Enums.PracticeExerciseScopes.LessonQuickCheck)
        {
            exercise.LessonTitle = Required(exercise.LessonTitle, "Bài học");
        }
        else if (exercise.Scope == Domain.Enums.PracticeExerciseScopes.GroupQuiz)
        {
            exercise.TopicTitle = exercise.TopicId is int tid
                ? await uow.Topics.Query().Where(t => t.Id == tid && !t.IsDeleted).Select(t => t.Title).SingleOrDefaultAsync(ct)
                    ?? throw new ArgumentException("Chặng/Chủ đề không tồn tại.")
                : Required(exercise.TopicTitle, "Chủ đề / Chặng học");
        }
        if (exercise.Questions is not { Count: > 0 }) throw new ArgumentException("Hãy thêm câu hỏi.");
        for (var i = 0; i < exercise.Questions.Count; i++)
        {
            var question = exercise.Questions[i] ?? throw new ArgumentException("Câu hỏi không hợp lệ.");
            question.SortOrder = i + 1;
            question.ImageUrl = MediaUrl(question.ImageUrl, $"URL ảnh câu {question.SortOrder}");
            question.QuestionText = question.ImageUrl is null ? Required(question.QuestionText, $"Nội dung câu {question.SortOrder}") : question.QuestionText?.Trim() ?? string.Empty;
            SetOptionOrder(question.Options, allowImage: true);
            if (question.Options.Count < 2 || question.Options.Count(x => x.IsCorrect) != 1)
                throw new ArgumentException($"Câu {question.SortOrder} cần ít nhất 2 đáp án và đúng 1 đáp án đúng.");
        }
    }

    private static void SetOptionOrder(List<ImportOptionPayloadResponse> options, bool allowImage)
    {
        if (options is null) throw new ArgumentException("Danh sách đáp án không hợp lệ.");
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i] ?? throw new ArgumentException("Đáp án không hợp lệ.");
            option.SortOrder = i + 1;
            option.ImageUrl = allowImage ? MediaUrl(option.ImageUrl, $"URL ảnh đáp án {i + 1}") : null;
            if (!allowImage || string.IsNullOrWhiteSpace(option.ImageUrl))
                option.OptionText = Required(option.OptionText, $"Đáp án {i + 1}");
            else
                option.OptionText ??= string.Empty;
        }
    }

    private async Task<string> RequireSkillAsync(int id, CancellationToken ct) =>
        await uow.LanguageSkills.Query().Where(x => !x.IsDeleted && x.Id == id).Select(x => x.Name)
            .SingleOrDefaultAsync(ct) ?? throw new ArgumentException("Hãy chọn kỹ năng hợp lệ.");

    private async Task<string?> DescribeTopicChangeAsync(int lessonId, string title, CancellationToken ct)
    {
        var previous = await uow.Lessons.Query().Where(x => x.Id == lessonId && x.Topic != null && !x.Topic.IsDeleted)
            .Select(x => x.Topic!.Title).SingleOrDefaultAsync(ct) ?? string.Empty;
        return previous == title ? null : JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["topicTitle"] = $"{previous} → {title}",
        });
    }

    private async Task<int?> ResolveTargetAsync(ImportEntityType type, JsonElement input, CancellationToken ct)
    {
        var fields = Read<ManualIdentity>(input);
        IQueryable<int> ids;
        if (type == ImportEntityType.PracticeExercise)
        {
            var scope = Domain.Enums.PracticeExerciseScopes.Normalize(fields.Scope);
            ids = scope switch
            {
                Domain.Enums.PracticeExerciseScopes.GroupQuiz => uow.PracticeExercises.Query().Where(x =>
                    !x.IsDeleted && x.Scope == Domain.Enums.PracticeExerciseScopes.GroupQuiz && x.Title == fields.Title
                    && x.Topic != null && !x.Topic.IsDeleted && x.Topic.Title == fields.TopicTitle
                    && (fields.CertificateLevelId <= 0 || x.CertificateLevelId == fields.CertificateLevelId)).Select(x => x.Id),
                Domain.Enums.PracticeExerciseScopes.StageAssessment => uow.PracticeExercises.Query().Where(x =>
                    !x.IsDeleted && x.Scope == Domain.Enums.PracticeExerciseScopes.StageAssessment && x.Title == fields.Title
                    && x.CertificateLevelId == fields.CertificateLevelId).Select(x => x.Id),
                _ => uow.PracticeExercises.Query().Where(x =>
                    !x.IsDeleted && x.Scope == Domain.Enums.PracticeExerciseScopes.LessonQuickCheck && x.Title == fields.Title
                    && x.Lesson != null && !x.Lesson.IsDeleted && x.Lesson.Title == fields.LessonTitle
                    && x.Lesson.CertificateLevelId == fields.CertificateLevelId).Select(x => x.Id),
            };
        }
        else
        {
            ids = type switch
            {
                ImportEntityType.Lesson => uow.Lessons.Query().Where(x => !x.IsDeleted
                    && x.Title == fields.Title && x.CertificateLevelId == fields.CertificateLevelId).Select(x => x.Id),
                ImportEntityType.MockTest => uow.MockTests.Query().Where(x => !x.IsDeleted
                    && x.Title == fields.Title && x.CertificateLevelId == fields.CertificateLevelId).Select(x => x.Id),
                ImportEntityType.Kanji => uow.Kanjis.Query().Where(x => !x.IsDeleted && x.Character == fields.Character).Select(x => x.Id),
                ImportEntityType.Vocabulary => uow.Vocabularies.Query().Where(x => !x.IsDeleted
                    && x.Word == fields.Word && x.Reading == fields.Reading).Select(x => x.Id),
                ImportEntityType.Grammar => uow.GrammarPatterns.Query().Where(x => !x.IsDeleted && x.Title == fields.Title).Select(x => x.Id),
                _ => throw new ArgumentException("Loại nội dung không được hỗ trợ.")
            };
        }
        var matches = await ids.Take(2).ToListAsync(ct);
        if (matches.Count > 1) throw new ArgumentException("Có nhiều bản đã xuất bản trùng thông tin. Hãy kiểm tra lại.");
        return matches.Count == 1 ? matches[0] : null;
    }

    private static string? MediaUrl(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (!Helpers.AllowedMediaUrlPolicy.IsApprovedMediaUrl(trimmed))
            throw new ArgumentException($"{label} phải là URL media hợp lệ trên storage được phép.");
        return trimmed;
    }

    internal static string? NormalizeTopicTitle(string? value)
    {
        var title = value?.Trim().Normalize();
        if (title is { Length: > 300 }) throw new ArgumentException("Tên chủ đề tối đa 300 ký tự.");
        return title;
    }

    internal static ManualListInfo? ReadListInfo(string? payload)
    {
        if (payload is null) return null;
        try { return JsonSerializer.Deserialize<ManualListInfo>(payload, Json); }
        catch (JsonException) { return null; }
    }

    private static T Read<T>(JsonElement input) where T : class =>
        input.Deserialize<T>(Json) ?? throw new ArgumentException("Thiếu dữ liệu nội dung.");

    private static string Required(string? value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label} không được để trống.") : value.Trim();

    private sealed record ManualIdentity(string? Title, string? Character, string? Word, string? Reading,
        string? LessonTitle, int CertificateLevelId, string? Scope = null, string? TopicTitle = null);

    internal sealed record ManualListInfo(int? CertificateLevelId, string? TopicTitle, string? Title);
}

public sealed record ManualContentItem(int Id, ImportEntityType EntityType, string RowKey, ContentApprovalStatus Status,
    string SourceFileName, int SourceRowNumber, string? ReviewNote, int? CertificateLevelId, string? TopicTitle,
    DateTime? UpdatedAt = null, DateTime? SubmittedAt = null, DateTime? ReviewedAt = null, string? CertificateLevelCode = null);
public sealed record AuthorImportBatch(Guid BatchId, string SourceFileName, DateTime SavedAt, int ItemCount, int DraftCount,
    int PendingCount, int ApprovedCount, int RejectedCount);
