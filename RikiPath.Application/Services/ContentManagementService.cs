using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using System.Globalization;

namespace RikiPath.Application.Services
{
    // Đã bỏ IReviewableContent + generic dispatch qua Expression.Property. Đổi lại: mỗi loại nội
    // dung (Lesson/Kanji/Vocabulary/GrammarPattern/MockTest) có bộ method riêng, code
    // dài hơn nhưng thẳng, không còn "ảo thuật" generic/reflection nào - dễ đọc, dễ debug độc lập
    // từng loại, không có rủi ro EF Core dịch sai LINQ expression qua interface.
    //
    // GHI CHÚ: entity không còn field "ReviewedById" (int FK) - đã đổi sang "ReviewedByName"
    // (string, lưu tên hiển thị của admin đã duyệt ngay tại thời điểm duyệt) - áp dụng cho cả 5 loại.
    public class ContentManagementService(IUnitOfWork unitOfWork, IExcelParser excelParser, IClaimService claimService)
        : IContentManagementService
    {
        public async Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(
            BulkImportRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var authorId = claimService.GetUserClaim().Id;
                var parseResult = await excelParser.ParseAsync(request.FileStream, cancellationToken);
                if (!parseResult.Success && parseResult.Rows.Count == 0)
                    return ApiResponse<BulkImportResultResponse>.Fail(
                        "Không đọc được file, vui lòng kiểm tra định dạng.", errors: parseResult.Errors);

                var certificateTypes = await unitOfWork.CertificateTypes.GetAllAsync(cancellationToken);
                var certificateLevels = await unitOfWork.CertificateLevels.GetAllAsync(cancellationToken);
                var errors = new List<string>(parseResult.Errors);
                var successCount = 0;
                Lesson? currentLesson = null;
                var queuedKanjiLinks = new HashSet<(Lesson Lesson, Kanji Kanji)>();
                var queuedVocabularyLinks = new HashSet<(Lesson Lesson, Vocabulary Vocabulary)>();
                var queuedGrammarLinks = new HashSet<(Lesson Lesson, GrammarPattern Grammar)>();

                foreach (var row in parseResult.Rows)
                {
                    try
                    {
                        var typeValue = GetRequired(row, "Type");
                        if (!Enum.GetNames<ContentEntityType>().Contains(typeValue, StringComparer.OrdinalIgnoreCase)
                            || !Enum.TryParse<ContentEntityType>(typeValue, true, out var entityType))
                            throw new InvalidOperationException("Type không hợp lệ. Giá trị hỗ trợ: Lesson, Kanji, KanaCharacter, Vocabulary, GrammarPattern, MockTest, MockQuestion, PracticeExercise.");

                        var certificateLevelId = 0;
                        if (entityType is not (ContentEntityType.MockQuestion or ContentEntityType.KanaCharacter))
                        {
                            certificateLevelId = ResolveCertificateLevelId(row, certificateTypes, certificateLevels);
                        }

                        switch (entityType)
                        {
                            case ContentEntityType.Lesson:
                            {
                                currentLesson = null;
                                var languageSkillId = GetRequiredInt(row, "LanguageSkillId");
                                var skill = await unitOfWork.LanguageSkills.GetByIdAsync(languageSkillId, cancellationToken);
                                if (skill is null || skill.IsDeleted)
                                    throw new InvalidOperationException($"LanguageSkillId {languageSkillId} không tồn tại hoặc đã bị xóa.");

                                var title = GetRequired(row, "Title");
                                var description = GetOptional(row, "Description");
                                var videoUrl = GetOptional(row, "VideoUrl") ?? string.Empty;
                                var durationSeconds = GetOptionalInt(row, "DurationSeconds") ?? 0;
                                var sortOrder = GetOptionalInt(row, "SortOrder") ?? row.RowNumber;
                                var lesson = await unitOfWork.Lessons.FirstOrDefaultAsync(
                                    x => !x.IsDeleted && x.ContentAuthorId == authorId
                                        && (x.Status == ContentStatus.Draft || x.Status == ContentStatus.Rejected)
                                        && x.CertificateLevelId == certificateLevelId
                                        && x.LanguageSkillId == languageSkillId && x.Title == title
                                        && x.Description == description && x.VideoUrl == videoUrl
                                        && x.DurationSeconds == durationSeconds && x.SortOrder == sortOrder,
                                    cancellationToken);

                                lesson ??= new Lesson
                                {
                                    Title = title,
                                    Description = description,
                                    VideoUrl = videoUrl,
                                    DurationSeconds = durationSeconds,
                                    SortOrder = sortOrder,
                                    CertificateLevelId = certificateLevelId,
                                    LanguageSkillId = languageSkillId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                };
                                if (lesson.Id == 0)
                                    await unitOfWork.Lessons.AddAsync(lesson, cancellationToken);
                                currentLesson = lesson;
                                break;
                            }
                            case ContentEntityType.Kanji:
                            {
                                var lesson = currentLesson;

                                var character = GetRequired(row, "Character");
                                var meaning = GetRequired(row, "Meaning");
                                var sinoVietnamese = GetOptional(row, "SinoVietnamese");
                                var onYomi = GetOptional(row, "OnYomi");
                                var kunYomi = GetOptional(row, "KunYomi");
                                var strokeCount = GetRequiredInt(row, "StrokeCount");
                                var strokeOrderImageUrl = GetOptional(row, "StrokeOrderImageUrl");
                                var audioUrl = GetOptional(row, "AudioUrl");

                                var kanji = lesson is null ? null : await unitOfWork.Kanjis.FirstOrDefaultAsync(
                                    x => !x.IsDeleted && x.ContentAuthorId == authorId
                                        && x.CertificateLevelId == certificateLevelId
                                        && x.Character == character && x.Meaning == meaning
                                        && x.SinoVietnamese == sinoVietnamese && x.OnYomi == onYomi
                                        && x.KunYomi == kunYomi && x.StrokeCount == strokeCount
                                        && x.StrokeOrderImageUrl == strokeOrderImageUrl && x.AudioUrl == audioUrl,
                                    cancellationToken);

                                kanji ??= new Kanji
                                {
                                    Character = character,
                                    Meaning = meaning,
                                    SinoVietnamese = sinoVietnamese,
                                    OnYomi = onYomi,
                                    KunYomi = kunYomi,
                                    StrokeCount = strokeCount,
                                    StrokeOrderImageUrl = strokeOrderImageUrl,
                                    AudioUrl = audioUrl,
                                    CertificateLevelId = certificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                };
                                if (kanji.Id == 0)
                                    await unitOfWork.Kanjis.AddAsync(kanji, cancellationToken);
                                if (lesson is not null)
                                {
                                    if (queuedKanjiLinks.Add((lesson, kanji))
                                        && !await unitOfWork.LessonKanjis.AnyAsync(
                                            x => x.LessonId == lesson.Id && x.KanjiId == kanji.Id, cancellationToken))
                                    {
                                        await unitOfWork.LessonKanjis.AddAsync(
                                            new LessonKanji { Lesson = lesson, Kanji = kanji }, cancellationToken);
                                    }
                                }
                                break;
                            }
                            case ContentEntityType.KanaCharacter:
                            {
                                if (!Enum.TryParse<RikiPath.Domain.Enums.KanaCharacterType>(GetRequired(row, "KanaType"), true, out var kanaType)
                                    || !Enum.IsDefined(kanaType))
                                    throw new InvalidOperationException("KanaType chỉ nhận Hiragana hoặc Katakana.");
                                var character = GetRequired(row, "Character");
                                if (System.Globalization.StringInfo.ParseCombiningCharacters(character).Length != 1)
                                    throw new InvalidOperationException("Character phải chứa đúng một ký tự Kana.");
                                var strokeCount = GetRequiredInt(row, "StrokeCount");
                                if (strokeCount <= 0) throw new InvalidOperationException("StrokeCount phải lớn hơn 0.");
                                await unitOfWork.KanaCharacters.AddAsync(new KanaCharacter
                                {
                                    Character = character,
                                    Type = kanaType, Romaji = GetRequired(row, "Romaji"), StrokeCount = strokeCount,
                                    StrokeOrderImageUrl = GetOptional(row, "StrokeOrderImageUrl"), AudioUrl = GetOptional(row, "AudioUrl"),
                                    ContentAuthorId = authorId, Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;
                            }

                            case ContentEntityType.Vocabulary:
                            {
                                var lesson = currentLesson;

                                var word = GetRequired(row, "Word");
                                var reading = GetRequired(row, "Reading");
                                var meaning = GetRequired(row, "Meaning");
                                var exampleSentence = GetOptional(row, "ExampleSentence");
                                var exampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning");
                                var audioUrl = GetOptional(row, "AudioUrl");

                                var vocabulary = lesson is null ? null : await unitOfWork.Vocabularies.FirstOrDefaultAsync(
                                    x => !x.IsDeleted && x.ContentAuthorId == authorId
                                        && x.CertificateLevelId == certificateLevelId
                                        && x.Word == word && x.Reading == reading && x.Meaning == meaning
                                        && x.ExampleSentence == exampleSentence
                                        && x.ExampleSentenceMeaning == exampleSentenceMeaning && x.AudioUrl == audioUrl,
                                    cancellationToken);

                                vocabulary ??= new Vocabulary
                                {
                                    Word = word,
                                    Reading = reading,
                                    Meaning = meaning,
                                    ExampleSentence = exampleSentence,
                                    ExampleSentenceMeaning = exampleSentenceMeaning,
                                    AudioUrl = audioUrl,
                                    CertificateLevelId = certificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                };
                                if (vocabulary.Id == 0)
                                    await unitOfWork.Vocabularies.AddAsync(vocabulary, cancellationToken);
                                if (lesson is not null)
                                {
                                    if (queuedVocabularyLinks.Add((lesson, vocabulary))
                                        && !await unitOfWork.LessonVocabularies.AnyAsync(
                                            x => x.LessonId == lesson.Id && x.VocabularyId == vocabulary.Id, cancellationToken))
                                    {
                                        await unitOfWork.LessonVocabularies.AddAsync(
                                            new LessonVocabulary { Lesson = lesson, Vocabulary = vocabulary }, cancellationToken);
                                    }
                                }
                                break;
                            }

                            case ContentEntityType.GrammarPattern:
                            {
                                var lesson = currentLesson;

                                var title = GetRequired(row, "Title");
                                var structure = GetRequired(row, "Structure");
                                var usageNotes = GetOptional(row, "UsageNotes");
                                var exampleSentence = GetOptional(row, "ExampleSentence");
                                var exampleSentenceMeaning = GetOptional(row, "ExampleSentenceMeaning");

                                var grammarPattern = lesson is null ? null : await unitOfWork.GrammarPatterns.FirstOrDefaultAsync(
                                    x => !x.IsDeleted && x.ContentAuthorId == authorId
                                        && x.CertificateLevelId == certificateLevelId
                                        && x.Title == title && x.Structure == structure
                                        && x.UsageNotes == usageNotes && x.ExampleSentence == exampleSentence
                                        && x.ExampleSentenceMeaning == exampleSentenceMeaning,
                                    cancellationToken);

                                grammarPattern ??= new GrammarPattern
                                {
                                    Title = title,
                                    Structure = structure,
                                    UsageNotes = usageNotes,
                                    ExampleSentence = exampleSentence,
                                    ExampleSentenceMeaning = exampleSentenceMeaning,
                                    CertificateLevelId = certificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                };
                                if (grammarPattern.Id == 0)
                                    await unitOfWork.GrammarPatterns.AddAsync(grammarPattern, cancellationToken);
                                if (lesson is not null)
                                {
                                    if (queuedGrammarLinks.Add((lesson, grammarPattern))
                                        && !await unitOfWork.LessonGrammars.AnyAsync(
                                            x => x.LessonId == lesson.Id && x.GrammarPatternId == grammarPattern.Id, cancellationToken))
                                    {
                                        await unitOfWork.LessonGrammars.AddAsync(
                                            new LessonGrammar { Lesson = lesson, GrammarPattern = grammarPattern }, cancellationToken);
                                    }
                                }
                                break;
                            }

                            case ContentEntityType.MockTest:
                                await unitOfWork.MockTests.AddAsync(new MockTest
                                {
                                    Title = GetRequired(row, "Title"),
                                    Description = GetOptional(row, "Description"),
                                    TimeLimitMinutes = GetRequiredInt(row, "TimeLimitMinutes"),
                                    CertificateLevelId = certificateLevelId,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;

                            case ContentEntityType.PracticeExercise:
                            {
                                var lesson = currentLesson;
                                if (lesson is null)
                                    throw new InvalidOperationException("Phải đặt dòng PracticeExercise bên dưới một dòng Lesson trong cùng file.");
                                if (lesson.CertificateLevelId != certificateLevelId)
                                    throw new InvalidOperationException("CertificateType/CertificateLevel phải khớp với Lesson gần nhất phía trên.");

                                var languageSkillId = GetRequiredInt(row, "LanguageSkillId");
                                var skill = await unitOfWork.LanguageSkills.GetByIdAsync(languageSkillId, cancellationToken);
                                if (skill is null || skill.IsDeleted)
                                    throw new InvalidOperationException($"LanguageSkillId {languageSkillId} không tồn tại hoặc đã bị xóa.");

                                await unitOfWork.PracticeExercises.AddAsync(new PracticeExercise
                                {
                                    Title = GetRequired(row, "Title"),
                                    Description = GetOptional(row, "Description"),
                                    Lesson = lesson,
                                    LanguageSkillId = languageSkillId,
                                    CertificateLevelId = lesson.CertificateLevelId,
                                    SortOrder = GetOptionalInt(row, "SortOrder") ?? row.RowNumber,
                                    ContentAuthorId = authorId,
                                    Status = ContentStatus.Draft
                                }, cancellationToken);
                                break;
                            }

                            case ContentEntityType.MockQuestion:
                            {
                                var sectionId = GetRequiredInt(row, "MockTestSectionId");
                                var section = await unitOfWork.MockTestSections.GetByIdAsync(sectionId, cancellationToken);
                                var test = section is null ? null : await unitOfWork.MockTests.GetByIdAsync(section.MockTestId, cancellationToken);
                                if (test is null || test.IsDeleted || test.ContentAuthorId != authorId || test.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                                    throw new InvalidOperationException("MockTestSectionId phải thuộc Mock Test Draft/Rejected do chính bạn tạo.");

                                await unitOfWork.MockQuestions.AddAsync(new MockQuestion
                                {
                                    QuestionText = GetRequired(row, "QuestionText"),
                                    Explanation = GetOptional(row, "Explanation"),
                                    AudioUrl = GetOptional(row, "AudioUrl"),
                                    ImageUrl = GetOptional(row, "ImageUrl"),
                                    MockTestSectionId = sectionId,
                                    SortOrder = GetOptionalInt(row, "SortOrder") ?? row.RowNumber
                                }, cancellationToken);
                                break;
                            }

                            default:
                                throw new InvalidOperationException("Loại nội dung này chưa được hỗ trợ trong Excel import.");
                        }

                        successCount++;
                    }
                    catch (Exception rowEx)
                    {
                        errors.Add($"Dòng {row.RowNumber}: {rowEx.Message}");
                    }
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<BulkImportResultResponse>.Success(
                    new BulkImportResultResponse
                    {
                        TotalRows = parseResult.Rows.Count,
                        SuccessCount = successCount,
                        FailedCount = parseResult.Rows.Count - successCount,
                        Errors = errors
                    });
            }
            catch (Exception ex)
            {
                return ApiResponse<BulkImportResultResponse>.Fail($"Import thất bại: {ex.Message}");
            }
        }

        // ================= SubmitForReviewAsync =================

        public Task<ApiResponse<ContentReviewStatusResponse>> SubmitForReviewAsync(
            ContentEntityType entityType, int entityId, CancellationToken cancellationToken)
        {
            var authorId = claimService.GetUserClaim().Id;
            return entityType switch
            {
                ContentEntityType.Lesson => SubmitLessonForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.Kanji => SubmitKanjiForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.KanaCharacter => SubmitKanaForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.Vocabulary => SubmitVocabularyForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.GrammarPattern => SubmitGrammarForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.MockTest => SubmitMockTestForReviewAsync(authorId, entityId, cancellationToken),
                ContentEntityType.PracticeExercise => SubmitPracticeExerciseForReviewAsync(authorId, entityId, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ gửi duyệt."))
            };
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitLessonForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Lessons.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.Lessons.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapLessonToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitKanjiForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Kanjis.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.Kanjis.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapKanjiToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitKanaForReviewAsync(int authorId, int entityId, CancellationToken ct)
        {
            try
            {
                var entity = await unitOfWork.KanaCharacters.GetByIdAsync(entityId, ct);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId) return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");
                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected)) return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");
                entity.Status = ContentStatus.PendingReview; entity.IsApproved = false; entity.ReviewedByName = null; entity.ReviewNote = null; entity.ReviewedDate = null;
                unitOfWork.KanaCharacters.Update(entity); await unitOfWork.SaveChangesAsync(ct);
                return ApiResponse<ContentReviewStatusResponse>.Success(MapKanaToResponse(entity));
            }
            catch (Exception ex) { return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}"); }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitVocabularyForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Vocabularies.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.Vocabularies.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapVocabularyToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitGrammarForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.GrammarPatterns.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.GrammarPatterns.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapGrammarToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitMockTestForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.MockTests.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");

                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;

                unitOfWork.MockTests.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapMockTestToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}");
            }
        }

        // ================= GetMyContentAsync =================

        private async Task<ApiResponse<ContentReviewStatusResponse>> SubmitPracticeExerciseForReviewAsync(
            int authorId, int entityId, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.PracticeExercises.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.ContentAuthorId != authorId)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung.");
                if (entity.Status is not (ContentStatus.Draft or ContentStatus.Rejected))
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Chỉ có thể gửi duyệt nội dung Draft hoặc Rejected.");

                entity.Status = ContentStatus.PendingReview;
                entity.IsApproved = false;
                entity.ReviewedByName = null;
                entity.ReviewNote = null;
                entity.ReviewedDate = null;
                unitOfWork.PracticeExercises.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<ContentReviewStatusResponse>.Success(MapPracticeExerciseToResponse(entity));
            }
            catch (Exception ex) { return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể gửi duyệt: {ex.Message}"); }
        }

        public async Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            try
            {
                var authorId = claimService.GetUserClaim().Id;

                List<ContentReviewStatusResponse> result = entityType switch
                {
                    ContentEntityType.Lesson => (await unitOfWork.Lessons.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapLessonToResponse).ToList(),
                    ContentEntityType.Kanji => (await unitOfWork.Kanjis.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapKanjiToResponse).ToList(),
                    ContentEntityType.KanaCharacter => (await unitOfWork.KanaCharacters.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapKanaToResponse).ToList(),
                    ContentEntityType.Vocabulary => (await unitOfWork.Vocabularies.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapVocabularyToResponse).ToList(),
                    ContentEntityType.GrammarPattern => (await unitOfWork.GrammarPatterns.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapGrammarToResponse).ToList(),
                    ContentEntityType.MockTest => (await unitOfWork.MockTests.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapMockTestToResponse).ToList(),
                    ContentEntityType.PracticeExercise => (await unitOfWork.PracticeExercises.FindAsync(e => !e.IsDeleted && e.ContentAuthorId == authorId, cancellationToken))
                        .Select(MapPracticeExerciseToResponse).ToList(),
                    _ => throw new InvalidOperationException("Loại nội dung không hợp lệ.")
                };

                return ApiResponse<List<ContentReviewStatusResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ContentReviewStatusResponse>>.Fail($"Không thể tải danh sách: {ex.Message}");
            }
        }

