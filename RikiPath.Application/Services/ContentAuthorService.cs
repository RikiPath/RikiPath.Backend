//using System.Text.Json;
//using RikiPath.Application.DTOs.Content;
//using RikiPath.Application.IServices;
//using RikiPath.Application.Requests.Content;
//using RikiPath.Application.Responses;
//using RikiPath.Application.Responses.Content;
//using RikiPath.Domain.Entities;
//using RikiPath.Domain.Enums;

//namespace RikiPath.Application.Services;

//public class ContentAuthorService(IUnitOfWork unitOfWork, IClaimService claimService) : IContentAuthorService
//{
//    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

//    public async Task<ApiResponse<AuthorContentDetailsResponse>> GetByIdAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
//        => await GetContentDetailsAsync(type, id, claimService.GetUserClaim().Id, cancellationToken);

//    public Task<ApiResponse<AuthorContentDetailsResponse>> GetForAdminAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
//        => GetContentDetailsAsync(type, id, null, cancellationToken);

//    private async Task<ApiResponse<AuthorContentDetailsResponse>> GetContentDetailsAsync(ContentEntityType type, int id, int? ownerId, CancellationToken cancellationToken)
//    {
//        try
//        {
//            AuthorContentDetailsResponse? result = type switch
//            {
//                // Lesson/Kanji/Vocabulary/GrammarPattern: "id" là ContentApprovalRequest.Id (nội dung chưa/đang chờ vào bảng chính).
//                ContentEntityType.Lesson or ContentEntityType.Kanji or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern
//                    => await GetStagedAsync(type, id, ownerId, cancellationToken),
//                ContentEntityType.KanaCharacter => Map(await unitOfWork.KanaCharacters.GetByIdAsync(id, cancellationToken), type, ownerId, e => new() { Title = e.Character, Character = e.Character, KanaType = e.Type, Romaji = e.Romaji, StrokeCount = e.StrokeCount, StrokeOrderImageUrl = e.StrokeOrderImageUrl, AudioUrl = e.AudioUrl, Status = e.Status, ReviewNote = e.ReviewNote, ReviewedDate = e.ReviewedDate, ReviewedByName = e.ReviewedByName }),
//                ContentEntityType.MockTest => MapMockTest(await unitOfWork.MockTests.GetWithSectionsAndQuestionsAsync(id), type, ownerId),
//                ContentEntityType.PracticeExercise => MapPracticeExercise(await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(id, cancellationToken), type, ownerId),
//                _ => null
//            };
//            return result is null ? ApiResponse<AuthorContentDetailsResponse>.NotFound("Không tìm thấy nội dung.") : ApiResponse<AuthorContentDetailsResponse>.Success(result);
//        }
//        catch (Exception ex) { return ApiResponse<AuthorContentDetailsResponse>.Fail($"Không thể tải nội dung: {ex.Message}"); }
//    }

//    public async Task<ApiResponse<ContentReviewStatusResponse>> SaveAsync(ContentEntityType type, int? id, AuthorContentRequest request, CancellationToken cancellationToken)
//    {
//        var authorId = claimService.GetUserClaim().Id;
//        try
//        {
//            ContentReviewStatusResponse? result = type switch
//            {
//                ContentEntityType.Lesson or ContentEntityType.Kanji or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern
//                    => await SaveStagedAsync(type, authorId, id, request, cancellationToken),
//                ContentEntityType.KanaCharacter => await SaveKanaAsync(authorId, id, request, cancellationToken),
//                ContentEntityType.MockTest => await SaveMockTestAsync(authorId, id, request, cancellationToken),
//                //ContentEntityType.PracticeExercise => await SavePracticeExerciseAsync(authorId, id, request, cancellationToken),
//                _ => null
//            };
//            if (result is null) return ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung này không hỗ trợ tạo hoặc cập nhật.");
//            return id is null
//                ? ApiResponse<ContentReviewStatusResponse>.Created(result)
//                : ApiResponse<ContentReviewStatusResponse>.Success(result);
//        }
//        catch (InvalidOperationException ex)
//        {
//            return ApiResponse<ContentReviewStatusResponse>.Fail(ex.Message);
//        }
//        catch (Exception ex)
//        {
//            return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể lưu nội dung: {ex.Message}");
//        }
//    }

