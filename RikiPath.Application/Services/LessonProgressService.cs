using RikiPath.Application.Helpers;
using RikiPath.Application.Requests.Reviews;
using RikiPath.Application.Responses.Reviews;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Lessons;
using System.Net;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RikiPath.Application.Services
{
    public class LessonProgressService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        ILogger<LessonProgressService>? logger = null,
        IReviewService? reviewService = null) : ILessonProgressService
    {
        private const int AutoCompleteThresholdPercent = 95;

        public async Task<ApiResponse<LearningDiscoveryResponse>> GetLearningDiscoveryAsync(
            int certificateLevelId,
            CancellationToken cancellationToken)
        {
            if (certificateLevelId <= 0)
            {
                return ApiResponse<LearningDiscoveryResponse>.Fail(
                    "Cấp độ JLPT không hợp lệ.",
                    HttpStatusCode.BadRequest);
            }

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var level = await unitOfWork.CertificateLevels.FirstOrDefaultAsync(
                    item => item.Id == certificateLevelId
                        && !item.IsDeleted
                        && !item.CertificateType.IsDeleted
                        && item.CertificateType.IsActive
                        && item.CertificateType.Name == "JLPT",
                    cancellationToken);

                if (level is null)
                {
                    return ApiResponse<LearningDiscoveryResponse>.NotFound(
                        $"Không tìm thấy cấp độ JLPT Id = {certificateLevelId}.");
                }

                var lessons = await unitOfWork.Lessons.GetLearnerVisibleByLevelAsync(
                    certificateLevelId,
                    userId,
                    cancellationToken);
                var lessonIds = lessons.Select(lesson => lesson.Id).ToArray();
                var withExercises = await LessonPracticeProgress.WithExercisesAsync(unitOfWork, lessonIds, cancellationToken);
                var passed = await LessonPracticeProgress.PassedAsync(unitOfWork, userId, lessonIds, cancellationToken);
                var lessonResponses = lessons.Select(lesson => MapToSummary(
                    lesson, withExercises.Contains(lesson.Id), passed.Contains(lesson.Id))).ToList();
                var completedLessons = lessonResponses.Count(item => item.IsCompleted);
                var overallProgress = lessonResponses.Count == 0
                    ? 0
                    : (int)Math.Round(lessonResponses.Average(item => item.ProgressPercent));

                return ApiResponse<LearningDiscoveryResponse>.Success(
                    new LearningDiscoveryResponse
                    {
                        CertificateLevelId = level.Id,
                        LevelCode = level.Code,
                        TotalLessons = lessonResponses.Count,
                        CompletedLessons = completedLessons,
                        OverallProgressPercent = overallProgress,
                        Lessons = lessonResponses,
                    });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return ApiResponse<LearningDiscoveryResponse>.Fail(
                    "Không thể tải danh sách bài học.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<LessonDetailResponse>> GetLessonDetailAsync(
            int lessonId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(
                    lessonId,
                    cancellationToken);
                if (lesson is null)
                {
                    return ApiResponse<LessonDetailResponse>.NotFound(
                        $"Không tìm thấy bài học Id = {lessonId}.");
                }

                var progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(
                    userId,
                    lessonId,
                    cancellationToken);

                var now = DateTime.UtcNow;
                if (progress is null)
                {
                    progress = new LessonProgress
                    {
                        UserId = userId,
                        LessonId = lessonId,
                        ProgressPercent = 0,
                        IsCompleted = false,
                        FirstViewedAt = now,
                        LastWatchedAt = now,
                    };
                    await unitOfWork.LessonProgresses.AddAsync(progress, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else if (progress.FirstViewedAt is null || progress.IsDeleted)
                {
                    progress.IsDeleted = false;
                    progress.FirstViewedAt ??= now;
                    unitOfWork.LessonProgresses.Update(progress);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                var detail = MapToResponse(lesson, progress);
                if (progress.IsCompleted)
                    detail.SuggestedSrsCards = await BuildLessonSrsSuggestionsAsync(userId, lesson, cancellationToken);

                return ApiResponse<LessonDetailResponse>.Success(detail);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DbException exception) when (exception.SqlState is "42703" or "42P01")
            {
                logger?.LogError(exception, "The database schema is incomplete for lesson {LessonId}.", lessonId);
                return ApiResponse<LessonDetailResponse>.Fail(
                    "Cơ sở dữ liệu cần được cập nhật trước khi mở bài học này.",
                    HttpStatusCode.InternalServerError, "LESSON_SCHEMA_UPDATE_REQUIRED");
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Failed to load lesson {LessonId}.", lessonId);
                return ApiResponse<LessonDetailResponse>.Fail(
                    "Không thể tải chi tiết bài học.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<LessonDetailResponse>> UpdatePlaybackPositionAsync(
            int lessonId,
            UpdatePlaybackPositionRequest request,
            CancellationToken cancellationToken)
        {
            if (request.DurationSeconds <= 0)
            {
                return ApiResponse<LessonDetailResponse>.Fail("Thời lượng video không hợp lệ.");
            }

            if (request.PositionSeconds < 0)
            {
                return ApiResponse<LessonDetailResponse>.Fail("Vị trí phát không hợp lệ.");
            }

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(
                    lessonId,
                    cancellationToken);
                if (lesson is null)
                {
                    return ApiResponse<LessonDetailResponse>.NotFound(
                        $"Không tìm thấy bài học Id = {lessonId}.");
                }

                LessonProgress? progress = null;
                var now = DateTime.UtcNow;
                var clampedPosition = Math.Min(request.PositionSeconds, request.DurationSeconds);
                var percent = (int)Math.Round(clampedPosition * 100.0 / request.DurationSeconds);
                percent = Math.Clamp(percent, 0, 100);
                bool newlyCompleted = false;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(
                        userId,
                        lessonId,
                        token);

                    if (progress is null)
                    {
                        progress = new LessonProgress
                        {
                            UserId = userId,
                            LessonId = lessonId,
                            ProgressPercent = percent,
                            LastPositionSeconds = clampedPosition,
                            LastWatchedAt = now,
                            FirstViewedAt = now,
                            LastInteractedAt = now,
                            InteractionCount = 1,
                        };
                        await unitOfWork.LessonProgresses.AddAsync(progress, token);
                    }
                    else
                    {
                        progress.IsDeleted = false;
                        progress.ProgressPercent = Math.Max(progress.ProgressPercent, percent);
                        progress.LastPositionSeconds = clampedPosition;
                        progress.LastWatchedAt = now;
                        progress.FirstViewedAt ??= now;
                        progress.LastInteractedAt = now;
                        progress.InteractionCount++;
                        unitOfWork.LessonProgresses.Update(progress);
                    }

                    if (!progress.IsCompleted && progress.ProgressPercent >= AutoCompleteThresholdPercent)
                    {
                        progress.IsCompleted = true;
                        progress.CompletedAt = now;
                        progress.ModifiedDate = now;
                        newlyCompleted = true;
                    }

                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                var detail = MapToResponse(lesson, progress!);
                if (newlyCompleted || progress!.IsCompleted)
                {
                    var (suggestions, autoEnrolledCount) = await PrepareCompletionSuggestionsAndAutoEnrollAsync(
                        userId, lesson, newlyCompleted, cancellationToken);
                    detail.SuggestedSrsCards = suggestions;
                    detail.AutoEnrolledSrsCount = autoEnrolledCount;
                }
                return ApiResponse<LessonDetailResponse>.Success(detail);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return ApiResponse<LessonDetailResponse>.Fail(
                    "Không thể lưu tiến độ xem video.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<LessonDetailResponse>> CompleteLessonAsync(
            int lessonId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(
                    lessonId,
                    cancellationToken);
                if (lesson is null)
                {
                    return ApiResponse<LessonDetailResponse>.NotFound(
                        $"Không tìm thấy bài học Id = {lessonId}.");
                }

                LessonProgress? progress = null;
                var newlyCompleted = false;
                var now = DateTime.UtcNow;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(
                        userId,
                        lessonId,
                        token);

                    if (progress?.IsCompleted == true && !progress.IsDeleted)
                        return;

                    newlyCompleted = true;
                    if (progress is null)
                    {
                        progress = new LessonProgress
                        {
                            UserId = userId,
                            LessonId = lessonId,
                            ProgressPercent = 100,
                            IsCompleted = true,
                            FirstViewedAt = now,
                            LastInteractedAt = now,
                            InteractionCount = 1,
                            CompletedAt = now,
                            LastWatchedAt = now,
                            ModifiedDate = now,
                        };
                        await unitOfWork.LessonProgresses.AddAsync(progress, token);
                    }
                    else
                    {
                        // Retry hoặc concurrent completion cùng một lesson không tạo progress trùng và không auto-enroll lặp.
                        progress.IsDeleted = false;
                        progress.ProgressPercent = 100;
                        progress.IsCompleted = true;
                        progress.FirstViewedAt ??= now;
                        progress.LastInteractedAt = now;
                        progress.InteractionCount = Math.Max(1, progress.InteractionCount);
                        progress.CompletedAt ??= now;
                        progress.LastWatchedAt = now;
                        progress.ModifiedDate ??= now;
                        unitOfWork.LessonProgresses.Update(progress);
                    }

                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                var (suggestions, autoEnrolled) = await PrepareCompletionSuggestionsAndAutoEnrollAsync(
                    userId, lesson, triggerAutoEnroll: newlyCompleted, cancellationToken);
                var detail = MapToResponse(lesson, progress!);
                detail.SuggestedSrsCards = suggestions;
                detail.AutoEnrolledSrsCount = autoEnrolled;
                return ApiResponse<LessonDetailResponse>.Success(detail);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return ApiResponse<LessonDetailResponse>.Fail(
                    "Không thể hoàn thành bài học.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<LessonDetailResponse>> RecordInteractionAsync(
            int lessonId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(lessonId, cancellationToken);
                if (lesson is null)
                    return ApiResponse<LessonDetailResponse>.NotFound($"Không tìm thấy bài học Id = {lessonId}.");

                var now = DateTime.UtcNow;
                var progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(userId, lessonId, cancellationToken);
                if (progress is null)
                {
                    progress = new LessonProgress
                    {
                        UserId = userId,
                        LessonId = lessonId,
                        ProgressPercent = 0,
                        IsCompleted = false,
                        FirstViewedAt = now,
                        LastInteractedAt = now,
                        InteractionCount = 1,
                        LastWatchedAt = now,
                    };
                    await unitOfWork.LessonProgresses.AddAsync(progress, cancellationToken);
                }
                else
                {
                    progress.IsDeleted = false;
                    progress.FirstViewedAt ??= now;
                    progress.LastInteractedAt = now;
                    progress.InteractionCount++;
                    unitOfWork.LessonProgresses.Update(progress);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                var detail = MapToResponse(lesson, progress);
                if (progress.IsCompleted)
                    detail.SuggestedSrsCards = await BuildLessonSrsSuggestionsAsync(userId, lesson, cancellationToken);

                return ApiResponse<LessonDetailResponse>.Success(detail);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                return ApiResponse<LessonDetailResponse>.Fail("Không thể ghi nhận tương tác bài học.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<SrsSuggestedCardResponse>>> GetLessonSrsSuggestionsAsync(
            int lessonId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(lessonId, cancellationToken);
                if (lesson is null)
                    return ApiResponse<List<SrsSuggestedCardResponse>>.NotFound($"Không tìm thấy bài học Id = {lessonId}.");

                var suggestions = await BuildLessonSrsSuggestionsAsync(userId, lesson, cancellationToken);
                return ApiResponse<List<SrsSuggestedCardResponse>>.Success(suggestions);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                return ApiResponse<List<SrsSuggestedCardResponse>>.Fail("Không thể tải gợi ý thẻ SRS từ bài học.", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<(List<SrsSuggestedCardResponse> Suggestions, int AutoEnrolledCount)> PrepareCompletionSuggestionsAndAutoEnrollAsync(
            int userId,
            Lesson lesson,
            bool triggerAutoEnroll,
            CancellationToken cancellationToken)
        {
            var suggestions = await BuildLessonSrsSuggestionsAsync(userId, lesson, cancellationToken);
            var autoEnrolled = 0;

            if (triggerAutoEnroll && reviewService is not null)
            {
                var pref = await unitOfWork.UserSrsPreferences.GetByUserIdAsync(userId, cancellationToken);
                if (pref?.AutoEnrollAfterLesson == true)
                {
                    var pending = suggestions.Where(s => !s.AlreadyEnrolled).Select(s => new EnrollSrsCardRequest
                    {
                        VocabularyId = s.VocabularyId,
                        KanjiId = s.KanjiId,
                        GrammarPatternId = s.GrammarPatternId,
                        SrsCardTemplateId = s.SrsCardTemplateId,
                        LessonId = lesson.Id,
                        SourceType = "Lesson",
                        SourceId = lesson.Id,
                        SourceLabel = lesson.Title,
                        CardType = s.CardType,
                        JlptLevel = s.JlptLevel,
                    }).ToList();

                    if (pending.Count > 0)
                    {
                        foreach (var chunk in pending.Chunk(100))
                        {
                            var enrolled = await reviewService.BatchEnrollCardsAsync(
                                userId,
                                new BatchEnrollSrsCardsRequest { Cards = [.. chunk] },
                                cancellationToken);
                            if (enrolled.IsSuccess && enrolled.Result is not null)
                            {
                                autoEnrolled += enrolled.Result.CreatedCount + enrolled.Result.ReactivatedCount;
                            }
                        }
                        suggestions = await BuildLessonSrsSuggestionsAsync(userId, lesson, cancellationToken);
                    }
                }
            }

            return (suggestions, autoEnrolled);
        }

        private async Task<List<SrsSuggestedCardResponse>> BuildLessonSrsSuggestionsAsync(
            int userId,
            Lesson lesson,
            CancellationToken cancellationToken)
        {
            var levelCode = SrsSchedulingHelper.NormalizeSupportedJlptLevel(lesson.CertificateLevel?.Code);
            var cards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
            var vocabCards = cards
                .Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && c.VocabularyId.HasValue)
                .GroupBy(c => c.VocabularyId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
            var kanjiCards = cards
                .Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && c.KanjiId.HasValue)
                .GroupBy(c => c.KanjiId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
            var grammarCards = cards
                .Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && c.GrammarPatternId.HasValue)
                .GroupBy(c => c.GrammarPatternId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
            var templateCards = cards
                .Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived && c.SrsCardTemplateId.HasValue)
                .GroupBy(c => c.SrsCardTemplateId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var suggestions = new List<SrsSuggestedCardResponse>();

            foreach (var link in (lesson.LessonVocabularies ?? []).Where(l => l.Vocabulary is not null && !l.Vocabulary.IsDeleted && l.Vocabulary.Status == ContentStatus.Published).OrderBy(l => l.VocabularyId))
            {
                var v = link.Vocabulary;
                vocabCards.TryGetValue(v.Id, out var existing);
                suggestions.Add(new SrsSuggestedCardResponse
                {
                    SuggestedKey = $"vocabulary:{v.Id}",
                    CardType = "Vocabulary",
                    SourceType = "Lesson",
                    SourceId = lesson.Id,
                    LessonId = lesson.Id,
                    VocabularyId = v.Id,
                    JlptLevel = levelCode,
                    FrontText = v.Word,
                    BackText = v.Meaning,
                    Reading = v.Reading,
                    Meaning = v.Meaning,
                    ExampleSentence = v.ExampleSentence,
                    ExampleMeaning = v.ExampleSentenceMeaning,
                    AudioUrl = v.AudioUrl,
                    SourceLabel = lesson.Title,
                    AlreadyEnrolled = existing is not null,
                    ExistingReviewCardId = existing?.Id,
                    ExistingStatus = existing?.Status,
                });
            }

            foreach (var link in (lesson.LessonKanjis ?? []).Where(l => l.Kanji is not null && !l.Kanji.IsDeleted && l.Kanji.Status == ContentStatus.Published).OrderBy(l => l.KanjiId))
            {
                var k = link.Kanji;
                kanjiCards.TryGetValue(k.Id, out var existing);
                suggestions.Add(new SrsSuggestedCardResponse
                {
                    SuggestedKey = $"kanji:{k.Id}",
                    CardType = "Kanji",
                    SourceType = "Lesson",
                    SourceId = lesson.Id,
                    LessonId = lesson.Id,
                    KanjiId = k.Id,
                    JlptLevel = levelCode,
                    FrontText = k.Character,
                    BackText = k.Meaning,
                    Reading = k.OnYomi ?? k.KunYomi ?? k.SinoVietnamese,
                    Meaning = k.Meaning,
                    AudioUrl = k.AudioUrl,
                    SourceLabel = lesson.Title,
                    AlreadyEnrolled = existing is not null,
                    ExistingReviewCardId = existing?.Id,
                    ExistingStatus = existing?.Status,
                });
            }

            foreach (var link in (lesson.LessonGrammars ?? []).Where(l => l.GrammarPattern is not null && !l.GrammarPattern.IsDeleted && l.GrammarPattern.Status == ContentStatus.Published).OrderBy(l => l.GrammarPatternId))
            {
                var g = link.GrammarPattern;
                grammarCards.TryGetValue(g.Id, out var existing);
                var meaning = !string.IsNullOrWhiteSpace(g.UsageNotes) ? g.UsageNotes : g.Structure;
                suggestions.Add(new SrsSuggestedCardResponse
                {
                    SuggestedKey = $"grammar:{g.Id}",
                    CardType = "Grammar",
                    SourceType = "Lesson",
                    SourceId = lesson.Id,
                    LessonId = lesson.Id,
                    GrammarPatternId = g.Id,
                    JlptLevel = levelCode,
                    FrontText = g.Title,
                    BackText = meaning,
                    Reading = g.Structure,
                    Meaning = meaning,
                    ExampleSentence = g.ExampleSentence,
                    ExampleMeaning = g.ExampleSentenceMeaning,
                    SourceLabel = lesson.Title,
                    AlreadyEnrolled = existing is not null,
                    ExistingReviewCardId = existing?.Id,
                    ExistingStatus = existing?.Status,
                });
            }

            var publishedTemplates = await unitOfWork.SrsCardTemplates.Query()
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Status == ContentStatus.Published && t.LessonId == lesson.Id)
                .ToListAsync(cancellationToken);

            foreach (var t in publishedTemplates)
            {
                ReviewCard? existing = null;
                templateCards.TryGetValue(t.Id, out existing);
                if (existing is null && t.VocabularyId.HasValue)
                    vocabCards.TryGetValue(t.VocabularyId.Value, out existing);
                if (existing is null && t.KanjiId.HasValue)
                    kanjiCards.TryGetValue(t.KanjiId.Value, out existing);
                if (existing is null && t.GrammarPatternId.HasValue)
                    grammarCards.TryGetValue(t.GrammarPatternId.Value, out existing);

                suggestions.Add(new SrsSuggestedCardResponse
                {
                    SuggestedKey = $"template:{t.Id}",
                    CardType = t.CardType,
                    SourceType = "AuthorTemplate",
                    SourceId = t.Id,
                    LessonId = lesson.Id,
                    SrsCardTemplateId = t.Id,
                    VocabularyId = t.VocabularyId,
                    KanjiId = t.KanjiId,
                    GrammarPatternId = t.GrammarPatternId,
                    JlptLevel = t.JlptLevel ?? levelCode,
                    FrontText = t.FrontText,
                    BackText = t.BackText,
                    Reading = t.Reading,
                    Meaning = t.Meaning ?? t.BackText,
                    ExampleSentence = t.ExampleSentence,
                    ExampleMeaning = t.ExampleMeaning,
                    AudioUrl = t.AudioUrl,
                    SourceLabel = $"{lesson.Title} · Thẻ mẫu",
                    AlreadyEnrolled = existing is not null,
                    ExistingReviewCardId = existing?.Id,
                    ExistingStatus = existing?.Status,
                });
            }

            return suggestions;
        }

        private static LearningLessonSummaryResponse MapToSummary(Lesson lesson, bool hasPracticeExercise, bool practicePassed)
        {
            var progress = lesson.LessonProgresses?.SingleOrDefault();
            return new LearningLessonSummaryResponse
            {
                LessonId = lesson.Id,
                HasPracticeExercise = hasPracticeExercise,
                PracticePassed = practicePassed,
                Title = lesson.Title,
                Description = lesson.Description,
                HasAudio = !string.IsNullOrWhiteSpace(lesson.AudioUrl),
                SortOrder = lesson.SortOrder,
                LanguageSkillId = lesson.LanguageSkillId,
                LanguageSkillName = lesson.LanguageSkill.Name,
                ProgressPercent = progress?.ProgressPercent ?? 0,
                ResumePositionSeconds = progress?.LastPositionSeconds ?? 0,
                IsCompleted = progress?.IsCompleted ?? false,
                CompletedAt = progress?.ModifiedDate,
            };
        }

        private static LessonDetailResponse MapToResponse(
            Lesson lesson,
            LessonProgress? progress) => new()
        {
            LessonId = lesson.Id,
            CertificateLevelId = lesson.CertificateLevelId,
            LevelCode = lesson.CertificateLevel.Code,
            LanguageSkillId = lesson.LanguageSkillId,
            LanguageSkillName = lesson.LanguageSkill.Name,
            Title = lesson.Title,
            Content = lesson.Description ?? string.Empty,
            AudioUrl = lesson.AudioUrl,
            ImageUrl = lesson.ImageUrl,
            AttachmentUrl = null,
            OrderIndex = lesson.SortOrder,
            ProgressPercent = progress?.ProgressPercent ?? 0,
            ResumePositionSeconds = progress?.LastPositionSeconds ?? 0,
            IsCompleted = progress?.IsCompleted ?? false,
            CompletedAt = progress?.CompletedAt ?? progress?.ModifiedDate,
            FirstViewedAt = progress?.FirstViewedAt,
            LastInteractedAt = progress?.LastInteractedAt,
            InteractionCount = progress?.InteractionCount ?? 0,
            Vocabularies = (lesson.LessonVocabularies ?? new List<LessonVocabulary>())
                .Where(link => link.Vocabulary is not null && !link.Vocabulary.IsDeleted && link.Vocabulary.Status == ContentStatus.Published)
                .OrderBy(link => link.VocabularyId)
                .Select(link => new LessonVocabularyResponse
                {
                    VocabularyId = link.VocabularyId,
                    Word = link.Vocabulary.Word,
                    ImageUrl = link.Vocabulary.ImageUrl,
                    Reading = link.Vocabulary.Reading,
                    Meaning = link.Vocabulary.Meaning,
                    ExampleSentence = link.Vocabulary.ExampleSentence,
                    ExampleSentenceMeaning = link.Vocabulary.ExampleSentenceMeaning,
                    AudioUrl = link.Vocabulary.AudioUrl,
                })
                .ToList(),
            Kanjis = (lesson.LessonKanjis ?? new List<LessonKanji>())
                .Where(link => link.Kanji is not null && !link.Kanji.IsDeleted && link.Kanji.Status == ContentStatus.Published)
                .OrderBy(link => link.KanjiId)
                .Select(link => new LessonKanjiResponse
                {
                    KanjiId = link.KanjiId,
                    Character = link.Kanji.Character,
                    ImageUrl = link.Kanji.ImageUrl,
                    Meaning = link.Kanji.Meaning,
                    SinoVietnamese = link.Kanji.SinoVietnamese,
                    OnYomi = link.Kanji.OnYomi,
                    KunYomi = link.Kanji.KunYomi,
                    StrokeCount = link.Kanji.StrokeCount,
                    StrokeOrderImageUrl = link.Kanji.StrokeOrderImageUrl,
                    AudioUrl = link.Kanji.AudioUrl,
                })
                .ToList(),
            Grammars = (lesson.LessonGrammars ?? new List<LessonGrammar>())
                .Where(link => link.GrammarPattern is not null && !link.GrammarPattern.IsDeleted && link.GrammarPattern.Status == ContentStatus.Published)
                .OrderBy(link => link.GrammarPatternId)
                .Select(link => new LessonGrammarResponse
                {
                    GrammarPatternId = link.GrammarPatternId,
                    Title = link.GrammarPattern.Title,
                    Structure = link.GrammarPattern.Structure,
                    ImageUrl = link.GrammarPattern.ImageUrl,
                    UsageNotes = link.GrammarPattern.UsageNotes,
                    ExampleSentence = link.GrammarPattern.ExampleSentence,
                    ExampleSentenceMeaning = link.GrammarPattern.ExampleSentenceMeaning,
                })
                .ToList(),
        };
    }
}
