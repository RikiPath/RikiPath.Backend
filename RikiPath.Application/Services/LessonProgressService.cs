using RikiPath.Domain.Entities;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Lessons;
using System.Net;

namespace RikiPath.Application.Services
{
    public class LessonProgressService(IUnitOfWork unitOfWork, IClaimService claimService) : ILessonProgressService
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
                var lessonResponses = lessons.Select(MapToSummary).ToList();
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

                return ApiResponse<LessonDetailResponse>.Success(MapToResponse(lesson, progress));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
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

                var progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(
                    userId,
                    lessonId,
                    cancellationToken);
                var now = DateTime.UtcNow;
                var clampedPosition = Math.Min(request.PositionSeconds, request.DurationSeconds);
                var percent = (int)Math.Round(clampedPosition * 100.0 / request.DurationSeconds);
                percent = Math.Clamp(percent, 0, 100);

                if (progress is null)
                {
                    progress = new LessonProgress
                    {
                        UserId = userId,
                        LessonId = lessonId,
                        ProgressPercent = percent,
                        LastPositionSeconds = clampedPosition,
                        LastWatchedAt = now,
                    };
                    await unitOfWork.LessonProgresses.AddAsync(progress, cancellationToken);
                }
                else
                {
                    progress.IsDeleted = false;
                    progress.ProgressPercent = Math.Max(progress.ProgressPercent, percent);
                    progress.LastPositionSeconds = clampedPosition;
                    progress.LastWatchedAt = now;
                    unitOfWork.LessonProgresses.Update(progress);
                }

                if (!progress.IsCompleted && progress.ProgressPercent >= AutoCompleteThresholdPercent)
                {
                    progress.IsCompleted = true;
                    progress.ModifiedDate = now;
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<LessonDetailResponse>.Success(MapToResponse(lesson, progress));
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

                var progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(
                    userId,
                    lessonId,
                    cancellationToken);
                if (progress?.IsCompleted == true && !progress.IsDeleted)
                {
                    return ApiResponse<LessonDetailResponse>.Success(
                        MapToResponse(lesson, progress));
                }

                var now = DateTime.UtcNow;
                if (progress is null)
                {
                    progress = new LessonProgress
                    {
                        UserId = userId,
                        LessonId = lessonId,
                        ProgressPercent = 100,
                        IsCompleted = true,
                        LastWatchedAt = now,
                        ModifiedDate = now,
                    };
                    await unitOfWork.LessonProgresses.AddAsync(progress, cancellationToken);
                }
                else
                {
                    // Retry hoàn thành cùng một lesson không được tạo thêm progress trùng.
                    progress.IsDeleted = false;
                    progress.ProgressPercent = 100;
                    progress.IsCompleted = true;
                    progress.LastWatchedAt = now;
                    progress.ModifiedDate ??= now;
                    unitOfWork.LessonProgresses.Update(progress);
                }

                // Chỉ một progress row được ghi; thống kê discovery luôn tính lại từ dữ liệu này.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<LessonDetailResponse>.Success(MapToResponse(lesson, progress));
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

        private static LearningLessonSummaryResponse MapToSummary(Lesson lesson)
        {
            var progress = lesson.LessonProgresses?.SingleOrDefault();
            return new LearningLessonSummaryResponse
            {
                LessonId = lesson.Id,
                Title = lesson.Title,
                Description = lesson.Description,
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
            AttachmentUrl = null,
            OrderIndex = lesson.SortOrder,
            ProgressPercent = progress?.ProgressPercent ?? 0,
            ResumePositionSeconds = progress?.LastPositionSeconds ?? 0,
            IsCompleted = progress?.IsCompleted ?? false,
            CompletedAt = progress?.ModifiedDate,
            Vocabularies = (lesson.LessonVocabularies ?? new List<LessonVocabulary>())
                .Where(link => link.Vocabulary is not null && !link.Vocabulary.IsDeleted)
                .OrderBy(link => link.VocabularyId)
                .Select(link => new LessonVocabularyResponse
                {
                    VocabularyId = link.VocabularyId,
                    Word = link.Vocabulary.Word,
                    Reading = link.Vocabulary.Reading,
                    Meaning = link.Vocabulary.Meaning,
                    ExampleSentence = link.Vocabulary.ExampleSentence,
                    ExampleSentenceMeaning = link.Vocabulary.ExampleSentenceMeaning,
                    AudioUrl = link.Vocabulary.AudioUrl,
                })
                .ToList(),
            Kanjis = (lesson.LessonKanjis ?? new List<LessonKanji>())
                .Where(link => link.Kanji is not null && !link.Kanji.IsDeleted)
                .OrderBy(link => link.KanjiId)
                .Select(link => new LessonKanjiResponse
                {
                    KanjiId = link.KanjiId,
                    Character = link.Kanji.Character,
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
                .Where(link => link.GrammarPattern is not null && !link.GrammarPattern.IsDeleted)
                .OrderBy(link => link.GrammarPatternId)
                .Select(link => new LessonGrammarResponse
                {
                    GrammarPatternId = link.GrammarPatternId,
                    Title = link.GrammarPattern.Title,
                    Structure = link.GrammarPattern.Structure,
                    UsageNotes = link.GrammarPattern.UsageNotes,
                    ExampleSentence = link.GrammarPattern.ExampleSentence,
                    ExampleSentenceMeaning = link.GrammarPattern.ExampleSentenceMeaning,
                })
                .ToList(),
        };
    }
}