//    public async Task<ApiResponse> DeleteAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
//    {
//        var authorId = claimService.GetUserClaim().Id;
//        try
//        {
//            bool deleted = type switch
//            {
//                ContentEntityType.Lesson or ContentEntityType.Kanji or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern
//                    => await DeleteStagedAsync(type, id, authorId, cancellationToken),
//                ContentEntityType.KanaCharacter => await DeleteAsync(await unitOfWork.KanaCharacters.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.KanaCharacters.Update),
//                ContentEntityType.MockTest => await DeleteAsync(await unitOfWork.MockTests.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.MockTests.Update),
//                ContentEntityType.PracticeExercise => await DeleteAsync(await unitOfWork.PracticeExercises.GetByIdAsync(id, cancellationToken), authorId, unitOfWork.PracticeExercises.Update),
//                _ => false
//            };
//            if (!deleted) return ApiResponse.Fail("Không tìm thấy nội dung của bạn ở trạng thái có thể xóa.");
//            await unitOfWork.SaveChangesAsync(cancellationToken);
//            return ApiResponse.Success();
//        }
//        catch (Exception ex) { return ApiResponse.Fail($"Không thể xóa nội dung: {ex.Message}"); }
//    }

//    // ===================== Lesson / Kanji / Vocabulary / GrammarPattern: lưu vào ContentApprovalRequest =====================

//    private async Task<AuthorContentDetailsResponse?> GetStagedAsync(ContentEntityType type, int id, int? ownerId, CancellationToken ct)
//    {
//        var req = await unitOfWork.ContentApprovalRequests.GetByIdWithReviewerAsync(id, ct);
//        if (req is null || req.IsDeleted || req.EntityType != ContentStagingMap.ToImportType(type)
//            || (ownerId is not null && req.SubmittedByUserId != ownerId))
//            return null;

//        var d = new AuthorContentDetailsResponse
//        {
//            Id = req.Id,
//            EntityType = type,
//            ContentAuthorId = req.SubmittedByUserId,
//            Status = ContentStagingMap.ToContentStatus(req.Status),
//            ReviewNote = req.ReviewNote,
//            ReviewedDate = req.ReviewedAt,
//            ReviewedByName = ContentStagingMap.ReviewerName(req.ReviewedBy),
//        };

//        switch (type)
//        {
//            case ContentEntityType.Lesson:
//                {
//                    var p = ReadPayload<LessonImportPayload>(req);
//                    d.Title = p.Title; d.Description = p.Description; d.SortOrder = p.SortOrder;
//                    d.CertificateLevelId = p.CertificateLevelId; d.LanguageSkillId = p.LanguageSkillId;
//                    break;
//                }
//            case ContentEntityType.Kanji:
//                {
//                    var p = ReadPayload<KanjiImportPayload>(req);
//                    d.Title = p.Character; d.Character = p.Character; d.Meaning = p.Meaning; d.SinoVietnamese = p.SinoVietnamese;
//                    d.OnYomi = p.OnYomi; d.KunYomi = p.KunYomi; d.StrokeCount = p.StrokeCount;
//                    d.StrokeOrderImageUrl = p.StrokeOrderImageUrl; d.AudioUrl = p.AudioUrl; d.CertificateLevelId = p.CertificateLevelId;
//                    break;
//                }
//            case ContentEntityType.Vocabulary:
//                {
//                    var p = ReadPayload<VocabularyImportPayload>(req);
//                    d.Title = p.Word; d.Word = p.Word; d.Reading = p.Reading; d.Meaning = p.Meaning;
//                    d.ExampleSentence = p.ExampleSentence; d.ExampleSentenceMeaning = p.ExampleSentenceMeaning;
//                    d.AudioUrl = p.AudioUrl; d.CertificateLevelId = p.CertificateLevelId;
//                    break;
//                }
//            case ContentEntityType.GrammarPattern:
//                {
//                    var p = ReadPayload<GrammarImportPayload>(req);
//                    d.Title = p.Title; d.Structure = p.Structure; d.UsageNotes = p.UsageNotes;
//                    d.ExampleSentence = p.ExampleSentence; d.ExampleSentenceMeaning = p.ExampleSentenceMeaning;
//                    d.CertificateLevelId = p.CertificateLevelId;
//                    break;
//                }
//        }
//        return d;
//    }

