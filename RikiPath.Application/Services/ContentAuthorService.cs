using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public class ContentAuthorService(IUnitOfWork unitOfWork, IClaimService claimService) : IContentAuthorService
{
    public async Task<ApiResponse<AuthorContentDetailsResponse>> GetByIdAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
        => await GetContentDetailsAsync(type, id, claimService.GetUserClaim().Id, cancellationToken);

    public Task<ApiResponse<AuthorContentDetailsResponse>> GetForAdminAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
        => GetContentDetailsAsync(type, id, null, cancellationToken);

    private async Task<ApiResponse<AuthorContentDetailsResponse>> GetContentDetailsAsync(ContentEntityType type, int id, int? ownerId, CancellationToken cancellationToken)
    {
        try
        {
            AuthorContentDetailsResponse? result = type switch
            {
                ContentEntityType.Lesson => Map(await unitOfWork.Lessons.GetByIdAsync(id, cancellationToken), type, ownerId, e => new() { Title=e.Title, Description=e.Description, VideoUrl=e.VideoUrl, DurationSeconds=e.DurationSeconds, SortOrder=e.SortOrder, CertificateLevelId=e.CertificateLevelId, LanguageSkillId=e.LanguageSkillId, Status=e.Status, ReviewNote=e.ReviewNote, ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName }),
                ContentEntityType.Kanji => Map(await unitOfWork.Kanjis.GetByIdAsync(id, cancellationToken), type, ownerId, e => new() { Title=e.Character, Character=e.Character, Meaning=e.Meaning, SinoVietnamese=e.SinoVietnamese, OnYomi=e.OnYomi, KunYomi=e.KunYomi, StrokeCount=e.StrokeCount, StrokeOrderImageUrl=e.StrokeOrderImageUrl, AudioUrl=e.AudioUrl, CertificateLevelId=e.CertificateLevelId, Status=e.Status, ReviewNote=e.ReviewNote, ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName }),
                ContentEntityType.KanaCharacter => Map(await unitOfWork.KanaCharacters.GetByIdAsync(id, cancellationToken), type, ownerId, e => new() { Title=e.Character, Character=e.Character, KanaType=e.Type, Romaji=e.Romaji, StrokeCount=e.StrokeCount, StrokeOrderImageUrl=e.StrokeOrderImageUrl, AudioUrl=e.AudioUrl, Status=e.Status, ReviewNote=e.ReviewNote, ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName }),
                ContentEntityType.Vocabulary => Map(await unitOfWork.Vocabularies.GetByIdAsync(id, cancellationToken), type, ownerId, e => new() { Title=e.Word, Word=e.Word, Reading=e.Reading, Meaning=e.Meaning, ExampleSentence=e.ExampleSentence, ExampleSentenceMeaning=e.ExampleSentenceMeaning, AudioUrl=e.AudioUrl, CertificateLevelId=e.CertificateLevelId, Status=e.Status, ReviewNote=e.ReviewNote, ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName }),
                ContentEntityType.GrammarPattern => Map(await unitOfWork.GrammarPatterns.GetByIdAsync(id, cancellationToken), type, ownerId, e => new() { Title=e.Title, Structure=e.Structure, UsageNotes=e.UsageNotes, ExampleSentence=e.ExampleSentence, ExampleSentenceMeaning=e.ExampleSentenceMeaning, CertificateLevelId=e.CertificateLevelId, Status=e.Status, ReviewNote=e.ReviewNote, ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName }),
                ContentEntityType.MockTest => MapMockTest(await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(id), type, ownerId),
                ContentEntityType.PracticeExercise => MapPracticeExercise(await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(id, cancellationToken), type, ownerId),
                _ => null
            };
            return result is null ? ApiResponse<AuthorContentDetailsResponse>.NotFound("Không tìm thấy nội dung.") : ApiResponse<AuthorContentDetailsResponse>.Success(result);
        }
        catch (Exception ex) { return ApiResponse<AuthorContentDetailsResponse>.Fail($"Không thể tải nội dung: {ex.Message}"); }
    }

    public async Task<ApiResponse<ContentReviewStatusResponse>> SaveAsync(ContentEntityType type, int? id, AuthorContentRequest request, CancellationToken cancellationToken)
    {
        var authorId = claimService.GetUserClaim().Id;
        try
        {
            ContentReviewStatusResponse? result = type switch
            {
                ContentEntityType.Lesson => await SaveLessonAsync(authorId, id, request, cancellationToken),
                ContentEntityType.Kanji => await SaveKanjiAsync(authorId, id, request, cancellationToken),
                ContentEntityType.KanaCharacter => await SaveKanaAsync(authorId, id, request, cancellationToken),
                ContentEntityType.Vocabulary => await SaveVocabularyAsync(authorId, id, request, cancellationToken),
                ContentEntityType.GrammarPattern => await SaveGrammarAsync(authorId, id, request, cancellationToken),
                ContentEntityType.MockTest => await SaveMockTestAsync(authorId, id, request, cancellationToken),
                ContentEntityType.PracticeExercise => await SavePracticeExerciseAsync(authorId, id, request, cancellationToken),
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
        var authorId = claimService.GetUserClaim().Id;
        try
        {
            bool deleted = type switch
            {
                ContentEntityType.Lesson => await DeleteAsync(await unitOfWork.Lessons.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.Lessons.Update),
                ContentEntityType.Kanji => await DeleteAsync(await unitOfWork.Kanjis.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.Kanjis.Update),
                ContentEntityType.KanaCharacter => await DeleteAsync(await unitOfWork.KanaCharacters.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.KanaCharacters.Update),
                ContentEntityType.Vocabulary => await DeleteAsync(await unitOfWork.Vocabularies.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.Vocabularies.Update),
                ContentEntityType.GrammarPattern => await DeleteAsync(await unitOfWork.GrammarPatterns.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.GrammarPatterns.Update),
                ContentEntityType.MockTest => await DeleteAsync(await unitOfWork.MockTests.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.MockTests.Update),
                ContentEntityType.PracticeExercise => await DeleteAsync(await unitOfWork.PracticeExercises.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.PracticeExercises.Update),
                _ => false
            };
            if (!deleted) return ApiResponse.Fail("Không tìm thấy nội dung của bạn ở trạng thái có thể xóa.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse.Success();
        }
        catch (Exception ex) { return ApiResponse.Fail($"Không thể xóa nội dung: {ex.Message}"); }
    }

    private async Task<ContentReviewStatusResponse> SaveLessonAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new Lesson { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.Lessons.GetByIdAsync(id.Value, ct), authorId);
        e.Title = Required(r.Title, nameof(r.Title)); e.Description = r.Description; e.VideoUrl = Required(r.VideoUrl, nameof(r.VideoUrl));
        e.DurationSeconds = r.DurationSeconds ?? 0; e.SortOrder = r.SortOrder ?? 0; e.CertificateLevelId = r.CertificateLevelId;
        e.LanguageSkillId = Required(r.LanguageSkillId, nameof(r.LanguageSkillId)); Prepare(e);
        if (id is null) await unitOfWork.Lessons.AddAsync(e, ct); else unitOfWork.Lessons.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.Lesson, e.Title, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private async Task<ContentReviewStatusResponse> SaveKanjiAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new Kanji { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.Kanjis.GetByIdAsync(id.Value, ct), authorId);
        e.Character = Required(r.Character, nameof(r.Character)); e.Meaning = Required(r.Meaning, nameof(r.Meaning));
        e.SinoVietnamese = r.SinoVietnamese; e.OnYomi = r.OnYomi; e.KunYomi = r.KunYomi; e.StrokeCount = r.StrokeCount ?? 0;
        e.StrokeOrderImageUrl = r.StrokeOrderImageUrl; e.AudioUrl = r.AudioUrl; e.CertificateLevelId = r.CertificateLevelId; Prepare(e);
        if (id is null) await unitOfWork.Kanjis.AddAsync(e, ct); else unitOfWork.Kanjis.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.Kanji, e.Character, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private async Task<ContentReviewStatusResponse> SaveKanaAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        if (r.KanaType is null || !Enum.IsDefined(r.KanaType.Value)) throw new InvalidOperationException("KanaType phải là Hiragana hoặc Katakana.");
        var e = id is null ? new KanaCharacter { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.KanaCharacters.GetByIdAsync(id.Value, ct), authorId);
        e.Character = Required(r.Character, nameof(r.Character));
        if (System.Globalization.StringInfo.ParseCombiningCharacters(e.Character).Length != 1)
            throw new InvalidOperationException("Character phải chứa đúng một ký tự Kana.");
        e.Type = r.KanaType.Value;
        e.Romaji = Required(r.Romaji, nameof(r.Romaji)); e.StrokeCount = r.StrokeCount is > 0 ? r.StrokeCount.Value : throw new InvalidOperationException("StrokeCount phải lớn hơn 0.");
        e.StrokeOrderImageUrl = r.StrokeOrderImageUrl; e.AudioUrl = r.AudioUrl; Prepare(e);
        if (id is null) await unitOfWork.KanaCharacters.AddAsync(e, ct); else unitOfWork.KanaCharacters.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.KanaCharacter, e.Character, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private async Task<ContentReviewStatusResponse> SaveVocabularyAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new Vocabulary { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.Vocabularies.GetByIdAsync(id.Value, ct), authorId);
        e.Word = Required(r.Word, nameof(r.Word)); e.Reading = Required(r.Reading, nameof(r.Reading)); e.Meaning = Required(r.Meaning, nameof(r.Meaning));
        e.ExampleSentence = r.ExampleSentence; e.ExampleSentenceMeaning = r.ExampleSentenceMeaning; e.AudioUrl = r.AudioUrl; e.CertificateLevelId = r.CertificateLevelId; Prepare(e);
        if (id is null) await unitOfWork.Vocabularies.AddAsync(e, ct); else unitOfWork.Vocabularies.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.Vocabulary, e.Word, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private async Task<ContentReviewStatusResponse> SaveGrammarAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new GrammarPattern { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.GrammarPatterns.GetByIdAsync(id.Value, ct), authorId);
        e.Title = Required(r.Title, nameof(r.Title)); e.Structure = Required(r.Structure, nameof(r.Structure)); e.UsageNotes = r.UsageNotes;
        e.ExampleSentence = r.ExampleSentence; e.ExampleSentenceMeaning = r.ExampleSentenceMeaning; e.CertificateLevelId = r.CertificateLevelId; Prepare(e);
        if (id is null) await unitOfWork.GrammarPatterns.AddAsync(e, ct); else unitOfWork.GrammarPatterns.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.GrammarPattern, e.Title, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private async Task<ContentReviewStatusResponse> SaveMockTestAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new MockTest { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.MockTests.GetByIdAsync(id.Value, ct), authorId);
        e.Title = Required(r.Title, nameof(r.Title)); e.Description = r.Description; e.TimeLimitMinutes = r.TimeLimitMinutes ?? 0; e.CertificateLevelId = r.CertificateLevelId; Prepare(e);
        if (id is null) await unitOfWork.MockTests.AddAsync(e, ct); else unitOfWork.MockTests.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.MockTest, e.Title, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private async Task<ContentReviewStatusResponse> SavePracticeExerciseAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
    {
        var e = id is null ? new PracticeExercise { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.PracticeExercises.GetByIdAsync(id.Value, ct), authorId);
        e.Title = Required(r.Title, nameof(r.Title)); e.Description = r.Description; e.LessonId = Required(r.LessonId, nameof(r.LessonId));
        e.LanguageSkillId = Required(r.LanguageSkillId, nameof(r.LanguageSkillId)); e.CertificateLevelId = r.CertificateLevelId; e.SortOrder = r.SortOrder ?? 0; Prepare(e);
        if (id is null) await unitOfWork.PracticeExercises.AddAsync(e, ct); else unitOfWork.PracticeExercises.Update(e);
        await unitOfWork.SaveChangesAsync(ct);
        return Map(e.Id, ContentEntityType.PracticeExercise, e.Title, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
    }

    private static async Task<T> EditableAsync<T>(T? entity, int authorId) where T : Base
    {
        if (entity is null || entity.IsDeleted) throw new InvalidOperationException("Không tìm thấy nội dung.");
        var contentAuthorId = entity switch { Lesson x => x.ContentAuthorId, Kanji x => x.ContentAuthorId, KanaCharacter x => x.ContentAuthorId, Vocabulary x => x.ContentAuthorId, GrammarPattern x => x.ContentAuthorId, MockTest x => x.ContentAuthorId, PracticeExercise x => x.ContentAuthorId, _ => 0 };
        var status = entity switch { Lesson x => x.Status, Kanji x => x.Status, KanaCharacter x => x.Status, Vocabulary x => x.Status, GrammarPattern x => x.Status, MockTest x => x.Status, PracticeExercise x => x.Status, _ => ContentStatus.Published };
        if (contentAuthorId != authorId || status is not (ContentStatus.Draft or ContentStatus.Rejected))
            throw new InvalidOperationException("Bạn chỉ sửa được nội dung của mình đang Draft hoặc bị từ chối.");
        return await Task.FromResult(entity);
    }

    private static async Task<bool> DeleteAsync<T>(T? entity, int authorId, Action<T> update) where T : Base
    {
        if (entity is null || entity.IsDeleted) return false;
        var editable = await EditableAsync(entity, authorId);
        editable.IsDeleted = true; editable.ModifiedDate = DateTime.UtcNow; update(editable); return true;
    }

    private static void Prepare(Lesson e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static void Prepare(Kanji e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static void Prepare(KanaCharacter e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static void Prepare(Vocabulary e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static void Prepare(GrammarPattern e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static void Prepare(MockTest e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static void Prepare(PracticeExercise e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
    private static string Required(string? value, string field) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException($"'{field}' không được để trống.");
    private static int Required(int? value, string field) => value is > 0 ? value.Value : throw new InvalidOperationException($"'{field}' phải lớn hơn 0.");
    private static ContentReviewStatusResponse Map(int id, ContentEntityType type, string title, int authorId, ContentStatus status, string? note, DateTime? reviewedAt, string? reviewer) => new()
    { Id = id, EntityType = type, Title = title, ContentAuthorId = authorId, Status = status, ReviewNote = note, ReviewedDate = reviewedAt, ReviewedByName = reviewer };

    private static AuthorContentDetailsResponse? Map<T>(T? entity, ContentEntityType type, int? ownerId, Func<T, AuthorContentDetailsResponse> map) where T : Base
    {
        if (entity is null || entity.IsDeleted || (ownerId is not null && GetAuthorId(entity) != ownerId)) return null;
        var result = map(entity); result.Id = GetId(entity); result.EntityType = type; result.ContentAuthorId = GetAuthorId(entity); return result;
    }

    private static int GetId(Base e) => e switch { Lesson x => x.Id, Kanji x => x.Id, KanaCharacter x => x.Id, Vocabulary x => x.Id, GrammarPattern x => x.Id, _ => 0 };
    private static int GetAuthorId(Base e) => e switch { Lesson x => x.ContentAuthorId, Kanji x => x.ContentAuthorId, KanaCharacter x => x.ContentAuthorId, Vocabulary x => x.ContentAuthorId, GrammarPattern x => x.ContentAuthorId, MockTest x => x.ContentAuthorId, PracticeExercise x => x.ContentAuthorId, _ => 0 };
    private static AuthorContentDetailsResponse? MapMockTest(MockTest? e, ContentEntityType type, int? ownerId) => e is null || e.IsDeleted || (ownerId is not null && e.ContentAuthorId != ownerId) ? null : new()
    {
        Id=e.Id, EntityType=type, Title=e.Title, Description=e.Description, TimeLimitMinutes=e.TimeLimitMinutes, CertificateLevelId=e.CertificateLevelId,
        ContentAuthorId=e.ContentAuthorId, Status=e.Status, ReviewNote=e.ReviewNote, ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName,
        MockSections=e.Sections?.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).Select(s => new AuthorMockSectionDetails
        { Id=s.Id, Title=s.Title, SortOrder=s.SortOrder, LanguageSkillId=s.LanguageSkillId, Questions=s.Questions?.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).Select(q => new AuthorQuestionDetails
        { Id=q.Id, QuestionText=q.QuestionText, Explanation=q.Explanation, AudioUrl=q.AudioUrl, ImageUrl=q.ImageUrl, SortOrder=q.SortOrder, Options=q.Options?.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(ToOption).ToList() ?? [] }).ToList() ?? [] }).ToList()
    };
    private static AuthorContentDetailsResponse? MapPracticeExercise(PracticeExercise? e, ContentEntityType type, int? ownerId) => e is null || e.IsDeleted || (ownerId is not null && e.ContentAuthorId != ownerId) ? null : new()
    {
        Id=e.Id, EntityType=type, Title=e.Title, Description=e.Description, LessonId=e.LessonId, LanguageSkillId=e.LanguageSkillId,
        CertificateLevelId=e.CertificateLevelId, SortOrder=e.SortOrder, ContentAuthorId=e.ContentAuthorId, Status=e.Status, ReviewNote=e.ReviewNote,
        ReviewedDate=e.ReviewedDate, ReviewedByName=e.ReviewedByName,
        PracticeQuestions=e.Questions?.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).Select(q => new AuthorPracticeQuestionDetails
        { Id=q.Id, QuestionText=q.QuestionText, Explanation=q.Explanation, SortOrder=q.SortOrder, Options=q.Options?.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(ToOption).ToList() ?? [] }).ToList()
    };
    private static AuthorOptionDetails ToOption(MockQuestionOption o) => new() { Id=o.Id, OptionText=o.OptionText, IsCorrect=o.IsCorrect, SortOrder=o.SortOrder };
    private static AuthorOptionDetails ToOption(PracticeQuestionOption o) => new() { Id=o.Id, OptionText=o.OptionText, IsCorrect=o.IsCorrect, SortOrder=o.SortOrder };
}
