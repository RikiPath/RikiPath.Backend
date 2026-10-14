using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Globalization;

namespace RikiPath.Application.Services
{
    public class ContentManagementService(IUnitOfWork unitOfWork, IExcelParser excelParser, IClaimService claimService)
        : IContentManagementService
    {
        public async Task<ApiResponse<BulkImportResultResponse>> BulkImportAsync(
            BulkImportRequest request, CancellationToken cancellationToken)
        {
            try
            {
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
                                    var sortOrder = GetOptionalInt(row, "SortOrder") ?? row.RowNumber;
                                    var lesson = await unitOfWork.Lessons.FirstOrDefaultAsync(
                                        x => !x.IsDeleted
                                            && x.CertificateLevelId == certificateLevelId
                                            && x.LanguageSkillId == languageSkillId && x.Title == title
                                            && x.Description == description && x.SortOrder == sortOrder,
                                        cancellationToken);

                                    lesson ??= new Lesson
                                    {
                                        Title = title,
                                        Description = description,
                                        SortOrder = sortOrder,
                                        CertificateLevelId = certificateLevelId,
                                        LanguageSkillId = languageSkillId,
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

                                    var existingKanji = lesson is null ? null : await unitOfWork.Kanjis.FirstOrDefaultAsync(
                                        x => !x.IsDeleted
                                            && x.Character == character && x.Meaning == meaning
                                            && x.SinoVietnamese == sinoVietnamese && x.OnYomi == onYomi
                                            && x.KunYomi == kunYomi && x.StrokeCount == strokeCount
                                            && x.StrokeOrderImageUrl == strokeOrderImageUrl && x.AudioUrl == audioUrl,
                                        cancellationToken);

                                    var kanji = existingKanji ?? new Kanji
                                    {
                                        Character = character,
                                        Meaning = meaning,
                                        SinoVietnamese = sinoVietnamese,
                                        OnYomi = onYomi,
                                        KunYomi = kunYomi,
                                        StrokeCount = strokeCount,
                                        StrokeOrderImageUrl = strokeOrderImageUrl,
                                        AudioUrl = audioUrl,
                                    };
                                    if (kanji.Id == 0)
                                        await unitOfWork.Kanjis.AddAsync(kanji, cancellationToken);

                                    if (certificateLevelId > 0)
                                    {
                                        if (!await unitOfWork.ContentLevelMappings.AnyAsync(
                                            x => x.CertificateLevelId == certificateLevelId && x.KanjiId == kanji.Id && !x.IsDeleted, cancellationToken))
                                        {
                                            await unitOfWork.ContentLevelMappings.AddAsync(new ContentLevelMapping
                                            {
                                                CertificateLevelId = certificateLevelId,
                                                Kanji = kanji
                                            }, cancellationToken);
                                        }
                                    }

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
                                    if (!Enum.TryParse<KanaCharacterType>(GetRequired(row, "KanaType"), true, out var kanaType)
                                        || !Enum.IsDefined(kanaType))
                                        throw new InvalidOperationException("KanaType chỉ nhận Hiragana hoặc Katakana.");
                                    var character = GetRequired(row, "Character");
                                    if (StringInfo.ParseCombiningCharacters(character).Length != 1)
                                        throw new InvalidOperationException("Character phải chứa đúng một ký tự Kana.");
                                    var strokeCount = GetRequiredInt(row, "StrokeCount");
                                    if (strokeCount <= 0) throw new InvalidOperationException("StrokeCount phải lớn hơn 0.");

                                    await unitOfWork.KanaCharacters.AddAsync(new KanaCharacter
                                    {
                                        Character = character,
                                        Type = kanaType,
                                        Romaji = GetRequired(row, "Romaji"),
                                        StrokeCount = strokeCount,
                                        StrokeOrderImageUrl = GetOptional(row, "StrokeOrderImageUrl"),
                                        AudioUrl = GetOptional(row, "AudioUrl"),
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

                                    var existingVocabulary = lesson is null ? null : await unitOfWork.Vocabularies.FirstOrDefaultAsync(
                                        x => !x.IsDeleted
                                            && x.Word == word && x.Reading == reading && x.Meaning == meaning
                                            && x.ExampleSentence == exampleSentence
                                            && x.ExampleSentenceMeaning == exampleSentenceMeaning && x.AudioUrl == audioUrl,
                                        cancellationToken);

                                    var vocabulary = existingVocabulary ?? new Vocabulary
                                    {
                                        Word = word,
                                        Reading = reading,
                                        Meaning = meaning,
                                        ExampleSentence = exampleSentence,
                                        ExampleSentenceMeaning = exampleSentenceMeaning,
                                        AudioUrl = audioUrl,
                                    };
                                    if (vocabulary.Id == 0)
                                        await unitOfWork.Vocabularies.AddAsync(vocabulary, cancellationToken);

                                    if (certificateLevelId > 0)
                                    {
                                        if (!await unitOfWork.ContentLevelMappings.AnyAsync(
                                            x => x.CertificateLevelId == certificateLevelId && x.VocabularyId == vocabulary.Id && !x.IsDeleted, cancellationToken))
                                        {
                                            await unitOfWork.ContentLevelMappings.AddAsync(new ContentLevelMapping
                                            {
                                                CertificateLevelId = certificateLevelId,
                                                Vocabulary = vocabulary
                                            }, cancellationToken);
                                        }
                                    }

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

                                    var existingGrammarPattern = lesson is null ? null : await unitOfWork.GrammarPatterns.FirstOrDefaultAsync(
                                        x => !x.IsDeleted
                                            && x.Title == title && x.Structure == structure
                                            && x.UsageNotes == usageNotes && x.ExampleSentence == exampleSentence
                                            && x.ExampleSentenceMeaning == exampleSentenceMeaning,
                                        cancellationToken);

                                    var grammarPattern = existingGrammarPattern ?? new GrammarPattern
                                    {
                                        Title = title,
                                        Structure = structure,
                                        UsageNotes = usageNotes,
                                        ExampleSentence = exampleSentence,
                                        ExampleSentenceMeaning = exampleSentenceMeaning,
                                    };
                                    if (grammarPattern.Id == 0)
                                        await unitOfWork.GrammarPatterns.AddAsync(grammarPattern, cancellationToken);

                                    if (certificateLevelId > 0)
                                    {
                                        if (!await unitOfWork.ContentLevelMappings.AnyAsync(
                                            x => x.CertificateLevelId == certificateLevelId && x.GrammarPatternId == grammarPattern.Id && !x.IsDeleted, cancellationToken))
                                        {
                                            await unitOfWork.ContentLevelMappings.AddAsync(new ContentLevelMapping
                                            {
                                                CertificateLevelId = certificateLevelId,
                                                GrammarPattern = grammarPattern
                                            }, cancellationToken);
                                        }
                                    }

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
                                        SortOrder = GetOptionalInt(row, "SortOrder") ?? row.RowNumber,
                                    }, cancellationToken);
                                    break;
                                }

                            case ContentEntityType.MockQuestion:
                                {
                                    var sectionId = GetRequiredInt(row, "MockTestSectionId");
                                    var section = await unitOfWork.MockTestSections.GetByIdAsync(sectionId, cancellationToken);
                                    var test = section is null ? null : await unitOfWork.MockTests.GetByIdAsync(section.MockTestId, cancellationToken);
                                    if (test is null || test.IsDeleted)
                                        throw new InvalidOperationException("MockTestSectionId phải thuộc Mock Test hợp lệ.");

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

        public Task<ApiResponse<List<ContentReviewStatusResponse>>> GetPendingReviewAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApiResponse<List<ContentReviewStatusResponse>>.Success([]));
        }

        public Task<ApiResponse<ContentReviewStatusResponse>> ReviewAsync(
            ContentEntityType entityType, int entityId, ReviewContentRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApiResponse<ContentReviewStatusResponse>.Fail("Hệ thống đã chuyển sang mô hình tự động phát hành nội dung."));
        }

        public Task<ApiResponse<bool>> SubmitForReviewAsync(
            ContentEntityType entityType, int entityId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApiResponse<bool>.Success(true));
        }

        public Task<ApiResponse<List<ContentReviewStatusResponse>>> GetMyContentAsync(
            ContentEntityType entityType, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApiResponse<List<ContentReviewStatusResponse>>.Success([]));
        }

        #region Helper Methods
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
        #endregion
    }
}