//    private async Task<ContentReviewStatusResponse> SaveStagedAsync(ContentEntityType type, int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
//    {
//        var importType = ContentStagingMap.ToImportType(type);
//        var level = await RequireLevelAsync(Required(r.CertificateLevelId, nameof(r.CertificateLevelId)), ct);

//        string rowKey;
//        object payload;
//        switch (type)
//        {
//            case ContentEntityType.Lesson:
//                {
//                    var languageSkillId = Required(r.LanguageSkillId, nameof(r.LanguageSkillId));
//                    var skill = await unitOfWork.LanguageSkills.GetByIdAsync(languageSkillId, ct);
//                    if (skill is null || skill.IsDeleted)
//                        throw new InvalidOperationException($"LanguageSkillId {languageSkillId} không tồn tại hoặc đã bị xóa.");
//                    var title = Required(r.Title, nameof(r.Title));
//                    rowKey = title;
//                    payload = new LessonImportPayload
//                    {
//                        Title = title,
//                        Description = r.Description,
//                        SortOrder = r.SortOrder ?? 0,
//                        CertificateLevelId = level.Id,
//                        CertificateLevelCode = level.Code,
//                        LanguageSkillId = languageSkillId,
//                    };
//                    break;
//                }
//            case ContentEntityType.Kanji:
//                {
//                    var character = Required(r.Character, nameof(r.Character));
//                    rowKey = character;
//                    payload = new KanjiImportPayload
//                    {
//                        Character = character,
//                        Meaning = Required(r.Meaning, nameof(r.Meaning)),
//                        SinoVietnamese = r.SinoVietnamese,
//                        OnYomi = r.OnYomi,
//                        KunYomi = r.KunYomi,
//                        StrokeCount = r.StrokeCount ?? 0,
//                        StrokeOrderImageUrl = r.StrokeOrderImageUrl,
//                        AudioUrl = r.AudioUrl,
//                        CertificateLevelId = level.Id,
//                        CertificateLevelCode = level.Code,
//                    };
//                    break;
//                }
//            case ContentEntityType.Vocabulary:
//                {
//                    var word = Required(r.Word, nameof(r.Word));
//                    rowKey = word;
//                    payload = new VocabularyImportPayload
//                    {
//                        Word = word,
//                        Reading = Required(r.Reading, nameof(r.Reading)),
//                        Meaning = Required(r.Meaning, nameof(r.Meaning)),
//                        ExampleSentence = r.ExampleSentence,
//                        ExampleSentenceMeaning = r.ExampleSentenceMeaning,
//                        AudioUrl = r.AudioUrl,
//                        CertificateLevelId = level.Id,
//                        CertificateLevelCode = level.Code,
//                    };
//                    break;
//                }
//            case ContentEntityType.GrammarPattern:
//                {
//                    var title = Required(r.Title, nameof(r.Title));
//                    rowKey = title;
//                    payload = new GrammarImportPayload
//                    {
//                        Title = title,
//                        Structure = Required(r.Structure, nameof(r.Structure)),
//                        UsageNotes = r.UsageNotes,
//                        ExampleSentence = r.ExampleSentence,
//                        ExampleSentenceMeaning = r.ExampleSentenceMeaning,
//                        CertificateLevelId = level.Id,
//                        CertificateLevelCode = level.Code,
//                    };
//                    break;
//                }
//            default:
//                throw new InvalidOperationException("Loại nội dung này không hỗ trợ tạo hoặc cập nhật.");
//        }

