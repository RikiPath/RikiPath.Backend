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

/// <summary>Dịch vụ lắp ráp Bộ đề luyện tập, Luyện tập hỗn hợp và Đề thi thử JLPT từ Ngân hàng câu hỏi thống nhất.</summary>
public sealed class ExamBuilderService(IUnitOfWork unitOfWork) : IExamBuilderService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ApiResponse<PagedResult<ExamBuilderDto>>> ListAuthorExamsAsync(
        ExamKind? examKind, int? levelId, ContentStatus? status, string? keyword,
        int page, int pageSize, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = unitOfWork.MockTests.Query()
            .AsNoTracking()
            .Include(t => t.CertificateLevel)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
            .Where(t => !t.IsDeleted && t.ExamKind != ExamKind.Placement);

        if (!isAdmin)
            query = query.Where(t => t.AuthorUserId == actorUserId || t.PublishStatus == ContentStatus.Published);

        if (examKind.HasValue)
            query = query.Where(t => t.ExamKind == examKind.Value);
        if (levelId.HasValue)
            query = query.Where(t => t.CertificateLevelId == levelId.Value);
        if (status.HasValue)
            query = query.Where(t => t.PublishStatus == status.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(kw) || (t.Description != null && t.Description.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.ModifiedDate ?? t.CreatedDate)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var skillsMap = (await unitOfWork.LanguageSkills.Query().AsNoTracking().Where(s => !s.IsDeleted).ToListAsync(ct))
            .ToDictionary(s => s.Id);
        var certSecMap = (await unitOfWork.CertificateSections.Query().AsNoTracking().Where(c => !c.IsDeleted).ToListAsync(ct))
            .ToDictionary(c => c.Id);

        var dtos = items.Select(t => MapSummaryDto(t, skillsMap, certSecMap)).ToList();
        return ApiResponse<PagedResult<ExamBuilderDto>>.Success(new PagedResult<ExamBuilderDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = dtos,
        });
    }

    public async Task<ApiResponse<ExamBuilderDto>> GetDetailAsync(int examId, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var test = await unitOfWork.MockTests.Query()
            .AsNoTracking()
            .Include(t => t.CertificateLevel)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.LanguageSkill)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.CertificateSection)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Passage)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == examId && !t.IsDeleted, ct);

        if (test is null)
            return ApiResponse<ExamBuilderDto>.NotFound("Không tìm thấy bộ đề hoặc đề thi.");

        var isOwner = test.AuthorUserId == actorUserId;
        if (!isAdmin && !isOwner && test.PublishStatus != ContentStatus.Published)
            return ApiResponse<ExamBuilderDto>.Forbidden("Bạn không có quyền xem đề chưa xuất bản này.");

        var dto = MapFullDto(test, includeProtectedTranscript: isAdmin || isOwner);
        return ApiResponse<ExamBuilderDto>.Success(dto);
    }

    public async Task<ApiResponse<ExamBuilderDto>> SaveDraftAsync(ExamBuilderDto request, int actorUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResponse<ExamBuilderDto>.Fail("Tiêu đề đề không được để trống.");
        if (request.TimeLimitMinutes <= 0)
            return ApiResponse<ExamBuilderDto>.Fail("Thời gian làm bài phải lớn hơn 0.");

        var level = await unitOfWork.CertificateLevels.Query()
            .FirstOrDefaultAsync(l => !l.IsDeleted &&
                (l.Id == request.CertificateLevelId || (!string.IsNullOrEmpty(request.CertificateLevelCode) && l.Code == request.CertificateLevelCode)), ct);
        if (level is null)
            return ApiResponse<ExamBuilderDto>.Fail("Không tìm thấy cấp độ JLPT hợp lệ.");

        var skills = await unitOfWork.LanguageSkills.Query().Where(s => !s.IsDeleted).ToListAsync(ct);
        var certSections = await unitOfWork.CertificateSections.Query()
            .Where(c => !c.IsDeleted && c.CertificateLevelId == level.Id)
            .ToListAsync(ct);
        var allowedLevelCodes = await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Where(l => !l.IsDeleted && l.Code != null).Select(l => l.Code.ToUpper()).ToListAsync(ct);
        var allowedSet = allowedLevelCodes.Where(CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.Sections is null || request.Sections.Count == 0)
            return ApiResponse<ExamBuilderDto>.Fail("Đề cần có ít nhất một phần thi (section).");

        // Pre-validate every section + resolve QuestionBank items before mutating DB
        var resolvedSections = new List<(ExamSectionBuilderDto Raw, LanguageSkill Skill, CertificateSection CertSec, List<(QuestionBankItem? BankItem, CanonicalQuestionDto Canonical, int Sort)> Questions)>();
        var bankIdsNeeded = request.Sections
            .SelectMany(s => s.Questions)
            .Where(q => q.QuestionBankItemId is > 0)
            .Select(q => q.QuestionBankItemId!.Value)
            .Distinct()
            .ToList();
        var bankItemsMap = bankIdsNeeded.Count == 0
            ? new Dictionary<int, QuestionBankItem>()
            : (await unitOfWork.QuestionBankItems.Query()
                .Include(q => q.CertificateLevel)
                .Include(q => q.LanguageSkill)
                .Include(q => q.Passage)
                .Where(q => !q.IsDeleted && bankIdsNeeded.Contains(q.Id))
                .ToListAsync(ct)).ToDictionary(q => q.Id);

        var globalSort = 1;
        foreach (var sec in request.Sections.OrderBy(s => s.SortOrder))
        {
            var skill = skills.FirstOrDefault(s => s.Id == sec.LanguageSkillId)
                ?? (!string.IsNullOrWhiteSpace(sec.SkillName)
                    ? skills.FirstOrDefault(s => CanonicalQuestionValidator.NormalizeSkillName(s.Name).Equals(CanonicalQuestionValidator.NormalizeSkillName(sec.SkillName), StringComparison.OrdinalIgnoreCase))
                    : null);
            if (skill is null)
                return ApiResponse<ExamBuilderDto>.Fail($"Phần '{sec.Title}': không xác định được kỹ năng.");

            var certSec = certSections.FirstOrDefault(c => c.Id == sec.CertificateSectionId)
                ?? (!string.IsNullOrWhiteSpace(sec.CertificateSectionCode)
                    ? certSections.FirstOrDefault(c => c.Code.Equals(sec.CertificateSectionCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    : null)
                ?? certSections.FirstOrDefault();
            if (certSec is null)
                return ApiResponse<ExamBuilderDto>.Fail($"Cấp độ {level.Code} chưa có CertificateSection cấu hình trong hệ thống.");

            var resolvedSlots = new List<(QuestionBankItem? BankItem, CanonicalQuestionDto Canonical, int Sort)>();
            foreach (var slot in sec.Questions.OrderBy(q => q.SortOrder))
            {
                if (slot.QuestionBankItemId is > 0)
                {
                    if (!bankItemsMap.TryGetValue(slot.QuestionBankItemId.Value, out var bankItem))
                        return ApiResponse<ExamBuilderDto>.Fail($"Không tìm thấy câu hỏi #{slot.QuestionBankItemId} trong Ngân hàng câu hỏi.");
                    if (bankItem.AuthorUserId != actorUserId && bankItem.PublishStatus != ContentStatus.Published)
                        return ApiResponse<ExamBuilderDto>.Forbidden($"Câu hỏi #{bankItem.Id} chưa xuất bản và không thuộc quyền sở hữu của bạn.");

                    var canonical = QuestionBankService.MapToDto(bankItem, includeTranscript: true);
                    resolvedSlots.Add((bankItem, canonical, slot.SortOrder > 0 ? slot.SortOrder : globalSort));
                }
                else if (slot.InlineQuestion is not null)
                {
                    var qDto = slot.InlineQuestion;
                    qDto.CertificateLevelId = level.Id;
                    qDto.CertificateLevelCode = level.Code;
                    qDto.LanguageSkillId = skill.Id;
                    qDto.SkillName = skill.Name;
                    var qVal = CanonicalQuestionValidator.ValidateQuestion(qDto, forSubmitOrPublish: false, allowedSet);
                    if (!qVal.CanSaveDraft)
                        return ApiResponse<ExamBuilderDto>.Fail($"Phần '{sec.Title}' câu #{globalSort}: {string.Join(" ", qVal.Errors.Select(e => e.Message))}");
                    resolvedSlots.Add((null, qDto, slot.SortOrder > 0 ? slot.SortOrder : globalSort));
                }
                globalSort++;
            }

            resolvedSections.Add((sec, skill, certSec, resolvedSlots));
        }

        MockTest exam = null!;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (request.Id is > 0)
            {
                var existing = await unitOfWork.MockTests.Query()
                    .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                        .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .FirstOrDefaultAsync(t => t.Id == request.Id.Value && !t.IsDeleted, token)
                    ?? throw new InvalidOperationException("Không tìm thấy đề cần cập nhật.");

                if (existing.AuthorUserId.HasValue && existing.AuthorUserId != actorUserId)
                    throw new UnauthorizedAccessException("Bạn không thể sửa đề của tác giả khác.");
                if (existing.PublishStatus == ContentStatus.Published)
                    throw new InvalidOperationException("Đề đã xuất bản không được sửa trực tiếp. Hãy tạo phiên bản mới.");
                if (existing.PublishStatus == ContentStatus.PendingReview)
                    throw new InvalidOperationException("Đề đang chờ Admin xét duyệt.");

                exam = existing;
                // Xóa mềm sections cũ trước khi đồng bộ cấu trúc mới trong cùng transaction
                foreach (var oldSec in exam.Sections ?? [])
                {
                    oldSec.IsDeleted = true;
                    foreach (var oldQ in oldSec.Questions ?? [])
                        oldQ.IsDeleted = true;
                }
            }
            else
            {
                exam = new MockTest
                {
                    Version = 1,
                    AuthorUserId = actorUserId,
                    IsManagedWorkflow = true,
                    PublishStatus = ContentStatus.Draft,
                    Sections = [],
                };
                await unitOfWork.MockTests.AddAsync(exam, token);
            }

            exam.Title = request.Title.Trim();
            exam.Description = request.Description?.Trim();
            exam.CertificateLevelId = level.Id;
            exam.TimeLimitMinutes = request.TimeLimitMinutes;
            exam.ExamKind = request.ExamKind;
            exam.PublishStatus = ContentStatus.Draft;
            exam.IsManagedWorkflow = true;
            exam.FocusSkillId = request.FocusSkillId ?? (request.ExamKind == ExamKind.PracticeSet ? resolvedSections.FirstOrDefault().Skill?.Id : null);
            var included = resolvedSections.Select(r => CanonicalQuestionValidator.NormalizeSkillName(r.Skill.Name)).Distinct().ToList();
            exam.IncludedSkillsJson = JsonSerializer.Serialize(included, JsonOpts);
            exam.TagsJson = request.Tags is { Count: > 0 } ? JsonSerializer.Serialize(request.Tags, JsonOpts) : null;
            exam.SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim();
            exam.SourceReference = request.SourceReference?.Trim();
            exam.RightsStatus = request.RightsStatus?.Trim();
            exam.BlueprintPolicyJson = request.BlueprintPolicy is not null
                ? JsonSerializer.Serialize(request.BlueprintPolicy, JsonOpts)
                : JsonSerializer.Serialize(new ExamBlueprintPolicyDto(), JsonOpts);
            exam.AiProvenanceJson = request.AiProvenance is null ? null : JsonSerializer.Serialize(request.AiProvenance, JsonOpts);
            exam.Sections ??= [];

            var secOrder = 1;
            var qCounter = 1;
            foreach (var (rawSec, skill, certSec, slots) in resolvedSections)
            {
                var sectionEntity = new MockTestSection
                {
                    Title = string.IsNullOrWhiteSpace(rawSec.Title) ? certSec.Name : rawSec.Title.Trim(),
                    SortOrder = rawSec.SortOrder > 0 ? rawSec.SortOrder : secOrder,
                    LanguageSkillId = skill.Id,
                    CertificateSectionId = certSec.Id,
                    DurationMinutes = rawSec.DurationMinutes,
                    Instructions = rawSec.Instructions?.Trim(),
                    Questions = [],
                };

                foreach (var (existingBank, qDto, _) in slots)
                {
                    QuestionBankItem bankRef;
                    if (existingBank is not null)
                    {
                        bankRef = existingBank;
                    }
                    else
                    {
                        var qVal = CanonicalQuestionValidator.ValidateQuestion(qDto, false, allowedSet);
                        bankRef = new QuestionBankItem
                        {
                            QuestionIdentity = Guid.NewGuid(),
                            Version = 1,
                            PublishStatus = ContentStatus.Draft,
                            AuthorUserId = actorUserId,
                        };
                        QuestionBankService.ApplyQuestionDtoToEntity(bankRef, qDto, qVal.ContentFingerprint, qDto.PassageId);
                        await unitOfWork.QuestionBankItems.AddAsync(bankRef, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }

                    sectionEntity.Questions.Add(BuildLinkedMockQuestion(bankRef, qDto, qCounter++));
                }

                exam.Sections.Add(sectionEntity);
                secOrder++;
            }

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        return await GetDetailAsync(exam.Id, actorUserId, isAdmin: false, ct);
    }

    public async Task<ApiResponse<ExamBuilderDto>> CreateNewVersionAsync(int sourceExamId, int actorUserId, CancellationToken ct)
    {
        var sourceRes = await GetDetailAsync(sourceExamId, actorUserId, isAdmin: true, ct);
        if (!sourceRes.IsSuccess || sourceRes.Result is null)
            return sourceRes;

        var src = sourceRes.Result;
        var maxVersion = await unitOfWork.MockTests.Query()
            .Where(t => !t.IsDeleted && (t.Id == sourceExamId || t.ParentExamId == sourceExamId))
            .MaxAsync(t => t.Version, ct);

        src.Id = null;
        src.PublishStatus = ContentStatus.Draft;
        src.Version = maxVersion + 1;
        src.ParentExamId = sourceExamId;
        src.Title = src.Title.Contains($"(v{src.Version}", StringComparison.OrdinalIgnoreCase)
            ? src.Title
            : $"{src.Title} (v{src.Version})";

        var saved = await SaveDraftAsync(src, actorUserId, ct);
        if (saved.IsSuccess && saved.Result?.Id is > 0)
        {
            var created = await unitOfWork.MockTests.GetByIdAsync(saved.Result.Id.Value, ct);
            if (created is not null)
            {
                created.Version = maxVersion + 1;
                created.ParentExamId = sourceExamId;
                unitOfWork.MockTests.Update(created);
                await unitOfWork.SaveChangesAsync(ct);
                saved.Result.Version = created.Version;
                saved.Result.ParentExamId = sourceExamId;
            }
        }
        return saved;
    }

    public async Task<ApiResponse<ExamBlueprintValidationResponse>> ValidateExamAsync(int examId, CancellationToken ct)
    {
        var test = await unitOfWork.MockTests.Query()
            .AsNoTracking()
            .Include(t => t.CertificateLevel)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.LanguageSkill)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.CertificateSection)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Passage)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == examId && !t.IsDeleted, ct);

        if (test is null)
            return ApiResponse<ExamBlueprintValidationResponse>.NotFound("Không tìm thấy đề.");

        var allowed = await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Where(l => !l.IsDeleted && l.Code != null)
            .Select(l => l.Code.ToUpper())
            .ToListAsync(ct);
        var allowedSet = allowed.Where(CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ApiResponse<ExamBlueprintValidationResponse>.Success(ValidateLoadedExam(test, allowedSet));
    }

    public async Task<ApiResponse<ExamBuilderDto>> SubmitExamAsync(int examId, int actorUserId, CancellationToken ct)
    {
        var test = await unitOfWork.MockTests.Query()
            .Include(t => t.CertificateLevel)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.LanguageSkill)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.CertificateSection)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Options!.Where(o => !o.IsDeleted))
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Passage)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == examId && !t.IsDeleted, ct);

        if (test is null)
            return ApiResponse<ExamBuilderDto>.NotFound("Không tìm thấy đề.");
        if (test.AuthorUserId.HasValue && test.AuthorUserId != actorUserId)
            return ApiResponse<ExamBuilderDto>.Forbidden("Chỉ tác giả sở hữu mới có thể gửi duyệt.");
        if (test.PublishStatus == ContentStatus.Published)
            return ApiResponse<ExamBuilderDto>.Conflict("Đề này đã xuất bản.");

        var allowed = await unitOfWork.CertificateLevels.Query().AsNoTracking()
            .Where(l => !l.IsDeleted && l.Code != null).Select(l => l.Code.ToUpper()).ToListAsync(ct);
        var validation = ValidateLoadedExam(test, allowed.Where(CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase));

        if (!validation.CanPublish)
        {
            return ApiResponse<ExamBuilderDto>.Fail(
                string.Join(" ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message).Take(5)),
                HttpStatusCode.BadRequest,
                errors: validation.Issues.Select(i => $"{i.Field}: {i.Message}").ToList());
        }

        var now = DateTime.UtcNow;
        test.PublishStatus = ContentStatus.PendingReview;
        test.SubmittedAt = now;

        var payloadDto = MapFullDto(test, includeProtectedTranscript: true);
        var entityType = test.ExamKind == ExamKind.FullMockTest ? ImportEntityType.MockTest : ImportEntityType.PracticeSet;

        var approval = new ContentApprovalRequest
        {
            BatchId = Guid.NewGuid(),
            EntityType = entityType,
            Action = test.ParentExamId.HasValue ? ImportRowAction.Modified : ImportRowAction.New,
            TargetEntityId = test.Id,
            RowKey = $"{test.Title} / {test.CertificateLevel?.Code} (v{test.Version})",
            PayloadJson = JsonSerializer.Serialize(payloadDto, JsonOpts),
            SourceRowNumber = 1,
            SourceFileName = test.ExamKind == ExamKind.FullMockTest ? "Exam Builder (Full Mock Test)" : "Exam Builder (Practice Set)",
            Status = ContentApprovalStatus.PendingReview,
            SubmittedByUserId = actorUserId,
            SubmittedAt = now,
            SourceType = test.SourceType ?? "Manual",
            SourceReference = test.SourceReference,
            RightsStatus = test.RightsStatus,
            CertificateLevelId = test.CertificateLevelId,
            AiProvenanceJson = test.AiProvenanceJson,
            ValidationSummaryJson = JsonSerializer.Serialize(validation, JsonOpts),
        };

        await unitOfWork.ContentApprovalRequests.AddAsync(approval, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetDetailAsync(test.Id, actorUserId, isAdmin: false, ct);
    }

    public async Task<ApiResponse> DeleteDraftExamAsync(int examId, int actorUserId, bool isAdmin, CancellationToken ct)
    {
        var test = await unitOfWork.MockTests.Query().FirstOrDefaultAsync(t => t.Id == examId && !t.IsDeleted, ct);
        if (test is null) return ApiResponse.NotFound("Không tìm thấy đề.");
        if (!isAdmin && test.AuthorUserId != actorUserId) return ApiResponse.Forbidden("Không có quyền xóa.");
        if (test.PublishStatus == ContentStatus.Published)
            return ApiResponse.Conflict("Không thể xóa đề đã xuất bản.");

        test.IsDeleted = true;
        unitOfWork.MockTests.Update(test);
        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse.Success();
    }

    internal static ExamBlueprintValidationResponse ValidateLoadedExam(MockTest test, HashSet<string> allowedLevels)
    {
        var res = new ExamBlueprintValidationResponse();
        if (JlptBlueprintPolicy.IsRightsBlocked(test.RightsStatus))
        {
            res.Issues.Add(new(
                "rightsStatus",
                null,
                "Error",
                "PUBLISH_BLOCKED_RIGHTS",
                $"Nội dung có trạng thái bản quyền '{JlptBlueprintPolicy.NormalizeRightsStatus(test.RightsStatus)}' nên bị chặn gửi duyệt/xuất bản."));
        }

        var levelCode = test.CertificateLevel?.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!allowedLevels.Contains(levelCode))
            res.Issues.Add(new("certificateLevelCode", null, "Error", "LEVEL_UNSUPPORTED", $"Cấp độ '{levelCode}' không hợp lệ (N5, N4, N3, N2)."));

        if (test.TimeLimitMinutes <= 0)
            res.Issues.Add(new("timeLimitMinutes", null, "Error", "DURATION_INVALID", "Thời gian làm bài phải lớn hơn 0."));

        var sections = (test.Sections ?? []).Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToList();
        if (sections.Count == 0)
            res.Issues.Add(new("sections", null, "Error", "NO_SECTIONS", "Đề chưa có phần thi nào."));

        var coveredSkills = new HashSet<JlptStudySkill>();
        var coveredLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sec in sections)
        {
            var skillName = CanonicalQuestionValidator.NormalizeSkillName(sec.LanguageSkill?.Name);
            coveredLabels.Add(skillName);
            if (JlptSkillAssessmentPolicy.Classify(skillName) is { } studySkill)
                coveredSkills.Add(studySkill);

            var qs = (sec.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).ToList();
            if (qs.Count == 0)
            {
                res.Issues.Add(new("sections", null, "Error", "EMPTY_SECTION", $"Phần '{sec.Title}' chưa có câu hỏi nào."));
                continue;
            }

            foreach (var q in qs)
            {
                res.TotalQuestions++;
                var qDto = MapMockQuestionToCanonical(test, sec, q, includeTranscript: true);
                var qVal = CanonicalQuestionValidator.ValidateQuestion(qDto, forSubmitOrPublish: true, allowedLevels, res.TotalQuestions);
                res.Issues.AddRange(qVal.Errors);
                res.Issues.AddRange(qVal.Warnings);
                if (qVal.IncompleteMedia) res.MissingAudioCount++;
            }
        }

        if (test.ExamKind == ExamKind.FullMockTest)
        {
            // Đề thi thử JLPT chuẩn cần phủ đầy đủ Từ vựng/Chữ viết, Ngữ pháp, Đọc hiểu và Nghe hiểu
            foreach (var required in Enum.GetValues<JlptStudySkill>())
            {
                if (!coveredSkills.Contains(required))
                {
                    res.Issues.Add(new("blueprint", null, "Error", "BLUEPRINT_MISSING_SKILL",
                        $"Đề thi thử JLPT {levelCode} thiếu phần kỹ năng bắt buộc: {JlptSkillAssessmentPolicy.Label(required)}."));
                }
            }
        }
        else if (test.ExamKind == ExamKind.MixedPractice && coveredLabels.Count < 2)
        {
            res.Issues.Add(new("blueprint", null, "Warning", "MIXED_SINGLE_SKILL",
                "Bộ đề Luyện tập hỗn hợp nên kết hợp từ 2 kỹ năng trở lên (nếu 1 kỹ năng hãy đặt loại PracticeSet)."));
        }

        res.CoveredSkills = coveredLabels.ToList();
        res.IsValid = res.Issues.All(i => i.Severity != "Error");
        res.CanPublish = res.IsValid && res.MissingAudioCount == 0 && res.TotalQuestions > 0;
        return res;
    }

    internal static CanonicalQuestionDto MapMockQuestionToCanonical(
        MockTest test, MockTestSection sec, MockQuestion q, bool includeTranscript)
    {
        var skillName = CanonicalQuestionValidator.NormalizeSkillName(sec.LanguageSkill?.Name);
        var meta = !string.IsNullOrWhiteSpace(q.SkillMetadataJson)
            ? TryDeserialize<CanonicalSkillMetadataDto>(q.SkillMetadataJson)
            : new CanonicalSkillMetadataDto();
        meta ??= new CanonicalSkillMetadataDto();

        if (SentenceOrdering.IsType(q.QuestionType) &&
            SentenceOrdering.TryRead(q.InteractionDataJson, q.AnswerKeyJson, out var tokens, out var answer))
        {
            meta.OrderingTokens = tokens.ToList();
            meta.OrderingAnswer = answer.ToList();
        }
        else if (q.QuestionType.Equals(CanonicalQuestionTypes.FillBlank, StringComparison.OrdinalIgnoreCase) &&
                 !string.IsNullOrWhiteSpace(q.AnswerKeyJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(q.AnswerKeyJson);
                if (doc.RootElement.TryGetProperty("acceptedAnswers", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    meta.AcceptedAnswers = arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
            catch (JsonException) { }
        }

        var playback = TryDeserialize<ListeningPlaybackPolicyDto>(q.PlaybackPolicyJson)
            ?? TryDeserialize<ListeningPlaybackPolicyDto>(q.Passage?.PlaybackPolicyJson);

        return new CanonicalQuestionDto
        {
            Id = q.QuestionBankItemId ?? q.Id,
            Version = q.QuestionVersion,
            CertificateLevelId = test.CertificateLevelId,
            CertificateLevelCode = test.CertificateLevel?.Code ?? string.Empty,
            LanguageSkillId = sec.LanguageSkillId,
            SkillName = skillName,
            PassageId = q.PassageId,
            PassageCode = q.Passage?.PassageCode,
            Passage = q.Passage is null ? null : QuestionBankService.MapPassageToDto(q.Passage, includeTranscript),
            QuestionGroupTitle = q.QuestionGroupTitle,
            TemplateKind = string.IsNullOrWhiteSpace(q.TemplateKind)
                ? CanonicalQuestionValidator.InferDefaultTemplateKind(skillName, q.QuestionType)
                : q.TemplateKind,
            QuestionType = q.QuestionType,
            QuestionText = q.QuestionText,
            SkillMetadata = meta,
            AudioUrl = q.AudioUrl ?? q.Passage?.AudioUrl,
            AudioTranscript = includeTranscript ? (q.AudioTranscript ?? q.Passage?.AudioTranscript) : null,
            PlaybackPolicy = playback,
            ImageUrl = q.ImageUrl ?? q.Passage?.ImageUrl,
            Explanation = q.Explanation,
            SourceType = q.Passage?.SourceType ?? test.SourceType ?? "Manual",
            SourceReference = q.Passage?.SourceReference ?? test.SourceReference,
            RightsStatus = q.Passage?.RightsStatus ?? test.RightsStatus,
            Options = (q.Options ?? [])
                .Where(o => !o.IsDeleted)
                .OrderBy(o => o.SortOrder).ThenBy(o => o.Id)
                .Select(o => new CanonicalOptionDto
                {
                    SortOrder = o.SortOrder,
                    OptionText = o.OptionText,
                    ImageUrl = o.ImageUrl,
                    IsCorrect = o.IsCorrect,
                })
                .ToList(),
        };
    }

    internal static MockQuestion BuildLinkedMockQuestion(QuestionBankItem bank, CanonicalQuestionDto dto, int sortOrder) => new()
    {
        QuestionBankItemId = bank.Id,
        QuestionVersion = bank.Version,
        PassageId = bank.PassageId ?? dto.PassageId,
        TemplateKind = dto.TemplateKind,
        QuestionGroupTitle = dto.QuestionGroupTitle,
        PlaybackPolicyJson = bank.PlaybackPolicyJson ?? (dto.PlaybackPolicy is null ? null : JsonSerializer.Serialize(dto.PlaybackPolicy, JsonOpts)),
        SkillMetadataJson = bank.SkillMetadataJson ?? (dto.SkillMetadata is null ? null : JsonSerializer.Serialize(dto.SkillMetadata, JsonOpts)),
        QuestionText = dto.QuestionText.Trim(),
        QuestionType = dto.QuestionType.Trim(),
        InteractionDataJson = QuestionBankService.BuildInteractionDataJson(dto),
        AnswerKeyJson = QuestionBankService.BuildAnswerKeyJson(dto),
        AudioUrl = dto.AudioUrl ?? bank.AudioUrl,
        AudioTranscript = dto.AudioTranscript ?? bank.AudioTranscript,
        ImageUrl = dto.ImageUrl ?? bank.ImageUrl,
        Explanation = dto.Explanation ?? bank.Explanation,
        SortOrder = sortOrder,
        Options = dto.Options.Select((o, idx) => new MockQuestionOption
        {
            SortOrder = o.SortOrder > 0 ? o.SortOrder : idx + 1,
            OptionText = o.OptionText.Trim(),
            ImageUrl = o.ImageUrl?.Trim(),
            IsCorrect = o.IsCorrect,
        }).ToList(),
    };

    private static ExamBuilderDto MapSummaryDto(
        MockTest t, Dictionary<int, LanguageSkill> skills, Dictionary<int, CertificateSection> certSections) => new()
        {
            Id = t.Id,
            ExamKind = t.ExamKind,
            PublishStatus = t.PublishStatus,
            Version = t.Version,
            ParentExamId = t.ParentExamId,
            IsArchivedVersion = t.IsArchivedVersion,
            Title = t.Title,
            Description = t.Description,
            CertificateLevelId = t.CertificateLevelId,
            CertificateLevelCode = t.CertificateLevel?.Code,
            TimeLimitMinutes = t.TimeLimitMinutes,
            FocusSkillId = t.FocusSkillId,
            IncludedSkills = TryDeserialize<List<string>>(t.IncludedSkillsJson) ?? [],
            Tags = TryDeserialize<List<string>>(t.TagsJson) ?? [],
            SourceType = t.SourceType ?? "Manual",
            SourceReference = t.SourceReference,
            RightsStatus = t.RightsStatus,
            BlueprintPolicy = TryDeserialize<ExamBlueprintPolicyDto>(t.BlueprintPolicyJson),
            AiProvenance = TryDeserialize<AiProvenanceDto>(t.AiProvenanceJson),
            ReviewNote = t.ReviewNote,
            ReviewedByName = t.ReviewedByName,
            ReviewedAt = t.ReviewedAt,
            PublishedAt = t.PublishedAt,
            Sections = (t.Sections ?? []).OrderBy(s => s.SortOrder).Select(s => new ExamSectionBuilderDto
            {
                Id = s.Id,
                SortOrder = s.SortOrder,
                Title = s.Title,
                LanguageSkillId = s.LanguageSkillId,
                SkillName = skills.TryGetValue(s.LanguageSkillId, out var sk) ? sk.Name : null,
                CertificateSectionId = s.CertificateSectionId,
                CertificateSectionCode = certSections.TryGetValue(s.CertificateSectionId, out var cs) ? cs.Code : null,
                DurationMinutes = s.DurationMinutes,
                Instructions = s.Instructions,
                Questions = (s.Questions ?? []).OrderBy(q => q.SortOrder).Select(q => new ExamQuestionSlotDto
                {
                    MockQuestionId = q.Id,
                    QuestionBankItemId = q.QuestionBankItemId,
                    SortOrder = q.SortOrder,
                }).ToList(),
            }).ToList(),
        };

    private static ExamBuilderDto MapFullDto(MockTest t, bool includeProtectedTranscript) => new()
    {
        Id = t.Id,
        ExamKind = t.ExamKind,
        PublishStatus = t.PublishStatus,
        Version = t.Version,
        ParentExamId = t.ParentExamId,
        IsArchivedVersion = t.IsArchivedVersion,
        Title = t.Title,
        Description = t.Description,
        CertificateLevelId = t.CertificateLevelId,
        CertificateLevelCode = t.CertificateLevel?.Code,
        TimeLimitMinutes = t.TimeLimitMinutes,
        FocusSkillId = t.FocusSkillId,
        IncludedSkills = TryDeserialize<List<string>>(t.IncludedSkillsJson) ?? [],
        Tags = TryDeserialize<List<string>>(t.TagsJson) ?? [],
        SourceType = t.SourceType ?? "Manual",
        SourceReference = t.SourceReference,
        RightsStatus = t.RightsStatus,
        BlueprintPolicy = TryDeserialize<ExamBlueprintPolicyDto>(t.BlueprintPolicyJson),
        AiProvenance = TryDeserialize<AiProvenanceDto>(t.AiProvenanceJson),
        ReviewNote = t.ReviewNote,
        ReviewedByName = t.ReviewedByName,
        ReviewedAt = t.ReviewedAt,
        PublishedAt = t.PublishedAt,
        Sections = (t.Sections ?? []).Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).Select(s => new ExamSectionBuilderDto
        {
            Id = s.Id,
            SortOrder = s.SortOrder,
            Title = s.Title,
            LanguageSkillId = s.LanguageSkillId,
            SkillName = s.LanguageSkill?.Name,
            CertificateSectionId = s.CertificateSectionId,
            CertificateSectionCode = s.CertificateSection?.Code,
            DurationMinutes = s.DurationMinutes,
            Instructions = s.Instructions,
            Questions = (s.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).Select(q => new ExamQuestionSlotDto
            {
                MockQuestionId = q.Id,
                QuestionBankItemId = q.QuestionBankItemId,
                SortOrder = q.SortOrder,
                InlineQuestion = MapMockQuestionToCanonical(t, s, q, includeProtectedTranscript),
            }).ToList(),
        }).ToList(),
    };

    private static T? TryDeserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }
}
