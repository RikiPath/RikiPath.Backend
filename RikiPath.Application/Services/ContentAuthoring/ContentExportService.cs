using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>Xuất đề thi & ngân hàng câu hỏi ra Excel, JSON, ZIP (kèm media & checksum) và PDF Unicode A4 (Question Paper / Answer Key).</summary>
public sealed class ContentExportService(
    IUnitOfWork unitOfWork,
    IUnifiedImportService unifiedImportService,
    IQuestionBankService questionBankService,
    SubscriptionAccessService? subscriptionAccess = null) : IContentExportService
{
    private const string SchemaVersion = "2.0";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly object FontLock = new();
    private static bool _fontsInitialized;

    public async Task<ApiResponse<ExportFileResult>> ExportExamAsync(
        int examId, string format, bool includeAnswers, int actorUserId, bool isAuthorOrAdmin, CancellationToken ct)
    {
        if (includeAnswers && !isAuthorOrAdmin)
            return ApiResponse<ExportFileResult>.Forbidden("Chỉ Tác giả nội dung hoặc Quản trị viên mới được xuất kèm Đáp án và Giải thích.");

        var normalizedFmt = (format ?? "json").Trim().ToLowerInvariant();
        if (!isAuthorOrAdmin)
        {
            if (normalizedFmt != "pdf")
                return ApiResponse<ExportFileResult>.Forbidden("Học viên chỉ được phép tải đề dạng PDF.");

            if (subscriptionAccess is not null)
            {
                var decision = await subscriptionAccess.EvaluateFeatureAsync(FeatureCodes.MockTests, ct);
                if (!decision.Allowed)
                    return ApiResponse<ExportFileResult>.Fail(
                        decision.Message ?? "Bạn chưa có quyền truy cập tính năng này.",
                        decision.StatusCode,
                        errorCode: decision.ErrorCode ?? "FEATURE_ACCESS_DENIED");
            }
        }

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
                    .ThenInclude(q => q.Passage!)
                        .ThenInclude(p => p.LanguageSkill)
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Passage!)
                        .ThenInclude(p => p.CertificateLevel)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == examId && !t.IsDeleted, ct);

        if (test is null)
            return ApiResponse<ExportFileResult>.NotFound("Không tìm thấy bộ đề hoặc đề thi.");

        if (!isAuthorOrAdmin)
        {
            if (test.PublishStatus != ContentStatus.Published || test.IsArchivedVersion ||
                JlptBlueprintPolicy.IsRightsBlockedForLegacyOrManaged(test.RightsStatus, test.IsManagedWorkflow))
                return ApiResponse<ExportFileResult>.Forbidden("Đề thi chưa xuất bản hoặc đã bị thu hồi.");

            if (test.ExamKind is not (ExamKind.PracticeSet or ExamKind.MixedPractice or ExamKind.FullMockTest))
                return ApiResponse<ExportFileResult>.Forbidden("Không được phép xuất đề đánh giá năng lực.");

            if (test.ExamKind == ExamKind.FullMockTest)
            {
                var hasCompletedMock = await unitOfWork.MockTestAttempts.Query().AsNoTracking()
                    .AnyAsync(a => a.UserId == actorUserId &&
                                   !a.IsDeleted &&
                                   a.AttemptKind == AttemptKind.MockTest &&
                                   a.IsCompleted &&
                                   (a.SectionResults!.Any(sr => sr.MockTestSection.MockTestId == examId) ||
                                    a.Answers!.Any(ans => ans.MockQuestion.MockTestSection.MockTestId == examId)),
                        ct);
                if (!hasCompletedMock)
                    return ApiResponse<ExportFileResult>.Fail(
                        "Không thể tải PDF đề thi thử trước khi hoàn thành ít nhất một lượt thi.",
                        HttpStatusCode.Forbidden,
                        errorCode: "MOCK_TEST_PDF_REQUIRES_ATTEMPT");
            }
        }

        var slug = SanitizeFileName(test.Title);

        return normalizedFmt switch
        {
            "xlsx" or "excel" => ApiResponse<ExportFileResult>.Success(new ExportFileResult(
                BuildExamExcel(test, includeAnswers),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{slug}-v{test.Version}.xlsx")),
            "json" => ApiResponse<ExportFileResult>.Success(new ExportFileResult(
                BuildExamJson(test, includeAnswers),
                "application/json; charset=utf-8",
                $"{slug}-v{test.Version}.json")),
            "zip" => ApiResponse<ExportFileResult>.Success(new ExportFileResult(
                BuildExamZip(test, includeAnswers),
                "application/zip",
                $"{slug}-v{test.Version}.zip")),
            "pdf" => ApiResponse<ExportFileResult>.Success(new ExportFileResult(
                BuildExamPdf(test, includeAnswers),
                "application/pdf",
                includeAnswers ? $"{slug}-v{test.Version}-answer-key.pdf" : $"{slug}-v{test.Version}-paper.pdf")),
            _ => ApiResponse<ExportFileResult>.Fail("Định dạng export hỗ trợ: xlsx, json, zip, pdf.")
        };
    }

    public async Task<ApiResponse<ExportFileResult>> ExportQuestionBankAsync(
        QuestionBankQueryFilter filter, string format, bool includeAnswers, int actorUserId, bool isAuthorOrAdmin, CancellationToken ct)
    {
        if (!isAuthorOrAdmin)
            return ApiResponse<ExportFileResult>.Forbidden("Chỉ Quản lý nội dung hoặc Quản trị viên mới được phép xuất Ngân hàng câu hỏi.");

        filter.Page = 1;
        filter.PageSize = 200;
        var res = await questionBankService.SearchAsync(filter, actorUserId, isAuthorOrAdmin, ct);
        if (!res.IsSuccess || res.Result is null)
            return ApiResponse<ExportFileResult>.Fail(res.ErrorMessage ?? "Không thể tải câu hỏi để export.", (HttpStatusCode)res.StatusCode);

        var items = res.Result.Items.ToList();
        var fmt = (format ?? "xlsx").Trim().ToLowerInvariant();

        if (fmt == "json")
        {
            var payload = new
            {
                schemaVersion = SchemaVersion,
                exportedAtUtc = DateTime.UtcNow,
                count = items.Count,
                items = items.Select(q => StripProtectedIfNeeded(q, includeAnswers)).ToList(),
            };
            var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));
            return ApiResponse<ExportFileResult>.Success(new ExportFileResult(jsonBytes, "application/json; charset=utf-8", "rikipath-question-bank.json"));
        }

        if (fmt == "xlsx" || fmt == "excel")
        {
            var excelBytes = BuildBankExcel(items, includeAnswers);
            return ApiResponse<ExportFileResult>.Success(new ExportFileResult(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "rikipath-question-bank.xlsx"));
        }

        return ApiResponse<ExportFileResult>.Fail("Ngân hàng câu hỏi hỗ trợ export xlsx hoặc json.");
    }

    public async Task<ApiResponse<RoundTripVerificationResponse>> VerifyExamRoundTripAsync(
        int examId, string format, int actorUserId, bool isAuthorOrAdmin, CancellationToken ct)
    {
        if (!isAuthorOrAdmin)
            return ApiResponse<RoundTripVerificationResponse>.Forbidden("Chỉ Tác giả hoặc Quản trị viên được kiểm tra round-trip.");

        var exportFmt = format.Trim().ToLowerInvariant() is "zip" or "json" ? format.Trim().ToLowerInvariant() : "xlsx";
        var exported = await ExportExamAsync(examId, exportFmt, includeAnswers: true, actorUserId, isAuthorOrAdmin: true, ct);
        if (!exported.IsSuccess || exported.Result is null)
            return ApiResponse<RoundTripVerificationResponse>.Fail(exported.ErrorMessage ?? "Không xuất được dữ liệu gốc.", (HttpStatusCode)exported.StatusCode);

        await using var ms = new MemoryStream(exported.Result.Bytes, writable: false);
        var previewRes = await unifiedImportService.PreviewAsync(ms, exported.Result.FileName, UnifiedImportKind.Auto, ct);
        if (!previewRes.IsSuccess || previewRes.Result is null)
            return ApiResponse<RoundTripVerificationResponse>.Fail(previewRes.ErrorMessage ?? "Không thể parse lại file vừa xuất.");

        var exam = await unitOfWork.MockTests.Query().AsNoTracking()
            .Include(t => t.Sections!.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Questions!.Where(q => !q.IsDeleted))
            .FirstAsync(t => t.Id == examId, ct);

        var sourceQuestions = (exam.Sections ?? []).SelectMany(s => s.Questions ?? []).ToList();
        var reparsedQuestions = previewRes.Result.Rows.Where(r => r.QuestionPayload is not null).ToList();

        var discrepancies = new List<string>();
        if (sourceQuestions.Count != reparsedQuestions.Count)
        {
            discrepancies.Add($"Số câu hỏi gốc ({sourceQuestions.Count}) khác số câu đọc lại ({reparsedQuestions.Count}).");
        }
        foreach (var row in reparsedQuestions)
        {
            if (row.Status == "Error")
                discrepancies.Add($"Dòng {row.RowNumber}: {string.Join("; ", row.Issues.Select(i => i.Message))}");
        }

        return ApiResponse<RoundTripVerificationResponse>.Success(new RoundTripVerificationResponse
        {
            Passed = discrepancies.Count == 0 && previewRes.Result.ErrorCount == 0 && previewRes.Result.ExamBlueprint is not null,
            SourceQuestionCount = sourceQuestions.Count,
            ReparsedQuestionCount = reparsedQuestions.Count,
            MissingCount = Math.Max(0, sourceQuestions.Count - reparsedQuestions.Count),
            Discrepancies = discrepancies,
            ReparsedPreview = previewRes.Result,
        });
    }

    private static byte[] BuildExamExcel(MockTest test, bool includeAnswers)
    {
        using var wb = new XLWorkbook();
        var levelCode = test.CertificateLevel?.Code ?? "N4";
        var sections = (test.Sections ?? []).Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToList();
        var isMixed = test.ExamKind != ExamKind.FullMockTest;

        var bpWs = wb.AddWorksheet(isMixed ? "PracticeManifest" : "ExamBlueprint");
        string[] bpHeaders = isMixed
            ? ["Title", "LevelCode", "ExamVersion", "Description", "IncludedSkills", "TimeLimitMinutes", "Source", "RightsStatus"]
            : ["Title", "LevelCode", "ExamVersion", "Description", "TotalTimeLimitMinutes", "PassingRule", "Source", "RightsStatus"];
        WriteHeader(bpWs, bpHeaders);
        bpWs.Cell(2, 1).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(test.Title);
        bpWs.Cell(2, 2).Value = levelCode;
        bpWs.Cell(2, 3).Value = test.Version;
        bpWs.Cell(2, 4).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(test.Description);
        if (isMixed)
        {
            bpWs.Cell(2, 5).Value = string.Join(",", sections.Select(s => s.LanguageSkill?.Name ?? "Vocabulary").Distinct());
            bpWs.Cell(2, 6).Value = test.TimeLimitMinutes;
            bpWs.Cell(2, 7).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(test.SourceReference);
            bpWs.Cell(2, 8).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(test.RightsStatus);
        }
        else
        {
            bpWs.Cell(2, 5).Value = test.TimeLimitMinutes;
            bpWs.Cell(2, 6).Value = "SectionAndTotalScore";
            bpWs.Cell(2, 7).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(test.SourceReference);
            bpWs.Cell(2, 8).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(test.RightsStatus);
        }

        var secWs = wb.AddWorksheet("Sections");
        WriteHeader(secWs, ["SectionOrder", "CertificateSectionCode", "Title", "SkillName", "DurationMinutes", "Instructions"]);
        for (var i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            secWs.Cell(i + 2, 1).Value = s.SortOrder;
            secWs.Cell(i + 2, 2).Value = s.CertificateSection?.Code ?? string.Empty;
            secWs.Cell(i + 2, 3).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(s.Title);
            secWs.Cell(i + 2, 4).Value = s.LanguageSkill?.Name ?? string.Empty;
            secWs.Cell(i + 2, 5).Value = s.DurationMinutes ?? 0;
            secWs.Cell(i + 2, 6).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(s.Instructions);
        }

        var passages = sections.SelectMany(s => s.Questions ?? [])
            .Where(q => !q.IsDeleted && q.Passage is not null)
            .Select(q => q.Passage!)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList();

        var pasWs = wb.AddWorksheet("Passages");
        WriteHeader(pasWs, ["PassageCode", "SkillName", "Title", "PassageText", "AudioUrl", "AudioDurationSeconds", "Transcript"]);
        for (var i = 0; i < passages.Count; i++)
        {
            var p = passages[i];
            pasWs.Cell(i + 2, 1).Value = p.PassageCode;
            pasWs.Cell(i + 2, 2).Value = CanonicalQuestionValidator.NormalizeSkillName(p.LanguageSkill?.Name ?? (string.IsNullOrWhiteSpace(p.AudioUrl) ? "Reading" : "Listening"));
            pasWs.Cell(i + 2, 3).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(p.Title);
            pasWs.Cell(i + 2, 4).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(p.PassageText);
            pasWs.Cell(i + 2, 5).Value = p.AudioUrl ?? string.Empty;
            pasWs.Cell(i + 2, 6).Value = p.AudioDurationSeconds ?? 0;
            pasWs.Cell(i + 2, 7).Value = includeAnswers ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(p.AudioTranscript) : string.Empty;
        }

        var qWs = wb.AddWorksheet("Questions");
        WriteHeader(qWs, [
            "SectionOrder", "SkillName", "PassageCode", "TemplateKind", "QuestionType", "QuestionPrompt",
            "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "AcceptedAnswers",
            "OrderingTokens", "OrderingAnswer", "Explanation", "AudioUrl", "ImageUrl"
        ]);
        var rowIdx = 2;
        foreach (var sec in sections)
        {
            foreach (var mq in (sec.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder))
            {
                var qDto = ExamBuilderService.MapMockQuestionToCanonical(test, sec, mq, includeAnswers);
                qWs.Cell(rowIdx, 1).Value = sec.SortOrder;
                qWs.Cell(rowIdx, 2).Value = qDto.SkillName;
                qWs.Cell(rowIdx, 3).Value = qDto.PassageCode ?? string.Empty;
                qWs.Cell(rowIdx, 4).Value = qDto.TemplateKind;
                qWs.Cell(rowIdx, 5).Value = qDto.QuestionType;
                qWs.Cell(rowIdx, 6).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(qDto.QuestionText);
                for (var o = 0; o < 4; o++)
                {
                    qWs.Cell(rowIdx, 7 + o).Value = o < qDto.Options.Count
                        ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(qDto.Options[o].OptionText)
                        : string.Empty;
                }
                qWs.Cell(rowIdx, 11).Value = includeAnswers
                    ? string.Join(",", qDto.Options.Select((o, idx) => (o, idx)).Where(x => x.o.IsCorrect).Select(x => x.idx + 1))
                    : string.Empty;
                qWs.Cell(rowIdx, 12).Value = includeAnswers && qDto.SkillMetadata?.AcceptedAnswers is { Count: > 0 }
                    ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(string.Join("|", qDto.SkillMetadata.AcceptedAnswers))
                    : string.Empty;
                qWs.Cell(rowIdx, 13).Value = qDto.SkillMetadata?.OrderingTokens is { Count: > 0 }
                    ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(string.Join("|", qDto.SkillMetadata.OrderingTokens))
                    : string.Empty;
                qWs.Cell(rowIdx, 14).Value = includeAnswers && qDto.SkillMetadata?.OrderingAnswer is { Count: > 0 }
                    ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(string.Join("|", qDto.SkillMetadata.OrderingAnswer))
                    : string.Empty;
                qWs.Cell(rowIdx, 15).Value = includeAnswers ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(qDto.Explanation) : string.Empty;
                qWs.Cell(rowIdx, 16).Value = qDto.AudioUrl ?? string.Empty;
                qWs.Cell(rowIdx, 17).Value = qDto.ImageUrl ?? string.Empty;
                rowIdx++;
            }
        }

        foreach (var w in wb.Worksheets)
            w.Columns().AdjustToContents();
        using var outStream = new MemoryStream();
        wb.SaveAs(outStream);
        return outStream.ToArray();
    }

    private static byte[] BuildBankExcel(List<CanonicalQuestionDto> items, bool includeAnswers)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        WriteHeader(ws, [
            "LevelCode", "SkillName", "TemplateKind", "QuestionType", "QuestionPrompt",
            "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "AcceptedAnswers",
            "OrderingTokens", "OrderingAnswer", "Explanation", "Topic", "Source"
        ]);
        for (var i = 0; i < items.Count; i++)
        {
            var q = items[i];
            var r = i + 2;
            ws.Cell(r, 1).Value = q.CertificateLevelCode;
            ws.Cell(r, 2).Value = q.SkillName;
            ws.Cell(r, 3).Value = q.TemplateKind;
            ws.Cell(r, 4).Value = q.QuestionType;
            ws.Cell(r, 5).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(q.QuestionText);
            for (var o = 0; o < 4; o++)
                ws.Cell(r, 6 + o).Value = o < q.Options.Count ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(q.Options[o].OptionText) : string.Empty;
            ws.Cell(r, 10).Value = includeAnswers
                ? string.Join(",", q.Options.Select((o, idx) => (o, idx)).Where(x => x.o.IsCorrect).Select(x => x.idx + 1))
                : string.Empty;
            ws.Cell(r, 11).Value = includeAnswers && q.SkillMetadata?.AcceptedAnswers is { Count: > 0 }
                ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(string.Join("|", q.SkillMetadata.AcceptedAnswers))
                : string.Empty;
            ws.Cell(r, 12).Value = q.SkillMetadata?.OrderingTokens is { Count: > 0 }
                ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(string.Join("|", q.SkillMetadata.OrderingTokens))
                : string.Empty;
            ws.Cell(r, 13).Value = includeAnswers && q.SkillMetadata?.OrderingAnswer is { Count: > 0 }
                ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(string.Join("|", q.SkillMetadata.OrderingAnswer))
                : string.Empty;
            ws.Cell(r, 14).Value = includeAnswers ? CanonicalQuestionValidator.SanitizeSpreadsheetCell(q.Explanation) : string.Empty;
            ws.Cell(r, 15).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(q.Topic);
            ws.Cell(r, 16).Value = CanonicalQuestionValidator.SanitizeSpreadsheetCell(q.SourceReference ?? q.SourceType);
        }
        ws.Columns().AdjustToContents();
        using var outStream = new MemoryStream();
        wb.SaveAs(outStream);
        return outStream.ToArray();
    }

    private static byte[] BuildExamJson(MockTest test, bool includeAnswers)
    {
        var sections = (test.Sections ?? []).Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToList();
        var passages = sections.SelectMany(s => s.Questions ?? [])
            .Where(q => !q.IsDeleted && q.Passage is not null)
            .Select(q => q.Passage!)
            .GroupBy(p => p.Id)
            .Select(g => QuestionBankService.MapPassageToDto(g.First(), includeAnswers))
            .ToList();

        var questions = new List<CanonicalQuestionDto>();
        foreach (var sec in sections)
        {
            foreach (var mq in (sec.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder))
            {
                var qDto = ExamBuilderService.MapMockQuestionToCanonical(test, sec, mq, includeAnswers);
                qDto.GroupSortOrder = sec.SortOrder;
                qDto.Passage = null; // Tham chiếu qua passageCode để không lặp đoạn đọc
                questions.Add(StripProtectedIfNeeded(qDto, includeAnswers));
            }
        }

        var coreObj = new
        {
            schemaVersion = SchemaVersion,
            exportedAtUtc = DateTime.UtcNow,
            examKind = test.ExamKind.ToString(),
            title = test.Title,
            description = test.Description,
            certificateLevelCode = test.CertificateLevel?.Code ?? string.Empty,
            version = test.Version,
            timeLimitMinutes = test.TimeLimitMinutes,
            publishStatus = test.PublishStatus.ToString(),
            sourceReference = test.SourceReference,
            rightsStatus = test.RightsStatus,
            includedSkills = sections.Select(s => s.LanguageSkill?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList(),
            sections = sections.Select(s => new
            {
                sortOrder = s.SortOrder,
                title = s.Title,
                skillName = s.LanguageSkill?.Name ?? "",
                certificateSectionCode = s.CertificateSection?.Code,
                durationMinutes = s.DurationMinutes,
                instructions = s.Instructions,
            }).ToList(),
            passages,
            questions,
        };

        var canonicalBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(coreObj, JsonOpts));
        var checksum = Convert.ToHexString(SHA256.HashData(canonicalBytes));
        var finalEnvelope = new
        {
            schemaVersion = SchemaVersion,
            checksumSha256 = checksum,
            includeAnswers,
            coreObj.exportedAtUtc,
            coreObj.examKind,
            coreObj.title,
            coreObj.description,
            coreObj.certificateLevelCode,
            coreObj.version,
            coreObj.timeLimitMinutes,
            coreObj.publishStatus,
            coreObj.sourceReference,
            coreObj.rightsStatus,
            coreObj.includedSkills,
            coreObj.sections,
            coreObj.passages,
            coreObj.questions,
        };

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(finalEnvelope, JsonOpts));
    }

    private static byte[] BuildExamZip(MockTest test, bool includeAnswers)
    {
        var manifestBytes = BuildExamJson(test, includeAnswers);
        var excelBytes = BuildExamExcel(test, includeAnswers);

        using var outMs = new MemoryStream();
        using (var archive = new ZipArchive(outMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(archive, "manifest.json", manifestBytes);
            AddZipEntry(archive, "workbook.xlsx", excelBytes);

            var checksums = new Dictionary<string, string>
            {
                ["manifest.json"] = Convert.ToHexString(SHA256.HashData(manifestBytes)),
                ["workbook.xlsx"] = Convert.ToHexString(SHA256.HashData(excelBytes)),
            };

            AddZipEntry(archive, "checksums.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                schemaVersion = SchemaVersion,
                generatedAtUtc = DateTime.UtcNow,
                files = checksums,
            }, JsonOpts)));
        }
        return outMs.ToArray();
    }

    private static byte[] BuildExamPdf(MockTest test, bool includeAnswers)
    {
        EnsureQuestPdfReady();

        var sections = (test.Sections ?? []).Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToList();
        var levelCode = test.CertificateLevel?.Code ?? "JLPT";
        var examTypeLabel = test.ExamKind switch
        {
            ExamKind.PracticeSet => "BỘ ĐỀ LUYỆN TẬP",
            ExamKind.MixedPractice => "LUYỆN TẬP HỖN HỢP",
            _ => "ĐỀ THI THỬ JLPT"
        };

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").Fallback(f => f.FontFamily("Yu Gothic")).FontSize(10.5f).FontColor("#1F2937"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text($"RikiPath JLPT · {examTypeLabel} · {levelCode} (v{test.Version})")
                                .FontSize(9).SemiBold().FontColor("#B53D65");
                            left.Item().Text(test.Title).FontSize(15).Bold().FontColor("#111827");
                        });
                        row.ConstantItem(140).AlignRight().Column(right =>
                        {
                            right.Item().Text($"Thời gian: {test.TimeLimitMinutes} phút").FontSize(10).SemiBold();
                            right.Item().Text(includeAnswers ? "BẢN KÈM ĐÁP ÁN" : "PHIẾU ĐỀ THI").FontSize(9).FontColor("#6B7280");
                        });
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#E5E7EB");
                });

                page.Content().PaddingVertical(10).Column(body =>
                {
                    body.Spacing(10);
                    if (!string.IsNullOrWhiteSpace(test.Description))
                    {
                        body.Item().Text(test.Description).Italic().FontSize(9.5f).FontColor("#4B5563");
                    }

                    var qCounter = 1;
                    var renderedPassageIds = new HashSet<int>();

                    foreach (var sec in sections)
                    {
                        body.Item().Background("#FFF5F8").Padding(8).Row(r =>
                        {
                            r.RelativeItem().Text($"Phần {sec.SortOrder}: {sec.Title} ({sec.LanguageSkill?.Name})").Bold().FontSize(11).FontColor("#8A2447");
                            if (sec.DurationMinutes is > 0)
                                r.AutoItem().Text($"{sec.DurationMinutes} phút").FontSize(9.5f).FontColor("#6B7280");
                        });

                        if (!string.IsNullOrWhiteSpace(sec.Instructions))
                            body.Item().Text($"Hướng dẫn: {sec.Instructions}").FontSize(9.5f).FontColor("#4B5563");

                        foreach (var mq in (sec.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder))
                        {
                            if (mq.Passage is not null && renderedPassageIds.Add(mq.Passage.Id))
                            {
                                body.Item().Border(1).BorderColor("#E5E7EB").Padding(10).Column(pCol =>
                                {
                                    pCol.Spacing(4);
                                    pCol.Item().Text($"Đoạn / Nhóm: {mq.Passage.Title}").Bold().FontSize(10).FontColor("#111827");
                                    if (!string.IsNullOrWhiteSpace(mq.Passage.PassageText))
                                        pCol.Item().Text(mq.Passage.PassageText).LineHeight(1.35f);
                                    if (!string.IsNullOrWhiteSpace(mq.Passage.AudioUrl))
                                        pCol.Item().Text($"[Audio nghe hiểu: {(mq.Passage.AudioDurationSeconds is > 0 ? $"{mq.Passage.AudioDurationSeconds}s" : "Tệp audio đính kèm")}]").FontSize(9.5f).Italic().FontColor("#4B5563");
                                });
                            }

                            var qDto = ExamBuilderService.MapMockQuestionToCanonical(test, sec, mq, includeAnswers);
                            var currentNum = qCounter++;

                            body.Item().PaddingTop(4).Column(qCol =>
                            {
                                qCol.Spacing(3);
                                qCol.Item().Text($"Câu {currentNum}. {qDto.QuestionText}").SemiBold();
                                if (!string.IsNullOrWhiteSpace(qDto.AudioUrl) && mq.Passage is null)
                                    qCol.Item().Text("[Có file âm thanh Nghe hiểu đính kèm]").FontSize(9).Italic().FontColor("#6B7280");

                                if (qDto.QuestionType == CanonicalQuestionTypes.Ordering && qDto.SkillMetadata?.OrderingTokens is { Count: > 0 })
                                {
                                    qCol.Item().PaddingLeft(12).Text("Các cụm sắp xếp: [ " + string.Join("  |  ", qDto.SkillMetadata.OrderingTokens) + " ]");
                                }
                                else if (qDto.QuestionType == CanonicalQuestionTypes.FillBlank)
                                {
                                    qCol.Item().PaddingLeft(12).Text("Trả lời điền từ: ____________________________");
                                }
                                else
                                {
                                    var labels = new[] { "A", "B", "C", "D", "E", "F" };
                                    for (var o = 0; o < qDto.Options.Count; o++)
                                    {
                                        var label = o < labels.Length ? labels[o] : (o + 1).ToString();
                                        var mark = includeAnswers && qDto.Options[o].IsCorrect ? "  ✓" : string.Empty;
                                        qCol.Item().PaddingLeft(12).Text($"{label}. {qDto.Options[o].OptionText}{mark}");
                                    }
                                }
                            });
                        }
                    }

                    if (includeAnswers)
                    {
                        body.Item().PageBreak();
                        body.Item().Text("ĐÁP ÁN & GIẢI THÍCH CHI TIẾT (CHỈ TÁC GIẢ / QUẢN TRỊ)").FontSize(13).Bold().FontColor("#8A2447");

                        var ansNum = 1;
                        var labels = new[] { "A", "B", "C", "D", "E", "F" };
                        foreach (var sec in sections)
                        {
                            foreach (var mq in (sec.Questions ?? []).Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder))
                            {
                                var qDto = ExamBuilderService.MapMockQuestionToCanonical(test, sec, mq, includeTranscript: true);
                                var answerText = qDto.QuestionType switch
                                {
                                    CanonicalQuestionTypes.Ordering => string.Join(" -> ", qDto.SkillMetadata?.OrderingAnswer ?? []),
                                    CanonicalQuestionTypes.FillBlank => string.Join(" / ", qDto.SkillMetadata?.AcceptedAnswers ?? []),
                                    _ => string.Join(", ", qDto.Options
                                        .Select((o, idx) => (o, idx))
                                        .Where(x => x.o.IsCorrect)
                                        .Select(x => $"{(x.idx < labels.Length ? labels[x.idx] : (x.idx + 1).ToString())} ({x.o.OptionText})"))
                                };

                                body.Item().PaddingTop(3).Column(aCol =>
                                {
                                    aCol.Item().Text($"Câu {ansNum++}: Đáp án đúng = {answerText}").SemiBold();
                                    if (!string.IsNullOrWhiteSpace(qDto.Explanation))
                                        aCol.Item().PaddingLeft(10).Text($"Giải thích: {qDto.Explanation}").FontSize(9.5f).FontColor("#374151");
                                    if (!string.IsNullOrWhiteSpace(qDto.AudioTranscript))
                                        aCol.Item().PaddingLeft(10).Text($"Transcript: {qDto.AudioTranscript}").FontSize(9.5f).Italic().FontColor("#4B5563");
                                });
                            }
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void EnsureQuestPdfReady()
    {
        if (_fontsInitialized) return;
        lock (FontLock)
        {
            if (_fontsInitialized) return;
            QuestPDF.Settings.License = LicenseType.Community;

            var fontFiles = new[]
            {
                @"C:\Windows\Fonts\segoeui.ttf",
                @"C:\Windows\Fonts\segoeuib.ttf",
                @"C:\Windows\Fonts\YuGothR.ttc",
                @"C:\Windows\Fonts\YuGothB.ttc",
                @"C:\Windows\Fonts\msgothic.ttc",
                @"C:\Windows\Fonts\arial.ttf",
            };
            foreach (var f in fontFiles)
            {
                if (File.Exists(f))
                {
                    try
                    {
                        using var fs = File.OpenRead(f);
                        QuestPDF.Drawing.FontManager.RegisterFont(fs);
                    }
                    catch
                    {
                        // Bỏ qua font không đọc được để dùng fallback hệ thống
                    }
                }
            }
            _fontsInitialized = true;
        }
    }

    private static CanonicalQuestionDto StripProtectedIfNeeded(CanonicalQuestionDto q, bool includeAnswers)
    {
        if (includeAnswers) return q;
        return new CanonicalQuestionDto
        {
            Id = q.Id,
            QuestionIdentity = q.QuestionIdentity,
            Version = q.Version,
            CertificateLevelId = q.CertificateLevelId,
            CertificateLevelCode = q.CertificateLevelCode,
            LanguageSkillId = q.LanguageSkillId,
            SkillName = q.SkillName,
            PassageId = q.PassageId,
            PassageCode = q.PassageCode,
            GroupSortOrder = q.GroupSortOrder,
            TemplateKind = q.TemplateKind,
            QuestionType = q.QuestionType,
            QuestionText = q.QuestionText,
            AudioUrl = q.AudioUrl,
            AudioDurationSeconds = q.AudioDurationSeconds,
            PlaybackPolicy = q.PlaybackPolicy,
            ImageUrl = q.ImageUrl,
            Options = q.Options.Select(o => new CanonicalOptionDto
            {
                SortOrder = o.SortOrder,
                OptionText = o.OptionText,
                ImageUrl = o.ImageUrl,
                IsCorrect = false,
            }).ToList(),
        };
    }

    private static void WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
    }

    private static void AddZipEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(bytes, 0, bytes.Length);
    }

    private static string SanitizeFileName(string input)
    {
        var clean = new string((input ?? "rikipath-export")
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray())
            .Trim('-');
        return string.IsNullOrWhiteSpace(clean) ? "rikipath-export" : (clean.Length > 50 ? clean[..50] : clean);
    }
}
