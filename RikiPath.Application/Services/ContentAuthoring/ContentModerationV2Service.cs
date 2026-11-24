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

/// <summary>Kiểm duyệt, yêu cầu chỉnh sửa, phê duyệt, xuất bản có xác nhận bản quyền và ngừng xuất bản (lưu AdminAuditLog).</summary>
public sealed class ContentModerationV2Service(
    IUnitOfWork unitOfWork,
    ISidebarCounterNotifier? sidebarNotifier = null) : IContentModerationV2Service
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ApiResponse<PagedResult<ModerationQueueItemDto>>> ListQueueAsync(
        ContentApprovalStatus? status, ImportEntityType? entityType, string? keyword,
        int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = unitOfWork.ContentApprovalRequests.Query()
            .AsNoTracking()
            .Include(r => r.SubmittedBy)
            .Where(r => !r.IsDeleted && r.Status != ContentApprovalStatus.Draft);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (entityType.HasValue)
            query = query.Where(r => r.EntityType == entityType.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(r => r.RowKey.ToLower().Contains(kw) ||
                (r.SourceFileName != null && r.SourceFileName.ToLower().Contains(kw)) ||
                (r.SubmittedBy != null && (((r.SubmittedBy.FirstName ?? "") + " " + (r.SubmittedBy.LastName ?? "")).ToLower().Contains(kw) || (r.SubmittedBy.Email != null && r.SubmittedBy.Email.ToLower().Contains(kw)))));
        }

        var levels = (await unitOfWork.CertificateLevels.Query().AsNoTracking().Where(l => !l.IsDeleted).ToListAsync(ct))
            .ToDictionary(l => l.Id, l => l.Code);
        var skills = (await unitOfWork.LanguageSkills.Query().AsNoTracking().Where(s => !s.IsDeleted).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => s.Name);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(r => r.SubmittedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<ModerationQueueItemDto>>.Success(new PagedResult<ModerationQueueItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(r => MapQueueItem(r, levels, skills)).ToList(),
        });
    }

    public async Task<ApiResponse<ModerationDetailDto>> GetDetailAsync(int requestId, CancellationToken ct)
    {
        var req = await unitOfWork.ContentApprovalRequests.Query()
            .AsNoTracking()
            .Include(r => r.SubmittedBy)
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, ct);
        if (req is null)
            return ApiResponse<ModerationDetailDto>.NotFound("Không tìm thấy yêu cầu kiểm duyệt.");

        var levels = (await unitOfWork.CertificateLevels.Query().AsNoTracking().Where(l => !l.IsDeleted).ToListAsync(ct))
            .ToDictionary(l => l.Id, l => l.Code);
        var skills = (await unitOfWork.LanguageSkills.Query().AsNoTracking().Where(s => !s.IsDeleted).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => s.Name);

        object? structured = null;
        ContentStatus? publishStatus = null;
        int? version = null;

        if (req.EntityType == ImportEntityType.QuestionBank && req.TargetEntityId is > 0)
        {
            var q = await unitOfWork.QuestionBankItems.Query().AsNoTracking()
                .Include(x => x.CertificateLevel).Include(x => x.LanguageSkill).Include(x => x.Passage)
                .FirstOrDefaultAsync(x => x.Id == req.TargetEntityId.Value && !x.IsDeleted, ct);
            if (q is not null)
            {
                structured = QuestionBankService.MapToDto(q, includeTranscript: true);
                publishStatus = q.PublishStatus;
                version = q.Version;
            }
        }
        else if (req.EntityType == ImportEntityType.Passage && req.TargetEntityId is > 0)
        {
            var p = await unitOfWork.ContentPassages.Query().AsNoTracking()
                .Include(x => x.CertificateLevel).Include(x => x.LanguageSkill)
                .Include(x => x.Questions!.Where(q => !q.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == req.TargetEntityId.Value && !x.IsDeleted, ct);
            if (p is not null)
            {
                structured = new CanonicalPassageWithQuestionsDto
                {
                    Passage = QuestionBankService.MapPassageToDto(p, includeTranscript: true),
                    Questions = (p.Questions ?? []).OrderBy(q => q.GroupSortOrder).Select(q => QuestionBankService.MapToDto(q, true)).ToList(),
                };
                publishStatus = p.PublishStatus;
                version = p.Version;
            }
        }
        else if (req.EntityType is ImportEntityType.MockTest or ImportEntityType.PracticeSet && req.TargetEntityId is > 0)
        {
            var t = await unitOfWork.MockTests.Query().AsNoTracking()
                .Include(x => x.CertificateLevel)
                .Include(x => x.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.LanguageSkill)
                .Include(x => x.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.CertificateSection)
                .Include(x => x.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
                .Include(x => x.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Passage)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == req.TargetEntityId.Value && !x.IsDeleted, ct);
            if (t is not null)
            {
                publishStatus = t.PublishStatus;
                version = t.Version;
                structured = TryDeserialize<object>(req.PayloadJson);
            }
        }

        structured ??= TryDeserialize<object>(req.PayloadJson);

        return ApiResponse<ModerationDetailDto>.Success(new ModerationDetailDto
        {
            Summary = MapQueueItem(req, levels, skills),
            PayloadJson = req.PayloadJson,
            StructuredPreview = structured,
            ValidationSummaryJson = req.ValidationSummaryJson,
            ReviewHistory = TryDeserialize<List<ReviewHistoryEntryDto>>(req.ReviewHistoryJson) ?? [],
            CurrentEntityPublishStatus = publishStatus,
            CurrentEntityVersion = version,
        });
    }

    public async Task<ApiResponse<ModerationDetailDto>> ModerateAsync(
        int requestId, ModerateContentV2Request request, int adminUserId, string adminName, CancellationToken ct)
    {
        var req = await unitOfWork.ContentApprovalRequests.Query()
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, ct);
        if (req is null)
            return ApiResponse<ModerationDetailDto>.NotFound("Không tìm thấy yêu cầu kiểm duyệt.");

        if (request.Decision is ModerationDecision.RequestChanges or ModerationDecision.Reject &&
            string.IsNullOrWhiteSpace(request.ReviewNote))
        {
            return ApiResponse<ModerationDetailDto>.Fail("Vui lòng nhập lý do/ghi chú khi yêu cầu chỉnh sửa hoặc từ chối.");
        }

        var wantsPublish = request.Decision is ModerationDecision.ApproveAndPublish or ModerationDecision.Publish;
        if (wantsPublish && !request.RightsConfirmed && !req.RightsConfirmed)
        {
            return ApiResponse<ModerationDetailDto>.Fail(
                "Bắt buộc xác nhận bản quyền/nguồn gốc hợp lệ (Rights Confirmed) trước khi xuất bản nội dung.",
                HttpStatusCode.BadRequest);
        }

        if (wantsPublish && JlptBlueprintPolicy.IsRightsBlocked(req.RightsStatus))
        {
            return ApiResponse<ModerationDetailDto>.Fail(
                $"Không thể xuất bản: yêu cầu kiểm duyệt mang trạng thái bản quyền '{JlptBlueprintPolicy.NormalizeRightsStatus(req.RightsStatus)}' (PUBLISH_BLOCKED_RIGHTS).",
                HttpStatusCode.BadRequest,
                "PUBLISH_BLOCKED_RIGHTS");
        }

        var allowedLevelCodes = (await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Where(l => !l.IsDeleted && l.Code != null)
            .Select(l => l.Code.ToUpper())
            .ToListAsync(ct))
            .Where(CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prevStatus = req.Status;
        var now = DateTime.UtcNow;
        ApiResponse<ModerationDetailDto>? blockedFailure = null;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var newStatus = request.Decision switch
            {
                ModerationDecision.Approve => ContentApprovalStatus.Approved,
                ModerationDecision.ApproveAndPublish => ContentApprovalStatus.Approved,
                ModerationDecision.Publish => ContentApprovalStatus.Approved,
                ModerationDecision.RequestChanges => ContentApprovalStatus.ChangesRequested,
                ModerationDecision.Reject => ContentApprovalStatus.Rejected,
                _ => throw new InvalidOperationException("Quyết định kiểm duyệt không hợp lệ.")
            };

            var targetPublishStatus = request.Decision switch
            {
                ModerationDecision.Approve => ContentStatus.PendingReview, // Đã duyệt nội dung, sẵn sàng Publish
                ModerationDecision.ApproveAndPublish or ModerationDecision.Publish => ContentStatus.Published,
                ModerationDecision.RequestChanges => ContentStatus.ChangesRequested,
                ModerationDecision.Reject => ContentStatus.Rejected,
                _ => ContentStatus.Draft
            };

            // Revalidate strictly on server before Approve / Publish
            if (req.EntityType == ImportEntityType.QuestionBank && req.TargetEntityId is > 0)
            {
                var q = await unitOfWork.QuestionBankItems.Query()
                    .Include(x => x.CertificateLevel).Include(x => x.LanguageSkill).Include(x => x.Passage)
                    .FirstOrDefaultAsync(x => x.Id == req.TargetEntityId.Value && !x.IsDeleted, token);
                if (q is null)
                {
                    blockedFailure = ApiResponse<ModerationDetailDto>.NotFound("Không tìm thấy câu hỏi mục tiêu.");
                    return;
                }

                if (wantsPublish)
                {
                    var rightsCheck = JlptBlueprintPolicy.CheckPublishRights(req.RightsStatus, q.RightsStatus, q.Passage?.RightsStatus);
                    if (rightsCheck.Blocked)
                    {
                        blockedFailure = ApiResponse<ModerationDetailDto>.Fail(rightsCheck.Message!, HttpStatusCode.BadRequest, "PUBLISH_BLOCKED_RIGHTS");
                        return;
                    }
                }

                if (newStatus == ContentApprovalStatus.Approved)
                {
                    var qDto = QuestionBankService.MapToDto(q, includeTranscript: true);
                    var val = CanonicalQuestionValidator.ValidateQuestion(qDto, forSubmitOrPublish: true, allowedLevelCodes);
                    if (!val.CanPublish)
                    {
                        var errCode = val.Errors.FirstOrDefault(e => e.Code == "PUBLISH_BLOCKED_RIGHTS")?.Code;
                        blockedFailure = ApiResponse<ModerationDetailDto>.Fail(string.Join(" ", val.Errors.Select(e => e.Message)), HttpStatusCode.BadRequest, errCode);
                        return;
                    }
                }

                var prevEntityStatus = q.PublishStatus.ToString();
                q.PublishStatus = targetPublishStatus;
                q.ReviewedAt = now;
                q.ReviewedByName = adminName;
                q.ReviewNote = request.ReviewNote?.Trim();
                if (targetPublishStatus == ContentStatus.Published)
                {
                    q.PublishedAt = now;
                    await ArchivePreviousQuestionVersionAsync(q.ParentQuestionId, token);
                }
                QuestionBankService.AppendHistory(q, request.Decision.ToString(), prevEntityStatus, targetPublishStatus.ToString(), adminUserId, adminName, request.ReviewNote, request.RightsConfirmed);
                unitOfWork.QuestionBankItems.Update(q);
            }
            else if (req.EntityType == ImportEntityType.Passage && req.TargetEntityId is > 0)
            {
                var p = await unitOfWork.ContentPassages.Query()
                    .Include(x => x.CertificateLevel).Include(x => x.LanguageSkill)
                    .Include(x => x.Questions!.Where(q => !q.IsDeleted))
                    .FirstOrDefaultAsync(x => x.Id == req.TargetEntityId.Value && !x.IsDeleted, token);
                if (p is null)
                {
                    blockedFailure = ApiResponse<ModerationDetailDto>.NotFound("Không tìm thấy nhóm/đoạn đọc mục tiêu.");
                    return;
                }

                if (wantsPublish)
                {
                    var childStatuses = (p.Questions ?? []).Select(c => c.RightsStatus).ToArray();
                    var rightsCheck = JlptBlueprintPolicy.CheckPublishRights([req.RightsStatus, p.RightsStatus, .. childStatuses]);
                    if (rightsCheck.Blocked)
                    {
                        blockedFailure = ApiResponse<ModerationDetailDto>.Fail(rightsCheck.Message!, HttpStatusCode.BadRequest, "PUBLISH_BLOCKED_RIGHTS");
                        return;
                    }
                }

                if (newStatus == ContentApprovalStatus.Approved)
                {
                    var pVal = CanonicalQuestionValidator.ValidatePassage(QuestionBankService.MapPassageToDto(p, true), true);
                    if (!pVal.CanPublish)
                    {
                        blockedFailure = ApiResponse<ModerationDetailDto>.Fail(string.Join(" ", pVal.Errors.Select(e => e.Message)), HttpStatusCode.BadRequest);
                        return;
                    }

                    foreach (var child in p.Questions ?? [])
                    {
                        child.CertificateLevel = p.CertificateLevel;
                        child.LanguageSkill = p.LanguageSkill;
                        child.Passage = p;
                        var qVal = CanonicalQuestionValidator.ValidateQuestion(QuestionBankService.MapToDto(child, true), true, allowedLevelCodes);
                        if (!qVal.CanPublish)
                        {
                            var errCode = qVal.Errors.FirstOrDefault(e => e.Code == "PUBLISH_BLOCKED_RIGHTS")?.Code;
                            blockedFailure = ApiResponse<ModerationDetailDto>.Fail(string.Join(" ", qVal.Errors.Select(e => e.Message)), HttpStatusCode.BadRequest, errCode);
                            return;
                        }
                    }
                }

                p.PublishStatus = targetPublishStatus;
                p.ReviewedAt = now;
                p.ReviewedByName = adminName;
                p.ReviewNote = request.ReviewNote?.Trim();
                if (targetPublishStatus == ContentStatus.Published) p.PublishedAt = now;
                foreach (var child in p.Questions ?? [])
                {
                    child.PublishStatus = targetPublishStatus;
                    child.ReviewedAt = now;
                    child.ReviewedByName = adminName;
                    child.ReviewNote = request.ReviewNote?.Trim();
                    if (targetPublishStatus == ContentStatus.Published) child.PublishedAt = now;
                }
                unitOfWork.ContentPassages.Update(p);
            }
            else if (req.EntityType is ImportEntityType.MockTest or ImportEntityType.PracticeSet && req.TargetEntityId is > 0)
            {
                var test = await unitOfWork.MockTests.Query()
                    .Include(t => t.CertificateLevel)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.LanguageSkill)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.CertificateSection)
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted)).ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted)).ThenInclude(q => q.Passage)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(t => t.Id == req.TargetEntityId.Value && !t.IsDeleted, token);
                if (test is null)
                {
                    blockedFailure = ApiResponse<ModerationDetailDto>.NotFound("Không tìm thấy bộ đề hoặc đề thi mục tiêu.");
                    return;
                }

                if (wantsPublish)
                {
                    var passageStatuses = (test.Sections ?? [])
                        .SelectMany(s => s.Questions ?? [])
                        .Select(q => q.Passage?.RightsStatus)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                    var bankIdsForRights = (test.Sections ?? [])
                        .SelectMany(s => s.Questions ?? [])
                        .Where(q => q.QuestionBankItemId.HasValue)
                        .Select(q => q.QuestionBankItemId!.Value)
                        .Distinct()
                        .ToList();
                    var bankStatuses = bankIdsForRights.Count == 0
                        ? []
                        : await unitOfWork.QuestionBankItems.Query().AsNoTracking()
                            .Where(bi => bankIdsForRights.Contains(bi.Id))
                            .Select(bi => bi.RightsStatus)
                            .ToListAsync(token);

                    var rightsCheck = JlptBlueprintPolicy.CheckPublishRights([req.RightsStatus, test.RightsStatus, .. passageStatuses, .. bankStatuses]);
                    if (rightsCheck.Blocked)
                    {
                        blockedFailure = ApiResponse<ModerationDetailDto>.Fail(rightsCheck.Message!, HttpStatusCode.BadRequest, "PUBLISH_BLOCKED_RIGHTS");
                        return;
                    }
                }

                if (newStatus == ContentApprovalStatus.Approved)
                {
                    var val = ExamBuilderService.ValidateLoadedExam(test, allowedLevelCodes);
                    if (!val.CanPublish)
                    {
                        var errCode = val.Issues.FirstOrDefault(i => i.Code == "PUBLISH_BLOCKED_RIGHTS")?.Code;
                        blockedFailure = ApiResponse<ModerationDetailDto>.Fail(string.Join(" ", val.Issues.Where(i => i.Severity == "Error").Select(i => i.Message).Take(5)), HttpStatusCode.BadRequest, errCode);
                        return;
                    }
                }

                test.PublishStatus = targetPublishStatus;
                test.IsManagedWorkflow = true;
                test.ReviewedAt = now;
                test.ReviewedByName = adminName;
                test.ReviewNote = request.ReviewNote?.Trim();
                if (targetPublishStatus == ContentStatus.Published)
                {
                    test.PublishedAt = now;
                    await ArchivePreviousExamVersionAsync(test.ParentExamId, token);
                }

                // Đồng bộ PublishStatus cho câu hỏi ngân hàng tạo kèm (nếu còn Draft/Pending)
                if (targetPublishStatus == ContentStatus.Published)
                {
                    var bankIds = (test.Sections ?? [])
                        .SelectMany(s => s.Questions ?? [])
                        .Where(q => q.QuestionBankItemId.HasValue)
                        .Select(q => q.QuestionBankItemId!.Value)
                        .Distinct()
                        .ToList();
                    if (bankIds.Count > 0)
                    {
                        var bankItems = await unitOfWork.QuestionBankItems.Query()
                            .Where(q => bankIds.Contains(q.Id) && q.PublishStatus != ContentStatus.Published)
                            .ToListAsync(token);
                        foreach (var bi in bankItems)
                        {
                            bi.PublishStatus = ContentStatus.Published;
                            bi.PublishedAt = now;
                            bi.ReviewedAt = now;
                            bi.ReviewedByName = adminName;
                        }
                    }
                }

                unitOfWork.MockTests.Update(test);
            }

            req.Status = newStatus;
            req.ReviewedAt = now;
            req.ReviewedByName = adminName;
            req.ReviewNote = request.ReviewNote?.Trim();
            if (request.RightsConfirmed) req.RightsConfirmed = true;

            var history = TryDeserialize<List<ReviewHistoryEntryDto>>(req.ReviewHistoryJson) ?? [];
            history.Add(new ReviewHistoryEntryDto
            {
                Action = request.Decision.ToString(),
                FromStatus = prevStatus.ToString(),
                ToStatus = newStatus.ToString(),
                ActorUserId = adminUserId,
                ActorName = adminName,
                AtUtc = now,
                Note = request.ReviewNote?.Trim(),
                RightsConfirmed = req.RightsConfirmed,
            });
            req.ReviewHistoryJson = JsonSerializer.Serialize(history, JsonOpts);
            unitOfWork.ContentApprovalRequests.Update(req);

            await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
            {
                ActorUserId = adminUserId,
                EntityType = $"ContentApproval:{req.EntityType}",
                EntityId = req.TargetEntityId ?? req.Id,
                Action = request.Decision.ToString(),
                BeforeJson = JsonSerializer.Serialize(new { status = prevStatus.ToString() }, JsonOpts),
                AfterJson = JsonSerializer.Serialize(new { status = newStatus.ToString(), publishStatus = targetPublishStatus.ToString(), rightsConfirmed = req.RightsConfirmed }, JsonOpts),
                Reason = request.ReviewNote?.Trim(),
                CreatedAtUtc = now,
            }, token);

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        if (blockedFailure is not null)
            return blockedFailure;

        var detail = await GetDetailAsync(requestId, ct);
        if (detail.IsSuccess && detail.Result is not null && sidebarNotifier is not null)
        {
            var authorId = detail.Result.Summary.AuthorUserId;
            if (authorId > 0)
                await sidebarNotifier.NotifyUsersAsync([authorId], "content_moderated", ct);
            await sidebarNotifier.NotifyAdminsAsync("content_moderated", ct);
        }
        return detail;
    }

    public async Task<ApiResponse> UnpublishAsync(
        UnpublishContentRequest request, int adminUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse.Fail("Vui lòng nhập lý do ngừng xuất bản.");

        var now = DateTime.UtcNow;
        var kind = (request.TargetType ?? "Exam").Trim().ToLowerInvariant();
        if (kind == "exam")
        {
            var test = await unitOfWork.MockTests.Query().FirstOrDefaultAsync(t => t.Id == request.TargetId && !t.IsDeleted, ct);
            if (test is null) return ApiResponse.NotFound("Không tìm thấy đề thi.");
            var prev = test.PublishStatus.ToString();
            test.PublishStatus = ContentStatus.Unpublished;
            test.IsManagedWorkflow = true;
            test.ReviewNote = request.Reason.Trim();
            test.ReviewedAt = now;
            unitOfWork.MockTests.Update(test);
            await LogUnpublishAsync("Exam", test.Id, prev, adminUserId, request.Reason.Trim(), now, ct);
        }
        else if (kind == "question")
        {
            var q = await unitOfWork.QuestionBankItems.Query().FirstOrDefaultAsync(x => x.Id == request.TargetId && !x.IsDeleted, ct);
            if (q is null) return ApiResponse.NotFound("Không tìm thấy câu hỏi.");
            var prev = q.PublishStatus.ToString();
            q.PublishStatus = ContentStatus.Unpublished;
            q.ReviewNote = request.Reason.Trim();
            q.ReviewedAt = now;
            unitOfWork.QuestionBankItems.Update(q);
            await LogUnpublishAsync("Question", q.Id, prev, adminUserId, request.Reason.Trim(), now, ct);
        }
        else if (kind == "passage")
        {
            var p = await unitOfWork.ContentPassages.Query().FirstOrDefaultAsync(x => x.Id == request.TargetId && !x.IsDeleted, ct);
            if (p is null) return ApiResponse.NotFound("Không tìm thấy đoạn/nhóm.");
            var prev = p.PublishStatus.ToString();
            p.PublishStatus = ContentStatus.Unpublished;
            p.ReviewNote = request.Reason.Trim();
            p.ReviewedAt = now;
            unitOfWork.ContentPassages.Update(p);
            await LogUnpublishAsync("Passage", p.Id, prev, adminUserId, request.Reason.Trim(), now, ct);
        }
        else
        {
            return ApiResponse.Fail("TargetType không hợp lệ (Exam | Question | Passage).");
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse.Success();
    }

    private async Task ArchivePreviousQuestionVersionAsync(int? parentId, CancellationToken ct)
    {
        if (!parentId.HasValue) return;
        var prev = await unitOfWork.QuestionBankItems.Query().FirstOrDefaultAsync(q => q.Id == parentId.Value && !q.IsDeleted, ct);
        if (prev is not null)
        {
            prev.IsArchivedVersion = true;
            unitOfWork.QuestionBankItems.Update(prev);
        }
    }

    private async Task ArchivePreviousExamVersionAsync(int? parentExamId, CancellationToken ct)
    {
        if (!parentExamId.HasValue) return;
        var prev = await unitOfWork.MockTests.Query().FirstOrDefaultAsync(t => t.Id == parentExamId.Value && !t.IsDeleted, ct);
        if (prev is not null)
        {
            prev.IsArchivedVersion = true;
            unitOfWork.MockTests.Update(prev);
        }
    }

    private async Task LogUnpublishAsync(
        string entityType, int id, string prevStatus, int adminId, string reason, DateTime now, CancellationToken ct)
    {
        await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
        {
            ActorUserId = adminId,
            EntityType = entityType,
            EntityId = id,
            Action = "Unpublish",
            BeforeJson = JsonSerializer.Serialize(new { publishStatus = prevStatus }, JsonOpts),
            AfterJson = JsonSerializer.Serialize(new { publishStatus = ContentStatus.Unpublished.ToString() }, JsonOpts),
            Reason = reason,
            CreatedAtUtc = now,
        }, ct);
    }

    private static ModerationQueueItemDto MapQueueItem(
        ContentApprovalRequest r, Dictionary<int, string> levels, Dictionary<int, string> skills) => new()
        {
            RequestId = r.Id,
            BatchId = r.BatchId,
            EntityType = r.EntityType,
            RowKey = r.RowKey,
            TargetEntityId = r.TargetEntityId,
            Status = r.Status,
            AuthorUserId = r.SubmittedByUserId,
            AuthorName = r.SubmittedBy is not null
                ? (string.IsNullOrWhiteSpace($"{r.SubmittedBy.FirstName} {r.SubmittedBy.LastName}")
                    ? r.SubmittedBy.Email ?? $"User #{r.SubmittedByUserId}"
                    : $"{r.SubmittedBy.FirstName} {r.SubmittedBy.LastName}".Trim())
                : $"User #{r.SubmittedByUserId}",
            SourceType = r.SourceType,
            SourceReference = r.SourceReference,
            RightsStatus = r.RightsStatus,
            RightsConfirmed = r.RightsConfirmed,
            LevelCode = r.CertificateLevelId.HasValue && levels.TryGetValue(r.CertificateLevelId.Value, out var lc) ? lc : null,
            SkillName = r.LanguageSkillId.HasValue && skills.TryGetValue(r.LanguageSkillId.Value, out var sn) ? sn : null,
            AiProvenance = TryDeserialize<AiProvenanceDto>(r.AiProvenanceJson),
            SubmittedAt = r.SubmittedAt,
            ReviewedByName = r.ReviewedByName,
            ReviewedAt = r.ReviewedAt,
            ReviewNote = r.ReviewNote,
        };

    private static T? TryDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
