using System.Text.Json;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Application.Responses.ContentReview;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public class ContentAuthorService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    IContentAiReviewService aiReviewService) : IContentAuthorService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<ApiResponse<AuthorContentDetailsResponse>> GetByIdAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
        => await GetContentDetailsAsync(type, id, cancellationToken);

    public Task<ApiResponse<AuthorContentDetailsResponse>> GetForAdminAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
        => GetContentDetailsAsync(type, id, cancellationToken);

    private async Task<ApiResponse<AuthorContentDetailsResponse>> GetContentDetailsAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        try
        {
            AuthorContentDetailsResponse? result = type switch
            {
                ContentEntityType.Lesson or ContentEntityType.Kanji or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern
                    => await GetStagedAsync(type, id, cancellationToken),
                ContentEntityType.KanaCharacter => Map(await unitOfWork.KanaCharacters.GetByIdAsync(id, cancellationToken), type, e => new() { Title = e.Character, Character = e.Character, KanaType = e.Type, Romaji = e.Romaji, StrokeCount = e.StrokeCount, StrokeOrderImageUrl = e.StrokeOrderImageUrl, AudioUrl = e.AudioUrl }),
                ContentEntityType.MockTest => MapMockTest(await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(id), type),
                ContentEntityType.PracticeExercise => MapPracticeExercise(await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(id, cancellationToken), type),
                _ => null
            };
            return result is null ? ApiResponse<AuthorContentDetailsResponse>.NotFound("Không tìm thấy nội dung.") : ApiResponse<AuthorContentDetailsResponse>.Success(result);
        }
        catch (Exception ex) { return ApiResponse<AuthorContentDetailsResponse>.Fail($"Không thể tải nội dung: {ex.Message}"); }
    }

    public async Task<ApiResponse<ContentReviewStatusResponse>> SaveAsync(ContentEntityType type, int? id, AuthorContentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ContentReviewStatusResponse? result = type switch
            {
                ContentEntityType.Lesson or ContentEntityType.Kanji or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern
                    => await SaveStagedAsync(type, id, request, cancellationToken),
                ContentEntityType.KanaCharacter => await SaveKanaAsync(id, request, cancellationToken),
                ContentEntityType.MockTest => await SaveMockTestAsync(id, request, cancellationToken),
                _ => null
            };
            if (result is null) return ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung này không hỗ trợ tạo hoặc cập nhật.");
            return id is null
                ? ApiResponse<ContentReviewStatusResponse>.Created(result)
                : ApiResponse<ContentReviewStatusResponse>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<ContentReviewStatusResponse>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể lưu nội dung: {ex.Message}");
        }
    }

    public async Task<ApiResponse> DeleteAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = type switch
            {
                ContentEntityType.Lesson or ContentEntityType.Kanji or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern
                    => await DeleteStagedAsync(type, id, cancellationToken),
                ContentEntityType.KanaCharacter => await DeleteAsync(await unitOfWork.KanaCharacters.GetByIdAsync(id, cancellationToken), unitOfWork.KanaCharacters.Update),
                ContentEntityType.MockTest => await DeleteAsync(await unitOfWork.MockTests.GetByIdAsync(id, cancellationToken), unitOfWork.MockTests.Update),
                ContentEntityType.PracticeExercise => await DeleteAsync(await unitOfWork.PracticeExercises.GetByIdAsync(id, cancellationToken), unitOfWork.PracticeExercises.Update),
                _ => false
            };
            if (!deleted) return ApiResponse.Fail("Không tìm thấy nội dung để xóa.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse.Success();
        }
        catch (Exception ex) { return ApiResponse.Fail($"Không thể xóa nội dung: {ex.Message}"); }
    }

    // ===================== AI kiểm tra nội dung =====================

    /// <summary>
    /// AI kiểm tra nội dung (Rule + AI) và trả danh sách lỗi chỉ rõ mục/trường/lý do.
    /// Lesson/Kanji/Vocabulary/GrammarPattern: kiểm tra bản nháp (PayloadJson); MockTest/PracticeExercise: đọc từ DB.
    /// </summary>
    public async Task<ApiResponse<ContentAiReviewResponse>> CheckWithAiAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        try
        {
            AuthorContentDetailsResponse? staged = null;
            if (ContentStagingMap.IsStaged(type))
            {
                staged = await GetStagedAsync(type, id, cancellationToken);
                if (staged is null)
                    return ApiResponse<ContentAiReviewResponse>.NotFound("Không tìm thấy nội dung.");
            }
            return await aiReviewService.ReviewAsync(type, id, staged, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ApiResponse<ContentAiReviewResponse>.Fail($"Không thể kiểm tra nội dung bằng AI: {ex.Message}"); }
    }

    /// <summary>Kết quả AI kiểm tra gần nhất (Author xem để sửa, Admin xem khi duyệt).</summary>
    public Task<ApiResponse<ContentAiReviewResponse?>> GetLatestAiCheckAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
        => aiReviewService.GetLatestAsync(type, id, cancellationToken);

    // ===================== Lesson / Kanji / Vocabulary / GrammarPattern =====================

    private async Task<AuthorContentDetailsResponse?> GetStagedAsync(ContentEntityType type, int id, CancellationToken ct)
    {
        var req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(id, ct);
        if (req is null || req.IsDeleted || req.EntityType != ContentStagingMap.ToImportType(type))
            return null;

        var d = new AuthorContentDetailsResponse
        {
            Id = req.Id,
            EntityType = type
        };

        switch (type)
        {
            case ContentEntityType.Lesson:
                {
                    var p = ReadPayload<LessonImportPayloadResponse>(req);
                    d.Title = p.Title; d.Description = p.Description; d.SortOrder = p.SortOrder;
                    d.CertificateLevelId = p.CertificateLevelId; d.LanguageSkillId = p.LanguageSkillId;
                    break;
                }
            case ContentEntityType.Kanji:
                {
                    var p = ReadPayload<KanjiImportPayloadResponse>(req);
                    d.Title = p.Character; d.Character = p.Character; d.Meaning = p.Meaning; d.SinoVietnamese = p.SinoVietnamese;
                    d.OnYomi = p.OnYomi; d.KunYomi = p.KunYomi; d.StrokeCount = p.StrokeCount;
                    d.StrokeOrderImageUrl = p.StrokeOrderImageUrl; d.AudioUrl = p.AudioUrl; d.CertificateLevelId = p.CertificateLevelId;
                    break;
                }
            case ContentEntityType.Vocabulary:
                {
                    var p = ReadPayload<VocabularyImportPayloadResponse>(req);
                    d.Title = p.Word; d.Word = p.Word; d.Reading = p.Reading; d.Meaning = p.Meaning;
                    d.ExampleSentence = p.ExampleSentence; d.ExampleSentenceMeaning = p.ExampleSentenceMeaning;
                    d.AudioUrl = p.AudioUrl; d.CertificateLevelId = p.CertificateLevelId;
                    break;
                }
            case ContentEntityType.GrammarPattern:
                {
                    var p = ReadPayload<GrammarImportPayloadResponse>(req);
                    d.Title = p.Title; d.Structure = p.Structure; d.UsageNotes = p.UsageNotes;
                    d.ExampleSentence = p.ExampleSentence; d.ExampleSentenceMeaning = p.ExampleSentenceMeaning;
                    d.CertificateLevelId = p.CertificateLevelId;
                    break;
                }
        }
        return d;
    }

    private async Task<ContentReviewStatusResponse> SaveStagedAsync(ContentEntityType type, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var importType = ContentStagingMap.ToImportType(type);
        var level = await RequireLevelAsync(Required(r.CertificateLevelId, nameof(r.CertificateLevelId)), ct);
        var userId = claimService.GetUserClaim().Id;

        string rowKey;
        object payload;
        switch (type)
        {
            case ContentEntityType.Lesson:
                {
                    var languageSkillId = Required(r.LanguageSkillId, nameof(r.LanguageSkillId));
                    var skill = await unitOfWork.LanguageSkills.GetByIdAsync(languageSkillId, ct);
                    if (skill is null || skill.IsDeleted)
                        throw new InvalidOperationException($"LanguageSkillId {languageSkillId} không tồn tại hoặc đã bị xóa.");
                    var title = Required(r.Title, nameof(r.Title));
                    rowKey = title;
                    payload = new LessonImportPayloadResponse
                    {
                        Title = title,
                        Description = r.Description,
                        SortOrder = r.SortOrder ?? 0,
                        CertificateLevelId = level.Id,
                        CertificateLevelCode = level.Code,
                        LanguageSkillId = languageSkillId,
                    };
                    break;
                }
            case ContentEntityType.Kanji:
                {
                    var character = Required(r.Character, nameof(r.Character));
                    rowKey = character;
                    payload = new KanjiImportPayloadResponse
                    {
                        Character = character,
                        Meaning = Required(r.Meaning, nameof(r.Meaning)),
                        SinoVietnamese = r.SinoVietnamese,
                        OnYomi = r.OnYomi,
                        KunYomi = r.KunYomi,
                        StrokeCount = r.StrokeCount ?? 0,
                        StrokeOrderImageUrl = r.StrokeOrderImageUrl,
                        AudioUrl = r.AudioUrl,
                        CertificateLevelId = level.Id,
                        CertificateLevelCode = level.Code,
                    };
                    break;
                }
            case ContentEntityType.Vocabulary:
                {
                    var word = Required(r.Word, nameof(r.Word));
                    rowKey = word;
                    payload = new VocabularyImportPayloadResponse
                    {
                        Word = word,
                        Reading = Required(r.Reading, nameof(r.Reading)),
                        Meaning = Required(r.Meaning, nameof(r.Meaning)),
                        ExampleSentence = r.ExampleSentence,
                        ExampleSentenceMeaning = r.ExampleSentenceMeaning,
                        AudioUrl = r.AudioUrl,
                        CertificateLevelId = level.Id,
                        CertificateLevelCode = level.Code,
                    };
                    break;
                }
            case ContentEntityType.GrammarPattern:
                {
                    var title = Required(r.Title, nameof(r.Title));
                    rowKey = title;
                    payload = new GrammarImportPayloadResponse
                    {
                        Title = title,
                        Structure = Required(r.Structure, nameof(r.Structure)),
                        UsageNotes = r.UsageNotes,
                        ExampleSentence = r.ExampleSentence,
                        ExampleSentenceMeaning = r.ExampleSentenceMeaning,
                        CertificateLevelId = level.Id,
                        CertificateLevelCode = level.Code,
                    };
                    break;
                }
            default:
                throw new InvalidOperationException("Loại nội dung này không hỗ trợ tạo hoặc cập nhật.");
        }

        var now = DateTime.UtcNow;
        ContentApprovalRequest req;
        if (id is null)
        {
            req = new ContentApprovalRequest
            {
                BatchId = Guid.NewGuid(),
                EntityType = importType,
                Action = ImportRowAction.New,
                SourceFileName = "manual",
                SourceRowNumber = 0,
                SubmittedByUserId = userId,
                SubmittedAt = now,
            };
        }
        else
        {
            req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(id.Value, ct)
                ?? throw new InvalidOperationException("Không tìm thấy nội dung.");
            if (req.IsDeleted || req.EntityType != importType)
                throw new InvalidOperationException("Không tìm thấy nội dung.");
        }

        req.RowKey = rowKey.Length > 500 ? rowKey[..500] : rowKey;
        req.PayloadJson = JsonSerializer.Serialize(payload, payload.GetType());
        req.ModifiedDate = now;

        if (id is null) await unitOfWork.ContentApprovalRequests.AddAsync(req, ct); else unitOfWork.ContentApprovalRequests.Update(req);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(req.Id, type, rowKey);
    }

    private async Task<bool> DeleteStagedAsync(ContentEntityType type, int id, CancellationToken ct)
    {
        var req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(id, ct);
        if (req is null || req.IsDeleted || req.EntityType != ContentStagingMap.ToImportType(type)) return false;

        req.IsDeleted = true;
        req.ModifiedDate = DateTime.UtcNow;
        unitOfWork.ContentApprovalRequests.Update(req);
        return true;
    }

    private async Task<CertificateLevel> RequireLevelAsync(int certificateLevelId, CancellationToken ct)
    {
        var level = await unitOfWork.CertificateLevels.GetByIdAsync(certificateLevelId, ct);
        return level is null || level.IsDeleted
            ? throw new InvalidOperationException($"CertificateLevelId {certificateLevelId} không tồn tại hoặc đã bị xóa.")
            : level;
    }

    private static T ReadPayload<T>(ContentApprovalRequest req) where T : class
        => JsonSerializer.Deserialize<T>(req.PayloadJson, Json) ?? throw new InvalidOperationException("Dữ liệu bản nháp không hợp lệ.");

    // ===================== KanaCharacter / MockTest / PracticeExercise =====================

    private async Task<ContentReviewStatusResponse> SaveKanaAsync(int? id, AuthorContentRequest r, CancellationToken ct)
    {
        if (r.KanaType is null || !Enum.IsDefined(r.KanaType.Value)) throw new InvalidOperationException("KanaType phải là Hiragana hoặc Katakana.");
        var e = id is null ? new KanaCharacter() : await GetValidEntityAsync(await unitOfWork.KanaCharacters.GetByIdAsync(id.Value, ct));
        e.Character = Required(r.Character, nameof(r.Character));
        if (System.Globalization.StringInfo.ParseCombiningCharacters(e.Character).Length != 1)
            throw new InvalidOperationException("Character phải chứa đúng một ký tự Kana.");
        e.Type = r.KanaType.Value;
        e.Romaji = Required(r.Romaji, nameof(r.Romaji));
        e.StrokeCount = r.StrokeCount is > 0 ? r.StrokeCount.Value : throw new InvalidOperationException("StrokeCount phải lớn hơn 0.");
        e.StrokeOrderImageUrl = r.StrokeOrderImageUrl;
        e.AudioUrl = r.AudioUrl;
        e.ModifiedDate = DateTime.UtcNow;

        if (id is null) await unitOfWork.KanaCharacters.AddAsync(e, ct); else unitOfWork.KanaCharacters.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.KanaCharacter, e.Character);
    }

    private async Task<ContentReviewStatusResponse> SaveMockTestAsync(int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new MockTest() : await GetValidEntityAsync(await unitOfWork.MockTests.GetByIdAsync(id.Value, ct));
        e.Title = Required(r.Title, nameof(r.Title));
        e.Description = r.Description;
        e.TimeLimitMinutes = r.TimeLimitMinutes ?? 0;
        e.CertificateLevelId = r.CertificateLevelId;
        e.ModifiedDate = DateTime.UtcNow;

        if (id is null) await unitOfWork.MockTests.AddAsync(e, ct); else unitOfWork.MockTests.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.MockTest, e.Title);
    }

    private static async Task<T> GetValidEntityAsync<T>(T? entity) where T : Base
    {
        if (entity is null || entity.IsDeleted) throw new InvalidOperationException("Không tìm thấy nội dung.");
        return await Task.FromResult(entity);
    }

    private static async Task<bool> DeleteAsync<T>(T? entity, Action<T> update) where T : Base
    {
        if (entity is null || entity.IsDeleted) return false;
        entity.IsDeleted = true;
        entity.ModifiedDate = DateTime.UtcNow;
        update(entity);
        return true;
    }

    private static string Required(string? value, string field) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException($"'{field}' không được để trống.");
    private static int Required(int? value, string field) => value is > 0 ? value.Value : throw new InvalidOperationException($"'{field}' phải lớn hơn 0.");
    private static ContentReviewStatusResponse Map(int id, ContentEntityType type, string title) => new()
    { Id = id, EntityType = type, Title = title };

    private static AuthorContentDetailsResponse? Map<T>(T? entity, ContentEntityType type, Func<T, AuthorContentDetailsResponse> map) where T : Base
    {
        if (entity is null || entity.IsDeleted) return null;
        var result = map(entity);
        result.Id = GetId(entity);
        result.EntityType = type;
        return result;
    }

    private static int GetId(Base e) => e switch { KanaCharacter x => x.Id, MockTest x => x.Id, PracticeExercise x => x.Id, _ => 0 };

    private static AuthorContentDetailsResponse? MapMockTest(MockTest? e, ContentEntityType type) => e is null || e.IsDeleted ? null : new()
    {
        Id = e.Id,
        EntityType = type,
        Title = e.Title,
        Description = e.Description,
        TimeLimitMinutes = e.TimeLimitMinutes,
        CertificateLevelId = e.CertificateLevelId,
        MockSections = e.Sections?.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).Select(s => new AuthorMockSectionDetails
        {
            Id = s.Id,
            Title = s.Title,
            SortOrder = s.SortOrder,
            LanguageSkillId = s.LanguageSkillId,
            Questions = s.Questions?.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).Select(q => new AuthorQuestionDetails
            { Id = q.Id, QuestionText = q.QuestionText, Explanation = q.Explanation, AudioUrl = q.AudioUrl, ImageUrl = q.ImageUrl, SortOrder = q.SortOrder, Options = q.Options?.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(ToOption).ToList() ?? [] }).ToList() ?? []
        }).ToList()
    };

    private static AuthorContentDetailsResponse? MapPracticeExercise(PracticeExercise? e, ContentEntityType type) => e is null || e.IsDeleted ? null : new()
    {
        Id = e.Id,
        EntityType = type,
        Title = e.Title,
        Description = e.Description,
        LessonId = e.LessonId,
        LanguageSkillId = e.LanguageSkillId,
        SortOrder = e.SortOrder,
        PracticeQuestions = e.Questions?.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).Select(q => new AuthorPracticeQuestionDetails
        { Id = q.Id, QuestionText = q.QuestionText, Explanation = q.Explanation, SortOrder = q.SortOrder, Options = q.Options?.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(ToOption).ToList() ?? [] }).ToList()
    };

    private static AuthorOptionDetails ToOption(MockQuestionOption o) => new() { Id = o.Id, OptionText = o.OptionText, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder };
    private static AuthorOptionDetails ToOption(PracticeQuestionOption o) => new() { Id = o.Id, OptionText = o.OptionText, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder };
}