using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Common;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>Dịch vụ ngân hàng câu hỏi & nhóm đoạn đọc/nghe thống nhất (hỗ trợ version, dò trùng, nháp và gửi duyệt).</summary>
public sealed class QuestionBankService(
    IUnitOfWork unitOfWork,
    ISidebarCounterNotifier? sidebarNotifier = null) : IQuestionBankService
{
    internal static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<ApiResponse<PagedResult<CanonicalQuestionDto>>> SearchAsync(
        QuestionBankQueryFilter filter, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = unitOfWork.QuestionBankItems.Query()
            .AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Include(q => q.LanguageSkill)
            .Include(q => q.Passage)
            .Where(q => !q.IsDeleted);

        if (!filter.IncludeArchived)
            query = query.Where(q => !q.IsArchivedVersion);

        if (!isAdmin)
        {
            query = filter.OnlyMine
                ? query.Where(q => q.AuthorUserId == actorUserId)
                : query.Where(q => q.PublishStatus == ContentStatus.Published || q.AuthorUserId == actorUserId);
        }
        else if (filter.OnlyMine)
        {
            query = query.Where(q => q.AuthorUserId == actorUserId);
        }

        if (filter.CertificateLevelId.HasValue)
            query = query.Where(q => q.CertificateLevelId == filter.CertificateLevelId.Value);
        if (filter.LanguageSkillId.HasValue)
            query = query.Where(q => q.LanguageSkillId == filter.LanguageSkillId.Value);
        if (!string.IsNullOrWhiteSpace(filter.QuestionType))
            query = query.Where(q => q.QuestionType == filter.QuestionType.Trim());
        if (!string.IsNullOrWhiteSpace(filter.TemplateKind))
            query = query.Where(q => q.TemplateKind == filter.TemplateKind.Trim());
        if (filter.PublishStatus.HasValue)
            query = query.Where(q => q.PublishStatus == filter.PublishStatus.Value);
        if (!string.IsNullOrWhiteSpace(filter.SourceType))
            query = query.Where(q => q.SourceType == filter.SourceType.Trim());
        if (filter.PassageId.HasValue)
            query = query.Where(q => q.PassageId == filter.PassageId.Value);
        if (filter.HasMedia.HasValue)
        {
            query = filter.HasMedia.Value
                ? query.Where(q => q.AudioUrl != null || q.ImageUrl != null || (q.Passage != null && (q.Passage.AudioUrl != null || q.Passage.ImageUrl != null)))
                : query.Where(q => q.AudioUrl == null && q.ImageUrl == null);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(q =>
                q.QuestionText.ToLower().Contains(kw) ||
                (q.Topic != null && q.Topic.ToLower().Contains(kw)) ||
                (q.Explanation != null && q.Explanation.ToLower().Contains(kw)) ||
                (q.Passage != null && q.Passage.Title.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(q => q.ModifiedDate ?? q.CreatedDate)
            .ThenByDescending(q => q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var mapped = items.Select(i => MapToDto(i, includeTranscript: isAdmin || i.AuthorUserId == actorUserId)).ToList();
        return ApiResponse<PagedResult<CanonicalQuestionDto>>.Success(new PagedResult<CanonicalQuestionDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = mapped,
        });
    }

    public async Task<ApiResponse<CanonicalQuestionDto>> GetDetailAsync(
        int id, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var item = await unitOfWork.QuestionBankItems.Query()
            .AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Include(q => q.LanguageSkill)
            .Include(q => q.Passage)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (item is null)
            return ApiResponse<CanonicalQuestionDto>.NotFound("Không tìm thấy câu hỏi.");

        var isOwner = item.AuthorUserId == actorUserId;
        if (!isAdmin && !isOwner && item.PublishStatus != ContentStatus.Published)
            return ApiResponse<CanonicalQuestionDto>.Forbidden("Bạn không có quyền xem câu hỏi chưa xuất bản này.");

        return ApiResponse<CanonicalQuestionDto>.Success(MapToDto(item, includeTranscript: isAdmin || isOwner));
    }

    public async Task<ApiResponse<CanonicalQuestionDto>> SaveDraftAsync(
        CanonicalQuestionDto request, int actorUserId, CancellationToken ct)
    {
        var resolved = await ResolveLevelAndSkillAsync(request, ct);
        if (resolved.Error is not null)
            return ApiResponse<CanonicalQuestionDto>.Fail(resolved.Error, resolved.StatusCode);

        var allowedLevelCodes = await LoadAllowedLevelCodesAsync(ct);
        var validation = CanonicalQuestionValidator.ValidateQuestion(request, forSubmitOrPublish: false, allowedLevelCodes);
        if (!validation.CanSaveDraft)
            return ApiResponse<CanonicalQuestionDto>.Fail(
                string.Join(" ", validation.Errors.Select(e => e.Message)),
                HttpStatusCode.BadRequest,
                errors: validation.Errors.Select(e => $"{e.Field}: {e.Message}").ToList());

        ContentPassage? passage = null;
        if (request.PassageId.HasValue)
        {
            passage = await unitOfWork.ContentPassages.Query()
                .FirstOrDefaultAsync(p => p.Id == request.PassageId.Value && !p.IsDeleted, ct);
            if (passage is null)
                return ApiResponse<CanonicalQuestionDto>.NotFound("Không tìm thấy đoạn đọc/nhóm nghe liên kết.");
        }
        else if (request.Passage is not null && (!string.IsNullOrWhiteSpace(request.Passage.Title) || !string.IsNullOrWhiteSpace(request.Passage.PassageText)))
        {
            request.Passage.CertificateLevelId = request.CertificateLevelId;
            request.Passage.CertificateLevelCode = request.CertificateLevelCode;
            request.Passage.LanguageSkillId = request.LanguageSkillId;
            request.Passage.SkillName = request.SkillName;
            var pVal = CanonicalQuestionValidator.ValidatePassage(request.Passage, forSubmitOrPublish: false);
            if (!pVal.CanSaveDraft)
                return ApiResponse<CanonicalQuestionDto>.Fail(string.Join(" ", pVal.Errors.Select(e => e.Message)));

            passage = BuildPassageEntity(request.Passage, pVal.ContentFingerprint, actorUserId);
            await unitOfWork.ContentPassages.AddAsync(passage, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        QuestionBankItem entity;
        if (request.Id is > 0)
        {
            var existing = await unitOfWork.QuestionBankItems.Query()
                .FirstOrDefaultAsync(q => q.Id == request.Id.Value && !q.IsDeleted, ct);
            if (existing is null)
                return ApiResponse<CanonicalQuestionDto>.NotFound("Không tìm thấy câu hỏi cần cập nhật.");
            if (existing.AuthorUserId.HasValue && existing.AuthorUserId != actorUserId)
                return ApiResponse<CanonicalQuestionDto>.Forbidden("Bạn không thể chỉnh sửa câu hỏi của tác giả khác.");
            if (existing.PublishStatus == ContentStatus.Published)
                return ApiResponse<CanonicalQuestionDto>.Conflict(
                    "Câu hỏi đã xuất bản không được sửa đè trực tiếp để bảo toàn lịch sử bài làm. Hãy tạo phiên bản mới.");
            if (existing.PublishStatus == ContentStatus.PendingReview)
                return ApiResponse<CanonicalQuestionDto>.Conflict(
                    "Câu hỏi đang chờ Quản trị viên xét duyệt. Vui lòng chờ kết quả trước khi chỉnh sửa.");

            entity = existing;
            ApplyQuestionDtoToEntity(entity, request, validation.ContentFingerprint, passage?.Id);
            entity.PublishStatus = ContentStatus.Draft;
            AppendHistory(entity, "UpdateDraft", entity.PublishStatus.ToString(), ContentStatus.Draft.ToString(), actorUserId, "Author", null, false);
            unitOfWork.QuestionBankItems.Update(entity);
        }
        else
        {
            entity = new QuestionBankItem
            {
                QuestionIdentity = Guid.NewGuid(),
                Version = 1,
                PublishStatus = ContentStatus.Draft,
                AuthorUserId = actorUserId,
            };
            ApplyQuestionDtoToEntity(entity, request, validation.ContentFingerprint, passage?.Id);
            AppendHistory(entity, "CreateDraft", null, ContentStatus.Draft.ToString(), actorUserId, "Author", null, false);
            await unitOfWork.QuestionBankItems.AddAsync(entity, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return await GetDetailAsync(entity.Id, actorUserId, isAdmin: false, ct);
    }

    public async Task<ApiResponse<CanonicalQuestionDto>> CreateNewVersionAsync(
        int sourceQuestionId, CanonicalQuestionDto? updates, int actorUserId, CancellationToken ct)
    {
        var source = await unitOfWork.QuestionBankItems.Query()
            .Include(q => q.CertificateLevel)
            .Include(q => q.LanguageSkill)
            .Include(q => q.Passage)
            .FirstOrDefaultAsync(q => q.Id == sourceQuestionId && !q.IsDeleted, ct);
        if (source is null)
            return ApiResponse<CanonicalQuestionDto>.NotFound("Không tìm thấy câu hỏi gốc.");

        var maxVersion = await unitOfWork.QuestionBankItems.Query()
            .Where(q => q.QuestionIdentity == source.QuestionIdentity && !q.IsDeleted)
            .MaxAsync(q => q.Version, ct);

        var hasReplacementContent = updates is not null && !string.IsNullOrWhiteSpace(updates.QuestionText);
        var clonedDto = hasReplacementContent ? updates! : MapToDto(source, includeTranscript: true);
        clonedDto.Id = null;
        clonedDto.CertificateLevelId = clonedDto.CertificateLevelId > 0 ? clonedDto.CertificateLevelId : source.CertificateLevelId;
        clonedDto.LanguageSkillId = clonedDto.LanguageSkillId > 0 ? clonedDto.LanguageSkillId : source.LanguageSkillId;

        var resolved = await ResolveLevelAndSkillAsync(clonedDto, ct);
        if (resolved.Error is not null)
            return ApiResponse<CanonicalQuestionDto>.Fail(resolved.Error, resolved.StatusCode);

        var validation = CanonicalQuestionValidator.ValidateQuestion(clonedDto, forSubmitOrPublish: false, await LoadAllowedLevelCodesAsync(ct));
        if (!validation.CanSaveDraft)
            return ApiResponse<CanonicalQuestionDto>.Fail(string.Join(" ", validation.Errors.Select(e => e.Message)));

        var newVersion = new QuestionBankItem
        {
            QuestionIdentity = source.QuestionIdentity,
            Version = maxVersion + 1,
            ParentQuestionId = source.Id,
            PublishStatus = ContentStatus.Draft,
            AuthorUserId = actorUserId,
        };
        ApplyQuestionDtoToEntity(newVersion, clonedDto, validation.ContentFingerprint, clonedDto.PassageId ?? source.PassageId);
        AppendHistory(newVersion, "CreateVersion", source.PublishStatus.ToString(), ContentStatus.Draft.ToString(), actorUserId, "Author", $"Phiên bản mới từ v{source.Version}", false);

        await unitOfWork.QuestionBankItems.AddAsync(newVersion, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetDetailAsync(newVersion.Id, actorUserId, isAdmin: false, ct);
    }

    public async Task<ApiResponse<CanonicalQuestionDto>> SubmitQuestionAsync(
        int id, int actorUserId, CancellationToken ct)
    {
        var item = await unitOfWork.QuestionBankItems.Query()
            .Include(q => q.CertificateLevel)
            .Include(q => q.LanguageSkill)
            .Include(q => q.Passage)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);
        if (item is null)
            return ApiResponse<CanonicalQuestionDto>.NotFound("Không tìm thấy câu hỏi.");
        if (item.AuthorUserId.HasValue && item.AuthorUserId != actorUserId)
            return ApiResponse<CanonicalQuestionDto>.Forbidden("Bạn chỉ có thể gửi duyệt câu hỏi của chính mình.");
        if (item.PublishStatus == ContentStatus.Published)
            return ApiResponse<CanonicalQuestionDto>.Conflict("Câu hỏi này đã được xuất bản.");

        var dto = MapToDto(item, includeTranscript: true);
        var validation = CanonicalQuestionValidator.ValidateQuestion(dto, forSubmitOrPublish: true, await LoadAllowedLevelCodesAsync(ct));
        if (!validation.CanPublish)
            return ApiResponse<CanonicalQuestionDto>.Fail(
                string.Join(" ", validation.Errors.Select(e => e.Message)),
                HttpStatusCode.BadRequest,
                errors: validation.Errors.Select(e => $"{e.Field}: {e.Message}").ToList());

        var prev = item.PublishStatus.ToString();
        item.PublishStatus = ContentStatus.PendingReview;
        item.SubmittedAt = DateTime.UtcNow;
        AppendHistory(item, "Submit", prev, ContentStatus.PendingReview.ToString(), actorUserId, "Author", null, false);

        var approval = new ContentApprovalRequest
        {
            BatchId = Guid.NewGuid(),
            EntityType = ImportEntityType.QuestionBank,
            Action = item.ParentQuestionId.HasValue ? ImportRowAction.Modified : ImportRowAction.New,
            TargetEntityId = item.Id,
            RowKey = $"{dto.CertificateLevelCode} · {dto.SkillName} · v{item.Version}",
            PayloadJson = JsonSerializer.Serialize(dto, JsonOpts),
            SourceRowNumber = 1,
            SourceFileName = item.SourceType == "Manual" ? "Manual Question Editor" : item.SourceType,
            Status = ContentApprovalStatus.PendingReview,
            SubmittedByUserId = actorUserId,
            SubmittedAt = item.SubmittedAt.Value,
            SourceType = item.SourceType,
            SourceReference = item.SourceReference,
            RightsStatus = item.RightsStatus,
            CertificateLevelId = item.CertificateLevelId,
            LanguageSkillId = item.LanguageSkillId,
            AiProvenanceJson = item.AiProvenanceJson,
            ValidationSummaryJson = JsonSerializer.Serialize(new { isValid = true, warnings = validation.Warnings }, JsonOpts),
        };

        await unitOfWork.ContentApprovalRequests.AddAsync(approval, ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (sidebarNotifier is not null)
        {
            await sidebarNotifier.NotifyAdminsAsync("question_submitted", ct);
            await sidebarNotifier.NotifyUsersAsync([actorUserId], "question_submitted", ct);
        }

        return await GetDetailAsync(item.Id, actorUserId, isAdmin: false, ct);
    }

    public async Task<ApiResponse> DeleteDraftAsync(int id, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var item = await unitOfWork.QuestionBankItems.Query()
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);
        if (item is null)
            return ApiResponse.NotFound("Không tìm thấy câu hỏi.");
        if (!isAdmin && item.AuthorUserId != actorUserId)
            return ApiResponse.Forbidden("Không có quyền xóa câu hỏi này.");
        if (item.PublishStatus == ContentStatus.Published)
            return ApiResponse.Conflict("Không thể xóa câu hỏi đã xuất bản. Quản trị viên có thể chuyển sang trạng thái Ngừng xuất bản.");

        item.IsDeleted = true;
        unitOfWork.QuestionBankItems.Update(item);
        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse.Success();
    }

    public async Task<ApiResponse<DuplicateCheckResponseDto>> CheckDuplicatesAsync(
        CanonicalQuestionDto candidate, int? excludeId, CancellationToken ct)
    {
        var resolved = await ResolveLevelAndSkillAsync(candidate, ct);
        if (resolved.Error is not null)
            return ApiResponse<DuplicateCheckResponseDto>.Fail(resolved.Error, resolved.StatusCode);

        var fingerprint = CanonicalQuestionValidator.ComputeQuestionFingerprint(candidate);
        var existing = await unitOfWork.QuestionBankItems.Query()
            .AsNoTracking()
            .Include(q => q.CertificateLevel)
            .Include(q => q.LanguageSkill)
            .Where(q => !q.IsDeleted && !q.IsArchivedVersion &&
                        (!excludeId.HasValue || q.Id != excludeId.Value) &&
                        (q.ContentFingerprint == fingerprint ||
                         (q.CertificateLevelId == candidate.CertificateLevelId && q.LanguageSkillId == candidate.LanguageSkillId)))
            .Take(200)
            .ToListAsync(ct);

        var matches = new List<DuplicateMatchDto>();
        foreach (var item in existing)
        {
            if (item.ContentFingerprint == fingerprint)
            {
                matches.Add(new DuplicateMatchDto
                {
                    QuestionId = item.Id,
                    QuestionIdentity = item.QuestionIdentity,
                    Version = item.Version,
                    PublishStatus = item.PublishStatus,
                    MatchKind = "ExactFingerprint",
                    SimilarityScore = 1.0,
                    QuestionText = item.QuestionText,
                    SkillName = item.LanguageSkill?.Name ?? string.Empty,
                    LevelCode = item.CertificateLevel?.Code ?? string.Empty,
                });
                continue;
            }

            var score = CanonicalQuestionValidator.ComputeSimilarityScore(candidate.QuestionText, item.QuestionText);
            if (score >= 0.70)
            {
                matches.Add(new DuplicateMatchDto
                {
                    QuestionId = item.Id,
                    QuestionIdentity = item.QuestionIdentity,
                    Version = item.Version,
                    PublishStatus = item.PublishStatus,
                    MatchKind = "NearDuplicate",
                    SimilarityScore = score,
                    QuestionText = item.QuestionText,
                    SkillName = item.LanguageSkill?.Name ?? string.Empty,
                    LevelCode = item.CertificateLevel?.Code ?? string.Empty,
                });
            }
        }

        return ApiResponse<DuplicateCheckResponseDto>.Success(new DuplicateCheckResponseDto
        {
            Fingerprint = fingerprint,
            Matches = matches.OrderByDescending(m => m.SimilarityScore).Take(10).ToList(),
        });
    }

    public async Task<ApiResponse<List<CanonicalPassageDto>>> ListPassagesAsync(
        int? levelId, int? skillId, ContentStatus? status, string? keyword,
        int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var query = unitOfWork.ContentPassages.Query()
            .AsNoTracking()
            .Include(p => p.CertificateLevel)
            .Include(p => p.LanguageSkill)
            .Where(p => !p.IsDeleted && !p.IsArchivedVersion);

        if (!isAdmin)
            query = query.Where(p => p.PublishStatus == ContentStatus.Published || p.AuthorUserId == actorUserId);
        if (levelId.HasValue)
            query = query.Where(p => p.CertificateLevelId == levelId.Value);
        if (skillId.HasValue)
            query = query.Where(p => p.LanguageSkillId == skillId.Value);
        if (status.HasValue)
            query = query.Where(p => p.PublishStatus == status.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(kw) || (p.Topic != null && p.Topic.ToLower().Contains(kw)));
        }

        var rows = await query.OrderByDescending(p => p.ModifiedDate ?? p.CreatedDate).Take(100).ToListAsync(ct);
        return ApiResponse<List<CanonicalPassageDto>>.Success(rows
            .Select(p => MapPassageToDto(p, isAdmin || p.AuthorUserId == actorUserId))
            .ToList());
    }

    public async Task<ApiResponse<CanonicalPassageWithQuestionsDto>> GetPassageGroupDetailAsync(
        int passageId, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var passage = await unitOfWork.ContentPassages.Query()
            .AsNoTracking()
            .Include(p => p.CertificateLevel)
            .Include(p => p.LanguageSkill)
            .Include(p => p.Questions!.Where(q => !q.IsDeleted))
                .ThenInclude(q => q.CertificateLevel)
            .Include(p => p.Questions!.Where(q => !q.IsDeleted))
                .ThenInclude(q => q.LanguageSkill)
            .FirstOrDefaultAsync(p => p.Id == passageId && !p.IsDeleted, ct);

        if (passage is null)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.NotFound("Không tìm thấy đoạn đọc hoặc nhóm nghe.");

        var isOwner = passage.AuthorUserId == actorUserId;
        if (!isAdmin && !isOwner && passage.PublishStatus != ContentStatus.Published)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Forbidden("Không có quyền truy cập nội dung chưa xuất bản.");

        var includeTranscript = isAdmin || isOwner;
        var questions = (passage.Questions ?? [])
            .OrderBy(q => q.GroupSortOrder).ThenBy(q => q.Id)
            .Select(q => MapToDto(q, includeTranscript))
            .ToList();

        return ApiResponse<CanonicalPassageWithQuestionsDto>.Success(new CanonicalPassageWithQuestionsDto
        {
            Passage = MapPassageToDto(passage, includeTranscript),
            Questions = questions,
            QuestionGroupTitle = questions.FirstOrDefault()?.QuestionGroupTitle,
        });
    }

    public async Task<ApiResponse<CanonicalPassageWithQuestionsDto>> SavePassageGroupDraftAsync(
        CanonicalPassageWithQuestionsDto request, int actorUserId, CancellationToken ct)
    {
        var levelSkill = await ResolvePassageLevelAndSkillAsync(request.Passage, ct);
        if (levelSkill.Error is not null)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail(levelSkill.Error, levelSkill.StatusCode);

        var pVal = CanonicalQuestionValidator.ValidatePassage(request.Passage, forSubmitOrPublish: false);
        if (!pVal.CanSaveDraft)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail(string.Join(" ", pVal.Errors.Select(e => e.Message)));

        if (request.Questions is null || request.Questions.Count == 0)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail("Nhóm câu hỏi phải có ít nhất 1 câu hỏi.");

        var allowedLevels = await LoadAllowedLevelCodesAsync(ct);
        for (var i = 0; i < request.Questions.Count; i++)
        {
            var q = request.Questions[i];
            q.CertificateLevelId = request.Passage.CertificateLevelId;
            q.CertificateLevelCode = request.Passage.CertificateLevelCode;
            q.LanguageSkillId = request.Passage.LanguageSkillId;
            q.SkillName = request.Passage.SkillName;
            q.Passage = request.Passage;
            if (string.IsNullOrWhiteSpace(q.AudioUrl) && !string.IsNullOrWhiteSpace(request.Passage.AudioUrl))
                q.AudioUrl = request.Passage.AudioUrl;
            var qVal = CanonicalQuestionValidator.ValidateQuestion(q, forSubmitOrPublish: false, allowedLevels, i + 1);
            if (!qVal.CanSaveDraft)
                return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail(
                    $"Câu #{i + 1}: {string.Join(" ", qVal.Errors.Select(e => e.Message))}");
        }

        ContentPassage passageEntity = null!;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (request.Passage.Id is > 0)
            {
                passageEntity = await unitOfWork.ContentPassages.Query()
                    .FirstOrDefaultAsync(p => p.Id == request.Passage.Id.Value && !p.IsDeleted, token)
                    ?? throw new InvalidOperationException("Không tìm thấy nhóm/đoạn.");
                if (passageEntity.PublishStatus == ContentStatus.Published)
                    throw new InvalidOperationException("Nội dung đã xuất bản không được ghi đè trực tiếp.");
                UpdatePassageEntity(passageEntity, request.Passage, pVal.ContentFingerprint);
                passageEntity.PublishStatus = ContentStatus.Draft;
                unitOfWork.ContentPassages.Update(passageEntity);
            }
            else
            {
                passageEntity = BuildPassageEntity(request.Passage, pVal.ContentFingerprint, actorUserId);
                await unitOfWork.ContentPassages.AddAsync(passageEntity, token);
                await unitOfWork.SaveChangesAsync(token);
            }

            var existingChildren = await unitOfWork.QuestionBankItems.Query()
                .Where(q => q.PassageId == passageEntity.Id && !q.IsDeleted)
                .ToListAsync(token);
            var retainedIds = new HashSet<int>();

            for (var i = 0; i < request.Questions.Count; i++)
            {
                var qDto = request.Questions[i];
                var qVal = CanonicalQuestionValidator.ValidateQuestion(qDto, false, allowedLevels, i + 1);
                QuestionBankItem qEntity;
                if (qDto.Id is > 0 && existingChildren.FirstOrDefault(x => x.Id == qDto.Id.Value) is { } match)
                {
                    qEntity = match;
                    retainedIds.Add(match.Id);
                    ApplyQuestionDtoToEntity(qEntity, qDto, qVal.ContentFingerprint, passageEntity.Id);
                    qEntity.GroupSortOrder = i + 1;
                    qEntity.QuestionGroupTitle ??= request.QuestionGroupTitle;
                    qEntity.PublishStatus = ContentStatus.Draft;
                    unitOfWork.QuestionBankItems.Update(qEntity);
                }
                else
                {
                    qEntity = new QuestionBankItem
                    {
                        QuestionIdentity = Guid.NewGuid(),
                        Version = 1,
                        PublishStatus = ContentStatus.Draft,
                        AuthorUserId = actorUserId,
                    };
                    ApplyQuestionDtoToEntity(qEntity, qDto, qVal.ContentFingerprint, passageEntity.Id);
                    qEntity.GroupSortOrder = i + 1;
                    qEntity.QuestionGroupTitle ??= request.QuestionGroupTitle;
                    await unitOfWork.QuestionBankItems.AddAsync(qEntity, token);
                }
            }

            foreach (var removed in existingChildren.Where(c => !retainedIds.Contains(c.Id)))
            {
                removed.IsDeleted = true;
                unitOfWork.QuestionBankItems.Update(removed);
            }

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        return await GetPassageGroupDetailAsync(passageEntity.Id, actorUserId, isAdmin: false, ct);
    }

    public async Task<ApiResponse<CanonicalPassageWithQuestionsDto>> SubmitPassageGroupAsync(
        int passageId, int actorUserId, CancellationToken ct)
    {
        var passage = await unitOfWork.ContentPassages.Query()
            .Include(p => p.CertificateLevel)
            .Include(p => p.LanguageSkill)
            .Include(p => p.Questions!.Where(q => !q.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == passageId && !p.IsDeleted, ct);
        if (passage is null)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.NotFound("Không tìm thấy nhóm/đoạn.");
        if (passage.AuthorUserId.HasValue && passage.AuthorUserId != actorUserId)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Forbidden("Chỉ tác giả sở hữu mới có thể gửi duyệt.");

        var passageDto = MapPassageToDto(passage, includeTranscript: true);
        var pVal = CanonicalQuestionValidator.ValidatePassage(passageDto, forSubmitOrPublish: true);
        if (!pVal.CanPublish)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail(string.Join(" ", pVal.Errors.Select(e => e.Message)));

        var childQuestions = (passage.Questions ?? []).OrderBy(q => q.GroupSortOrder).ThenBy(q => q.Id).ToList();
        if (childQuestions.Count == 0)
            return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail("Nhóm/đoạn cần có ít nhất 1 câu hỏi trước khi gửi duyệt.");

        var allowed = await LoadAllowedLevelCodesAsync(ct);
        var mappedQuestions = new List<CanonicalQuestionDto>();
        foreach (var child in childQuestions)
        {
            child.CertificateLevel = passage.CertificateLevel;
            child.LanguageSkill = passage.LanguageSkill;
            child.Passage = passage;
            var qDto = MapToDto(child, includeTranscript: true);
            var qVal = CanonicalQuestionValidator.ValidateQuestion(qDto, forSubmitOrPublish: true, allowed);
            if (!qVal.CanPublish)
                return ApiResponse<CanonicalPassageWithQuestionsDto>.Fail(string.Join(" ", qVal.Errors.Select(e => e.Message)));
            mappedQuestions.Add(qDto);
        }

        var now = DateTime.UtcNow;
        passage.PublishStatus = ContentStatus.PendingReview;
        passage.SubmittedAt = now;
        foreach (var child in childQuestions)
        {
            child.PublishStatus = ContentStatus.PendingReview;
            child.SubmittedAt = now;
        }

        var payload = new CanonicalPassageWithQuestionsDto
        {
            Passage = passageDto,
            Questions = mappedQuestions,
            QuestionGroupTitle = mappedQuestions.FirstOrDefault()?.QuestionGroupTitle,
        };

        var approval = new ContentApprovalRequest
        {
            BatchId = Guid.NewGuid(),
            EntityType = ImportEntityType.Passage,
            Action = passage.ParentPassageId.HasValue ? ImportRowAction.Modified : ImportRowAction.New,
            TargetEntityId = passage.Id,
            RowKey = $"{passageDto.CertificateLevelCode} · {passageDto.SkillName} · {passage.Title}",
            PayloadJson = JsonSerializer.Serialize(payload, JsonOpts),
            SourceRowNumber = 1,
            SourceFileName = passage.SourceType == "Manual" ? "Manual Group Editor" : passage.SourceType,
            Status = ContentApprovalStatus.PendingReview,
            SubmittedByUserId = actorUserId,
            SubmittedAt = now,
            SourceType = passage.SourceType,
            SourceReference = passage.SourceReference,
            RightsStatus = passage.RightsStatus,
            CertificateLevelId = passage.CertificateLevelId,
            LanguageSkillId = passage.LanguageSkillId,
            AiProvenanceJson = passage.AiProvenanceJson,
        };

        await unitOfWork.ContentApprovalRequests.AddAsync(approval, ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (sidebarNotifier is not null)
        {
            await sidebarNotifier.NotifyAdminsAsync("passage_submitted", ct);
            await sidebarNotifier.NotifyUsersAsync([actorUserId], "passage_submitted", ct);
        }

        return await GetPassageGroupDetailAsync(passage.Id, actorUserId, isAdmin: false, ct);
    }

    internal static CanonicalQuestionDto MapToDto(QuestionBankItem e, bool includeTranscript)
    {
        var meta = DeserializeOrDefault<CanonicalSkillMetadataDto>(e.SkillMetadataJson);
        var options = DeserializeOrDefault<List<CanonicalOptionDto>>(e.OptionsJson) ?? [];
        var playback = DeserializeOrDefault<ListeningPlaybackPolicyDto>(e.PlaybackPolicyJson);
        var aiProv = DeserializeOrDefault<AiProvenanceDto>(e.AiProvenanceJson);
        var tags = DeserializeOrDefault<List<string>>(e.TagsJson) ?? [];

        return new CanonicalQuestionDto
        {
            Id = e.Id,
            QuestionIdentity = e.QuestionIdentity,
            Version = e.Version,
            ParentQuestionId = e.ParentQuestionId,
            IsArchivedVersion = e.IsArchivedVersion,
            CertificateLevelId = e.CertificateLevelId,
            CertificateLevelCode = e.CertificateLevel?.Code ?? string.Empty,
            LanguageSkillId = e.LanguageSkillId,
            SkillName = e.LanguageSkill?.Name ?? string.Empty,
            PassageId = e.PassageId,
            PassageCode = e.Passage?.PassageCode,
            Passage = e.Passage is null ? null : MapPassageToDto(e.Passage, includeTranscript),
            QuestionGroupTitle = e.QuestionGroupTitle,
            GroupSortOrder = e.GroupSortOrder,
            TemplateKind = e.TemplateKind,
            QuestionType = e.QuestionType,
            QuestionText = e.QuestionText,
            SkillMetadata = meta,
            Options = options,
            AudioUrl = e.AudioUrl ?? e.Passage?.AudioUrl,
            AudioDurationSeconds = e.AudioDurationSeconds ?? e.Passage?.AudioDurationSeconds,
            AudioTranscript = includeTranscript ? (e.AudioTranscript ?? e.Passage?.AudioTranscript) : null,
            PlaybackPolicy = playback ?? DeserializeOrDefault<ListeningPlaybackPolicyDto>(e.Passage?.PlaybackPolicyJson),
            ImageUrl = e.ImageUrl ?? e.Passage?.ImageUrl,
            Explanation = e.Explanation,
            Difficulty = e.Difficulty,
            Topic = e.Topic,
            Tags = tags,
            SourceType = e.SourceType,
            SourceReference = e.SourceReference,
            RightsStatus = e.RightsStatus,
            AiProvenance = aiProv,
            PublishStatus = e.PublishStatus,
            ContentFingerprint = e.ContentFingerprint,
            ReviewNote = e.ReviewNote,
            ReviewedByName = e.ReviewedByName,
            ReviewedAt = e.ReviewedAt,
        };
    }

    internal static CanonicalPassageDto MapPassageToDto(ContentPassage p, bool includeTranscript) => new()
    {
        Id = p.Id,
        PassageIdentity = p.PassageIdentity,
        PassageCode = p.PassageCode,
        CertificateLevelId = p.CertificateLevelId,
        CertificateLevelCode = p.CertificateLevel?.Code ?? string.Empty,
        LanguageSkillId = p.LanguageSkillId,
        SkillName = p.LanguageSkill?.Name ?? string.Empty,
        Title = p.Title,
        Topic = p.Topic,
        PassageText = p.PassageText,
        Instructions = p.Instructions,
        ListeningSection = p.ListeningSection,
        AudioUrl = p.AudioUrl,
        AudioDurationSeconds = p.AudioDurationSeconds,
        AudioTranscript = includeTranscript ? p.AudioTranscript : null,
        ImageUrl = p.ImageUrl,
        PlaybackPolicy = DeserializeOrDefault<ListeningPlaybackPolicyDto>(p.PlaybackPolicyJson),
        SourceType = p.SourceType,
        SourceReference = p.SourceReference,
        RightsStatus = p.RightsStatus,
        AiProvenance = DeserializeOrDefault<AiProvenanceDto>(p.AiProvenanceJson),
        PublishStatus = p.PublishStatus,
        Version = p.Version,
    };

    private async Task<(string? Error, HttpStatusCode StatusCode)> ResolveLevelAndSkillAsync(
        CanonicalQuestionDto dto, CancellationToken ct)
    {
        var levels = await unitOfWork.CertificateLevels.Query().Where(l => !l.IsDeleted).ToListAsync(ct);
        var level = dto.CertificateLevelId > 0
            ? levels.FirstOrDefault(l => l.Id == dto.CertificateLevelId)
            : levels.FirstOrDefault(l => l.Code.Equals(dto.CertificateLevelCode?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (level is null)
            return ("Không tìm thấy cấp độ JLPT hợp lệ.", HttpStatusCode.BadRequest);

        dto.CertificateLevelId = level.Id;
        dto.CertificateLevelCode = level.Code;

        var skills = await unitOfWork.LanguageSkills.Query().Where(s => !s.IsDeleted).ToListAsync(ct);
        var normalizedSkill = CanonicalQuestionValidator.NormalizeSkillName(dto.SkillName);
        var skill = dto.LanguageSkillId > 0
            ? skills.FirstOrDefault(s => s.Id == dto.LanguageSkillId)
            : skills.FirstOrDefault(s => CanonicalQuestionValidator.NormalizeSkillName(s.Name).Equals(normalizedSkill, StringComparison.OrdinalIgnoreCase));
        if (skill is null)
            return ("Không tìm thấy kỹ năng JLPT hợp lệ.", HttpStatusCode.BadRequest);

        dto.LanguageSkillId = skill.Id;
        dto.SkillName = skill.Name;
        return (null, HttpStatusCode.OK);
    }

    private async Task<(string? Error, HttpStatusCode StatusCode)> ResolvePassageLevelAndSkillAsync(
        CanonicalPassageDto dto, CancellationToken ct)
    {
        var temp = new CanonicalQuestionDto
        {
            CertificateLevelId = dto.CertificateLevelId,
            CertificateLevelCode = dto.CertificateLevelCode,
            LanguageSkillId = dto.LanguageSkillId,
            SkillName = dto.SkillName,
        };
        var res = await ResolveLevelAndSkillAsync(temp, ct);
        dto.CertificateLevelId = temp.CertificateLevelId;
        dto.CertificateLevelCode = temp.CertificateLevelCode;
        dto.LanguageSkillId = temp.LanguageSkillId;
        dto.SkillName = temp.SkillName;
        return res;
    }

    private async Task<HashSet<string>> LoadAllowedLevelCodesAsync(CancellationToken ct)
    {
        var codes = await unitOfWork.CertificateLevels.Query()
            .AsNoTracking()
            .Where(l => !l.IsDeleted && l.Code != null)
            .Select(l => l.Code.Trim().ToUpper())
            .ToListAsync(ct);
        return codes.Where(c => CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal static void ApplyQuestionDtoToEntity(
        QuestionBankItem e, CanonicalQuestionDto dto, string fingerprint, int? passageId)
    {
        e.CertificateLevelId = dto.CertificateLevelId;
        e.LanguageSkillId = dto.LanguageSkillId;
        e.PassageId = passageId ?? dto.PassageId;
        e.QuestionGroupTitle = dto.QuestionGroupTitle?.Trim();
        e.GroupSortOrder = dto.GroupSortOrder;
        e.TemplateKind = string.IsNullOrWhiteSpace(dto.TemplateKind)
            ? CanonicalQuestionValidator.InferDefaultTemplateKind(dto.SkillName, dto.QuestionType)
            : dto.TemplateKind.Trim();
        e.QuestionType = dto.QuestionType.Trim();
        e.QuestionText = dto.QuestionText.Trim();
        e.SkillMetadataJson = dto.SkillMetadata is null ? null : JsonSerializer.Serialize(dto.SkillMetadata, JsonOpts);
        e.InteractionDataJson = BuildInteractionDataJson(dto);
        e.AnswerKeyJson = BuildAnswerKeyJson(dto);
        e.OptionsJson = dto.Options is { Count: > 0 }
            ? JsonSerializer.Serialize(dto.Options.Select((o, idx) => new CanonicalOptionDto
            {
                SortOrder = o.SortOrder > 0 ? o.SortOrder : idx + 1,
                OptionText = o.OptionText.Trim(),
                ImageUrl = o.ImageUrl?.Trim(),
                IsCorrect = o.IsCorrect,
            }), JsonOpts)
            : null;
        e.AudioUrl = dto.AudioUrl?.Trim();
        e.AudioDurationSeconds = dto.AudioDurationSeconds;
        e.AudioTranscript = dto.AudioTranscript?.Trim();
        e.PlaybackPolicyJson = dto.PlaybackPolicy is null ? null : JsonSerializer.Serialize(dto.PlaybackPolicy, JsonOpts);
        e.ImageUrl = dto.ImageUrl?.Trim();
        e.Explanation = dto.Explanation?.Trim();
        e.Difficulty = dto.Difficulty?.Trim();
        e.Topic = dto.Topic?.Trim();
        e.TagsJson = dto.Tags is { Count: > 0 } ? JsonSerializer.Serialize(dto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList(), JsonOpts) : null;
        e.SourceType = string.IsNullOrWhiteSpace(dto.SourceType) ? "Manual" : dto.SourceType.Trim();
        e.SourceReference = dto.SourceReference?.Trim();
        e.RightsStatus = dto.RightsStatus?.Trim();
        e.AiProvenanceJson = dto.AiProvenance is null ? null : JsonSerializer.Serialize(dto.AiProvenance, JsonOpts);
        e.ContentFingerprint = fingerprint;
    }

    internal static string? BuildInteractionDataJson(CanonicalQuestionDto dto)
    {
        if (dto.QuestionType.Equals(CanonicalQuestionTypes.Ordering, StringComparison.OrdinalIgnoreCase) &&
            dto.SkillMetadata?.OrderingTokens is { Count: > 0 })
        {
            return JsonSerializer.Serialize(new
            {
                tokens = dto.SkillMetadata.OrderingTokens,
                starIndex = dto.SkillMetadata.StarIndexZeroBased,
            }, JsonOpts);
        }
        if (dto.QuestionType.Equals(CanonicalQuestionTypes.FillBlank, StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                caseSensitive = dto.SkillMetadata?.CaseSensitive ?? false,
                normalizeKana = dto.SkillMetadata?.NormalizeKana ?? true,
            }, JsonOpts);
        }
        return dto.PlaybackPolicy is not null ? JsonSerializer.Serialize(new { playbackPolicy = dto.PlaybackPolicy }, JsonOpts) : null;
    }

    internal static string? BuildAnswerKeyJson(CanonicalQuestionDto dto)
    {
        if (dto.QuestionType.Equals(CanonicalQuestionTypes.Ordering, StringComparison.OrdinalIgnoreCase) &&
            dto.SkillMetadata?.OrderingAnswer is { Count: > 0 })
        {
            return JsonSerializer.Serialize(dto.SkillMetadata.OrderingAnswer, JsonOpts);
        }
        if (dto.QuestionType.Equals(CanonicalQuestionTypes.FillBlank, StringComparison.OrdinalIgnoreCase) &&
            dto.SkillMetadata?.AcceptedAnswers is { Count: > 0 })
        {
            return JsonSerializer.Serialize(new { acceptedAnswers = dto.SkillMetadata.AcceptedAnswers }, JsonOpts);
        }
        if (dto.QuestionType.Equals(CanonicalQuestionTypes.MultipleChoice, StringComparison.OrdinalIgnoreCase))
        {
            var correctIdx = dto.Options.Select((o, idx) => (o, idx)).Where(x => x.o.IsCorrect).Select(x => x.idx).ToList();
            return JsonSerializer.Serialize(new { correctOptionIndexes = correctIdx }, JsonOpts);
        }
        return null;
    }

    internal static ContentPassage BuildPassageEntity(CanonicalPassageDto dto, string fingerprint, int authorId) => new()
    {
        PassageIdentity = Guid.NewGuid(),
        PassageCode = string.IsNullOrWhiteSpace(dto.PassageCode) ? $"PSG-{Guid.NewGuid():N}"[..12] : dto.PassageCode.Trim(),
        CertificateLevelId = dto.CertificateLevelId,
        LanguageSkillId = dto.LanguageSkillId,
        Title = dto.Title.Trim(),
        Topic = dto.Topic?.Trim(),
        PassageText = dto.PassageText?.Trim(),
        Instructions = dto.Instructions?.Trim(),
        ListeningSection = dto.ListeningSection?.Trim(),
        AudioUrl = dto.AudioUrl?.Trim(),
        AudioDurationSeconds = dto.AudioDurationSeconds,
        AudioTranscript = dto.AudioTranscript?.Trim(),
        ImageUrl = dto.ImageUrl?.Trim(),
        PlaybackPolicyJson = dto.PlaybackPolicy is null ? null : JsonSerializer.Serialize(dto.PlaybackPolicy, JsonOpts),
        SourceType = string.IsNullOrWhiteSpace(dto.SourceType) ? "Manual" : dto.SourceType.Trim(),
        SourceReference = dto.SourceReference?.Trim(),
        RightsStatus = dto.RightsStatus?.Trim(),
        AiProvenanceJson = dto.AiProvenance is null ? null : JsonSerializer.Serialize(dto.AiProvenance, JsonOpts),
        ContentFingerprint = fingerprint,
        AuthorUserId = authorId,
    };

    private static void UpdatePassageEntity(ContentPassage e, CanonicalPassageDto dto, string fingerprint)
    {
        e.Title = dto.Title.Trim();
        e.Topic = dto.Topic?.Trim();
        e.PassageText = dto.PassageText?.Trim();
        e.Instructions = dto.Instructions?.Trim();
        e.ListeningSection = dto.ListeningSection?.Trim();
        e.AudioUrl = dto.AudioUrl?.Trim();
        e.AudioDurationSeconds = dto.AudioDurationSeconds;
        e.AudioTranscript = dto.AudioTranscript?.Trim();
        e.ImageUrl = dto.ImageUrl?.Trim();
        e.PlaybackPolicyJson = dto.PlaybackPolicy is null ? null : JsonSerializer.Serialize(dto.PlaybackPolicy, JsonOpts);
        e.SourceReference = dto.SourceReference?.Trim();
        e.RightsStatus = dto.RightsStatus?.Trim();
        e.ContentFingerprint = fingerprint;
    }

    internal static void AppendHistory(
        QuestionBankItem item, string action, string? fromStatus, string toStatus,
        int actorId, string actorName, string? note, bool rightsConfirmed)
    {
        var history = DeserializeOrDefault<List<ReviewHistoryEntryDto>>(item.ReviewHistoryJson) ?? [];
        history.Add(new ReviewHistoryEntryDto
        {
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorId,
            ActorName = actorName,
            AtUtc = DateTime.UtcNow,
            Note = note,
            RightsConfirmed = rightsConfirmed,
        });
        item.ReviewHistoryJson = JsonSerializer.Serialize(history, JsonOpts);
    }

    private static T? DeserializeOrDefault<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
