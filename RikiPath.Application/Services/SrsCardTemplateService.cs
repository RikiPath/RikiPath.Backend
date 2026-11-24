using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.DTOs.SrsTemplates;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public class SrsCardTemplateService(
    IUnitOfWork unitOfWork,
    ISidebarCounterNotifier? sidebarNotifier = null) : ISrsCardTemplateService
{
    public async Task<ApiResponse<List<SrsCardTemplateResponse>>> GetPublishedForLearnerAsync(
        int userId,
        string? jlptLevel,
        string? cardType,
        int? lessonId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(jlptLevel)
            && SrsSchedulingHelper.NormalizeSupportedJlptLevel(jlptLevel) is null)
        {
            return ApiResponse<List<SrsCardTemplateResponse>>.Fail(
                "Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.",
                HttpStatusCode.BadRequest);
        }

        var items = await unitOfWork.SrsCardTemplates.QueryTemplatesAsync(
            ContentStatus.Published,
            jlptLevel,
            cardType,
            submittedByUserId: null,
            search,
            cancellationToken);

        if (lessonId.HasValue)
            items = items.Where(x => x.LessonId == lessonId.Value).ToList();

        var userCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
        var enrolledByTemplateId = userCards
            .Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && c.SrsCardTemplateId.HasValue)
            .GroupBy(c => c.SrsCardTemplateId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var enrolledKeys = userCards
            .Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && !string.IsNullOrEmpty(c.NormalizedContentKey))
            .GroupBy(c => c.NormalizedContentKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        return ApiResponse<List<SrsCardTemplateResponse>>.Success(items.Select(t =>
        {
            var dto = Map(t);
            if (enrolledByTemplateId.TryGetValue(t.Id, out var cid)
                || enrolledKeys.TryGetValue(t.NormalizedContentKey, out cid))
            {
                dto.AlreadyEnrolled = true;
                dto.ExistingReviewCardId = cid;
            }
            return dto;
        }).ToList());
    }

    public async Task<ApiResponse<List<SrsCardTemplateResponse>>> GetMyTemplatesAsync(
        int authorId,
        ContentStatus? status,
        string? cardType = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var items = await unitOfWork.SrsCardTemplates.QueryTemplatesAsync(
            status,
            jlptLevel: null,
            cardType,
            submittedByUserId: authorId,
            search,
            cancellationToken);

        return ApiResponse<List<SrsCardTemplateResponse>>.Success(items.Select(Map).ToList());
    }

    public async Task<ApiResponse<List<SrsCardTemplateResponse>>> GetForAdminAsync(
        ContentStatus? status,
        string? jlptLevel,
        string? cardType,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(jlptLevel)
            && SrsSchedulingHelper.NormalizeSupportedJlptLevel(jlptLevel) is null)
        {
            return ApiResponse<List<SrsCardTemplateResponse>>.Fail(
                "Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.",
                HttpStatusCode.BadRequest);
        }

        var items = await unitOfWork.SrsCardTemplates.QueryTemplatesAsync(
            status,
            jlptLevel,
            cardType,
            submittedByUserId: null,
            search,
            cancellationToken);

        return ApiResponse<List<SrsCardTemplateResponse>>.Success(items.Select(Map).ToList());
    }

    public async Task<ApiResponse<SrsCardTemplateResponse>> CreateDraftAsync(
        int authorId,
        SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ResolveAndValidateTemplateFieldsAsync(request, cancellationToken);
        if (!validation.IsSuccess)
            return ApiResponse<SrsCardTemplateResponse>.Fail(
                validation.ErrorMessage!,
                (HttpStatusCode)validation.StatusCode);

        var data = validation.Result!;
        var duplicateExists = await HasAuthorDuplicateTemplateAsync(
            authorId, data.CardType, data.NormalizedKey, excludeTemplateId: null, cancellationToken);
        if (duplicateExists)
        {
            return ApiResponse<SrsCardTemplateResponse>.Fail(
                "Bạn đã có thẻ mẫu tương tự trong danh sách.",
                HttpStatusCode.Conflict);
        }

        var utcNow = DateTime.UtcNow;
        var entity = new SrsCardTemplate
        {
            CardType = data.CardType,
            FrontText = data.FrontText,
            BackText = data.BackText,
            Reading = data.Reading,
            Meaning = data.Meaning,
            ExampleSentence = data.ExampleSentence,
            ExampleMeaning = data.ExampleMeaning,
            UsageNotes = data.UsageNotes,
            AcceptedReadingsJson = SrsSchedulingHelper.SerializeStringList(data.AcceptedReadings),
            SynonymsJson = SrsSchedulingHelper.SerializeStringList(data.Synonyms),
            AudioUrl = data.AudioUrl,
            JlptLevel = data.JlptLevel,
            CertificateLevelId = data.CertificateLevelId,
            LessonId = data.LessonId,
            VocabularyId = data.VocabularyId,
            KanjiId = data.KanjiId,
            GrammarPatternId = data.GrammarPatternId,
            NormalizedContentKey = data.NormalizedKey,
            Status = ContentStatus.Draft,
            Version = 1,
            SubmittedByUserId = authorId,
            CreatedDate = utcNow,
        };

        await unitOfWork.SrsCardTemplates.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var loaded = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(entity.Id, cancellationToken) ?? entity;
        return ApiResponse<SrsCardTemplateResponse>.Created(Map(loaded));
    }

    public async Task<ApiResponse<SrsCardTemplateResponse>> UpdateDraftAsync(
        int authorId,
        bool isAdmin,
        int templateId,
        SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(templateId, cancellationToken);
        if (entity is null)
            return ApiResponse<SrsCardTemplateResponse>.NotFound("Không tìm thấy thẻ mẫu SRS.");
        if (!isAdmin && entity.SubmittedByUserId != authorId)
            return ApiResponse<SrsCardTemplateResponse>.Fail("Bạn không có quyền chỉnh sửa thẻ mẫu này.", HttpStatusCode.Forbidden);
        if (!isAdmin && entity.Status is ContentStatus.Published or ContentStatus.PendingReview)
            return ApiResponse<SrsCardTemplateResponse>.Fail("Chỉ có thể chỉnh sửa thẻ ở trạng thái Draft, Rejected hoặc Unpublished.", HttpStatusCode.Conflict);

        var validation = await ResolveAndValidateTemplateFieldsAsync(request, cancellationToken);
        if (!validation.IsSuccess)
            return ApiResponse<SrsCardTemplateResponse>.Fail(validation.ErrorMessage!, (HttpStatusCode)validation.StatusCode);

        var data = validation.Result!;
        var ownerId = entity.SubmittedByUserId ?? authorId;
        var duplicateExists = await HasAuthorDuplicateTemplateAsync(
            ownerId, data.CardType, data.NormalizedKey, excludeTemplateId: templateId, cancellationToken);
        if (duplicateExists)
        {
            return ApiResponse<SrsCardTemplateResponse>.Fail(
                "Bạn đã có thẻ mẫu tương tự trong danh sách.",
                HttpStatusCode.Conflict);
        }
        entity.CardType = data.CardType;
        entity.FrontText = data.FrontText;
        entity.BackText = data.BackText;
        entity.Reading = data.Reading;
        entity.Meaning = data.Meaning;
        entity.ExampleSentence = data.ExampleSentence;
        entity.ExampleMeaning = data.ExampleMeaning;
        entity.UsageNotes = data.UsageNotes;
        entity.AcceptedReadingsJson = SrsSchedulingHelper.SerializeStringList(data.AcceptedReadings);
        entity.SynonymsJson = SrsSchedulingHelper.SerializeStringList(data.Synonyms);
        entity.AudioUrl = data.AudioUrl;
        entity.JlptLevel = data.JlptLevel;
        entity.CertificateLevelId = data.CertificateLevelId;
        entity.LessonId = data.LessonId;
        entity.VocabularyId = data.VocabularyId;
        entity.KanjiId = data.KanjiId;
        entity.GrammarPatternId = data.GrammarPatternId;
        entity.NormalizedContentKey = data.NormalizedKey;
        if (!isAdmin)
            entity.Status = ContentStatus.Draft;
        else if (entity.Status == ContentStatus.Published)
            entity.Version++;
        entity.ModifiedDate = DateTime.UtcNow;

        unitOfWork.SrsCardTemplates.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<SrsCardTemplateResponse>.Success(Map(entity));
    }

    public async Task<ApiResponse<SrsCardTemplateResponse>> SubmitForReviewAsync(
        int authorId,
        bool isAdmin,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(templateId, cancellationToken);
        if (entity is null)
            return ApiResponse<SrsCardTemplateResponse>.NotFound("Không tìm thấy thẻ mẫu SRS.");
        if (!isAdmin && entity.SubmittedByUserId != authorId)
            return ApiResponse<SrsCardTemplateResponse>.Fail("Bạn không có quyền gửi duyệt thẻ mẫu này.", HttpStatusCode.Forbidden);
        if (entity.Status is ContentStatus.Published or ContentStatus.PendingReview)
            return ApiResponse<SrsCardTemplateResponse>.Fail("Thẻ đang chờ duyệt hoặc đã xuất bản.", HttpStatusCode.Conflict);

        entity.Status = ContentStatus.PendingReview;
        entity.SubmittedAtUtc = DateTime.UtcNow;
        entity.ModifiedDate = DateTime.UtcNow;
        entity.ReviewNote = null;

        unitOfWork.SrsCardTemplates.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (sidebarNotifier is not null)
            await sidebarNotifier.NotifyAdminsAsync("srs_template_submitted", cancellationToken);
        return ApiResponse<SrsCardTemplateResponse>.Success(Map(entity));
    }

    public async Task<ApiResponse<bool>> DeleteDraftAsync(
        int authorId,
        bool isAdmin,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(templateId, cancellationToken);
        if (entity is null)
            return ApiResponse<bool>.NotFound("Không tìm thấy thẻ mẫu SRS.");
        if (!isAdmin && entity.SubmittedByUserId != authorId)
            return ApiResponse<bool>.Fail("Không có quyền xóa thẻ mẫu này.", HttpStatusCode.Forbidden);
        if (!isAdmin && entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
            return ApiResponse<bool>.Fail("Chỉ có thể xóa thẻ ở trạng thái Draft hoặc Rejected.", HttpStatusCode.Conflict);

        entity.IsDeleted = true;
        entity.ModifiedDate = DateTime.UtcNow;
        unitOfWork.SrsCardTemplates.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<SrsCardTemplateResponse>> ReviewTemplateAsync(
        int adminId,
        int templateId,
        ReviewSrsCardTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewNote))
            return ApiResponse<SrsCardTemplateResponse>.Fail("Vui lòng nhập lý do từ chối thẻ mẫu.", HttpStatusCode.BadRequest);

        SrsCardTemplate? updated = null;
        ApiResponse<SrsCardTemplateResponse>? error = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var entity = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(templateId, token);
            if (entity is null)
            {
                error = ApiResponse<SrsCardTemplateResponse>.NotFound("Không tìm thấy thẻ mẫu SRS.");
                return;
            }

            if (entity.Status != ContentStatus.PendingReview)
            {
                error = ApiResponse<SrsCardTemplateResponse>.Fail("Thẻ mẫu không ở trạng thái chờ duyệt.", HttpStatusCode.Conflict);
                return;
            }

            var utcNow = DateTime.UtcNow;
            var beforeJson = JsonSerializer.Serialize(new { entity.Status, entity.Version });
            entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
            entity.ReviewedByAdminId = adminId;
            entity.ReviewedAtUtc = utcNow;
            entity.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
            if (request.Approve)
            {
                entity.UnpublishedAtUtc = null;
                entity.UnpublishedReason = null;
            }
            entity.ModifiedDate = utcNow;
            unitOfWork.SrsCardTemplates.Update(entity);

            await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = adminId,
                EntityType = nameof(SrsCardTemplate),
                EntityId = entity.Id,
                Action = request.Approve ? "Approve" : "Reject",
                BeforeJson = beforeJson,
                AfterJson = JsonSerializer.Serialize(new { entity.Status, entity.Version }),
                Reason = entity.ReviewNote,
                CreatedAtUtc = utcNow,
            }, token);

            await unitOfWork.SaveChangesAsync(token);
            updated = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(entity.Id, token) ?? entity;
        }, cancellationToken);

        if (error is not null)
            return error;
        if (updated?.SubmittedByUserId is int submitterId && sidebarNotifier is not null)
            await sidebarNotifier.NotifyUsersAsync([submitterId], "srs_template_reviewed", cancellationToken);
        if (sidebarNotifier is not null)
            await sidebarNotifier.NotifyAdminsAsync("srs_template_reviewed", cancellationToken);
        return ApiResponse<SrsCardTemplateResponse>.Success(Map(updated!));
    }

    public async Task<ApiResponse<SrsCardTemplateResponse>> TogglePublishAsync(
        int adminId,
        int templateId,
        bool publish,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!publish && string.IsNullOrWhiteSpace(reason))
            return ApiResponse<SrsCardTemplateResponse>.Fail("Vui lòng nhập lý do hủy xuất bản.");

        SrsCardTemplate? updated = null;
        ApiResponse<SrsCardTemplateResponse>? error = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var entity = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(templateId, token);
            if (entity is null)
            {
                error = ApiResponse<SrsCardTemplateResponse>.NotFound("Không tìm thấy thẻ mẫu SRS.");
                return;
            }

            if (entity.Status is not (ContentStatus.Published or ContentStatus.Unpublished))
            {
                error = ApiResponse<SrsCardTemplateResponse>.Fail(
                    "Chỉ có thể thay đổi trạng thái xuất bản đối với thẻ mẫu đã từng được duyệt.",
                    HttpStatusCode.Conflict);
                return;
            }

            var utcNow = DateTime.UtcNow;
            var beforeJson = JsonSerializer.Serialize(new { entity.Status, entity.Version });

            if (publish)
            {
                if (entity.Status == ContentStatus.Unpublished)
                    entity.Version++;
                entity.Status = ContentStatus.Published;
                entity.UnpublishedAtUtc = null;
                entity.UnpublishedReason = null;
            }
            else
            {
                entity.Status = ContentStatus.Unpublished;
                entity.UnpublishedAtUtc = utcNow;
                entity.UnpublishedReason = reason?.Trim();
            }

            entity.ReviewedByAdminId = adminId;
            entity.ReviewedAtUtc = utcNow;
            entity.ModifiedDate = utcNow;
            unitOfWork.SrsCardTemplates.Update(entity);

            await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = adminId,
                EntityType = nameof(SrsCardTemplate),
                EntityId = entity.Id,
                Action = publish ? "Republish" : "Unpublish",
                BeforeJson = beforeJson,
                AfterJson = JsonSerializer.Serialize(new { entity.Status, entity.Version }),
                Reason = reason?.Trim(),
                CreatedAtUtc = utcNow,
            }, token);

            await unitOfWork.SaveChangesAsync(token);
            updated = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(entity.Id, token) ?? entity;
        }, cancellationToken);

        if (error is not null)
            return error;
        if (updated?.SubmittedByUserId is int submitterId && sidebarNotifier is not null)
            await sidebarNotifier.NotifyUsersAsync([submitterId], "srs_template_reviewed", cancellationToken);
        if (sidebarNotifier is not null)
            await sidebarNotifier.NotifyAdminsAsync("srs_template_reviewed", cancellationToken);
        return ApiResponse<SrsCardTemplateResponse>.Success(Map(updated!));
    }

    private sealed record ResolvedTemplateFields(
        string CardType,
        string FrontText,
        string BackText,
        string? Reading,
        string? Meaning,
        string? ExampleSentence,
        string? ExampleMeaning,
        string? UsageNotes,
        List<string> AcceptedReadings,
        List<string> Synonyms,
        string? AudioUrl,
        string? JlptLevel,
        int? CertificateLevelId,
        int? LessonId,
        int? VocabularyId,
        int? KanjiId,
        int? GrammarPatternId,
        string NormalizedKey);

    private async Task<ApiResponse<ResolvedTemplateFields>> ResolveAndValidateTemplateFieldsAsync(
        SaveSrsCardTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var cardType = NormalizeCardType(request.CardType);
        if (cardType is null)
            return ApiResponse<ResolvedTemplateFields>.Fail("Loại thẻ mẫu không hợp lệ.", HttpStatusCode.BadRequest);

        string? front = request.FrontText?.Trim();
        string? back = request.BackText?.Trim();
        string? reading = request.Reading?.Trim();
        string? meaning = request.Meaning?.Trim();
        string? example = request.ExampleSentence?.Trim();
        string? exampleMeaning = request.ExampleMeaning?.Trim();
        string? usageNotes = request.UsageNotes?.Trim();
        string? audioUrl = request.AudioUrl?.Trim();
        string? jlptLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel);

        if (!string.IsNullOrWhiteSpace(request.JlptLevel) && jlptLevel is null)
            return ApiResponse<ResolvedTemplateFields>.Fail("Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.");

        if (request.VocabularyId.HasValue)
        {
            var vocab = await unitOfWork.Vocabularies.GetPublicByIdAsync(request.VocabularyId.Value, cancellationToken);
            if (vocab is null)
                return ApiResponse<ResolvedTemplateFields>.NotFound("Không tìm thấy từ vựng nguồn đã xuất bản.");
            front ??= vocab.Word;
            reading ??= vocab.Reading;
            meaning ??= vocab.Meaning;
            back ??= vocab.Meaning;
            example ??= vocab.ExampleSentence;
            exampleMeaning ??= vocab.ExampleSentenceMeaning;
            audioUrl ??= vocab.AudioUrl;
            jlptLevel ??= vocab.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null);
        }

        if (request.KanjiId.HasValue)
        {
            var kanji = await unitOfWork.Kanjis.GetPublicByIdAsync(request.KanjiId.Value, cancellationToken);
            if (kanji is null)
                return ApiResponse<ResolvedTemplateFields>.NotFound("Không tìm thấy Kanji nguồn đã xuất bản.");
            front ??= kanji.Character;
            reading ??= string.Join(" · ", new[] { kanji.OnYomi, kanji.KunYomi, kanji.SinoVietnamese }.Where(x => !string.IsNullOrWhiteSpace(x)));
            meaning ??= kanji.Meaning;
            back ??= kanji.Meaning;
            audioUrl ??= kanji.AudioUrl;
            jlptLevel ??= kanji.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null);
        }

        if (request.GrammarPatternId.HasValue)
        {
            var grammar = await unitOfWork.GrammarPatterns.GetPublicByIdAsync(request.GrammarPatternId.Value, cancellationToken);
            if (grammar is null)
                return ApiResponse<ResolvedTemplateFields>.NotFound("Không tìm thấy mẫu ngữ pháp nguồn đã xuất bản.");
            front ??= grammar.Title;
            reading ??= grammar.Structure;
            usageNotes ??= grammar.UsageNotes;
            meaning ??= grammar.UsageNotes ?? grammar.Structure;
            back ??= grammar.UsageNotes ?? grammar.Structure;
            example ??= grammar.ExampleSentence;
            exampleMeaning ??= grammar.ExampleSentenceMeaning;
            jlptLevel ??= grammar.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null);
        }

        if (request.LessonId.HasValue)
        {
            var lesson = await unitOfWork.Lessons.GetByIdAsync(request.LessonId.Value, cancellationToken);
            if (lesson is null || lesson.IsDeleted)
                return ApiResponse<ResolvedTemplateFields>.NotFound("Không tìm thấy bài học liên kết.");
            var level = await unitOfWork.CertificateLevels.GetByIdAsync(lesson.CertificateLevelId, cancellationToken);
            jlptLevel ??= SrsSchedulingHelper.NormalizeSupportedJlptLevel(level?.Code);
        }

        back ??= meaning;
        meaning ??= back;

        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back))
            return ApiResponse<ResolvedTemplateFields>.Fail("Mặt trước (FrontText) và mặt sau/nghĩa (BackText/Meaning) là bắt buộc.");

        int? certLevelId = null;
        if (!string.IsNullOrWhiteSpace(jlptLevel))
        {
            var matchedLevel = await unitOfWork.CertificateLevels.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(l => !l.IsDeleted && l.Code == jlptLevel && l.CertificateType.Name == "JLPT", cancellationToken);
            certLevelId = matchedLevel?.Id;
        }

        var key = SrsSchedulingHelper.BuildNormalizedContentKey(
            cardType, request.VocabularyId, request.KanjiId, request.GrammarPatternId, null, null, front, meaning ?? back);

        return ApiResponse<ResolvedTemplateFields>.Success(new ResolvedTemplateFields(
            cardType, front, back, reading, meaning, example, exampleMeaning, usageNotes,
            request.AcceptedReadings ?? [], request.Synonyms ?? [], audioUrl,
            jlptLevel, certLevelId, request.LessonId, request.VocabularyId, request.KanjiId, request.GrammarPatternId, key));
    }

    private static SrsCardTemplateResponse Map(SrsCardTemplate t) => new()
    {
        Id = t.Id,
        CardType = t.CardType,
        FrontText = t.FrontText,
        BackText = t.BackText,
        Reading = t.Reading,
        Meaning = t.Meaning,
        ExampleSentence = t.ExampleSentence,
        ExampleMeaning = t.ExampleMeaning,
        UsageNotes = t.UsageNotes,
        AcceptedReadings = SrsSchedulingHelper.DeserializeStringList(t.AcceptedReadingsJson),
        Synonyms = SrsSchedulingHelper.DeserializeStringList(t.SynonymsJson),
        AudioUrl = t.AudioUrl,
        JlptLevel = t.JlptLevel,
        CertificateLevelId = t.CertificateLevelId,
        LessonId = t.LessonId,
        VocabularyId = t.VocabularyId,
        KanjiId = t.KanjiId,
        GrammarPatternId = t.GrammarPatternId,
        Status = t.Status,
        Version = t.Version,
        SubmittedByUserId = t.SubmittedByUserId,
        SubmittedByName = t.SubmittedByUser is null ? null : $"{t.SubmittedByUser.FirstName} {t.SubmittedByUser.LastName}".Trim(),
        ReviewedByAdminId = t.ReviewedByAdminId,
        ReviewedByAdminName = t.ReviewedByAdmin is null ? null : $"{t.ReviewedByAdmin.FirstName} {t.ReviewedByAdmin.LastName}".Trim(),
        SubmittedAtUtc = t.SubmittedAtUtc,
        ReviewedAtUtc = t.ReviewedAtUtc,
        ReviewNote = t.ReviewNote,
        UnpublishedAtUtc = t.UnpublishedAtUtc,
        UnpublishedReason = t.UnpublishedReason,
        CreatedDate = t.CreatedDate,
        ModifiedDate = t.ModifiedDate,
    };

    private static string? NormalizeCardType(string? rawCardType)
    {
        if (string.IsNullOrWhiteSpace(rawCardType))
            return "Vocabulary";

        return rawCardType.Trim().ToLowerInvariant() switch
        {
            "vocabulary" => "Vocabulary",
            "kanji" => "Kanji",
            "grammar" => "Grammar",
            "custom" => "Custom",
            _ => null,
        };
    }

    private Task<bool> HasAuthorDuplicateTemplateAsync(
        int authorId,
        string cardType,
        string normalizedKey,
        int? excludeTemplateId,
        CancellationToken cancellationToken)
        => unitOfWork.SrsCardTemplates.Query()
            .AsNoTracking()
            .AnyAsync(
                t => !t.IsDeleted
                    && t.SubmittedByUserId == authorId
                    && (excludeTemplateId == null || t.Id != excludeTemplateId.Value)
                    && t.CardType == cardType
                    && t.NormalizedContentKey == normalizedKey,
                cancellationToken);
}