        // ================= GetPendingReviewAsync =================

        public async Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            try
            {
                List<ContentReviewStatusResponse> result = entityType switch
                {
                    ContentEntityType.Lesson => (await unitOfWork.Lessons.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapLessonToResponse).ToList(),
                    ContentEntityType.Kanji => (await unitOfWork.Kanjis.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapKanjiToResponse).ToList(),
                    ContentEntityType.KanaCharacter => (await unitOfWork.KanaCharacters.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapKanaToResponse).ToList(),
                    ContentEntityType.Vocabulary => (await unitOfWork.Vocabularies.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapVocabularyToResponse).ToList(),
                    ContentEntityType.GrammarPattern => (await unitOfWork.GrammarPatterns.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapGrammarToResponse).ToList(),
                    ContentEntityType.MockTest => (await unitOfWork.MockTests.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapMockTestToResponse).ToList(),
                    ContentEntityType.PracticeExercise => (await unitOfWork.PracticeExercises.FindAsync(e => !e.IsDeleted && e.Status == ContentStatus.PendingReview, cancellationToken))
                        .Select(MapPracticeExerciseToResponse).ToList(),
                    _ => throw new InvalidOperationException("Loại nội dung không hợp lệ.")
                };

                return ApiResponse<List<ContentReviewStatusResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ContentReviewStatusResponse>>.Fail($"Không thể tải danh sách chờ duyệt: {ex.Message}");
            }
        }

        // ================= ReviewAsync =================

        public Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(
            ContentEntityType entityType, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            var adminId = claimService.GetUserClaim().Id;
            return entityType switch
            {
                ContentEntityType.Lesson => ReviewLessonAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.Kanji => ReviewKanjiAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.KanaCharacter => ReviewKanaAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.Vocabulary => ReviewVocabularyAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.GrammarPattern => ReviewGrammarAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.MockTest => ReviewMockTestAsync(adminId, entityId, request, cancellationToken),
                ContentEntityType.PracticeExercise => ReviewPracticeExerciseAsync(adminId, entityId, request, cancellationToken),
                _ => Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Loại nội dung không hỗ trợ duyệt."))
            };
        }

        // Lấy tên hiển thị của admin đã duyệt - dùng chung cho cả 5 loại (private helper, không
        // phải interface nên không vi phạm yêu cầu "bỏ IReviewableContent").
        // Sửa lại lỗi precedence: "a + " " + b ?? fallback" KHÔNG hoạt động như mong đợi vì
        // string + null vẫn ra chuỗi non-null, nên "?? fallback" không bao giờ được kích hoạt.
        private async Task<string> GetReviewerNameAsync(int adminId, CancellationToken cancellationToken)
        {
            var admin = await unitOfWork.UserAccounts.GetByIdAsync(adminId, cancellationToken);
            return admin is null ? $"Admin #{adminId}" : $"{admin.FirstName} {admin.LastName}".Trim();
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewLessonAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Lessons.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.Lessons.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapLessonToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewKanjiAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Kanjis.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.Kanjis.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapKanjiToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewKanaAsync(int adminId, int entityId, ReviewContentRequest request, CancellationToken ct)
        {
            try
            {
                var entity = await unitOfWork.KanaCharacters.GetByIdAsync(entityId, ct);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview) return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");
                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve; entity.ReviewedByName = await GetReviewerNameAsync(adminId, ct);
                entity.ReviewNote = request.Approve ? null : request.ReviewNote; entity.ReviewedDate = DateTime.UtcNow;
                unitOfWork.KanaCharacters.Update(entity); await unitOfWork.SaveChangesAsync(ct);
                return ApiResponse<ContentReviewStatusResponse>.Success(MapKanaToResponse(entity));
            }
            catch (Exception ex) { return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}"); }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewVocabularyAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.Vocabularies.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.Vocabularies.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapVocabularyToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewGrammarAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.GrammarPatterns.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.GrammarPatterns.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapGrammarToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewMockTestAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.MockTests.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;

                unitOfWork.MockTests.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<ContentReviewStatusResponse>.Success(MapMockTestToResponse(entity));
            }
            catch (Exception ex)
            {
                return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ContentReviewStatusResponse>> ReviewPracticeExerciseAsync(
            int adminId, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await unitOfWork.PracticeExercises.GetByIdAsync(entityId, cancellationToken);
                if (entity is null || entity.IsDeleted || entity.Status != ContentStatus.PendingReview)
                    return ApiResponse<ContentReviewStatusResponse>.Fail("Không tìm thấy nội dung đang chờ duyệt.");

                entity.Status = request.Approve ? ContentStatus.Published : ContentStatus.Rejected;
                entity.IsApproved = request.Approve;
                entity.ReviewedByName = await GetReviewerNameAsync(adminId, cancellationToken);
                entity.ReviewNote = request.Approve ? null : request.ReviewNote;
                entity.ReviewedDate = DateTime.UtcNow;
                unitOfWork.PracticeExercises.Update(entity);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<ContentReviewStatusResponse>.Success(MapPracticeExerciseToResponse(entity));
            }
            catch (Exception ex) { return ApiResponse<ContentReviewStatusResponse>.Fail($"Không thể duyệt nội dung: {ex.Message}"); }
        }

        // ================= Mapping (1 method riêng / loại, không qua interface) =================

        private static ContentReviewStatusResponse MapLessonToResponse(Lesson e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.Lesson,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapKanjiToResponse(Kanji e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.Kanji,
            Title = e.Character,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapKanaToResponse(KanaCharacter e) => new()
        {
            Id = e.Id, EntityType = ContentEntityType.KanaCharacter, Title = e.Character,
            Status = e.Status, ContentAuthorId = e.ContentAuthorId, ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate, ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapVocabularyToResponse(Vocabulary e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.Vocabulary,
            Title = e.Word,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapGrammarToResponse(GrammarPattern e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.GrammarPattern,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapMockTestToResponse(MockTest e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.MockTest,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static ContentReviewStatusResponse MapPracticeExerciseToResponse(PracticeExercise e) => new()
        {
            Id = e.Id,
            EntityType = ContentEntityType.PracticeExercise,
            Title = e.Title,
            Status = e.Status,
            ContentAuthorId = e.ContentAuthorId,
            ReviewNote = e.ReviewNote,
            ReviewedDate = e.ReviewedDate,
            ReviewedByName = e.ReviewedByName
        };

        private static string GetRequired(ParsedContentRow row, string key)
            => row.Fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : throw new InvalidOperationException($"Thiếu trường bắt buộc '{key}'.");

        private static string? GetOptional(ParsedContentRow row, string key)
            => row.Fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        private static int GetRequiredInt(ParsedContentRow row, string key)
        {
            var value = GetRequired(row, key);
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                throw new InvalidOperationException($"Trường '{key}' phải là số nguyên hợp lệ.");
            return parsed;
        }

        private static int? GetOptionalInt(ParsedContentRow row, string key)
        {
            var value = GetOptional(row, key);
            if (value is null) return null;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                throw new InvalidOperationException($"Trường '{key}' phải là số nguyên hợp lệ.");
            return parsed;
        }

        private static int ResolveCertificateLevelId(
            ParsedContentRow row,
            IReadOnlyList<CertificateType> certificateTypes,
            IReadOnlyList<CertificateLevel> certificateLevels)
        {
            var certificateTypeName = GetRequired(row, "CertificateType").Trim();
            var certificateLevelCode = GetRequired(row, "CertificateLevel").Trim();

            var certificateType = certificateTypes.FirstOrDefault(x =>
                !x.IsDeleted && x.IsActive &&
                string.Equals(x.Name, certificateTypeName, StringComparison.OrdinalIgnoreCase));
            if (certificateType is null)
                throw new InvalidOperationException($"CertificateType '{certificateTypeName}' không tồn tại, đã bị xóa hoặc không hoạt động.");

            var certificateLevel = certificateLevels.FirstOrDefault(x =>
                !x.IsDeleted
                && x.CertificateTypeId == certificateType.Id
                && string.Equals(x.Code?.Trim(), certificateLevelCode, StringComparison.OrdinalIgnoreCase));
            if (certificateLevel is null)
                throw new InvalidOperationException($"Không tìm thấy CertificateLevel '{certificateLevelCode}' thuộc CertificateType '{certificateTypeName}' hoặc cấp độ đã bị xóa.");

            return certificateLevel.Id;
        }

    }
}