//        var now = DateTime.UtcNow;
//        ContentApprovalRequest req;
//        if (id is null)
//        {
//            req = new ContentApprovalRequest
//            {
//                BatchId = Guid.NewGuid(),
//                EntityType = importType,
//                Action = ImportRowAction.New,
//                SourceFileName = "manual",
//                SourceRowNumber = 0,
//                SubmittedByUserId = authorId,
//                SubmittedAt = now,
//            };
//        }
//        else
//        {
//            req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(id.Value, ct)
//                ?? throw new InvalidOperationException("Không tìm thấy nội dung.");
//            EnsureEditableRequest(req, importType, authorId);
//        }

//        req.RowKey = rowKey.Length > 500 ? rowKey[..500] : rowKey;
//        req.PayloadJson = JsonSerializer.Serialize(payload, payload.GetType());
//        req.Status = ContentApprovalStatus.Draft;
//        // Sửa lại sau khi bị từ chối: xóa kết quả duyệt cũ trên request (lịch sử vẫn nằm ở ContentApprovalReview).
//        req.ReviewedByUserId = null; req.ReviewedAt = null; req.ReviewNote = null;
//        req.ModifiedDate = now;

//        if (id is null) await unitOfWork.ContentApprovalRequests.AddAsync(req, ct); else unitOfWork.ContentApprovalRequests.Update(req);
//        await unitOfWork.SaveChangesAsync(ct);
//        return Map(req.Id, type, rowKey, authorId, ContentStatus.Draft, null, null, null);
//    }

//    private async Task<bool> DeleteStagedAsync(ContentEntityType type, int id, int authorId, CancellationToken ct)
//    {
//        var req = await unitOfWork.ContentApprovalRequests.GetByIdAsync(id, ct);
//        if (req is null || req.IsDeleted) return false;
//        EnsureEditableRequest(req, ContentStagingMap.ToImportType(type), authorId);
//        req.IsDeleted = true;
//        req.ModifiedDate = DateTime.UtcNow;
//        unitOfWork.ContentApprovalRequests.Update(req);
//        return true;
//    }

//    private static void EnsureEditableRequest(ContentApprovalRequest req, ImportEntityType importType, int authorId)
//    {
//        if (req.IsDeleted || req.EntityType != importType) throw new InvalidOperationException("Không tìm thấy nội dung.");
//        if (req.SubmittedByUserId != authorId || req.Status is not (ContentApprovalStatus.Draft or ContentApprovalStatus.Rejected))
//            throw new InvalidOperationException("Bạn chỉ sửa được nội dung của mình đang Draft hoặc bị từ chối.");
//    }

//    private async Task<CertificateLevel> RequireLevelAsync(int certificateLevelId, CancellationToken ct)
//    {
//        var level = await unitOfWork.CertificateLevels.GetByIdAsync(certificateLevelId, ct);
//        return level is null || level.IsDeleted
//            ? throw new InvalidOperationException($"CertificateLevelId {certificateLevelId} không tồn tại hoặc đã bị xóa.")
//            : level;
//    }

//    private static T ReadPayload<T>(ContentApprovalRequest req) where T : class
//        => JsonSerializer.Deserialize<T>(req.PayloadJson, Json) ?? throw new InvalidOperationException("Dữ liệu bản nháp không hợp lệ.");

//    // ===================== KanaCharacter / MockTest / PracticeExercise: vẫn lưu thẳng bảng chính =====================

//    private async Task<ContentReviewStatusResponse> SaveKanaAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
//    {
//        if (r.KanaType is null || !Enum.IsDefined(r.KanaType.Value)) throw new InvalidOperationException("KanaType phải là Hiragana hoặc Katakana.");
//        var e = id is null ? new KanaCharacter { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.KanaCharacters.GetByIdAsync(id.Value, ct), authorId);
//        e.Character = Required(r.Character, nameof(r.Character));
//        if (System.Globalization.StringInfo.ParseCombiningCharacters(e.Character).Length != 1)
//            throw new InvalidOperationException("Character phải chứa đúng một ký tự Kana.");
//        e.Type = r.KanaType.Value;
//        e.Romaji = Required(r.Romaji, nameof(r.Romaji)); e.StrokeCount = r.StrokeCount is > 0 ? r.StrokeCount.Value : throw new InvalidOperationException("StrokeCount phải lớn hơn 0.");
//        e.StrokeOrderImageUrl = r.StrokeOrderImageUrl; e.AudioUrl = r.AudioUrl; Prepare(e);
//        if (id is null) await unitOfWork.KanaCharacters.AddAsync(e, ct); else unitOfWork.KanaCharacters.Update(e);
//        await unitOfWork.SaveChangesAsync(ct);
//        return Map(e.Id, ContentEntityType.KanaCharacter, e.Character, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
//    }

