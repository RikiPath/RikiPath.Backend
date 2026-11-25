using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using UglyToad.PdfPig;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>Pipeline nhập liệu đa định dạng hợp nhất (.xlsx, .json, .zip, text .pdf) với Preview không side-effect và Confirm transaction + idempotent.</summary>
public sealed partial class UnifiedImportService(
    IUnitOfWork unitOfWork,
    IQuestionBankService questionBankService,
    IFileStorageService fileStorage) : IUnifiedImportService
{
    private const long MaxUploadBytes = 25 * 1024 * 1024;
    private const long MaxZipUncompressedBytes = 50 * 1024 * 1024;
    private const int MaxZipEntries = 200;
    private const double MaxZipCompressionRatio = 100.0;
    private const int MaxPdfPages = 60;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"^(?:(?:Q|Câu|問)?\s*(\d{1,3}))[\.\):]\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PdfQuestionHeaderRegex();

    [GeneratedRegex(@"^([A-D1-4ア-エ])[\.\):]\s*(.+)$")]
    private static partial Regex PdfOptionRegex();

    [GeneratedRegex(@"^(?:Đáp án|Answer|Ans|正解|答え)\s*[:：]\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PdfAnswerRegex();

    [GeneratedRegex(@"^(?:Giải thích|Explanation|Note|解説)\s*[:：]\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PdfExplanationRegex();

    public async Task<ApiResponse<UnifiedImportPreviewResponse>> PreviewAsync(
        Stream fileStream, string fileName, UnifiedImportKind expectedKind, CancellationToken ct)
    {
        if (fileStream is null || string.IsNullOrWhiteSpace(fileName))
            return ApiResponse<UnifiedImportPreviewResponse>.Fail("Vui lòng chọn tệp nhập liệu hợp lệ.");

        await using var buffer = new MemoryStream();
        await CopyBoundedAsync(fileStream, buffer, MaxUploadBytes, ct);
        if (buffer.Length == 0)
            return ApiResponse<UnifiedImportPreviewResponse>.Fail("Tệp tải lên đang rỗng.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var bytes = buffer.ToArray();

        if (!MatchesFileMagicBytes(ext, bytes))
        {
            return ApiResponse<UnifiedImportPreviewResponse>.Fail(
                $"Nội dung tệp không khớp định dạng '{ext}' (lỗi kiểm tra chữ ký tệp).",
                HttpStatusCode.BadRequest);
        }

        var preview = new UnifiedImportPreviewResponse
        {
            FileName = Path.GetFileName(fileName),
            DetectedFormat = ext.TrimStart('.').ToUpperInvariant(),
        };

        try
        {
            switch (ext)
            {
                case ".xlsx":
                    await ParseExcelAsync(bytes, preview, mediaAssets: null, ct);
                    break;
                case ".json":
                    await ParseJsonAsync(bytes, expectedKind, preview, mediaAssets: null, ct);
                    break;
                case ".zip":
                    await ParseZipAsync(bytes, expectedKind, preview, onConfirmStorage: false, actorUserId: 0, ct);
                    break;
                case ".pdf":
                    ParsePdf(bytes, expectedKind, preview, ct);
                    break;
                default:
                    return ApiResponse<UnifiedImportPreviewResponse>.Fail(
                        "Định dạng chưa hỗ trợ. Vui lòng chọn .xlsx, .json, .zip hoặc .pdf có lớp văn bản.");
            }
        }
        catch (InvalidOperationException ex)
        {
            preview.FileErrors.Add(ex.Message);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            preview.FileErrors.Add($"Không thể đọc tệp nhập liệu: {ex.Message}");
        }

        await ValidateAndCheckDuplicatesAsync(preview, ct);
        return ApiResponse<UnifiedImportPreviewResponse>.Success(preview);
    }

    public async Task<ApiResponse<ConfirmUnifiedImportResponse>> ConfirmAsync(
        ConfirmUnifiedImportRequest request,
        IReadOnlyDictionary<string, (Stream Content, string ContentType, string FileName)>? mediaFiles,
        int actorUserId,
        CancellationToken ct)
    {
        if (actorUserId <= 0)
            return ApiResponse<ConfirmUnifiedImportResponse>.Fail("Không xác định được tài khoản tác giả.", HttpStatusCode.Unauthorized);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var idemKey = request.IdempotencyKey.Trim();
            var existing = await unitOfWork.ContentApprovalRequests.Query()
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.SubmittedByUserId == actorUserId && r.IdempotencyKey == idemKey)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                return ApiResponse<ConfirmUnifiedImportResponse>.Success(new ConfirmUnifiedImportResponse
                {
                    BatchId = existing[0].BatchId,
                    DeduplicatedByIdempotency = true,
                    PassagesDrafted = existing.Count(e => e.EntityType == ImportEntityType.Passage),
                    QuestionsDrafted = existing.Count(e => e.EntityType == ImportEntityType.QuestionBank),
                    ExamDraftId = existing.FirstOrDefault(e => e.EntityType is ImportEntityType.MockTest or ImportEntityType.PracticeSet)?.TargetEntityId,
                    ResultingStatus = ContentStatus.Draft,
                    Warnings = ["Yêu cầu này đã được xác nhận trước đó (idempotent)."],
                });
            }
        }

        // Upload staged external media files ONLY at Confirm time (zero side effects on Preview)
        var uploadedMediaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mediaFiles is { Count: > 0 })
        {
            foreach (var (key, entry) in mediaFiles)
            {
                var uploaded = await fileStorage.UploadAsync(entry.Content, entry.FileName, entry.ContentType, "import-media", ct);
                uploadedMediaMap[key] = uploaded.Url;
                uploadedMediaMap[Path.GetFileName(key)] = uploaded.Url;
            }
        }

        var levels = await unitOfWork.CertificateLevels.Query().Where(l => !l.IsDeleted).ToListAsync(ct);
        var skills = await unitOfWork.LanguageSkills.Query().Where(s => !s.IsDeleted).ToListAsync(ct);
        var certSections = await unitOfWork.CertificateSections.Query().Where(c => !c.IsDeleted).ToListAsync(ct);
        var allowedLevels = levels
            .Where(l => !string.IsNullOrWhiteSpace(l.Code) && CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains(l.Code.Trim().ToUpperInvariant()))
            .Select(l => l.Code.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var passageRows = request.Rows.Where(r => r.PassagePayload is not null).ToList();
        var questionRows = request.Rows.Where(r => r.QuestionPayload is not null).ToList();

        if (passageRows.Count == 0 && questionRows.Count == 0 && request.ExamBlueprint is null)
            return ApiResponse<ConfirmUnifiedImportResponse>.Fail("Không có dòng hợp lệ nào để xác nhận nhập.");

        var validationFailures = new List<string>();
        var result = new ConfirmUnifiedImportResponse { BatchId = batchId, ResultingStatus = ContentStatus.Draft };
        var idempotencyApplied = false;
        var requireListeningAudio = IsExcelSource(request.SourceFormat);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var passageByCode = new Dictionary<string, ContentPassage>(StringComparer.OrdinalIgnoreCase);

            foreach (var pRow in passageRows)
            {
                var pDto = pRow.PassagePayload!;
                RewriteMediaUrl(pDto, uploadedMediaMap);
                ResolveLevelSkillOnPassage(pDto, levels, skills);
                var pVal = CanonicalQuestionValidator.ValidatePassage(
                    pDto,
                    forSubmitOrPublish: false,
                    requireListeningAudio: requireListeningAudio);
                if (!pVal.CanSaveDraft)
                {
                    validationFailures.Add($"Nhóm/Đoạn '{pDto.Title}': {string.Join(" ", pVal.Errors.Select(e => e.Message))}");
                    continue;
                }

                var entity = QuestionBankService.BuildPassageEntity(pDto, pVal.ContentFingerprint, actorUserId);
                entity.SourceType = NormalizeSourceType(request.SourceFormat);
                entity.PublishStatus = ContentStatus.Draft;
                await unitOfWork.ContentPassages.AddAsync(entity, token);
                await unitOfWork.SaveChangesAsync(token);

                if (!string.IsNullOrWhiteSpace(pDto.PassageCode))
                    passageByCode[pDto.PassageCode.Trim()] = entity;

                var pApproval = new ContentApprovalRequest
                {
                    BatchId = batchId,
                    EntityType = ImportEntityType.Passage,
                    Action = ImportRowAction.New,
                    TargetEntityId = entity.Id,
                    RowKey = $"{pDto.CertificateLevelCode} · {pDto.SkillName} · {pDto.Title}",
                    PayloadJson = JsonSerializer.Serialize(pDto, JsonOpts),
                    SourceRowNumber = pRow.RowNumber,
                    SourceFileName = request.SourceFileName,
                    Status = ContentApprovalStatus.Draft,
                    SubmittedByUserId = actorUserId,
                    SubmittedAt = now,
                    SourceType = entity.SourceType,
                    SourceReference = entity.SourceReference,
                    RightsStatus = entity.RightsStatus,
                    CertificateLevelId = entity.CertificateLevelId,
                    LanguageSkillId = entity.LanguageSkillId,
                    IdempotencyKey = !idempotencyApplied && !string.IsNullOrWhiteSpace(request.IdempotencyKey) ? request.IdempotencyKey.Trim() : null,
                };
                idempotencyApplied = true;
                await unitOfWork.ContentApprovalRequests.AddAsync(pApproval, token);
                result.PassagesDrafted++;
            }

            var createdQuestions = new List<(QuestionBankItem Entity, CanonicalQuestionDto Dto, int SectionOrder)>();
            var rowOrder = 1;
            foreach (var qRow in questionRows)
            {
                var qDto = qRow.QuestionPayload!;
                RewriteMediaUrl(qDto, uploadedMediaMap);
                ResolveLevelSkillOnQuestion(qDto, levels, skills);

                int? passageId = qDto.PassageId;
                ContentPassage? linkedPassage = null;
                if (!passageId.HasValue && !string.IsNullOrWhiteSpace(qDto.PassageCode) &&
                    passageByCode.TryGetValue(qDto.PassageCode.Trim(), out linkedPassage))
                {
                    passageId = linkedPassage.Id;
                    if (string.IsNullOrWhiteSpace(qDto.AudioUrl) && !string.IsNullOrWhiteSpace(linkedPassage.AudioUrl))
                        qDto.AudioUrl = linkedPassage.AudioUrl;
                }
                else if (qDto.Passage is not null && !string.IsNullOrWhiteSpace(qDto.Passage.PassageText) && !passageId.HasValue)
                {
                    var key = !string.IsNullOrWhiteSpace(qDto.Passage.PassageCode) ? qDto.Passage.PassageCode : qDto.Passage.Title;
                    if (!passageByCode.TryGetValue(key, out linkedPassage))
                    {
                        qDto.Passage.CertificateLevelId = qDto.CertificateLevelId;
                        qDto.Passage.CertificateLevelCode = qDto.CertificateLevelCode;
                        qDto.Passage.LanguageSkillId = qDto.LanguageSkillId;
                        qDto.Passage.SkillName = qDto.SkillName;
                        var pVal = CanonicalQuestionValidator.ValidatePassage(qDto.Passage, false);
                        linkedPassage = QuestionBankService.BuildPassageEntity(qDto.Passage, pVal.ContentFingerprint, actorUserId);
                        linkedPassage.SourceType = NormalizeSourceType(request.SourceFormat);
                        await unitOfWork.ContentPassages.AddAsync(linkedPassage, token);
                        await unitOfWork.SaveChangesAsync(token);
                        passageByCode[key] = linkedPassage;
                        result.PassagesDrafted++;
                    }
                    passageId = linkedPassage.Id;
                }

                var qVal = CanonicalQuestionValidator.ValidateQuestion(
                    qDto,
                    forSubmitOrPublish: false,
                    allowedLevels,
                    qRow.RowNumber,
                    requireListeningAudio: requireListeningAudio);
                if (!qVal.CanSaveDraft)
                {
                    validationFailures.Add($"Dòng {qRow.RowNumber}: {string.Join(" ", qVal.Errors.Select(e => e.Message))}");
                    continue;
                }

                if (qVal.IncompleteMedia)
                    result.Warnings.Add($"Dòng {qRow.RowNumber}: Thiếu file audio Nghe hiểu — lưu Draft chưa hoàn chỉnh.");

                var qEntity = new QuestionBankItem
                {
                    QuestionIdentity = Guid.NewGuid(),
                    Version = 1,
                    PublishStatus = ContentStatus.Draft,
                    AuthorUserId = actorUserId,
                };
                qDto.SourceType = NormalizeSourceType(request.SourceFormat);
                QuestionBankService.ApplyQuestionDtoToEntity(qEntity, qDto, qVal.ContentFingerprint, passageId);
                await unitOfWork.QuestionBankItems.AddAsync(qEntity, token);
                await unitOfWork.SaveChangesAsync(token);

                createdQuestions.Add((qEntity, qDto, qDto.GroupSortOrder > 0 ? qDto.GroupSortOrder : rowOrder));

                var approval = new ContentApprovalRequest
                {
                    BatchId = batchId,
                    EntityType = ImportEntityType.QuestionBank,
                    Action = ImportRowAction.New,
                    TargetEntityId = qEntity.Id,
                    RowKey = $"{qDto.CertificateLevelCode} · {qDto.SkillName} · {(qDto.QuestionText.Length > 45 ? qDto.QuestionText[..45] + "…" : qDto.QuestionText)}",
                    PayloadJson = JsonSerializer.Serialize(qDto, JsonOpts),
                    SourceRowNumber = qRow.RowNumber > 0 ? qRow.RowNumber : rowOrder,
                    SourceFileName = request.SourceFileName,
                    Status = ContentApprovalStatus.Draft,
                    SubmittedByUserId = actorUserId,
                    SubmittedAt = now,
                    SourceType = qEntity.SourceType,
                    SourceReference = qEntity.SourceReference,
                    RightsStatus = qEntity.RightsStatus,
                    CertificateLevelId = qEntity.CertificateLevelId,
                    LanguageSkillId = qEntity.LanguageSkillId,
                    ValidationSummaryJson = JsonSerializer.Serialize(qVal, JsonOpts),
                    IdempotencyKey = !idempotencyApplied && !string.IsNullOrWhiteSpace(request.IdempotencyKey) ? request.IdempotencyKey.Trim() : null,
                };
                idempotencyApplied = true;
                await unitOfWork.ContentApprovalRequests.AddAsync(approval, token);
                result.QuestionsDrafted++;
                rowOrder++;
            }

            if (validationFailures.Count > 0)
                throw new InvalidOperationException(string.Join(" | ", validationFailures.Take(5)));

            if (request.ExamBlueprint is not null)
            {
                var bp = request.ExamBlueprint;
                var bpLevel = levels.FirstOrDefault(l => l.Code.Equals(bp.CertificateLevelCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Không tìm thấy cấp độ '{bp.CertificateLevelCode}'.");

                var examKind = Enum.TryParse<ExamKind>(bp.ExamKind, true, out var parsedKind)
                    ? parsedKind : ExamKind.FullMockTest;

                var mockTest = new MockTest
                {
                    Title = bp.Title.Trim(),
                    Description = bp.Description?.Trim(),
                    TimeLimitMinutes = Math.Max(1, bp.TimeLimitMinutes),
                    CertificateLevelId = bpLevel.Id,
                    ExamKind = examKind,
                    PublishStatus = ContentStatus.Draft,
                    Version = Math.Max(1, bp.Version),
                    IsManagedWorkflow = true,
                    AuthorUserId = actorUserId,
                    SourceType = NormalizeSourceType(request.SourceFormat),
                    SourceReference = bp.SourceReference?.Trim(),
                    RightsStatus = bp.RightsStatus?.Trim(),
                    IncludedSkillsJson = bp.IncludedSkills.Count > 0 ? JsonSerializer.Serialize(bp.IncludedSkills, JsonOpts) : null,
                    BlueprintPolicyJson = JsonSerializer.Serialize(new
                    {
                        examKind = examKind.ToString(),
                        showAnswersAfterSubmit = true,
                        showExplanationsAfterSubmit = true,
                    }, JsonOpts),
                    Sections = [],
                };

                var secList = bp.Sections.Count > 0
                    ? bp.Sections
                    : [new UnifiedImportSectionPreviewDto
                    {
                        SortOrder = 1,
                        Title = bp.Title,
                        SkillName = createdQuestions.FirstOrDefault().Dto?.SkillName ?? "Vocabulary",
                    }];

                foreach (var sec in secList.OrderBy(s => s.SortOrder))
                {
                    var skillNorm = CanonicalQuestionValidator.NormalizeSkillName(sec.SkillName);
                    var skill = skills.FirstOrDefault(s => CanonicalQuestionValidator.NormalizeSkillName(s.Name).Equals(skillNorm, StringComparison.OrdinalIgnoreCase))
                        ?? skills.First();
                    var certSec = (!string.IsNullOrWhiteSpace(sec.CertificateSectionCode)
                        ? certSections.FirstOrDefault(c => c.Code.Equals(sec.CertificateSectionCode.Trim(), StringComparison.OrdinalIgnoreCase))
                        : null)
                        ?? certSections.FirstOrDefault(c => c.CertificateLevelId == bpLevel.Id)
                        ?? throw new InvalidOperationException($"Chưa cấu hình CertificateSection cho cấp độ {bpLevel.Code}.");

                    var mockSection = new MockTestSection
                    {
                        Title = sec.Title.Trim(),
                        SortOrder = sec.SortOrder,
                        LanguageSkillId = skill.Id,
                        CertificateSectionId = certSec.Id,
                        DurationMinutes = sec.DurationMinutes,
                        Instructions = sec.Instructions?.Trim(),
                        Questions = [],
                    };

                    // Liên kết các câu hỏi mới tạo cùng section hoặc từ ngân hàng câu hỏi sẵn có
                    var matchingCreated = secList.Count == 1
                        ? createdQuestions
                        : createdQuestions.Where(c => c.Dto.GroupSortOrder == sec.SortOrder ||
                            CanonicalQuestionValidator.NormalizeSkillName(c.Dto.SkillName).Equals(skillNorm, StringComparison.OrdinalIgnoreCase)).ToList();

                    var qOrder = 1;
                    foreach (var (bankEntity, qDto, _) in matchingCreated)
                    {
                        mockSection.Questions.Add(CreateLinkedMockQuestion(bankEntity, qOrder++));
                    }

                    if (sec.QuestionBankItemIds.Count > 0)
                    {
                        var referencedItems = await unitOfWork.QuestionBankItems.Query()
                            .Where(q => !q.IsDeleted && sec.QuestionBankItemIds.Contains(q.Id))
                            .ToListAsync(token);
                        foreach (var bankItem in referencedItems)
                            mockSection.Questions.Add(CreateLinkedMockQuestion(bankItem, qOrder++));
                    }

                    mockTest.Sections.Add(mockSection);
                }

                await unitOfWork.MockTests.AddAsync(mockTest, token);
                await unitOfWork.SaveChangesAsync(token);
                result.ExamDraftId = mockTest.Id;

                var examApproval = new ContentApprovalRequest
                {
                    BatchId = batchId,
                    EntityType = examKind == ExamKind.FullMockTest ? ImportEntityType.MockTest : ImportEntityType.PracticeSet,
                    Action = ImportRowAction.New,
                    TargetEntityId = mockTest.Id,
                    RowKey = $"{mockTest.Title} / {bpLevel.Code} (v{mockTest.Version})",
                    PayloadJson = JsonSerializer.Serialize(bp, JsonOpts),
                    SourceRowNumber = 1,
                    SourceFileName = request.SourceFileName,
                    Status = ContentApprovalStatus.Draft,
                    SubmittedByUserId = actorUserId,
                    SubmittedAt = now,
                    SourceType = mockTest.SourceType ?? "Import",
                    SourceReference = mockTest.SourceReference,
                    RightsStatus = mockTest.RightsStatus,
                    CertificateLevelId = mockTest.CertificateLevelId,
                    IdempotencyKey = !idempotencyApplied && !string.IsNullOrWhiteSpace(request.IdempotencyKey) ? request.IdempotencyKey.Trim() : null,
                };
                idempotencyApplied = true;
                await unitOfWork.ContentApprovalRequests.AddAsync(examApproval, token);
            }

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        return ApiResponse<ConfirmUnifiedImportResponse>.Success(result);
    }

    public async Task<ApiResponse<ConfirmUnifiedImportResponse>> ConfirmFromArchiveAsync(
        Stream zipOrDataStream,
        string fileName,
        UnifiedImportKind expectedKind,
        string? idempotencyKey,
        int actorUserId,
        CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await CopyBoundedAsync(zipOrDataStream, buffer, MaxUploadBytes, ct);
        var bytes = buffer.ToArray();

        if (!MatchesFileMagicBytes(".zip", bytes))
            return ApiResponse<ConfirmUnifiedImportResponse>.Fail("Gói nhập liệu trực tiếp phải là file .zip hợp lệ.");

        var preview = new UnifiedImportPreviewResponse
        {
            FileName = Path.GetFileName(fileName),
            DetectedFormat = "ZIP",
        };
        var extractedStreams = new Dictionary<string, (Stream Content, string ContentType, string FileName)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var assets = await ParseZipAsync(bytes, expectedKind, preview, onConfirmStorage: true, actorUserId, ct);
            await ValidateAndCheckDuplicatesAsync(preview, ct);

            if (preview.ErrorCount > 0 || preview.FileErrors.Count > 0)
            {
                return ApiResponse<ConfirmUnifiedImportResponse>.Fail(
                    string.Join(" | ", preview.FileErrors.Concat(preview.Rows.SelectMany(r => r.Issues.Where(i => i.Severity == "Error").Select(i => i.Message))).Take(6)),
                    HttpStatusCode.BadRequest);
            }

            foreach (var asset in assets)
            {
                var ms = new MemoryStream(asset.Bytes, writable: false);
                extractedStreams[asset.ArchivePath] = (ms, asset.ContentType, asset.FileName);
            }

            return await ConfirmAsync(new ConfirmUnifiedImportRequest
            {
                SourceFileName = preview.FileName,
                SourceFormat = "Zip",
                IdempotencyKey = idempotencyKey,
                ExamBlueprint = preview.ExamBlueprint,
                Rows = preview.Rows,
            }, extractedStreams, actorUserId, ct);
        }
        finally
        {
            foreach (var item in extractedStreams.Values)
                await item.Content.DisposeAsync();
        }
    }

    private sealed record ExtractedZipMedia(string ArchivePath, string FileName, string ContentType, byte[] Bytes);

    private async Task<List<ExtractedZipMedia>> ParseZipAsync(
        byte[] bytes,
        UnifiedImportKind expectedKind,
        UnifiedImportPreviewResponse preview,
        bool onConfirmStorage,
        int actorUserId,
        CancellationToken ct)
    {
        var mediaList = new List<ExtractedZipMedia>();
        await using var ms = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > MaxZipEntries)
            throw new InvalidOperationException($"Gói ZIP vượt quá số tệp tối đa ({MaxZipEntries}).");

        long totalUncompressed = 0;
        byte[]? manifestJsonBytes = null;
        byte[]? workbookBytes = null;

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry

            var fullPath = entry.FullName.Replace('\\', '/');
            if (fullPath.StartsWith('/') || fullPath.Contains("..", StringComparison.Ordinal) || fullPath.Contains('\0'))
                throw new InvalidOperationException($"Đường dẫn trong ZIP không an toàn: '{entry.FullName}'.");

            if (entry.CompressedLength > 0 && (double)entry.Length / entry.CompressedLength > MaxZipCompressionRatio)
                throw new InvalidOperationException($"Phát hiện tỷ lệ nén bất thường (ZIP bomb) tại '{entry.FullName}'.");

            totalUncompressed += entry.Length;
            if (totalUncompressed > MaxZipUncompressedBytes)
                throw new InvalidOperationException($"Tổng dung lượng giải nén ZIP vượt quá {MaxZipUncompressedBytes / (1024 * 1024)} MB.");

            await using var entryStream = entry.Open();
            await using var entryMem = new MemoryStream();
            await CopyBoundedAsync(entryStream, entryMem, MaxZipUncompressedBytes, ct);
            var entryBytes = entryMem.ToArray();

            var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (ext == ".json" && (entry.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || manifestJsonBytes is null) &&
                !entry.Name.Equals("checksums.json", StringComparison.OrdinalIgnoreCase))
            {
                manifestJsonBytes = entryBytes;
            }
            else if (ext == ".xlsx" && workbookBytes is null)
            {
                workbookBytes = entryBytes;
            }
            else if (IsAllowedMediaExt(ext))
            {
                if (!MatchesMediaMagicBytes(ext, entryBytes))
                    throw new InvalidOperationException($"Tệp media '{entry.FullName}' không đúng định dạng chữ ký byte.");

                var sha = Convert.ToHexString(SHA256.HashData(entryBytes));
                preview.MediaAssets.Add(new UnifiedImportMediaAssetDto
                {
                    ArchivePath = fullPath,
                    FileName = entry.Name,
                    ContentType = MediaMimeType(ext),
                    SizeBytes = entryBytes.Length,
                    Sha256 = sha,
                });

                if (onConfirmStorage && actorUserId > 0)
                    mediaList.Add(new ExtractedZipMedia(fullPath, entry.Name, MediaMimeType(ext), entryBytes));
            }
        }

        if (manifestJsonBytes is not null)
        {
            await ParseJsonAsync(manifestJsonBytes, expectedKind, preview, preview.MediaAssets, ct);
        }
        else if (workbookBytes is not null)
        {
            await ParseExcelAsync(workbookBytes, preview, preview.MediaAssets, ct);
        }
        else
        {
            throw new InvalidOperationException("Gói ZIP phải chứa tệp manifest.json hoặc một tệp Excel (.xlsx).");
        }

        return mediaList;
    }

    private static Task ParseExcelAsync(
        byte[] bytes,
        UnifiedImportPreviewResponse preview,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        CancellationToken ct)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var wb = new XLWorkbook(ms);

        var sheetNames = wb.Worksheets.Select(w => w.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (sheetNames.Contains("ExamBlueprint") || sheetNames.Contains("PracticeManifest"))
        {
            ParseExamWorkbook(wb, sheetNames, preview, mediaAssets, ct);
        }
        else if (sheetNames.Contains("Passages") || sheetNames.Contains("ReadingQuestions"))
        {
            preview.DetectedKind = "Reading";
            ParseReadingWorkbook(wb, preview, ct);
        }
        else if (sheetNames.Contains("ListeningGroups") || sheetNames.Contains("ListeningQuestions"))
        {
            preview.DetectedKind = "Listening";
            ParseListeningWorkbook(wb, preview, mediaAssets, ct);
        }
        else if (sheetNames.Contains("VocabularyQuestions"))
        {
            preview.DetectedKind = "Vocabulary";
            ParseFlatSkillSheet(wb.Worksheet("VocabularyQuestions"), "Vocabulary", preview, mediaAssets, ct);
        }
        else if (sheetNames.Contains("KanjiQuestions"))
        {
            preview.DetectedKind = "Kanji";
            ParseFlatSkillSheet(wb.Worksheet("KanjiQuestions"), "Kanji", preview, mediaAssets, ct);
        }
        else if (sheetNames.Contains("GrammarQuestions"))
        {
            preview.DetectedKind = "Grammar";
            ParseFlatSkillSheet(wb.Worksheet("GrammarQuestions"), "Grammar", preview, mediaAssets, ct);
        }
        else
        {
            preview.FileWarnings.Add("Không tìm thấy sheet chuẩn V2 (VocabularyQuestions, KanjiQuestions, GrammarQuestions, ReadingQuestions, ListeningQuestions, PracticeManifest, ExamBlueprint). Hãy dùng nút Tải template hoặc luồng Excel tiêu chuẩn.");
        }

        return Task.CompletedTask;
    }

    private static void ParseFlatSkillSheet(
        IXLWorksheet ws,
        string skillName,
        UnifiedImportPreviewResponse preview,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        CancellationToken ct)
    {
        foreach (var row in ReadWorksheetRows(ws))
        {
            ct.ThrowIfCancellationRequested();
            if (IsSampleRow(row)) continue;

            var q = MapRowToQuestion(row, skillName, mediaAssets);
            preview.Rows.Add(new UnifiedImportRowPreviewDto
            {
                RowNumber = row.RowNumber,
                RecordType = "Question",
                RowKey = q.QuestionText.Length > 60 ? q.QuestionText[..60] + "…" : q.QuestionText,
                LevelCode = q.CertificateLevelCode,
                SkillName = q.SkillName,
                TemplateKind = q.TemplateKind,
                QuestionType = q.QuestionType,
                SummaryText = q.QuestionText,
                QuestionPayload = q,
            });
        }
    }

    private static void ParseReadingWorkbook(XLWorkbook wb, UnifiedImportPreviewResponse preview, CancellationToken ct)
    {
        if (wb.Worksheets.Contains("Passages"))
        {
            foreach (var row in ReadWorksheetRows(wb.Worksheet("Passages")))
            {
                ct.ThrowIfCancellationRequested();
                if (IsSampleRow(row)) continue;
                var code = GetCell(row, "PassageCode");
                var p = new CanonicalPassageDto
                {
                    PassageCode = string.IsNullOrWhiteSpace(code) ? $"PSG-{row.RowNumber}" : code,
                    CertificateLevelCode = GetCell(row, "LevelCode", "CertificateLevelCode"),
                    SkillName = "Reading",
                    Title = GetCell(row, "Title"),
                    Topic = EmptyToNull(GetCell(row, "Topic")),
                    PassageText = EmptyToNull(GetCell(row, "PassageText")),
                    Instructions = EmptyToNull(GetCell(row, "Instructions")),
                    ImageUrl = EmptyToNull(GetCell(row, "ImageUrl")),
                    SourceReference = EmptyToNull(GetCell(row, "Source")),
                    SourceType = "ImportExcel",
                };
                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = row.RowNumber,
                    RecordType = "Passage",
                    RowKey = p.PassageCode,
                    LevelCode = p.CertificateLevelCode,
                    SkillName = "Reading",
                    TemplateKind = SkillTemplateKinds.ReadingPassageMcq,
                    SummaryText = p.Title,
                    PassagePayload = p,
                });
            }
        }

        if (wb.Worksheets.Contains("ReadingQuestions"))
        {
            foreach (var row in ReadWorksheetRows(wb.Worksheet("ReadingQuestions")))
            {
                ct.ThrowIfCancellationRequested();
                if (IsSampleRow(row)) continue;
                var q = MapRowToQuestion(row, "Reading", null);
                q.PassageCode = EmptyToNull(GetCell(row, "PassageCode"));
                q.QuestionGroupTitle = EmptyToNull(GetCell(row, "QuestionGroupTitle"));
                q.GroupSortOrder = ParseIntOrDefault(GetCell(row, "GroupSortOrder"), row.RowNumber);
                if (string.IsNullOrWhiteSpace(q.CertificateLevelCode) && !string.IsNullOrWhiteSpace(q.PassageCode))
                {
                    q.CertificateLevelCode = preview.Rows
                        .FirstOrDefault(r => r.PassagePayload?.PassageCode.Equals(q.PassageCode, StringComparison.OrdinalIgnoreCase) == true)
                        ?.LevelCode ?? string.Empty;
                }
                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = row.RowNumber,
                    RecordType = "Question",
                    RowKey = $"{q.PassageCode} #{q.GroupSortOrder}",
                    LevelCode = q.CertificateLevelCode,
                    SkillName = "Reading",
                    TemplateKind = SkillTemplateKinds.ReadingPassageMcq,
                    QuestionType = q.QuestionType,
                    SummaryText = q.QuestionText,
                    QuestionPayload = q,
                });
            }
        }
    }

    private static void ParseListeningWorkbook(
        XLWorkbook wb,
        UnifiedImportPreviewResponse preview,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        CancellationToken ct)
    {
        if (wb.Worksheets.Contains("ListeningGroups"))
        {
            foreach (var row in ReadWorksheetRows(wb.Worksheet("ListeningGroups")))
            {
                ct.ThrowIfCancellationRequested();
                if (IsSampleRow(row)) continue;
                var code = GetCell(row, "GroupCode", "PassageCode");
                var rawAudio = EmptyToNull(GetCell(row, "AudioUrl", "AudioFile"));
                var p = new CanonicalPassageDto
                {
                    PassageCode = string.IsNullOrWhiteSpace(code) ? $"LST-{row.RowNumber}" : code,
                    CertificateLevelCode = GetCell(row, "LevelCode", "CertificateLevelCode"),
                    SkillName = "Listening",
                    ListeningSection = EmptyToNull(GetCell(row, "ListeningSection")),
                    Title = GetCell(row, "Title"),
                    AudioUrl = ResolvePreviewAssetHint(rawAudio, mediaAssets),
                    AudioDurationSeconds = ParseNullableInt(GetCell(row, "AudioDurationSeconds")),
                    Instructions = EmptyToNull(GetCell(row, "Instructions")),
                    AudioTranscript = EmptyToNull(GetCell(row, "Transcript", "AudioTranscript")),
                    PlaybackPolicy = new ListeningPlaybackPolicyDto
                    {
                        MaxReplays = ParseNullableInt(GetCell(row, "MaxReplays")) ?? 2,
                        AllowPause = ParseBoolOrDefault(GetCell(row, "AllowPause"), true),
                        ShowTranscriptAfterSubmit = true,
                    },
                    SourceReference = EmptyToNull(GetCell(row, "Source")),
                    SourceType = "ImportExcel",
                };
                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = row.RowNumber,
                    RecordType = "ListeningGroup",
                    RowKey = p.PassageCode,
                    LevelCode = p.CertificateLevelCode,
                    SkillName = "Listening",
                    TemplateKind = SkillTemplateKinds.ListeningAudioMcq,
                    SummaryText = p.Title,
                    PassagePayload = p,
                });
            }
        }

        if (wb.Worksheets.Contains("ListeningQuestions"))
        {
            foreach (var row in ReadWorksheetRows(wb.Worksheet("ListeningQuestions")))
            {
                ct.ThrowIfCancellationRequested();
                if (IsSampleRow(row)) continue;
                var q = MapRowToQuestion(row, "Listening", mediaAssets);
                q.PassageCode = EmptyToNull(GetCell(row, "GroupCode", "PassageCode"));
                q.GroupSortOrder = ParseIntOrDefault(GetCell(row, "GroupSortOrder"), row.RowNumber);
                var group = preview.Rows.FirstOrDefault(r =>
                    r.PassagePayload?.PassageCode.Equals(q.PassageCode, StringComparison.OrdinalIgnoreCase) == true)?.PassagePayload;
                if (group is not null)
                {
                    if (string.IsNullOrWhiteSpace(q.CertificateLevelCode)) q.CertificateLevelCode = group.CertificateLevelCode;
                    if (string.IsNullOrWhiteSpace(q.AudioUrl)) q.AudioUrl = group.AudioUrl;
                    q.PlaybackPolicy ??= group.PlaybackPolicy;
                    if (string.IsNullOrWhiteSpace(q.AudioTranscript)) q.AudioTranscript = group.AudioTranscript;
                }
                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = row.RowNumber,
                    RecordType = "Question",
                    RowKey = $"{q.PassageCode} #{q.GroupSortOrder}",
                    LevelCode = q.CertificateLevelCode,
                    SkillName = "Listening",
                    TemplateKind = q.TemplateKind,
                    QuestionType = q.QuestionType,
                    SummaryText = q.QuestionText,
                    QuestionPayload = q,
                });
            }
        }
    }

    private static void ParseExamWorkbook(
        XLWorkbook wb,
        HashSet<string> sheetNames,
        UnifiedImportPreviewResponse preview,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        CancellationToken ct)
    {
        var isMixed = sheetNames.Contains("PracticeManifest");
        preview.DetectedKind = isMixed ? "MixedPractice" : "FullMockTest";

        var manifestSheet = wb.Worksheet(isMixed ? "PracticeManifest" : "ExamBlueprint");
        var metaRow = ReadWorksheetRows(manifestSheet).FirstOrDefault(r => !IsSampleRow(r));
        if (metaRow is null)
        {
            preview.FileErrors.Add($"Sheet '{manifestSheet.Name}' không có dữ liệu cấu hình đề.");
            return;
        }

        var bp = new UnifiedImportExamPreviewDto
        {
            Title = GetCell(metaRow, "Title"),
            ExamKind = isMixed ? "MixedPractice" : "FullMockTest",
            CertificateLevelCode = GetCell(metaRow, "LevelCode", "CertificateLevelCode"),
            Version = ParseIntOrDefault(GetCell(metaRow, "ExamVersion", "Version"), 1),
            TimeLimitMinutes = ParseIntOrDefault(GetCell(metaRow, "TotalTimeLimitMinutes", "TimeLimitMinutes"), 30),
            Description = EmptyToNull(GetCell(metaRow, "Description")),
            SourceReference = EmptyToNull(GetCell(metaRow, "Source")),
            RightsStatus = EmptyToNull(GetCell(metaRow, "RightsStatus")),
            IncludedSkills = GetCell(metaRow, "IncludedSkills")
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList(),
        };

        if (sheetNames.Contains("Sections") || sheetNames.Contains("QuestionSelection"))
        {
            var secSheet = wb.Worksheet(sheetNames.Contains("Sections") ? "Sections" : "QuestionSelection");
            foreach (var row in ReadWorksheetRows(secSheet))
            {
                if (IsSampleRow(row)) continue;
                bp.Sections.Add(new UnifiedImportSectionPreviewDto
                {
                    SortOrder = ParseIntOrDefault(GetCell(row, "SectionOrder", "SortOrder"), row.RowNumber),
                    Title = GetCell(row, "Title", "SectionTitle"),
                    SkillName = GetCell(row, "SkillName", "LanguageSkillName"),
                    CertificateSectionCode = EmptyToNull(GetCell(row, "CertificateSectionCode")),
                    DurationMinutes = ParseNullableInt(GetCell(row, "DurationMinutes")),
                    Instructions = EmptyToNull(GetCell(row, "Instructions")),
                    QuestionBankItemIds = GetCell(row, "QuestionBankItemId", "QuestionBankItemIds")
                        .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList(),
                });
            }
        }

        if (sheetNames.Contains("Passages"))
        {
            foreach (var row in ReadWorksheetRows(wb.Worksheet("Passages")))
            {
                ct.ThrowIfCancellationRequested();
                if (IsSampleRow(row)) continue;
                var skill = CanonicalQuestionValidator.NormalizeSkillName(GetCell(row, "SkillName"));
                if (string.IsNullOrEmpty(skill)) skill = string.IsNullOrWhiteSpace(GetCell(row, "AudioUrl")) ? "Reading" : "Listening";
                var p = new CanonicalPassageDto
                {
                    PassageCode = GetCell(row, "PassageCode"),
                    CertificateLevelCode = bp.CertificateLevelCode,
                    SkillName = skill,
                    Title = GetCell(row, "Title"),
                    PassageText = EmptyToNull(GetCell(row, "PassageText")),
                    AudioUrl = ResolvePreviewAssetHint(EmptyToNull(GetCell(row, "AudioUrl")), mediaAssets),
                    AudioDurationSeconds = ParseNullableInt(GetCell(row, "AudioDurationSeconds")),
                    AudioTranscript = EmptyToNull(GetCell(row, "Transcript", "AudioTranscript")),
                    PlaybackPolicy = new ListeningPlaybackPolicyDto { MaxReplays = ParseNullableInt(GetCell(row, "MaxReplays")) ?? 2, AllowPause = true, ShowTranscriptAfterSubmit = true },
                    SourceType = "ImportExcel",
                };
                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = row.RowNumber,
                    RecordType = skill == "Listening" ? "ListeningGroup" : "Passage",
                    RowKey = p.PassageCode,
                    LevelCode = p.CertificateLevelCode,
                    SkillName = p.SkillName,
                    SummaryText = p.Title,
                    PassagePayload = p,
                });
            }
        }

        if (sheetNames.Contains("Questions"))
        {
            foreach (var row in ReadWorksheetRows(wb.Worksheet("Questions")))
            {
                ct.ThrowIfCancellationRequested();
                if (IsSampleRow(row)) continue;
                var secOrder = ParseIntOrDefault(GetCell(row, "SectionOrder"), 1);
                var matchedSec = bp.Sections.FirstOrDefault(s => s.SortOrder == secOrder);
                var qSkill = !string.IsNullOrWhiteSpace(GetCell(row, "SkillName"))
                    ? GetCell(row, "SkillName")
                    : (matchedSec?.SkillName ?? "Vocabulary");
                var q = MapRowToQuestion(row, qSkill, mediaAssets);
                if (string.IsNullOrWhiteSpace(q.CertificateLevelCode)) q.CertificateLevelCode = bp.CertificateLevelCode;
                q.PassageCode = EmptyToNull(GetCell(row, "PassageCode"));
                q.GroupSortOrder = secOrder;
                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = row.RowNumber,
                    RecordType = "Question",
                    RowKey = $"S{secOrder} #{row.RowNumber}",
                    LevelCode = q.CertificateLevelCode,
                    SkillName = q.SkillName,
                    TemplateKind = q.TemplateKind,
                    QuestionType = q.QuestionType,
                    SummaryText = q.QuestionText,
                    QuestionPayload = q,
                });
            }
        }

        preview.ExamBlueprint = bp;
    }

    private Task ParseJsonAsync(
        byte[] bytes,
        UnifiedImportKind expectedKind,
        UnifiedImportPreviewResponse preview,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("isSample", out var isSampleEl) && isSampleEl.ValueKind == JsonValueKind.True)
        {
            preview.FileWarnings.Add("File JSON đang bật cờ isSample=true; vui lòng bỏ cờ mẫu trước khi nhập dữ liệu thật.");
        }

        preview.DetectedKind = expectedKind != UnifiedImportKind.Auto ? expectedKind.ToString() : "JsonPackage";

        if (root.ValueKind == JsonValueKind.Array)
        {
            var idx = 1;
            foreach (var el in root.EnumerateArray())
                AddJsonQuestion(el, idx++, mediaAssets, preview);
            return Task.CompletedTask;
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JSON phải là object hoặc mảng câu hỏi.");

        // Exam blueprint manifest (FullMockTest / MixedPractice / PracticeSet)
        if (root.TryGetProperty("examKind", out _) || root.TryGetProperty("sections", out _) || root.TryGetProperty("ExamKind", out _))
        {
            var bp = JsonSerializer.Deserialize<UnifiedImportExamPreviewDto>(bytes, JsonOpts);
            if (bp is not null)
            {
                preview.ExamBlueprint = bp;
                preview.DetectedKind = bp.ExamKind;
            }
        }

        var rowIdx = 1;
        if (TryGetArray(root, ["passages", "Passages"], out var passagesArr))
        {
            foreach (var pEl in passagesArr.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                AddJsonPassageWithQuestions(pEl, "Reading", ref rowIdx, mediaAssets, preview);
            }
        }
        if (TryGetArray(root, ["listeningGroups", "ListeningGroups"], out var listeningArr))
        {
            foreach (var pEl in listeningArr.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                AddJsonPassageWithQuestions(pEl, "Listening", ref rowIdx, mediaAssets, preview);
            }
        }
        if (TryGetArray(root, ["items", "questions", "Questions"], out var qArr))
        {
            foreach (var qEl in qArr.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                AddJsonQuestion(qEl, rowIdx++, mediaAssets, preview);
            }
        }

        return Task.CompletedTask;
    }

    private static void ParsePdf(
        byte[] bytes,
        UnifiedImportKind expectedKind,
        UnifiedImportPreviewResponse preview,
        CancellationToken ct)
    {
        preview.DetectedKind = expectedKind == UnifiedImportKind.Auto ? "PdfDocument" : expectedKind.ToString();
        using var document = PdfDocument.Open(bytes);
        if (document.NumberOfPages > MaxPdfPages)
            throw new InvalidOperationException($"PDF vượt quá số trang tối đa ({MaxPdfPages} trang).");

        var allLines = new List<string>();
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var text = page.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                allLines.AddRange(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
            }
        }

        var totalChars = allLines.Sum(l => l.Length);
        if (totalChars < 15)
        {
            preview.FileErrors.Add("PDF không có lớp văn bản (có thể là file scan/hình ảnh). Hệ thống không giả lập dữ liệu từ PDF scan; vui lòng dùng PDF có text chọn được hoặc template Excel/JSON.");
            return;
        }

        // Parse header metadata if present (Level: N4, Skill: Grammar, Title: ...)
        var detectedLevel = allLines.Select(l => Regex.Match(l, @"\b(N[2-5])\b", RegexOptions.IgnoreCase)).FirstOrDefault(m => m.Success)?.Groups[1].Value.ToUpperInvariant() ?? "N4";
        var detectedSkill = expectedKind switch
        {
            UnifiedImportKind.Vocabulary => "Vocabulary",
            UnifiedImportKind.Kanji => "Kanji",
            UnifiedImportKind.Grammar => "Grammar",
            UnifiedImportKind.Reading => "Reading",
            UnifiedImportKind.Listening => "Listening",
            _ => "Grammar",
        };

        CanonicalPassageDto? currentPassage = null;
        CanonicalQuestionDto? currentQ = null;
        var passageBuffer = new StringBuilder();
        var readingMode = false;

        void FlushCurrentQuestion()
        {
            if (currentQ is null) return;
            var qDto = currentQ;
            currentQ = null;

            var needsReview = qDto.Options.Count < 2 || qDto.Options.Count(o => o.IsCorrect) != 1;
            preview.Rows.Add(new UnifiedImportRowPreviewDto
            {
                RowNumber = preview.Rows.Count + 1,
                RecordType = "Question",
                RowKey = qDto.QuestionText.Length > 50 ? qDto.QuestionText[..50] + "…" : qDto.QuestionText,
                LevelCode = qDto.CertificateLevelCode,
                SkillName = qDto.SkillName,
                TemplateKind = qDto.TemplateKind,
                QuestionType = qDto.QuestionType,
                SummaryText = qDto.QuestionText,
                NeedsReview = needsReview,
                QuestionPayload = qDto,
            });
        }

        foreach (var line in allLines)
        {
            if (line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase) &&
                Regex.Match(line, @"\b(N[2-5])\b", RegexOptions.IgnoreCase) is { Success: true } lvlMatch)
            {
                detectedLevel = lvlMatch.Groups[1].Value.ToUpperInvariant();
                continue;
            }

            if (line.StartsWith("Passage:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Đoạn văn:", StringComparison.OrdinalIgnoreCase))
            {
                FlushCurrentQuestion();
                readingMode = true;
                passageBuffer.Clear();
                var content = line[(line.IndexOf(':') + 1)..].Trim();
                if (!string.IsNullOrEmpty(content)) passageBuffer.AppendLine(content);
                currentPassage = new CanonicalPassageDto
                {
                    PassageCode = $"PDF-P-{preview.Rows.Count + 1}",
                    CertificateLevelCode = detectedLevel,
                    SkillName = "Reading",
                    Title = $"Đoạn đọc từ {preview.FileName}",
                    SourceType = "ImportPdf",
                };
                continue;
            }

            var qMatch = PdfQuestionHeaderRegex().Match(line);
            if (qMatch.Success)
            {
                if (readingMode && currentPassage is not null)
                {
                    currentPassage.PassageText = passageBuffer.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(currentPassage.PassageText))
                    {
                        preview.Rows.Add(new UnifiedImportRowPreviewDto
                        {
                            RowNumber = preview.Rows.Count + 1,
                            RecordType = "Passage",
                            RowKey = currentPassage.PassageCode,
                            LevelCode = detectedLevel,
                            SkillName = "Reading",
                            SummaryText = currentPassage.Title,
                            PassagePayload = currentPassage,
                        });
                    }
                    readingMode = false;
                }

                FlushCurrentQuestion();
                var qSkill = currentPassage is not null ? "Reading" : detectedSkill;
                currentQ = new CanonicalQuestionDto
                {
                    CertificateLevelCode = detectedLevel,
                    SkillName = qSkill,
                    PassageCode = currentPassage?.PassageCode,
                    TemplateKind = CanonicalQuestionValidator.InferDefaultTemplateKind(qSkill, CanonicalQuestionTypes.SingleChoice),
                    QuestionType = CanonicalQuestionTypes.SingleChoice,
                    QuestionText = qMatch.Groups[2].Value.Trim(),
                    SourceType = "ImportPdf",
                };
                continue;
            }

            if (currentQ is not null)
            {
                var optMatch = PdfOptionRegex().Match(line);
                if (optMatch.Success)
                {
                    currentQ.Options.Add(new CanonicalOptionDto
                    {
                        SortOrder = currentQ.Options.Count + 1,
                        OptionText = optMatch.Groups[2].Value.Trim(),
                        IsCorrect = false,
                    });
                    continue;
                }

                var ansMatch = PdfAnswerRegex().Match(line);
                if (ansMatch.Success)
                {
                    var token = ansMatch.Groups[1].Value.Trim().ToUpperInvariant();
                    var correctIdx = token switch
                    {
                        "A" or "1" or "ア" => 0,
                        "B" or "2" or "イ" => 1,
                        "C" or "3" or "ウ" => 2,
                        "D" or "4" or "エ" => 3,
                        _ => -1,
                    };
                    if (correctIdx >= 0 && correctIdx < currentQ.Options.Count)
                        currentQ.Options[correctIdx].IsCorrect = true;
                    continue;
                }

                var expMatch = PdfExplanationRegex().Match(line);
                if (expMatch.Success)
                {
                    currentQ.Explanation = expMatch.Groups[1].Value.Trim();
                    continue;
                }

                if (currentQ.Options.Count == 0)
                    currentQ.QuestionText += "\n" + line;
            }
            else if (readingMode)
            {
                passageBuffer.AppendLine(line);
            }
        }

        FlushCurrentQuestion();

        if (preview.Rows.Count == 0)
        {
            preview.FileWarnings.Add("Không nhận diện được cấu trúc câu hỏi đánh số (1. ... / A. B. C. D. / Đáp án:) từ PDF.");
        }
        else if (preview.Rows.Any(r => r.NeedsReview))
        {
            preview.FileWarnings.Add("Một số câu từ PDF thiếu đáp án chuẩn hoặc ít lựa chọn (Needs Review) cần tác giả bổ sung trước khi Confirm.");
        }
    }

    private async Task ValidateAndCheckDuplicatesAsync(UnifiedImportPreviewResponse preview, CancellationToken ct)
    {
        var levels = await unitOfWork.CertificateLevels.Query().AsNoTracking().Where(l => !l.IsDeleted).ToListAsync(ct);
        var skills = await unitOfWork.LanguageSkills.Query().AsNoTracking().Where(s => !s.IsDeleted).ToListAsync(ct);
        var allowedCodes = levels
            .Where(l => !string.IsNullOrWhiteSpace(l.Code) && CanonicalQuestionValidator.SupportedJlptLevelCodes.Contains(l.Code.Trim().ToUpperInvariant()))
            .Select(l => l.Code.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in preview.Rows)
        {
            if (row.PassagePayload is not null)
            {
                ResolveLevelSkillOnPassage(row.PassagePayload, levels, skills);
                var pVal = CanonicalQuestionValidator.ValidatePassage(
                    row.PassagePayload,
                    forSubmitOrPublish: false,
                    requireListeningAudio: IsExcelSource(preview.DetectedFormat));
                row.Issues.AddRange(pVal.Errors);
                row.Issues.AddRange(pVal.Warnings);
                row.IncompleteMedia = pVal.IncompleteMedia;
                row.Status = !pVal.CanSaveDraft ? "Error" : (pVal.Warnings.Count > 0 ? "Warning" : "Valid");
            }
            else if (row.QuestionPayload is not null)
            {
                var q = row.QuestionPayload;
                ResolveLevelSkillOnQuestion(q, levels, skills);

                if (string.IsNullOrWhiteSpace(q.Passage?.PassageText) && !string.IsNullOrWhiteSpace(q.PassageCode))
                {
                    var matchingPassage = preview.Rows
                        .FirstOrDefault(r => r.PassagePayload?.PassageCode.Equals(q.PassageCode, StringComparison.OrdinalIgnoreCase) == true)
                        ?.PassagePayload;
                    if (matchingPassage is not null)
                    {
                        q.Passage = matchingPassage;
                        if (string.IsNullOrWhiteSpace(q.AudioUrl)) q.AudioUrl = matchingPassage.AudioUrl;
                    }
                }

                var qVal = CanonicalQuestionValidator.ValidateQuestion(
                    q,
                    forSubmitOrPublish: false,
                    allowedCodes,
                    row.RowNumber,
                    requireListeningAudio: IsExcelSource(preview.DetectedFormat));
                row.Issues.AddRange(qVal.Errors);
                row.Issues.AddRange(qVal.Warnings);
                row.IncompleteMedia = qVal.IncompleteMedia;

                if (!string.IsNullOrEmpty(qVal.ContentFingerprint) && !seenFingerprints.Add(qVal.ContentFingerprint))
                {
                    row.Issues.Add(new CanonicalValidationIssue("questionText", row.RowNumber, "Warning", "INTRA_FILE_DUPLICATE", "Câu hỏi bị trùng lặp với một dòng khác trong cùng file."));
                }

                if (q.CertificateLevelId > 0 && q.LanguageSkillId > 0 && !string.IsNullOrWhiteSpace(q.QuestionText))
                {
                    var dupRes = await questionBankService.CheckDuplicatesAsync(q, null, ct);
                    if (dupRes.IsSuccess && dupRes.Result is not null && dupRes.Result.Matches.Count > 0)
                    {
                        row.Duplicates = dupRes.Result.Matches;
                    }
                }

                row.Status = !qVal.CanSaveDraft
                    ? "Error"
                    : (row.Duplicates.Any(d => d.MatchKind == "ExactFingerprint")
                        ? "Duplicate"
                        : (row.Issues.Any(i => i.Severity == "Warning") || row.NeedsReview ? "Warning" : "Valid"));
            }
        }

        preview.TotalItems = preview.Rows.Count;
        preview.ErrorCount = preview.Rows.Count(r => r.Status == "Error") + preview.FileErrors.Count;
        preview.WarningCount = preview.Rows.Count(r => r.Status == "Warning");
        preview.DuplicateCount = preview.Rows.Count(r => r.Status == "Duplicate");
        preview.ValidCount = preview.Rows.Count(r => r.Status == "Valid");
        preview.CanConfirm = preview.FileErrors.Count == 0 && preview.Rows.Count > 0 && preview.Rows.All(r => r.Status != "Error");
    }

    private static CanonicalQuestionDto MapRowToQuestion(
        RawRow row, string defaultSkill, IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets)
    {
        var skill = CanonicalQuestionValidator.NormalizeSkillName(GetCell(row, "SkillName"));
        if (string.IsNullOrEmpty(skill)) skill = defaultSkill;

        var qType = CanonicalQuestionValidator.NormalizeQuestionType(GetCell(row, "QuestionType"));
        var template = EmptyToNull(GetCell(row, "TemplateKind")) ?? CanonicalQuestionValidator.InferDefaultTemplateKind(skill, qType);

        var options = new List<CanonicalOptionDto>();
        string[] optHeaders = ["Option1", "Option2", "Option3", "Option4"];
        var correctRaw = GetCell(row, "CorrectAnswer", "CorrectOption");
        var correctSet = ParseCorrectOptionIndices(correctRaw);

        for (var i = 0; i < optHeaders.Length; i++)
        {
            var txt = GetCell(row, optHeaders[i]);
            var img = ResolvePreviewAssetHint(EmptyToNull(GetCell(row, optHeaders[i] + "ImageUrl")), mediaAssets);
            if (!string.IsNullOrWhiteSpace(txt) || !string.IsNullOrWhiteSpace(img))
            {
                options.Add(new CanonicalOptionDto
                {
                    SortOrder = i + 1,
                    OptionText = txt,
                    ImageUrl = img,
                    IsCorrect = correctSet.Contains(i + 1),
                });
            }
        }

        var acceptedRaw = GetCell(row, "AcceptedAnswers");
        var tokensRaw = GetCell(row, "OrderingTokens");
        var answerTokensRaw = GetCell(row, "OrderingAnswer");

        return new CanonicalQuestionDto
        {
            CertificateLevelCode = GetCell(row, "LevelCode", "CertificateLevelCode"),
            SkillName = skill,
            TemplateKind = template,
            QuestionType = qType,
            QuestionText = GetCell(row, "QuestionPrompt", "QuestionText"),
            Options = options,
            Explanation = EmptyToNull(GetCell(row, "Explanation")),
            AudioUrl = ResolvePreviewAssetHint(EmptyToNull(GetCell(row, "AudioUrl", "AudioFile")), mediaAssets),
            ImageUrl = ResolvePreviewAssetHint(EmptyToNull(GetCell(row, "ImageUrl")), mediaAssets),
            Topic = EmptyToNull(GetCell(row, "Topic")),
            Tags = GetCell(row, "Tags").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
            SourceType = "ImportExcel",
            SourceReference = EmptyToNull(GetCell(row, "Source")),
            SkillMetadata = new CanonicalSkillMetadataDto
            {
                Word = EmptyToNull(GetCell(row, "Word")),
                Reading = EmptyToNull(GetCell(row, "Reading")),
                Meaning = EmptyToNull(GetCell(row, "Meaning")),
                ExampleSentence = EmptyToNull(GetCell(row, "ExampleSentence")),
                KanjiCharacter = EmptyToNull(GetCell(row, "Kanji", "KanjiCharacter")),
                SinoVietnamese = EmptyToNull(GetCell(row, "SinoVietnamese")),
                OnYomi = EmptyToNull(GetCell(row, "OnYomi")),
                KunYomi = EmptyToNull(GetCell(row, "KunYomi")),
                StrokeCount = ParseNullableInt(GetCell(row, "StrokeCount")),
                ContextSentence = EmptyToNull(GetCell(row, "ContextSentence")),
                GrammarPattern = EmptyToNull(GetCell(row, "GrammarPattern")),
                Structure = EmptyToNull(GetCell(row, "Structure")),
                UsageExplanation = EmptyToNull(GetCell(row, "UsageExplanation")),
                AcceptedAnswers = string.IsNullOrWhiteSpace(acceptedRaw)
                    ? null : acceptedRaw.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(),
                OrderingTokens = string.IsNullOrWhiteSpace(tokensRaw)
                    ? null : tokensRaw.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(),
                OrderingAnswer = string.IsNullOrWhiteSpace(answerTokensRaw)
                    ? null : answerTokensRaw.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(),
            },
        };
    }

    private static void AddJsonPassageWithQuestions(
        JsonElement pEl,
        string skillName,
        ref int rowIdx,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        UnifiedImportPreviewResponse preview)
    {
        var pDto = JsonSerializer.Deserialize<CanonicalPassageDto>(pEl.GetRawText(), JsonOpts);
        if (pDto is null) return;
        var inferredSkill = !string.IsNullOrWhiteSpace(pDto.SkillName)
            ? CanonicalQuestionValidator.NormalizeSkillName(pDto.SkillName)
            : (!string.IsNullOrWhiteSpace(pDto.AudioUrl) && string.IsNullOrWhiteSpace(pDto.PassageText) ? "Listening" : skillName);
        pDto.SkillName = string.IsNullOrWhiteSpace(inferredSkill) ? skillName : inferredSkill;
        if (string.IsNullOrWhiteSpace(pDto.PassageCode) && pEl.TryGetProperty("groupCode", out var gc))
            pDto.PassageCode = gc.GetString() ?? $"PSG-{rowIdx}";
        if (string.IsNullOrWhiteSpace(pDto.PassageCode)) pDto.PassageCode = $"PSG-{rowIdx}";
        pDto.AudioUrl = ResolvePreviewAssetHint(pDto.AudioUrl, mediaAssets);
        pDto.ImageUrl = ResolvePreviewAssetHint(pDto.ImageUrl, mediaAssets);
        pDto.SourceType = mediaAssets is { Count: > 0 } ? "ImportZip" : "ImportJson";

        preview.Rows.Add(new UnifiedImportRowPreviewDto
        {
            RowNumber = rowIdx++,
            RecordType = pDto.SkillName == "Listening" ? "ListeningGroup" : "Passage",
            RowKey = pDto.PassageCode,
            LevelCode = pDto.CertificateLevelCode,
            SkillName = pDto.SkillName,
            SummaryText = pDto.Title,
            PassagePayload = pDto,
        });

        if (TryGetArray(pEl, ["questions", "Questions"], out var childQs))
        {
            foreach (var cq in childQs.EnumerateArray())
            {
                var qDto = JsonSerializer.Deserialize<CanonicalQuestionDto>(cq.GetRawText(), JsonOpts);
                if (qDto is null) continue;
                qDto.CertificateLevelCode = string.IsNullOrWhiteSpace(qDto.CertificateLevelCode) ? pDto.CertificateLevelCode : qDto.CertificateLevelCode;
                qDto.SkillName = string.IsNullOrWhiteSpace(qDto.SkillName) ? pDto.SkillName : qDto.SkillName;
                qDto.PassageCode = pDto.PassageCode;
                qDto.Passage = pDto;
                qDto.AudioUrl = ResolvePreviewAssetHint(qDto.AudioUrl ?? pDto.AudioUrl, mediaAssets);
                qDto.ImageUrl = ResolvePreviewAssetHint(qDto.ImageUrl, mediaAssets);
                qDto.PlaybackPolicy ??= pDto.PlaybackPolicy;
                qDto.QuestionType = CanonicalQuestionValidator.NormalizeQuestionType(qDto.QuestionType);
                if (string.IsNullOrWhiteSpace(qDto.TemplateKind))
                    qDto.TemplateKind = CanonicalQuestionValidator.InferDefaultTemplateKind(qDto.SkillName, qDto.QuestionType);
                qDto.SourceType = pDto.SourceType;

                preview.Rows.Add(new UnifiedImportRowPreviewDto
                {
                    RowNumber = rowIdx++,
                    RecordType = "Question",
                    RowKey = $"{pDto.PassageCode} · {(qDto.QuestionText.Length > 40 ? qDto.QuestionText[..40] + "…" : qDto.QuestionText)}",
                    LevelCode = qDto.CertificateLevelCode,
                    SkillName = qDto.SkillName,
                    TemplateKind = qDto.TemplateKind,
                    QuestionType = qDto.QuestionType,
                    SummaryText = qDto.QuestionText,
                    QuestionPayload = qDto,
                });
            }
        }
    }

    private static void AddJsonQuestion(
        JsonElement qEl,
        int rowNum,
        IReadOnlyList<UnifiedImportMediaAssetDto>? mediaAssets,
        UnifiedImportPreviewResponse preview)
    {
        var qDto = JsonSerializer.Deserialize<CanonicalQuestionDto>(qEl.GetRawText(), JsonOpts);
        if (qDto is null) return;
        qDto.QuestionType = CanonicalQuestionValidator.NormalizeQuestionType(qDto.QuestionType);
        qDto.SkillName = CanonicalQuestionValidator.NormalizeSkillName(qDto.SkillName);
        if (string.IsNullOrWhiteSpace(qDto.TemplateKind))
            qDto.TemplateKind = CanonicalQuestionValidator.InferDefaultTemplateKind(qDto.SkillName, qDto.QuestionType);
        qDto.AudioUrl = ResolvePreviewAssetHint(qDto.AudioUrl, mediaAssets);
        qDto.ImageUrl = ResolvePreviewAssetHint(qDto.ImageUrl, mediaAssets);
        foreach (var opt in qDto.Options)
            opt.ImageUrl = ResolvePreviewAssetHint(opt.ImageUrl, mediaAssets);
        qDto.SourceType = mediaAssets is { Count: > 0 } ? "ImportZip" : "ImportJson";

        preview.Rows.Add(new UnifiedImportRowPreviewDto
        {
            RowNumber = rowNum,
            RecordType = "Question",
            RowKey = qDto.QuestionText.Length > 55 ? qDto.QuestionText[..55] + "…" : qDto.QuestionText,
            LevelCode = qDto.CertificateLevelCode,
            SkillName = qDto.SkillName,
            TemplateKind = qDto.TemplateKind,
            QuestionType = qDto.QuestionType,
            SummaryText = qDto.QuestionText,
            QuestionPayload = qDto,
        });
    }

    private static MockQuestion CreateLinkedMockQuestion(QuestionBankItem bank, int sortOrder)
    {
        var opts = new List<MockQuestionOption>();
        if (!string.IsNullOrWhiteSpace(bank.OptionsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<CanonicalOptionDto>>(bank.OptionsJson, JsonOpts) ?? [];
                opts = parsed.Select((o, idx) => new MockQuestionOption
                {
                    SortOrder = o.SortOrder > 0 ? o.SortOrder : idx + 1,
                    OptionText = o.OptionText,
                    ImageUrl = o.ImageUrl,
                    IsCorrect = o.IsCorrect,
                }).ToList();
            }
            catch (JsonException) { }
        }

        return new MockQuestion
        {
            QuestionBankItemId = bank.Id,
            QuestionVersion = bank.Version,
            PassageId = bank.PassageId,
            TemplateKind = bank.TemplateKind,
            QuestionGroupTitle = bank.QuestionGroupTitle,
            PlaybackPolicyJson = bank.PlaybackPolicyJson,
            SkillMetadataJson = bank.SkillMetadataJson,
            QuestionText = bank.QuestionText,
            QuestionType = bank.QuestionType,
            InteractionDataJson = bank.InteractionDataJson,
            AnswerKeyJson = bank.AnswerKeyJson,
            AudioUrl = bank.AudioUrl,
            AudioTranscript = bank.AudioTranscript,
            ImageUrl = bank.ImageUrl,
            Explanation = bank.Explanation,
            SortOrder = sortOrder,
            Options = opts,
        };
    }

    private static void ResolveLevelSkillOnPassage(
        CanonicalPassageDto dto, List<CertificateLevel> levels, List<LanguageSkill> skills)
    {
        if (dto.CertificateLevelId <= 0 && !string.IsNullOrWhiteSpace(dto.CertificateLevelCode))
        {
            var lvl = levels.FirstOrDefault(l => l.Code.Equals(dto.CertificateLevelCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (lvl is not null) { dto.CertificateLevelId = lvl.Id; dto.CertificateLevelCode = lvl.Code; }
        }
        var norm = CanonicalQuestionValidator.NormalizeSkillName(dto.SkillName);
        dto.SkillName = norm;
        if (dto.LanguageSkillId <= 0)
        {
            var s = skills.FirstOrDefault(x => CanonicalQuestionValidator.NormalizeSkillName(x.Name).Equals(norm, StringComparison.OrdinalIgnoreCase));
            if (s is not null) dto.LanguageSkillId = s.Id;
        }
    }

    private static void ResolveLevelSkillOnQuestion(
        CanonicalQuestionDto dto, List<CertificateLevel> levels, List<LanguageSkill> skills)
    {
        if (dto.CertificateLevelId <= 0 && !string.IsNullOrWhiteSpace(dto.CertificateLevelCode))
        {
            var lvl = levels.FirstOrDefault(l => l.Code.Equals(dto.CertificateLevelCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (lvl is not null) { dto.CertificateLevelId = lvl.Id; dto.CertificateLevelCode = lvl.Code; }
        }
        var norm = CanonicalQuestionValidator.NormalizeSkillName(dto.SkillName);
        dto.SkillName = norm;
        if (dto.LanguageSkillId <= 0)
        {
            var s = skills.FirstOrDefault(x => CanonicalQuestionValidator.NormalizeSkillName(x.Name).Equals(norm, StringComparison.OrdinalIgnoreCase));
            if (s is not null) dto.LanguageSkillId = s.Id;
        }
    }

    private static string? ResolvePreviewAssetHint(string? raw, IReadOnlyList<UnifiedImportMediaAssetDto>? assets)
    {
        if (string.IsNullOrWhiteSpace(raw) || assets is null || assets.Count == 0) return raw;
        var clean = raw.Trim().Replace('\\', '/');
        var found = assets.FirstOrDefault(a =>
            a.ArchivePath.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
            a.FileName.Equals(Path.GetFileName(clean), StringComparison.OrdinalIgnoreCase));
        if (found is null) return raw;
        // URL giả lập cô lập trong bộ nhớ khi preview — sẽ thay bằng URL thật khi Confirm
        return $"/api/FileStorage/local/import-media/{found.Sha256[..32].ToLowerInvariant()}{Path.GetExtension(found.FileName).ToLowerInvariant()}";
    }

    private static void RewriteMediaUrl(CanonicalPassageDto dto, Dictionary<string, string> uploadedMap)
    {
        if (uploadedMap.Count == 0) return;
        dto.AudioUrl = ReplaceIfMatched(dto.AudioUrl, uploadedMap);
        dto.ImageUrl = ReplaceIfMatched(dto.ImageUrl, uploadedMap);
    }

    private static void RewriteMediaUrl(CanonicalQuestionDto dto, Dictionary<string, string> uploadedMap)
    {
        if (uploadedMap.Count == 0) return;
        dto.AudioUrl = ReplaceIfMatched(dto.AudioUrl, uploadedMap);
        dto.ImageUrl = ReplaceIfMatched(dto.ImageUrl, uploadedMap);
        foreach (var opt in dto.Options)
            opt.ImageUrl = ReplaceIfMatched(opt.ImageUrl, uploadedMap);
    }

    private static string? ReplaceIfMatched(string? current, Dictionary<string, string> uploadedMap)
    {
        if (string.IsNullOrWhiteSpace(current)) return current;
        if (uploadedMap.TryGetValue(current, out var url)) return url;
        var name = Path.GetFileName(current);
        return uploadedMap.TryGetValue(name, out var url2) ? url2 : current;
    }

    private static bool MatchesFileMagicBytes(string ext, byte[] bytes) => ext switch
    {
        ".xlsx" or ".zip" => bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B,
        ".pdf" => bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46,
        ".json" => bytes.Length >= 2 && (bytes[0] is (byte)'{' or (byte)'[' || (bytes.Length >= 4 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)),
        _ => false,
    };

    private static bool MatchesMediaMagicBytes(string ext, byte[] bytes) => ext switch
    {
        ".png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".webp" => bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[8] == 0x57 && bytes[9] == 0x45,
        ".mp3" => bytes.Length >= 3 && ((bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3') || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0)),
        ".wav" => bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[8] == 'W' && bytes[9] == 'A',
        ".ogg" => bytes.Length >= 4 && bytes[0] == 'O' && bytes[1] == 'g' && bytes[2] == 'g' && bytes[3] == 'S',
        ".m4a" => bytes.Length >= 8 && bytes[4] == 'f' && bytes[5] == 't' && bytes[6] == 'y' && bytes[7] == 'p',
        _ => false,
    };

    private static bool IsAllowedMediaExt(string ext) => ext is ".mp3" or ".wav" or ".m4a" or ".ogg" or ".png" or ".jpg" or ".jpeg" or ".webp";

    private static string MediaMimeType(string ext) => ext switch
    {
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    private static async Task CopyBoundedAsync(Stream source, MemoryStream dest, long limit, CancellationToken ct)
    {
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (dest.Length + read > limit)
                throw new InvalidOperationException($"Dung lượng vượt quá giới hạn tối đa ({limit / (1024 * 1024)} MB).");
            await dest.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }

    private sealed record RawRow(int RowNumber, Dictionary<string, string> Cells);

    private static List<RawRow> ReadWorksheetRows(IXLWorksheet ws)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow < 2 || lastCol < 1) return [];

        var headers = new Dictionary<int, string>();
        for (var c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(h))
                headers[c] = NormalizeKey(h);
        }

        var rows = new List<RawRow>();
        for (var r = 2; r <= lastRow; r++)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var anyNonEmpty = false;
            foreach (var (c, key) in headers)
            {
                var val = ws.Cell(r, c).GetString().Trim();
                if (!string.IsNullOrEmpty(val)) anyNonEmpty = true;
                dict[key] = val;
            }
            if (anyNonEmpty)
                rows.Add(new RawRow(r, dict));
        }
        return rows;
    }

    private static bool IsSampleRow(RawRow row)
        => GetCell(row, "IsSample").Equals("true", StringComparison.OrdinalIgnoreCase) ||
           GetCell(row, "QuestionPrompt", "Title", "Word").StartsWith("[SAMPLE]", StringComparison.OrdinalIgnoreCase);

    private static string GetCell(RawRow row, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (row.Cells.TryGetValue(NormalizeKey(k), out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return string.Empty;
    }

    private static string NormalizeKey(string input) => new(input.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').Select(char.ToLowerInvariant).ToArray());

    private static HashSet<int> ParseCorrectOptionIndices(string raw)
    {
        var set = new HashSet<int>();
        foreach (var part in raw.Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim().ToUpperInvariant();
            if (int.TryParse(t, out var n) && n >= 1) set.Add(n);
            else if (t is "A") set.Add(1);
            else if (t is "B") set.Add(2);
            else if (t is "C") set.Add(3);
            else if (t is "D") set.Add(4);
        }
        return set;
    }

    private static bool TryGetArray(JsonElement obj, string[] names, out JsonElement array)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
                return true;
        }
        array = default;
        return false;
    }

    private static string NormalizeSourceType(string? format) => format?.Trim().ToLowerInvariant() switch
    {
        "json" => "ImportJson",
        "zip" => "ImportZip",
        "pdf" => "ImportPdf",
        _ => "ImportExcel",
    };

    private static bool IsExcelSource(string? format)
        => string.IsNullOrWhiteSpace(format)
           || format.Trim().Equals("Excel", StringComparison.OrdinalIgnoreCase)
           || format.Trim().Equals("Xlsx", StringComparison.OrdinalIgnoreCase)
           || format.Trim().Equals("ImportExcel", StringComparison.OrdinalIgnoreCase);

    private static string? EmptyToNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static int ParseIntOrDefault(string? raw, int fallback) => int.TryParse(raw, out var n) ? n : fallback;
    private static int? ParseNullableInt(string? raw) => int.TryParse(raw, out var n) ? n : null;
    private static bool ParseBoolOrDefault(string? raw, bool fallback) => bool.TryParse(raw, out var b) ? b : fallback;
}
