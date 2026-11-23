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

public class ContentPublishService(IUnitOfWork unitOfWork) : IContentPublishService
{
    public async Task<ApiResponse<List<AdminPublishCatalogItemResponse>>> GetCatalogAsync(
        string? entityType,
        ContentStatus? status,
        string? jlptLevel,
        string? search,
        CancellationToken cancellationToken = default)
    {
        string? normLevel = null;
        if (!string.IsNullOrWhiteSpace(jlptLevel))
        {
            normLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(jlptLevel);
            if (normLevel is null)
                return ApiResponse<List<AdminPublishCatalogItemResponse>>.Fail("Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.", HttpStatusCode.BadRequest);
        }

        var typeFilter = string.IsNullOrWhiteSpace(entityType) ? "Vocabulary" : entityType.Trim();
        var term = search?.Trim().ToLower();
        var items = new List<AdminPublishCatalogItemResponse>();

        if (string.Equals(typeFilter, "Lesson", StringComparison.OrdinalIgnoreCase))
        {
            var lessons = await unitOfWork.Lessons.Query()
                .AsNoTracking()
                .Include(l => l.CertificateLevel)
                .Where(l => !l.IsDeleted
                    && (status == null || l.Status == status.Value)
                    && (normLevel == null || l.CertificateLevel.Code == normLevel)
                    && (term == null || l.Title.ToLower().Contains(term)))
                .OrderByDescending(l => l.ModifiedDate ?? l.CreatedDate)
                .Take(60)
                .ToListAsync(cancellationToken);

            items.AddRange(lessons.Select(l => new AdminPublishCatalogItemResponse
            {
                EntityType = "Lesson",
                EntityId = l.Id,
                Title = l.Title,
                Subtitle = l.Description,
                JlptLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(l.CertificateLevel?.Code),
                Status = l.Status,
                Version = l.Version,
                UnpublishedAtUtc = l.UnpublishedAtUtc,
                UnpublishedReason = l.UnpublishedReason,
            }));
        }
        else if (string.Equals(typeFilter, "Kanji", StringComparison.OrdinalIgnoreCase))
        {
            var kanjis = await unitOfWork.Kanjis.Query()
                .AsNoTracking()
                .Include(k => k.ContentLevelMappings!.Where(m => !m.IsDeleted))
                    .ThenInclude(m => m.CertificateLevel)
                .Where(k => !k.IsDeleted
                    && (status == null || k.Status == status.Value)
                    && (normLevel == null || (k.ContentLevelMappings != null && k.ContentLevelMappings.Any(m => !m.IsDeleted && m.CertificateLevel.Code == normLevel)))
                    && (term == null || k.Character.Contains(search!.Trim()) || k.Meaning.ToLower().Contains(term)))
                .OrderByDescending(k => k.ModifiedDate ?? k.CreatedDate)
                .Take(60)
                .ToListAsync(cancellationToken);

            items.AddRange(kanjis.Select(k => new AdminPublishCatalogItemResponse
            {
                EntityType = "Kanji",
                EntityId = k.Id,
                Title = k.Character,
                Subtitle = $"{k.Meaning} ({k.OnYomi ?? k.KunYomi ?? k.SinoVietnamese})",
                JlptLevel = k.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null),
                Status = k.Status,
                Version = k.Version,
                UnpublishedAtUtc = k.UnpublishedAtUtc,
                UnpublishedReason = k.UnpublishedReason,
            }));
        }
        else if (string.Equals(typeFilter, "Grammar", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeFilter, "GrammarPattern", StringComparison.OrdinalIgnoreCase))
        {
            var grammars = await unitOfWork.GrammarPatterns.Query()
                .AsNoTracking()
                .Include(g => g.ContentLevelMappings!.Where(m => !m.IsDeleted))
                    .ThenInclude(m => m.CertificateLevel)
                .Where(g => !g.IsDeleted
                    && (status == null || g.Status == status.Value)
                    && (normLevel == null || (g.ContentLevelMappings != null && g.ContentLevelMappings.Any(m => !m.IsDeleted && m.CertificateLevel.Code == normLevel)))
                    && (term == null || g.Title.ToLower().Contains(term) || g.Structure.ToLower().Contains(term)))
                .OrderByDescending(g => g.ModifiedDate ?? g.CreatedDate)
                .Take(60)
                .ToListAsync(cancellationToken);

            items.AddRange(grammars.Select(g => new AdminPublishCatalogItemResponse
            {
                EntityType = "GrammarPattern",
                EntityId = g.Id,
                Title = g.Title,
                Subtitle = g.Structure,
                JlptLevel = g.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null),
                Status = g.Status,
                Version = g.Version,
                UnpublishedAtUtc = g.UnpublishedAtUtc,
                UnpublishedReason = g.UnpublishedReason,
            }));
        }
        else if (string.Equals(typeFilter, "SrsCardTemplate", StringComparison.OrdinalIgnoreCase))
        {
            var templates = await unitOfWork.SrsCardTemplates.QueryTemplatesAsync(
                status, normLevel, cardType: null, submittedByUserId: null, search, cancellationToken);
            var visibleTemplates = templates.Take(60).ToList();
            var templateIds = visibleTemplates.Select(t => t.Id).ToList();
            var enrolledCounts = templateIds.Count == 0
                ? new Dictionary<int, int>()
                : await unitOfWork.ReviewCards.Query()
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted
                        && c.Status != SrsCardStatus.Archived
                        && c.SrsCardTemplateId != null
                        && templateIds.Contains(c.SrsCardTemplateId.Value))
                    .GroupBy(c => c.SrsCardTemplateId!.Value)
                    .Select(g => new { TemplateId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.TemplateId, x => x.Count, cancellationToken);

            items.AddRange(visibleTemplates.Select(t => new AdminPublishCatalogItemResponse
            {
                EntityType = "SrsCardTemplate",
                EntityId = t.Id,
                Title = t.FrontText,
                Subtitle = t.Meaning ?? t.BackText,
                JlptLevel = t.JlptLevel,
                Status = t.Status,
                Version = t.Version,
                UnpublishedAtUtc = t.UnpublishedAtUtc,
                UnpublishedReason = t.UnpublishedReason,
                EnrolledLearnerCount = enrolledCounts.GetValueOrDefault(t.Id, 0),
            }));
        }
        else
        {
            var vocabs = await unitOfWork.Vocabularies.Query()
                .AsNoTracking()
                .Include(v => v.ContentLevelMappings!.Where(m => !m.IsDeleted))
                    .ThenInclude(m => m.CertificateLevel)
                .Where(v => !v.IsDeleted
                    && (status == null || v.Status == status.Value)
                    && (normLevel == null || (v.ContentLevelMappings != null && v.ContentLevelMappings.Any(m => !m.IsDeleted && m.CertificateLevel.Code == normLevel)))
                    && (term == null || v.Word.ToLower().Contains(term) || v.Reading.ToLower().Contains(term) || v.Meaning.ToLower().Contains(term)))
                .OrderByDescending(v => v.ModifiedDate ?? v.CreatedDate)
                .Take(60)
                .ToListAsync(cancellationToken);

            items.AddRange(vocabs.Select(v => new AdminPublishCatalogItemResponse
            {
                EntityType = "Vocabulary",
                EntityId = v.Id,
                Title = v.Word,
                Subtitle = $"{v.Reading} · {v.Meaning}",
                JlptLevel = v.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null),
                Status = v.Status,
                Version = v.Version,
                UnpublishedAtUtc = v.UnpublishedAtUtc,
                UnpublishedReason = v.UnpublishedReason,
            }));
        }

        return ApiResponse<List<AdminPublishCatalogItemResponse>>.Success(items);
    }

    public async Task<ApiResponse<AdminPublishCatalogItemResponse>> TogglePublishStateAsync(
        int adminId,
        TogglePublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Publish && string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<AdminPublishCatalogItemResponse>.Fail("Vui lòng nhập lý do hủy xuất bản.", HttpStatusCode.BadRequest);

        AdminPublishCatalogItemResponse? updated = null;
        ApiResponse<AdminPublishCatalogItemResponse>? error = null;
        var utcNow = DateTime.UtcNow;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var t = request.EntityType.Trim();
            if (string.Equals(t, "SrsCardTemplate", StringComparison.OrdinalIgnoreCase))
            {
                var e = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(request.EntityId, token);
                if (e is null) { error = ApiResponse<AdminPublishCatalogItemResponse>.NotFound("Không tìm thấy thẻ mẫu SRS."); return; }
                if (e.Status is not (ContentStatus.Published or ContentStatus.Unpublished))
                {
                    error = ApiResponse<AdminPublishCatalogItemResponse>.Fail("Chỉ có thể thay đổi trạng thái xuất bản đối với thẻ mẫu đã từng được duyệt.", HttpStatusCode.Conflict);
                    return;
                }
                var before = JsonSerializer.Serialize(new { e.Status, e.Version });
                ApplyState(request.Publish, request.Reason, utcNow, s => e.Status = s, () => e.Status, v => e.Version = v, () => e.Version, dt => e.UnpublishedAtUtc = dt, r => e.UnpublishedReason = r);
                e.ModifiedDate = utcNow;
                unitOfWork.SrsCardTemplates.Update(e);
                await AddAuditAsync(adminId, "SrsCardTemplate", e.Id, request.Publish, before, e.Status, e.Version, request.Reason, utcNow, token);
                await unitOfWork.SaveChangesAsync(token);
                var enrolledCount = await unitOfWork.ReviewCards.Query()
                    .AsNoTracking()
                    .CountAsync(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && c.SrsCardTemplateId == e.Id, token);
                updated = new AdminPublishCatalogItemResponse { EntityType = "SrsCardTemplate", EntityId = e.Id, Title = e.FrontText, Subtitle = e.Meaning ?? e.BackText, JlptLevel = e.JlptLevel, Status = e.Status, Version = e.Version, UnpublishedAtUtc = e.UnpublishedAtUtc, UnpublishedReason = e.UnpublishedReason, EnrolledLearnerCount = enrolledCount };
                return;
            }

            if (string.Equals(t, "Lesson", StringComparison.OrdinalIgnoreCase))
            {
                var e = await unitOfWork.Lessons.GetByIdAsync(request.EntityId, token);
                if (e is null || e.IsDeleted) { error = ApiResponse<AdminPublishCatalogItemResponse>.NotFound("Không tìm thấy bài học."); return; }
                var before = JsonSerializer.Serialize(new { e.Status, e.Version });
                ApplyState(request.Publish, request.Reason, utcNow, s => e.Status = s, () => e.Status, v => e.Version = v, () => e.Version, dt => e.UnpublishedAtUtc = dt, r => e.UnpublishedReason = r);
                e.ModifiedDate = utcNow;
                unitOfWork.Lessons.Update(e);
                await AddAuditAsync(adminId, "Lesson", e.Id, request.Publish, before, e.Status, e.Version, request.Reason, utcNow, token);
                await unitOfWork.SaveChangesAsync(token);
                updated = new AdminPublishCatalogItemResponse { EntityType = "Lesson", EntityId = e.Id, Title = e.Title, Subtitle = e.Description, Status = e.Status, Version = e.Version, UnpublishedAtUtc = e.UnpublishedAtUtc, UnpublishedReason = e.UnpublishedReason };
                return;
            }

            if (string.Equals(t, "Vocabulary", StringComparison.OrdinalIgnoreCase))
            {
                var e = await unitOfWork.Vocabularies.GetByIdAsync(request.EntityId, token);
                if (e is null || e.IsDeleted) { error = ApiResponse<AdminPublishCatalogItemResponse>.NotFound("Không tìm thấy từ vựng."); return; }
                var before = JsonSerializer.Serialize(new { e.Status, e.Version });
                ApplyState(request.Publish, request.Reason, utcNow, s => e.Status = s, () => e.Status, v => e.Version = v, () => e.Version, dt => e.UnpublishedAtUtc = dt, r => e.UnpublishedReason = r);
                e.ModifiedDate = utcNow;
                unitOfWork.Vocabularies.Update(e);
                await AddAuditAsync(adminId, "Vocabulary", e.Id, request.Publish, before, e.Status, e.Version, request.Reason, utcNow, token);
                await unitOfWork.SaveChangesAsync(token);
                updated = new AdminPublishCatalogItemResponse { EntityType = "Vocabulary", EntityId = e.Id, Title = e.Word, Subtitle = e.Meaning, Status = e.Status, Version = e.Version, UnpublishedAtUtc = e.UnpublishedAtUtc, UnpublishedReason = e.UnpublishedReason };
                return;
            }

            if (string.Equals(t, "Kanji", StringComparison.OrdinalIgnoreCase))
            {
                var e = await unitOfWork.Kanjis.GetByIdAsync(request.EntityId, token);
                if (e is null || e.IsDeleted) { error = ApiResponse<AdminPublishCatalogItemResponse>.NotFound("Không tìm thấy chữ Hán."); return; }
                var before = JsonSerializer.Serialize(new { e.Status, e.Version });
                ApplyState(request.Publish, request.Reason, utcNow, s => e.Status = s, () => e.Status, v => e.Version = v, () => e.Version, dt => e.UnpublishedAtUtc = dt, r => e.UnpublishedReason = r);
                e.ModifiedDate = utcNow;
                unitOfWork.Kanjis.Update(e);
                await AddAuditAsync(adminId, "Kanji", e.Id, request.Publish, before, e.Status, e.Version, request.Reason, utcNow, token);
                await unitOfWork.SaveChangesAsync(token);
                updated = new AdminPublishCatalogItemResponse { EntityType = "Kanji", EntityId = e.Id, Title = e.Character, Subtitle = e.Meaning, Status = e.Status, Version = e.Version, UnpublishedAtUtc = e.UnpublishedAtUtc, UnpublishedReason = e.UnpublishedReason };
                return;
            }

            if (string.Equals(t, "GrammarPattern", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "Grammar", StringComparison.OrdinalIgnoreCase))
            {
                var e = await unitOfWork.GrammarPatterns.GetByIdAsync(request.EntityId, token);
                if (e is null || e.IsDeleted) { error = ApiResponse<AdminPublishCatalogItemResponse>.NotFound("Không tìm thấy mẫu ngữ pháp."); return; }
                var before = JsonSerializer.Serialize(new { e.Status, e.Version });
                ApplyState(request.Publish, request.Reason, utcNow, s => e.Status = s, () => e.Status, v => e.Version = v, () => e.Version, dt => e.UnpublishedAtUtc = dt, r => e.UnpublishedReason = r);
                e.ModifiedDate = utcNow;
                unitOfWork.GrammarPatterns.Update(e);
                await AddAuditAsync(adminId, "GrammarPattern", e.Id, request.Publish, before, e.Status, e.Version, request.Reason, utcNow, token);
                await unitOfWork.SaveChangesAsync(token);
                updated = new AdminPublishCatalogItemResponse { EntityType = "GrammarPattern", EntityId = e.Id, Title = e.Title, Subtitle = e.Structure, Status = e.Status, Version = e.Version, UnpublishedAtUtc = e.UnpublishedAtUtc, UnpublishedReason = e.UnpublishedReason };
                return;
            }

            error = ApiResponse<AdminPublishCatalogItemResponse>.Fail("Loại học liệu không hợp lệ.");
        }, cancellationToken);

        return error ?? ApiResponse<AdminPublishCatalogItemResponse>.Success(updated!);
    }

    private static void ApplyState(
        bool publish,
        string? reason,
        DateTime utcNow,
        Action<ContentStatus> setStatus,
        Func<ContentStatus> getStatus,
        Action<int> setVersion,
        Func<int> getVersion,
        Action<DateTime?> setUnpubAt,
        Action<string?> setUnpubReason)
    {
        if (publish)
        {
            if (getStatus() == ContentStatus.Unpublished)
                setVersion(getVersion() + 1);
            setStatus(ContentStatus.Published);
            setUnpubAt(null);
            setUnpubReason(null);
        }
        else
        {
            setStatus(ContentStatus.Unpublished);
            setUnpubAt(utcNow);
            setUnpubReason(reason?.Trim());
        }
    }

    private async Task AddAuditAsync(
        int adminId,
        string entityType,
        int entityId,
        bool publish,
        string beforeJson,
        ContentStatus afterStatus,
        int afterVersion,
        string? reason,
        DateTime utcNow,
        CancellationToken ct)
    {
        await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
        {
            ActorUserId = adminId,
            EntityType = entityType,
            EntityId = entityId,
            Action = publish ? "Republish" : "Unpublish",
            BeforeJson = beforeJson,
            AfterJson = JsonSerializer.Serialize(new { Status = afterStatus, Version = afterVersion }),
            Reason = reason?.Trim(),
            CreatedAtUtc = utcNow,
        }, ct);
    }
}