//    private async Task<ContentReviewStatusResponse> SaveMockTestAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
//    {
//        var e = id is null ? new MockTest { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.MockTests.GetByIdAsync(id.Value, ct), authorId);
//        e.Title = Required(r.Title, nameof(r.Title)); e.Description = r.Description; e.TimeLimitMinutes = r.TimeLimitMinutes ?? 0; e.CertificateLevelId = r.CertificateLevelId; Prepare(e);
//        if (id is null) await unitOfWork.MockTests.AddAsync(e, ct); else unitOfWork.MockTests.Update(e);
//        await unitOfWork.SaveChangesAsync(ct);
//        return Map(e.Id, ContentEntityType.MockTest, e.Title, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
//    }

//    //private async Task<ContentReviewStatusResponse> SavePracticeExerciseAsync(int authorId, int? id, AuthorContentRequest r, CancellationToken ct)
//    //{
//    //    var e = id is null ? new PracticeExercise { ContentAuthorId = authorId } : await EditableAsync(await unitOfWork.PracticeExercises.GetByIdAsync(id.Value, ct), authorId);
//    //    e.Title = Required(r.Title, nameof(r.Title)); e.Description = r.Description; e.LessonId = Required(r.LessonId, nameof(r.LessonId));
//    //    e.LanguageSkillId = Required(r.LanguageSkillId, nameof(r.LanguageSkillId)); e.CertificateLevelId = r.CertificateLevelId; e.SortOrder = r.SortOrder ?? 0; Prepare(e);
//    //    if (id is null) await unitOfWork.PracticeExercises.AddAsync(e, ct); else unitOfWork.PracticeExercises.Update(e);
//    //    await unitOfWork.SaveChangesAsync(ct);
//    //    return Map(e.Id, ContentEntityType.PracticeExercise, e.Title, authorId, e.Status, e.ReviewNote, e.ReviewedDate, e.ReviewedByName);
//    //}

//    // Chỉ còn dùng cho KanaCharacter / MockTest / PracticeExercise (các entity này vẫn có ContentAuthorId + field duyệt).
//    private static async Task<T> EditableAsync<T>(T? entity, int authorId) where T : Base
//    {
//        if (entity is null || entity.IsDeleted) throw new InvalidOperationException("Không tìm thấy nội dung.");
//        var contentAuthorId = GetAuthorId(entity);
//        var status = entity switch { KanaCharacter x => x.Status, MockTest x => x.Status, PracticeExercise x => x.Status, _ => ContentStatus.Published };
//        if (contentAuthorId != authorId || status is not (ContentStatus.Draft or ContentStatus.Rejected))
//            throw new InvalidOperationException("Bạn chỉ sửa được nội dung của mình đang Draft hoặc bị từ chối.");
//        return await Task.FromResult(entity);
//    }

//    private static async Task<bool> DeleteAsync<T>(T? entity, int authorId, Action<T> update) where T : Base
//    {
//        if (entity is null || entity.IsDeleted) return false;
//        var editable = await EditableAsync(entity, authorId);
//        editable.IsDeleted = true; editable.ModifiedDate = DateTime.UtcNow; update(editable); return true;
//    }

//    private static void Prepare(KanaCharacter e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
//    private static void Prepare(MockTest e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
//    private static void Prepare(PracticeExercise e) { e.Status = ContentStatus.Draft; e.IsApproved = false; e.ReviewNote = null; e.ReviewedDate = null; e.ReviewedByName = null; e.ModifiedDate = DateTime.UtcNow; }
//    private static string Required(string? value, string field) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException($"'{field}' không được để trống.");
//    private static int Required(int? value, string field) => value is > 0 ? value.Value : throw new InvalidOperationException($"'{field}' phải lớn hơn 0.");
//    private static ContentReviewStatusResponse Map(int id, ContentEntityType type, string title, int authorId, ContentStatus status, string? note, DateTime? reviewedAt, string? reviewer) => new()
//    { Id = id, EntityType = type, Title = title, ContentAuthorId = authorId, Status = status, ReviewNote = note, ReviewedDate = reviewedAt, ReviewedByName = reviewer };

//    private static AuthorContentDetailsResponse? Map<T>(T? entity, ContentEntityType type, int? ownerId, Func<T, AuthorContentDetailsResponse> map) where T : Base
//    {
//        if (entity is null || entity.IsDeleted || (ownerId is not null && GetAuthorId(entity) != ownerId)) return null;
//        var result = map(entity); result.Id = GetId(entity); result.EntityType = type; result.ContentAuthorId = GetAuthorId(entity); return result;
//    }

//    private static int GetId(Base e) => e switch { KanaCharacter x => x.Id, MockTest x => x.Id, PracticeExercise x => x.Id, _ => 0 };
//    private static int GetAuthorId(Base e) => e switch { KanaCharacter x => x.ContentAuthorId, MockTest x => x.ContentAuthorId, PracticeExercise x => x.ContentAuthorId, _ => 0 };
//    private static AuthorContentDetailsResponse? MapMockTest(MockTest? e, ContentEntityType type, int? ownerId) => e is null || e.IsDeleted || (ownerId is not null && e.ContentAuthorId != ownerId) ? null : new()
//    {
//        Id = e.Id,
//        EntityType = type,
//        Title = e.Title,
//        Description = e.Description,
//        TimeLimitMinutes = e.TimeLimitMinutes,
//        CertificateLevelId = e.CertificateLevelId,
//        ContentAuthorId = e.ContentAuthorId,
//        Status = e.Status,
//        ReviewNote = e.ReviewNote,
//        ReviewedDate = e.ReviewedDate,
//        ReviewedByName = e.ReviewedByName,
//        MockSections = e.Sections?.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).Select(s => new AuthorMockSectionDetails
//        {
//            Id = s.Id,
//            Title = s.Title,
//            SortOrder = s.SortOrder,
//            LanguageSkillId = s.LanguageSkillId,
//            Questions = s.Questions?.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).Select(q => new AuthorQuestionDetails
//            { Id = q.Id, QuestionText = q.QuestionText, Explanation = q.Explanation, AudioUrl = q.AudioUrl, ImageUrl = q.ImageUrl, SortOrder = q.SortOrder, Options = q.Options?.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(ToOption).ToList() ?? [] }).ToList() ?? []
//        }).ToList()
//    };
//    private static AuthorContentDetailsResponse? MapPracticeExercise(PracticeExercise? e, ContentEntityType type, int? ownerId) => e is null || e.IsDeleted || (ownerId is not null && e.ContentAuthorId != ownerId) ? null : new()
//    {
//        Id = e.Id,
//        EntityType = type,
//        Title = e.Title,
//        Description = e.Description,
//        LessonId = e.LessonId,
//        LanguageSkillId = e.LanguageSkillId,
//        SortOrder = e.SortOrder,
//        ContentAuthorId = e.ContentAuthorId,
//        Status = e.Status,
//        PracticeQuestions = e.Questions?.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).Select(q => new AuthorPracticeQuestionDetails
//        { Id = q.Id, QuestionText = q.QuestionText, Explanation = q.Explanation, SortOrder = q.SortOrder, Options = q.Options?.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(ToOption).ToList() ?? [] }).ToList()
//    };
//    private static AuthorOptionDetails ToOption(MockQuestionOption o) => new() { Id = o.Id, OptionText = o.OptionText, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder };
//    private static AuthorOptionDetails ToOption(PracticeQuestionOption o) => new() { Id = o.Id, OptionText = o.OptionText, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder };
//}