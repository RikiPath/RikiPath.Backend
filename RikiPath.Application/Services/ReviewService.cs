using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Reviews;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Reviews;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    public class ReviewService(
        IUnitOfWork unitOfWork,
        AppSettings? appSettings = null,
        ISidebarCounterNotifier? sidebarNotifier = null) : IReviewService
    {
        private bool IsEmailProviderConfigured =>
            appSettings?.SmtpSettings is { } smtp
            && !string.IsNullOrWhiteSpace(smtp.Host)
            && smtp.Port is >= 1 and <= 65535
            && !string.IsNullOrWhiteSpace(smtp.FromEmail);
        public async Task<ApiResponse<SrsSummaryResponse>> GetSummaryAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                var pref = await GetOrBuildDefaultPreferenceAsync(userId, cancellationToken);
                var cards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);

                var activeCards = cards.Where(c => !c.IsDeleted && c.Status == SrsCardStatus.Active && c.Mode == ReviewMode.Recognition).ToList();
                var suspendedCards = cards.Count(c => !c.IsDeleted && c.Status == SrsCardStatus.Suspended);
                var archivedCards = cards.Count(c => c.IsDeleted || c.Status == SrsCardStatus.Archived);

                var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, pref.TimezoneId);
                var studyTodayDateOnly = DateOnly.FromDateTime(studyToday);
                var historyWindowStartUtc = studyToday.AddDays(-46);
                var recentHistories = await unitOfWork.ReviewHistories.Query()
                    .AsNoTracking()
                    .Where(h => h.ReviewCard.UserId == userId
                        && !h.IsDeleted
                        && h.ReviewedAt >= historyWindowStartUtc)
                    .Select(h => new
                    {
                        h.ReviewedAt,
                        h.IsCorrect,
                        h.Rating,
                        h.PreviousState,
                    })
                    .ToListAsync(cancellationToken);

                var histByDay = recentHistories
                    .GroupBy(h => DateOnly.FromDateTime(SrsSchedulingHelper.GetStudyDateInZone(h.ReviewedAt, pref.TimezoneId)))
                    .ToDictionary(g => g.Key, g => g.ToList());

                histByDay.TryGetValue(studyTodayDateOnly, out var todayHistories);
                todayHistories ??= [];

                var reviewedNewToday = todayHistories.Count(h => h.PreviousState == SrsLearningState.New);
                var reviewedTodayCount = todayHistories.Count;
                var correctTodayCount = todayHistories.Count(h =>
                    h.IsCorrect ?? (h.Rating != ReviewRating.Again));
                var accuracyPercent = reviewedTodayCount == 0
                    ? 0d
                    : Math.Round(correctTodayCount * 100.0 / reviewedTodayCount, 1);

                var rawDueNowCards = activeCards.Where(c => c.NextReviewDate <= utcNow).ToList();
                var actionableDueNowCards = SrsSchedulingHelper.SelectActionableDueCards(
                    rawDueNowCards,
                    pref.DailyNewLimit,
                    pref.DailyReviewLimit,
                    reviewedNewToday,
                    reviewedTodayCount);

                var dueTodayCount = actionableDueNowCards.Count;
                var rawDueNowCount = rawDueNowCards.Count;

                var futureCards = activeCards.Where(c => c.NextReviewDate > utcNow).ToList();
                var futureByDay = futureCards
                    .GroupBy(c => DateOnly.FromDateTime(SrsSchedulingHelper.GetStudyDateInZone(c.NextReviewDate, pref.TimezoneId)))
                    .ToDictionary(
                        g => g.Key,
                        g => (Count: g.Count(), EarliestUtc: (DateTime?)g.Min(x => x.NextReviewDate)));

                var dueLaterTodayInfo = futureByDay.GetValueOrDefault(studyTodayDateOnly);
                var dueLaterTodayCount = dueLaterTodayInfo.Count;

                var nextDueAt = futureCards
                    .OrderBy(c => c.NextReviewDate)
                    .Select(c => (DateTime?)c.NextReviewDate)
                    .FirstOrDefault();

                SrsCalendarDayResponse BuildDaySummary(DateOnly dayDate)
                {
                    histByDay.TryGetValue(dayDate, out var logs);
                    var revCount = logs?.Count ?? 0;
                    var remCount = logs?.Count(l => l.IsCorrect ?? (l.Rating != ReviewRating.Again)) ?? 0;

                    if (dayDate == studyTodayDateOnly)
                    {
                        var totalScheduledToday = dueTodayCount + dueLaterTodayCount;
                        var earliestToday = dueTodayCount > 0 ? utcNow : dueLaterTodayInfo.EarliestUtc;
                        var statusToday = dueTodayCount > 0
                            ? "DueNow"
                            : dueLaterTodayCount > 0
                                ? "DueLaterToday"
                                : revCount > 0
                                    ? "CompletedToday"
                                    : "NoCardsDueToday";

                        return new SrsCalendarDayResponse
                        {
                            Date = dayDate,
                            ReviewedCount = revCount,
                            RememberedCount = remCount,
                            DueNowCount = dueTodayCount,
                            DueLaterCount = dueLaterTodayCount,
                            DueScheduledCount = totalScheduledToday,
                            EarliestDueAtUtc = earliestToday,
                            NextDueAtUtc = nextDueAt,
                            Status = statusToday,
                        };
                    }

                    if (dayDate > studyTodayDateOnly)
                    {
                        var fut = futureByDay.GetValueOrDefault(dayDate);
                        return new SrsCalendarDayResponse
                        {
                            Date = dayDate,
                            ReviewedCount = revCount,
                            RememberedCount = remCount,
                            DueNowCount = 0,
                            DueLaterCount = fut.Count,
                            DueScheduledCount = fut.Count,
                            EarliestDueAtUtc = fut.EarliestUtc,
                            NextDueAtUtc = fut.EarliestUtc,
                            Status = fut.Count > 0 ? "ScheduledFuture" : "None",
                        };
                    }

                    return new SrsCalendarDayResponse
                    {
                        Date = dayDate,
                        ReviewedCount = revCount,
                        RememberedCount = remCount,
                        DueNowCount = 0,
                        DueLaterCount = 0,
                        DueScheduledCount = 0,
                        EarliestDueAtUtc = null,
                        NextDueAtUtc = null,
                        Status = revCount > 0 ? "ReviewedPast" : "None",
                    };
                }

                var forecast = Enumerable.Range(0, 7).Select(offset =>
                {
                    var dayDateOnly = studyTodayDateOnly.AddDays(offset);
                    var daySummary = BuildDaySummary(dayDateOnly);
                    return new SrsForecastDayResponse
                    {
                        Date = dayDateOnly,
                        Label = offset == 0 ? "Hôm nay" : dayDateOnly.ToString("dd/MM", CultureInfo.InvariantCulture),
                        DueCount = daySummary.DueScheduledCount,
                        DueNowCount = daySummary.DueNowCount,
                        DueLaterCount = daySummary.DueLaterCount,
                        ReviewedCount = daySummary.ReviewedCount,
                        EarliestDueAtUtc = daySummary.EarliestDueAtUtc,
                        Status = daySummary.Status,
                    };
                }).ToList();

                var candidateDates = new SortedSet<DateOnly> { studyTodayDateOnly };
                for (var offset = 0; offset <= 14; offset++)
                    candidateDates.Add(studyTodayDateOnly.AddDays(offset));
                foreach (var d in histByDay.Keys)
                    candidateDates.Add(d);
                foreach (var d in futureByDay.Keys)
                    candidateDates.Add(d);

                var calendarDays = candidateDates
                    .Select(BuildDaySummary)
                    .Where(d => d.Date == studyTodayDateOnly || d.DueScheduledCount > 0 || d.ReviewedCount > 0)
                    .ToList();

                var activeSession = await unitOfWork.SrsReviewSessions.GetActiveByUserIdAsync(userId, "Scheduled", cancellationToken);
                var activeSessionPendingCount = 0;
                if (activeSession is not null)
                {
                    var hydratedScheduled = await HydrateSessionAsync(activeSession, userId, cancellationToken);
                    activeSessionPendingCount = hydratedScheduled.PendingItems.Count;
                }

                var activePracticeSession = await unitOfWork.SrsReviewSessions.GetActiveByUserIdAsync(userId, "Practice", cancellationToken);
                var activePracticePendingCount = 0;
                if (activePracticeSession is not null)
                {
                    if (activePracticeSession.DeadlineAtUtc.HasValue && utcNow > activePracticeSession.DeadlineAtUtc.Value)
                    {
                        activePracticeSession = null;
                    }
                    else
                    {
                        var hydratedPractice = await HydrateSessionAsync(activePracticeSession, userId, cancellationToken);
                        activePracticePendingCount = hydratedPractice.PendingItems.Count;
                    }
                }

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                var streak = await ComputeEffectiveStreakAsync(user, pref.TimezoneId, utcNow, cancellationToken);

                return ApiResponse<SrsSummaryResponse>.Success(new SrsSummaryResponse
                {
                    TotalCards = activeCards.Count + suspendedCards,
                    ActiveCards = activeCards.Count,
                    SuspendedCards = suspendedCards,
                    ArchivedCards = archivedCards,
                    DueTodayCount = dueTodayCount,
                    TotalDueCards = dueTodayCount,
                    RawDueNowCount = rawDueNowCount,
                    DueLaterTodayCount = dueLaterTodayCount,
                    NewCount = activeCards.Count(c => c.LearningState == SrsLearningState.New),
                    LearningCount = activeCards.Count(c => c.LearningState == SrsLearningState.Learning),
                    RelearningCount = activeCards.Count(c => c.LearningState == SrsLearningState.Relearning),
                    ReviewCount = activeCards.Count(c => c.LearningState == SrsLearningState.Review),
                    MasteredCount = activeCards.Count(c => c.LearningState == SrsLearningState.Mastered),
                    ReviewedTodayCount = reviewedTodayCount,
                    CorrectTodayCount = correctTodayCount,
                    TodayAccuracyPercent = accuracyPercent,
                    CurrentStreakDays = streak.Current,
                    LongestStreakDays = streak.Longest,
                    NextDueAtUtc = nextDueAt,
                    StudyTodayDate = studyTodayDateOnly,
                    ServerUtcNow = utcNow,
                    TimezoneId = string.IsNullOrWhiteSpace(pref.TimezoneId) ? "Asia/Ho_Chi_Minh" : pref.TimezoneId,
                    PreferredReviewMode = NormalizeStudyMode(pref.PreferredReviewMode),
                    ReminderHour = Math.Clamp(pref.ReminderHour, 0, 23),
                    ReminderMinute = Math.Clamp(pref.ReminderMinute, 0, 59),
                    DailyReminderEnabled = pref.DailyReminderEnabled,
                    HasActiveSession = activeSession is not null && activeSessionPendingCount > 0,
                    ActiveSessionKey = activeSessionPendingCount > 0 ? activeSession?.SessionKey : null,
                    ActiveSessionStudyMode = activeSessionPendingCount > 0 ? activeSession?.StudyMode : null,
                    ActiveSessionPendingCount = activeSessionPendingCount,
                    HasActivePracticeSession = activePracticeSession is not null && activePracticePendingCount > 0,
                    ActivePracticeSessionKey = activePracticePendingCount > 0 ? activePracticeSession?.SessionKey : null,
                    ActivePracticeSessionStudyMode = activePracticePendingCount > 0 ? activePracticeSession?.StudyMode : null,
                    ActivePracticeSessionPendingCount = activePracticePendingCount,
                    DailyNewLimit = pref.DailyNewLimit,
                    DailyReviewLimit = pref.DailyReviewLimit,
                    ForecastNext7Days = forecast,
                    CalendarDays = calendarDays,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<SrsSummaryResponse>.Fail(
                    "Không thể tải thống kê SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<DailyReviewQueueResponse>> GetDailyReviewQueueAsync(
            int userId,
            string mode,
            string? cardType,
            string? jlptLevel,
            string? sourceType,
            CancellationToken cancellationToken)
        {
            try
            {
                var normalizedMode = NormalizeStudyMode(mode);
                string? normalizedLevel = null;
                if (!string.IsNullOrWhiteSpace(jlptLevel))
                {
                    normalizedLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(jlptLevel);
                    if (normalizedLevel is null)
                    {
                        return ApiResponse<DailyReviewQueueResponse>.Fail(
                            "Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.",
                            HttpStatusCode.BadRequest);
                    }
                }

                var utcNow = DateTime.UtcNow;
                var pref = await GetOrBuildDefaultPreferenceAsync(userId, cancellationToken);
                var dueCards = await unitOfWork.ReviewCards.GetDueQueueAsync(
                    userId,
                    utcNow,
                    cardType,
                    normalizedLevel,
                    sourceType,
                    cancellationToken);

                var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, pref.TimezoneId);
                var utcWindowStart = studyToday.AddDays(-1);
                var todayLogs = await unitOfWork.ReviewHistories.Query()
                    .AsNoTracking()
                    .Where(h => h.ReviewCard.UserId == userId
                        && !h.IsDeleted
                        && h.ReviewedAt >= utcWindowStart)
                    .Select(h => new { h.ReviewedAt, h.PreviousState })
                    .ToListAsync(cancellationToken);

                var todayInZone = todayLogs
                    .Where(h => SrsSchedulingHelper.GetStudyDateInZone(h.ReviewedAt, pref.TimezoneId) == studyToday)
                    .ToList();
                var reviewedNewToday = todayInZone.Count(h => h.PreviousState == SrsLearningState.New);
                var reviewedTotalToday = todayInZone.Count;

                var selectedCards = SrsSchedulingHelper.SelectActionableDueCards(
                    dueCards,
                    pref.DailyNewLimit,
                    pref.DailyReviewLimit,
                    reviewedNewToday,
                    reviewedTotalToday);

                List<ReviewCard> allUserCards = [];
                List<SrsCardTemplate> fallbackTemplates = [];
                List<Vocabulary> fallbackVocab = [];
                List<Kanji> fallbackKanji = [];
                List<GrammarPattern> fallbackGrammar = [];
                if (normalizedMode is "mcq" or "mixed" && selectedCards.Count > 0)
                {
                    allUserCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
                    fallbackTemplates = await unitOfWork.SrsCardTemplates.Query()
                        .AsNoTracking()
                        .Where(t => !t.IsDeleted && t.Status == ContentStatus.Published)
                        .OrderBy(t => t.Id)
                        .Take(60)
                        .ToListAsync(cancellationToken);
                    fallbackVocab = await unitOfWork.Vocabularies.Query()
                        .AsNoTracking()
                        .Where(v => !v.IsDeleted && v.Status == ContentStatus.Published)
                        .OrderBy(v => v.Id)
                        .Take(60)
                        .ToListAsync(cancellationToken);
                    fallbackKanji = await unitOfWork.Kanjis.Query()
                        .AsNoTracking()
                        .Where(k => !k.IsDeleted && k.Status == ContentStatus.Published)
                        .OrderBy(k => k.Id)
                        .Take(40)
                        .ToListAsync(cancellationToken);
                    fallbackGrammar = await unitOfWork.GrammarPatterns.Query()
                        .AsNoTracking()
                        .Where(g => !g.IsDeleted && g.Status == ContentStatus.Published)
                        .OrderBy(g => g.Id)
                        .Take(40)
                        .ToListAsync(cancellationToken);
                }

                var items = new List<ReviewQueueItemResponse>();
                var excludedMcqCards = new List<ExcludedMcqCardReasonResponse>();
                var excludedTypingCards = new List<ExcludedMcqCardReasonResponse>();
                var dailySeed = HashCode.Combine(userId, studyToday.Year, studyToday.DayOfYear);

                foreach (var card in selectedCards)
                {
                    var dto = MapToQueueItem(card, utcNow, includePreviews: true);
                    var eligibleModes = new List<string>();
                    var isFlipEligible = !string.IsNullOrWhiteSpace(dto.Term)
                        && (!string.IsNullOrWhiteSpace(dto.Meaning) || !string.IsNullOrWhiteSpace(dto.BackText));
                    if (isFlipEligible)
                        eligibleModes.Add("flip");

                    var typingElig = SrsSchedulingHelper.EvaluateTypingEligibility(card, dto.Term, dto.Reading, dto.Meaning, dto.BackText);
                    dto.TypingPromptKind = typingElig.PromptKind;
                    if (typingElig.IsEligible)
                        eligibleModes.Add("typing");

                    if (normalizedMode is "mcq" or "mixed")
                    {
                        var options = BuildMcqOptions(
                            dto,
                            allUserCards,
                            fallbackTemplates,
                            fallbackVocab,
                            fallbackKanji,
                            fallbackGrammar,
                            dailySeed);
                        if (options.Count >= 4)
                        {
                            dto.McqOptions = options;
                            eligibleModes.Add("mcq");
                        }
                    }

                    dto.EligibleModes = eligibleModes;

                    if (normalizedMode == "flip")
                    {
                        if (!isFlipEligible) continue;
                        dto.AssignedStudyMode = "flip";
                    }
                    else if (normalizedMode == "typing")
                    {
                        if (!typingElig.IsEligible)
                        {
                            excludedTypingCards.Add(new ExcludedMcqCardReasonResponse
                            {
                                ReviewCardId = dto.ReviewCardId,
                                Term = dto.Term,
                                Reason = "Thẻ chưa có dữ liệu cách đọc hoặc nghĩa hợp lệ để chấm tự động.",
                            });
                            continue;
                        }
                        dto.AssignedStudyMode = "typing";
                    }
                    else if (normalizedMode == "mcq")
                    {
                        if (dto.McqOptions.Count < 4)
                        {
                            excludedMcqCards.Add(new ExcludedMcqCardReasonResponse
                            {
                                ReviewCardId = dto.ReviewCardId,
                                Term = dto.Term,
                                Reason = "Chưa đủ phương án nhiễu hợp lệ từ học liệu đã xuất bản.",
                            });
                            continue;
                        }
                        dto.AssignedStudyMode = "mcq";
                    }
                    else // mixed
                    {
                        if (eligibleModes.Count == 0) continue;
                    }

                    items.Add(dto);
                }

                if (normalizedMode == "mixed" && items.Count > 0)
                {
                    AssignMixedModesDeterministically(items);
                }

                foreach (var dto in items)
                {
                    RedactAnswerKeyForSessionStep(dto);
                }

                var activeModesInQueue = items
                    .Select(x => x.AssignedStudyMode)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                DateTime? nextDueAt = null;
                string? emptyReason = null;
                if (items.Count == 0)
                {
                    var totalAll = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
                    var active = totalAll.Where(c => !c.IsDeleted && c.Status == SrsCardStatus.Active && c.Mode == ReviewMode.Recognition).ToList();
                    nextDueAt = active
                        .Where(c => c.NextReviewDate > utcNow)
                        .OrderBy(c => c.NextReviewDate)
                        .Select(c => (DateTime?)c.NextReviewDate)
                        .FirstOrDefault();

                    if (active.Count == 0)
                    {
                        emptyReason = "NoCardsYet";
                    }
                    else if (normalizedMode == "mcq" && excludedMcqCards.Count > 0)
                    {
                        emptyReason = "InsufficientMcqDistractors";
                    }
                    else if (normalizedMode == "typing" && excludedTypingCards.Count > 0)
                    {
                        emptyReason = "InsufficientTypingData";
                    }
                    else if (dueCards.Count > 0 && selectedCards.Count == 0)
                    {
                        emptyReason = "DailyLimitReached";
                    }
                    else
                    {
                        var anyDueUnfiltered = active.Any(c => c.NextReviewDate <= utcNow);
                        emptyReason = anyDueUnfiltered
                            ? "FiltersEmpty"
                            : (reviewedTotalToday > 0 ? "CompletedToday" : "NoCardsDueNow");
                    }
                }

                return ApiResponse<DailyReviewQueueResponse>.Success(new DailyReviewQueueResponse
                {
                    TotalDue = selectedCards.Count,
                    TotalDueCards = selectedCards.Count,
                    ModeEligibleCount = items.Count,
                    PlannedSessionCount = Math.Min(items.Count, 25),
                    RawDueCount = dueCards.Count,
                    NewInQueue = items.Count(x => x.LearningState == SrsLearningState.New),
                    RelearningInQueue = items.Count(x => x.LearningState == SrsLearningState.Relearning),
                    ReviewInQueue = items.Count(x => x.LearningState is SrsLearningState.Learning or SrsLearningState.Review or SrsLearningState.Mastered),
                    StudyMode = normalizedMode,
                    NextDueAtUtc = nextDueAt,
                    EmptyReason = emptyReason,
                    ActiveModesInQueue = activeModesInQueue,
                    Items = items,
                    ExcludedMcqCards = excludedMcqCards,
                    ExcludedTypingCards = excludedTypingCards,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<DailyReviewQueueResponse>.Fail(
                    "Không thể tải danh sách thẻ ôn tập hôm nay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<SrsCardLibraryResponse>> GetCardLibraryAsync(
            int userId,
            SrsCardStatus? status,
            SrsLearningState? learningState,
            string? cardType,
            string? jlptLevel,
            string? sourceType,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            try
            {
                string? normalizedLevel = null;
                if (!string.IsNullOrWhiteSpace(jlptLevel))
                {
                    normalizedLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(jlptLevel);
                    if (normalizedLevel is null)
                    {
                        return ApiResponse<SrsCardLibraryResponse>.Fail(
                            "Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.",
                            HttpStatusCode.BadRequest);
                    }
                }

                var cards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
                var utcNow = DateTime.UtcNow;

                IEnumerable<ReviewCard> filtered = cards;
                if (status.HasValue)
                {
                    filtered = status.Value == SrsCardStatus.Archived
                        ? filtered.Where(c => c.IsDeleted || c.Status == SrsCardStatus.Archived)
                        : filtered.Where(c => !c.IsDeleted && c.Status == status.Value);
                }
                else
                {
                    filtered = filtered.Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived);
                }

                if (learningState.HasValue)
                    filtered = filtered.Where(c => c.LearningState == learningState.Value);

                if (!string.IsNullOrWhiteSpace(cardType))
                {
                    var ct = cardType.Trim();
                    filtered = filtered.Where(c => string.Equals(c.CardType, ct, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(normalizedLevel))
                {
                    filtered = filtered.Where(c => string.Equals(c.JlptLevel, normalizedLevel, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(sourceType))
                {
                    var st = sourceType.Trim();
                    filtered = filtered.Where(c => string.Equals(c.SourceType, st, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var q = search.Trim();
                    filtered = filtered.Where(c =>
                    {
                        var dto = MapToQueueItem(c, utcNow, includePreviews: false);
                        return dto.Term.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || dto.Meaning.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || (!string.IsNullOrWhiteSpace(dto.Reading) && dto.Reading.Contains(q, StringComparison.OrdinalIgnoreCase))
                            || (!string.IsNullOrWhiteSpace(dto.Note) && dto.Note.Contains(q, StringComparison.OrdinalIgnoreCase));
                    });
                }

                var list = filtered.ToList();
                var safePage = Math.Max(1, page);
                var safeSize = Math.Clamp(pageSize, 1, 100);
                var paged = list
                    .Skip((safePage - 1) * safeSize)
                    .Take(safeSize)
                    .Select(c => MapToQueueItem(c, utcNow, includePreviews: true))
                    .ToList();

                return ApiResponse<SrsCardLibraryResponse>.Success(new SrsCardLibraryResponse
                {
                    TotalCount = list.Count,
                    Items = paged,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<SrsCardLibraryResponse>.Fail(
                    "Không thể tải thư viện thẻ SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<ReviewQueueItemResponse>> GetCardByIdAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken)
        {
            var card = await unitOfWork.ReviewCards.GetByIdDetailedAsync(cardId, userId, cancellationToken);
            if (card is null)
            {
                var other = await unitOfWork.ReviewCards.GetByIdAsync(cardId, cancellationToken);
                if (other is not null && other.UserId != userId)
                    return ApiResponse<ReviewQueueItemResponse>.Fail("Không có quyền truy cập thẻ SRS này.", HttpStatusCode.Forbidden);

                return ApiResponse<ReviewQueueItemResponse>.NotFound("Không tìm thấy thẻ SRS.");
            }

            return ApiResponse<ReviewQueueItemResponse>.Success(
                MapToQueueItem(card, DateTime.UtcNow, includePreviews: true));
        }

        public async Task<ApiResponse<List<SrsReviewHistoryItemResponse>>> GetCardHistoryAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken)
        {
            var card = await unitOfWork.ReviewCards.GetByIdAsync(cardId, cancellationToken);
            if (card is null)
                return ApiResponse<List<SrsReviewHistoryItemResponse>>.NotFound("Không tìm thấy thẻ SRS.");
            if (card.UserId != userId)
                return ApiResponse<List<SrsReviewHistoryItemResponse>>.Fail("Không có quyền xem lịch sử thẻ SRS này.", HttpStatusCode.Forbidden);

            var histories = await unitOfWork.ReviewHistories.GetByReviewCardIdAsync(cardId);
            return ApiResponse<List<SrsReviewHistoryItemResponse>>.Success(
                histories.Select(h => new SrsReviewHistoryItemResponse
                {
                    Id = h.Id,
                    ReviewCardId = h.ReviewCardId,
                    Rating = h.Rating,
                    Quality = h.Quality,
                    StudyMode = h.StudyMode,
                    UserAnswer = h.UserAnswer,
                    IsCorrect = h.IsCorrect,
                    ResponseTimeMs = h.ResponseTimeMs,
                    PreviousIntervalDays = h.PreviousIntervalDays,
                    NewIntervalDays = h.NewIntervalDays,
                    PreviousEaseFactor = h.PreviousEaseFactor,
                    NewEaseFactor = h.NewEaseFactor,
                    PreviousState = h.PreviousState,
                    NewState = h.NewState,
                    ReviewedAt = h.ReviewedAt,
                }).ToList());
        }

        public async Task<ApiResponse<ReviewQueueItemResponse>> PreviewEnrollAsync(
            int userId,
            EnrollSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            var resolved = await ResolveCandidateAsync(userId, request, cancellationToken);
            if (!resolved.IsSuccess)
                return ApiResponse<ReviewQueueItemResponse>.Fail(
                    resolved.ErrorMessage!,
                    (HttpStatusCode)resolved.StatusCode);

            var data = resolved.Result!;
            var existing = await unitOfWork.ReviewCards.FindExistingCardAsync(
                userId,
                data.VocabularyId,
                data.KanjiId,
                data.GrammarPatternId,
                data.LearnerNoteEntryId,
                data.SrsCardTemplateId,
                data.NormalizedKey,
                ReviewMode.Recognition,
                cancellationToken);

            if (existing is not null)
                return ApiResponse<ReviewQueueItemResponse>.Success(MapToQueueItem(existing, DateTime.UtcNow, true));

            var previewCard = new ReviewCard
            {
                UserId = userId,
                VocabularyId = data.VocabularyId,
                KanjiId = data.KanjiId,
                GrammarPatternId = data.GrammarPatternId,
                LearnerNoteEntryId = data.LearnerNoteEntryId,
                SrsCardTemplateId = data.SrsCardTemplateId,
                LessonId = data.LessonId,
                CardType = data.CardType,
                SourceType = data.SourceType,
                SourceId = data.SourceId,
                SourceVersion = data.SourceVersion,
                SourceLabel = data.SourceLabel,
                NormalizedContentKey = data.NormalizedKey,
                FrontText = data.FrontText,
                BackText = data.BackText,
                Reading = data.Reading,
                Meaning = data.Meaning,
                ExampleSentence = data.ExampleSentence,
                ExampleMeaning = data.ExampleMeaning,
                ContextSnippet = data.ContextSnippet,
                JlptLevel = data.JlptLevel,
                AudioUrl = data.AudioUrl,
                PersonalNote = data.PersonalNote,
                AcceptedReadingsJson = SrsSchedulingHelper.SerializeStringList(data.AcceptedReadings),
                SynonymsJson = SrsSchedulingHelper.SerializeStringList(data.Synonyms),
                Status = SrsCardStatus.Active,
                LearningState = SrsLearningState.New,
                EaseFactor = SrsSchedulingHelper.DefaultEaseFactor,
                NextReviewDate = DateTime.UtcNow.Date,
            };

            return ApiResponse<ReviewQueueItemResponse>.Success(MapToQueueItem(previewCard, DateTime.UtcNow, true));
        }

        public async Task<ApiResponse<SrsEnrollOutcomeResponse>> EnrollCardAsync(
            int userId,
            EnrollSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var resolved = await ResolveCandidateAsync(userId, request, cancellationToken);
                if (!resolved.IsSuccess)
                    return ApiResponse<SrsEnrollOutcomeResponse>.Fail(
                        resolved.ErrorMessage!,
                        (HttpStatusCode)resolved.StatusCode);

                SrsEnrollOutcomeResponse? outcome = null;
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    outcome = await EnrollResolvedCandidateAsync(userId, resolved.Result!, request.ReactivateIfArchived, token);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                if (sidebarNotifier is not null && outcome is not null && !string.Equals(outcome.Action, "Skipped", StringComparison.OrdinalIgnoreCase))
                    await sidebarNotifier.NotifyUsersAsync([userId], "srs_card_enrolled", cancellationToken);

                return ApiResponse<SrsEnrollOutcomeResponse>.Success(outcome!);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<SrsEnrollOutcomeResponse>.Fail(
                    "Không thể thêm thẻ vào SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<SrsBatchEnrollResponse>> BatchEnrollCardsAsync(
            int userId,
            BatchEnrollSrsCardsRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Cards.Count == 0)
                return ApiResponse<SrsBatchEnrollResponse>.Fail("Danh sách thẻ không được trống.");
            if (request.Cards.Count > 100)
                return ApiResponse<SrsBatchEnrollResponse>.Fail("Tối đa 100 thẻ trong một lần thêm.");

            try
            {
                var candidates = new List<ResolvedCardCandidate>(request.Cards.Count);
                foreach (var item in request.Cards)
                {
                    var resolved = await ResolveCandidateAsync(userId, item, cancellationToken);
                    if (!resolved.IsSuccess)
                        return ApiResponse<SrsBatchEnrollResponse>.Fail(
                            resolved.ErrorMessage!,
                            (HttpStatusCode)resolved.StatusCode);
                    candidates.Add(resolved.Result!);
                }

                var result = new SrsBatchEnrollResponse();
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    foreach (var candidate in candidates)
                    {
                        var outcome = await EnrollResolvedCandidateAsync(userId, candidate, reactivateIfArchived: true, token);
                        switch (outcome.Action)
                        {
                            case "Created": result.CreatedCount++; break;
                            case "LinkedSource": result.LinkedCount++; break;
                            case "Reactivated": result.ReactivatedCount++; break;
                            default: result.SkippedCount++; break;
                        }
                        result.Cards.Add(outcome.Card);
                    }
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                if (sidebarNotifier is not null && (result.CreatedCount > 0 || result.ReactivatedCount > 0 || result.LinkedCount > 0))
                    await sidebarNotifier.NotifyUsersAsync([userId], "srs_cards_batch_enrolled", cancellationToken);

                return ApiResponse<SrsBatchEnrollResponse>.Success(result);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<SrsBatchEnrollResponse>.Fail(
                    "Không thể thêm hàng loạt thẻ vào SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<ReviewQueueItemResponse>> UpdateCardAsync(
            int userId,
            int cardId,
            UpdateSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            var card = await unitOfWork.ReviewCards.GetByIdDetailedAsync(cardId, userId, cancellationToken);
            if (card is null)
            {
                var other = await unitOfWork.ReviewCards.GetByIdAsync(cardId, cancellationToken);
                if (other is not null && other.UserId != userId)
                    return ApiResponse<ReviewQueueItemResponse>.Fail("Không có quyền sửa thẻ này.", HttpStatusCode.Forbidden);
                return ApiResponse<ReviewQueueItemResponse>.NotFound("Không tìm thấy thẻ SRS.");
            }

            if (card.IsDeleted || card.Status == SrsCardStatus.Archived)
                return ApiResponse<ReviewQueueItemResponse>.Fail("Hãy khôi phục thẻ trước khi chỉnh sửa.", HttpStatusCode.Conflict);

            if (!string.IsNullOrWhiteSpace(request.CardType))
            {
                var normalizedCardType = NormalizeLearnerEditableCardType(request.CardType);
                if (normalizedCardType is null)
                    return ApiResponse<ReviewQueueItemResponse>.Fail("Loại thẻ chỉ hỗ trợ Từ vựng (Vocabulary) hoặc Kanji.", HttpStatusCode.BadRequest);
                card.CardType = normalizedCardType;
            }

            if (!string.IsNullOrWhiteSpace(request.JlptLevel))
            {
                var level = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel);
                if (level is null)
                    return ApiResponse<ReviewQueueItemResponse>.Fail("Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.", HttpStatusCode.BadRequest);
                card.JlptLevel = level;
            }

            if (!string.IsNullOrWhiteSpace(request.FrontText)) card.FrontText = request.FrontText.Trim();
            if (!string.IsNullOrWhiteSpace(request.BackText)) card.BackText = request.BackText.Trim();
            if (request.Reading is not null) card.Reading = NormalizeOptional(request.Reading);
            if (request.Meaning is not null) card.Meaning = NormalizeOptional(request.Meaning);
            if (request.ExampleSentence is not null) card.ExampleSentence = NormalizeOptional(request.ExampleSentence);
            if (request.ExampleMeaning is not null) card.ExampleMeaning = NormalizeOptional(request.ExampleMeaning);
            if (request.ContextSnippet is not null) card.ContextSnippet = NormalizeOptional(request.ContextSnippet);
            if (request.AudioUrl is not null) card.AudioUrl = NormalizeOptional(request.AudioUrl);
            if (request.PersonalNote is not null) card.PersonalNote = NormalizeOptional(request.PersonalNote);
            if (request.AcceptedReadings is not null)
                card.AcceptedReadingsJson = SrsSchedulingHelper.SerializeStringList(request.AcceptedReadings);
            if (request.Synonyms is not null)
                card.SynonymsJson = SrsSchedulingHelper.SerializeStringList(request.Synonyms);

            card.NormalizedContentKey = SrsSchedulingHelper.BuildNormalizedContentKey(
                card.CardType,
                card.VocabularyId,
                card.KanjiId,
                card.GrammarPatternId,
                card.LearnerNoteEntryId,
                card.SrsCardTemplateId,
                card.FrontText,
                card.Meaning ?? card.BackText);

            card.Revision++;
            card.ModifiedDate = DateTime.UtcNow;
            unitOfWork.ReviewCards.Update(card);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<ReviewQueueItemResponse>.Success(
                MapToQueueItem(card, DateTime.UtcNow, includePreviews: true));
        }

        public async Task<ApiResponse<ReviewQueueItemResponse>> SuspendCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken)
            => await ChangeCardLifecycleAsync(userId, cardId, SrsCardStatus.Suspended, cancellationToken);

        public async Task<ApiResponse<ReviewQueueItemResponse>> ResumeCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken)
            => await ChangeCardLifecycleAsync(userId, cardId, SrsCardStatus.Active, cancellationToken);

        public async Task<ApiResponse<ReviewQueueItemResponse>> RestoreCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken)
            => await ChangeCardLifecycleAsync(userId, cardId, SrsCardStatus.Active, cancellationToken);

        public async Task<ApiResponse<bool>> SoftDeleteCardAsync(
            int userId,
            int cardId,
            CancellationToken cancellationToken)
        {
            var result = await ChangeCardLifecycleAsync(userId, cardId, SrsCardStatus.Archived, cancellationToken);
            if (!result.IsSuccess)
                return ApiResponse<bool>.Fail(result.ErrorMessage!, (HttpStatusCode)result.StatusCode);
            return ApiResponse<bool>.Success(true);
        }

        private async Task<ApiResponse<ReviewQueueItemResponse>> ChangeCardLifecycleAsync(
            int userId,
            int cardId,
            SrsCardStatus targetStatus,
            CancellationToken cancellationToken)
        {
            var card = await unitOfWork.ReviewCards.GetByIdDetailedAsync(cardId, userId, cancellationToken);
            if (card is null)
            {
                var other = await unitOfWork.ReviewCards.GetByIdAsync(cardId, cancellationToken);
                if (other is not null && other.UserId != userId)
                    return ApiResponse<ReviewQueueItemResponse>.Fail("Không có quyền thao tác trên thẻ SRS này.", HttpStatusCode.Forbidden);
                return ApiResponse<ReviewQueueItemResponse>.NotFound("Không tìm thấy thẻ SRS.");
            }

            var utcNow = DateTime.UtcNow;
            card.Status = targetStatus;
            card.ModifiedDate = utcNow;
            card.Revision++;

            if (targetStatus == SrsCardStatus.Suspended)
            {
                card.IsDeleted = false;
                card.SuspendedAt = utcNow;
            }
            else if (targetStatus == SrsCardStatus.Archived)
            {
                card.IsDeleted = true;
                card.ArchivedAt = utcNow;
            }
            else // Active
            {
                card.IsDeleted = false;
                card.SuspendedAt = null;
                card.ArchivedAt = null;
                if (card.NextReviewDate < utcNow.Date.AddDays(-1))
                    card.NextReviewDate = utcNow.Date;
            }

            unitOfWork.ReviewCards.Update(card);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (sidebarNotifier is not null)
                await sidebarNotifier.NotifyUsersAsync([userId], "srs_card_lifecycle_changed", cancellationToken);

            return ApiResponse<ReviewQueueItemResponse>.Success(
                MapToQueueItem(card, utcNow, includePreviews: true));
        }

        public async Task<ApiResponse<UserSrsPreferenceResponse>> GetPreferencesAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var pref = await GetOrBuildDefaultPreferenceAsync(userId, cancellationToken, persistIfMissing: true);
            var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
            return ApiResponse<UserSrsPreferenceResponse>.Success(
                MapPreference(pref, IsEmailProviderConfigured, user?.IsEmailVerified ?? true));
        }

        public async Task<ApiResponse<UserSrsPreferenceResponse>> UpdatePreferencesAsync(
            int userId,
            UpdateUserSrsPreferenceRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Dữ liệu cài đặt không hợp lệ.", HttpStatusCode.BadRequest);

            if (request.DailyNewLimit is < 0 or > 200)
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Thẻ mới mỗi ngày phải từ 0 đến 200.", HttpStatusCode.BadRequest);

            if (request.DailyReviewLimit is < 5 or > 500)
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Mục tiêu ôn mỗi ngày phải từ 5 đến 500.", HttpStatusCode.BadRequest);

            if (request.ReminderHour is < 0 or > 23 || request.ReminderMinute is < 0 or > 59)
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Giờ nhắc không hợp lệ (00:00 – 23:59).", HttpStatusCode.BadRequest);

            var rawMode = (request.PreferredReviewMode ?? "flip").Trim().ToLowerInvariant();
            if (rawMode is not ("flip" or "typing" or "mcq" or "mixed"))
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Chế độ ôn mặc định không hợp lệ.", HttpStatusCode.BadRequest);

            var rawFreq = string.IsNullOrWhiteSpace(request.ReminderFrequency)
                ? "Daily"
                : request.ReminderFrequency.Trim();
            string freq;
            if (string.Equals(rawFreq, "Daily", StringComparison.OrdinalIgnoreCase)) freq = "Daily";
            else if (string.Equals(rawFreq, "Weekdays", StringComparison.OrdinalIgnoreCase)) freq = "Weekdays";
            else if (string.Equals(rawFreq, "CustomDays", StringComparison.OrdinalIgnoreCase)) freq = "CustomDays";
            else
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Tần suất nhắc học không hợp lệ.", HttpStatusCode.BadRequest);

            if (request.ReminderDays is not null && request.ReminderDays.Any(d => d is < 0 or > 6))
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Danh sách ngày nhắc học không hợp lệ (chỉ chấp nhận giá trị 0–6).", HttpStatusCode.BadRequest);

            var validDays = request.ReminderDays?.Distinct().OrderBy(d => d).ToList() ?? [];
            if (request.DailyReminderEnabled
                && string.Equals(freq, "CustomDays", StringComparison.OrdinalIgnoreCase)
                && validDays.Count == 0)
            {
                return ApiResponse<UserSrsPreferenceResponse>.Fail(
                    "Vui lòng chọn ít nhất 1 ngày nhận nhắc học hoặc đổi tần suất.",
                    HttpStatusCode.BadRequest);
            }

            if (!SrsSchedulingHelper.TryResolveTimeZone(request.TimezoneId, out var tz))
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Múi giờ không hợp lệ.", HttpStatusCode.BadRequest);

            var rawLocale = string.IsNullOrWhiteSpace(request.PreferredLocale) ? "vi" : request.PreferredLocale.Trim();
            string locale;
            if (string.Equals(rawLocale, "vi", StringComparison.OrdinalIgnoreCase)) locale = "vi";
            else if (string.Equals(rawLocale, "ja", StringComparison.OrdinalIgnoreCase)) locale = "ja";
            else
                return ApiResponse<UserSrsPreferenceResponse>.Fail("Ngôn ngữ thông báo chỉ hỗ trợ tiếng Việt (vi) hoặc tiếng Nhật (ja).", HttpStatusCode.BadRequest);

            try
            {
                var utcNow = DateTime.UtcNow;
                var daysJson = validDays.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(validDays)
                    : null;

                UserSrsPreference? saved = null;
                UserAccount? userAccount = null;
                ApiResponse<UserSrsPreferenceResponse>? conflict = null;
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    userAccount = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    var existing = await unitOfWork.UserSrsPreferences.GetByUserIdAsync(userId, token);
                    if (existing is null)
                    {
                        existing = new UserSrsPreference
                        {
                            UserId = userId,
                            CreatedDate = utcNow,
                        };
                        await unitOfWork.UserSrsPreferences.AddAsync(existing, token);
                    }
                    else
                    {
                        if (request.ExpectedUpdatedAtUtc.HasValue
                            && (existing.ModifiedDate ?? existing.CreatedDate) is { } currentVersion
                            && currentVersion != default
                            && Math.Abs((currentVersion - request.ExpectedUpdatedAtUtc.Value).TotalMilliseconds) > 5)
                        {
                            conflict = ApiResponse<UserSrsPreferenceResponse>.Fail(
                                "Cài đặt vừa được cập nhật từ tab hoặc thiết bị khác. Vui lòng tải lại bản mới nhất trước khi lưu.",
                                HttpStatusCode.Conflict);
                            return;
                        }
                        unitOfWork.UserSrsPreferences.Update(existing);
                    }

                    // Strict one-per-local-day reminder policy: do NOT clear LastReminderSentLocalDate when learner edits reminder time/locale/frequency;
                    // updated configuration applies only to upcoming eligible schedules.

                    existing.AutoEnrollAfterLesson = request.AutoEnrollAfterLesson;
                    existing.DailyNewLimit = request.DailyNewLimit;
                    existing.DailyReviewLimit = request.DailyReviewLimit;
                    existing.PreferredReviewMode = rawMode;
                    existing.AutoPlayAudio = request.AutoPlayAudio;
                    existing.ShowFuriganaHint = request.ShowFuriganaHint;
                    existing.DailyReminderEnabled = request.DailyReminderEnabled;
                    existing.ReminderHour = request.ReminderHour;
                    existing.ReminderMinute = request.ReminderMinute;
                    existing.ReminderFrequency = freq;
                    existing.ReminderDaysJson = daysJson;
                    existing.EmailReminderEnabled = request.EmailReminderEnabled;
                    existing.TimezoneId = tz;
                    existing.PreferredLocale = locale;
                    existing.ModifiedDate = utcNow;

                    await unitOfWork.SaveChangesAsync(token);
                    saved = existing;
                }, cancellationToken);

                if (conflict is not null)
                    return conflict;

                if (sidebarNotifier is not null)
                    await sidebarNotifier.NotifyUsersAsync([userId], "srs_preferences_updated", cancellationToken);

                return ApiResponse<UserSrsPreferenceResponse>.Success(
                    MapPreference(saved!, IsEmailProviderConfigured, userAccount?.IsEmailVerified ?? true));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<UserSrsPreferenceResponse>.Fail(
                    "Không thể lưu cài đặt SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<SubmitReviewResultResponse>> SubmitReviewResultAsync(
            int userId,
            SubmitReviewRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.ReviewCardId <= 0)
                return ApiResponse<SubmitReviewResultResponse>.Fail("ReviewCardId không hợp lệ.");

            try
            {
                SubmitReviewResultResponse? response = null;
                ApiResponse<SubmitReviewResultResponse>? failure = null;
                var notifyDueChanged = false;
                var utcNow = DateTime.UtcNow;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    var card = await unitOfWork.ReviewCards.GetByIdForUpdateAsync(request.ReviewCardId, userId, token);
                    if (card is null)
                    {
                        var anyCard = await unitOfWork.ReviewCards.GetByIdAsync(request.ReviewCardId, token);
                        if (anyCard is not null && anyCard.UserId != userId)
                        {
                            failure = ApiResponse<SubmitReviewResultResponse>.Fail(
                                "Không có quyền đánh giá ReviewCard này.",
                                HttpStatusCode.Forbidden);
                            return;
                        }
                        failure = ApiResponse<SubmitReviewResultResponse>.NotFound("Không tìm thấy ReviewCard.");
                        return;
                    }

                    if (card.Mode != ReviewMode.Recognition)
                    {
                        failure = ApiResponse<SubmitReviewResultResponse>.Fail("ReviewCard này không thuộc chế độ ôn nhận diện.");
                        return;
                    }

                    if (card.Status == SrsCardStatus.Suspended)
                    {
                        failure = ApiResponse<SubmitReviewResultResponse>.Fail(
                            "Thẻ đang ở trạng thái tạm ngừng, hãy kích hoạt lại trước khi ôn.",
                            HttpStatusCode.Conflict);
                        return;
                    }

                    if (card.Status == SrsCardStatus.Archived || card.IsDeleted)
                    {
                        failure = ApiResponse<SubmitReviewResultResponse>.NotFound("Thẻ SRS đã được xóa.");
                        return;
                    }

                    // Idempotency deduplication
                    var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
                    if (idempotencyKey is not null
                        && await unitOfWork.ReviewCards.HasReviewHistoryIdempotencyAsync(card.Id, idempotencyKey, token))
                    {
                        var detailedForDedup = await unitOfWork.ReviewCards.GetByIdDetailedAsync(card.Id, userId, token) ?? card;
                        var dedupDto = MapToQueueItem(detailedForDedup, utcNow, includePreviews: false);
                        var existingHistory = await unitOfWork.ReviewHistories.Query()
                            .AsNoTracking()
                            .Where(h => h.ReviewCardId == card.Id && h.IdempotencyKey == idempotencyKey && !h.IsDeleted)
                            .OrderByDescending(h => h.Id)
                            .FirstOrDefaultAsync(token);
                        var dedupCorrect = existingHistory?.IsCorrect ?? (existingHistory?.Rating != ReviewRating.Again);
                        var userRead = await unitOfWork.UserAccounts.GetByIdAsync(userId, token);
                        response = new SubmitReviewResultResponse
                        {
                            ReviewCardId = card.Id,
                            QualitySubmitted = existingHistory?.Quality ?? request.Quality,
                            RatingSubmitted = existingHistory?.Rating ?? SrsSchedulingHelper.ResolveRating(request.Rating, request.Quality, request.StudyMode),
                            StudyMode = NormalizeStudyMode(existingHistory?.StudyMode ?? request.StudyMode),
                            ShouldRepeatInSession = !dedupCorrect,
                            IsCorrect = dedupCorrect,
                            ExpectedAnswer = string.IsNullOrWhiteSpace(dedupDto.Meaning) ? dedupDto.BackText : dedupDto.Meaning,
                            ExpectedReading = dedupDto.Reading,
                            NewEaseFactor = card.EaseFactor,
                            NewIntervalDays = card.IntervalDays,
                            Repetitions = card.Repetitions,
                            Lapses = card.Lapses,
                            LearningState = card.LearningState,
                            NextReviewDate = card.NextReviewDate,
                            CurrentStreakDays = userRead?.CurrentStreak ?? 0,
                            LongestStreakDays = userRead?.LongestStreak ?? 0,
                            Deduplicated = true,
                        };
                        return;
                    }

                    var mode = NormalizeStudyMode(request.StudyMode);
                    var detailedCard = await unitOfWork.ReviewCards.GetByIdDetailedAsync(card.Id, userId, token) ?? card;
                    var snapshotDto = MapToQueueItem(detailedCard, utcNow, includePreviews: false);
                    if (string.IsNullOrEmpty(card.FrontText)) card.FrontText = snapshotDto.Term;
                    if (string.IsNullOrEmpty(card.BackText)) card.BackText = snapshotDto.BackText;
                    if (string.IsNullOrEmpty(card.Meaning)) card.Meaning = snapshotDto.Meaning;
                    if (string.IsNullOrEmpty(card.Reading)) card.Reading = snapshotDto.Reading;

                    bool autoEvaluated = false;
                    bool isCorrect = true;
                    string? matchedAgainst = null;

                    if (mode == "typing")
                    {
                        if (string.IsNullOrWhiteSpace(request.UserAnswer))
                        {
                            failure = ApiResponse<SubmitReviewResultResponse>.Fail("Vui lòng nhập đáp án trước khi gửi.");
                            return;
                        }
                        autoEvaluated = true;
                        var typingEval = SrsSchedulingHelper.EvaluateTypingAnswer(card, request.UserAnswer);
                        isCorrect = typingEval.IsMatch;
                        matchedAgainst = typingEval.MatchedAgainst;
                    }
                    else if (mode == "mcq")
                    {
                        if (string.IsNullOrWhiteSpace(request.UserAnswer))
                        {
                            failure = ApiResponse<SubmitReviewResultResponse>.Fail("Vui lòng chọn một phương án trả lời.");
                            return;
                        }
                        autoEvaluated = true;
                        isCorrect = EvaluateMcqAnswer(card, snapshotDto, request.UserAnswer);
                    }

                    ReviewRating effectiveRating;
                    if (autoEvaluated)
                    {
                        effectiveRating = isCorrect ? ReviewRating.Good : ReviewRating.Again;
                    }
                    else if (request.Rating.HasValue)
                    {
                        effectiveRating = request.Rating.Value;
                        isCorrect = effectiveRating != ReviewRating.Again;
                    }
                    else
                    {
                        effectiveRating = SrsSchedulingHelper.ResolveRating(null, request.Quality, mode);
                        isCorrect = effectiveRating != ReviewRating.Again;
                    }

                    var previousState = card.LearningState;
                    var previousInterval = card.IntervalDays;
                    var previousEf = card.EaseFactor;

                    SrsReviewSession? activeSession = null;
                    bool isInSessionRetryPass = false;
                    bool isPracticeSession = false;
                    var sessionKind = "Scheduled";
                    var processedStepKeys = new List<string>();

                    if (request.SessionKey.HasValue)
                    {
                        activeSession = await unitOfWork.SrsReviewSessions.GetBySessionKeyForUpdateAsync(
                            request.SessionKey.Value,
                            userId,
                            token);
                        if (activeSession is null)
                        {
                            failure = ApiResponse<SubmitReviewResultResponse>.NotFound("Không tìm thấy phiên ôn tập.");
                            return;
                        }

                        sessionKind = activeSession.SessionKind;
                        isPracticeSession = string.Equals(activeSession.SessionKind, "Practice", StringComparison.OrdinalIgnoreCase);

                        if (activeSession.Status != "Active")
                        {
                            var closedCode = activeSession.DeadlineAtUtc.HasValue && utcNow >= activeSession.DeadlineAtUtc.Value
                                ? "SRS_SESSION_TIME_EXPIRED"
                                : "SRS_SESSION_CLOSED";
                            failure = ApiResponse<SubmitReviewResultResponse>.Fail(
                                "Phiên ôn tập này đã kết thúc.",
                                HttpStatusCode.Conflict,
                                errorCode: closedCode);
                            return;
                        }

                        if (activeSession.DeadlineAtUtc.HasValue && utcNow > activeSession.DeadlineAtUtc.Value)
                        {
                            activeSession.Status = "Completed";
                            activeSession.CompletedAtUtc = activeSession.DeadlineAtUtc.Value;
                            activeSession.LastActivityAtUtc = utcNow;
                            activeSession.ModifiedDate = utcNow;
                            unitOfWork.SrsReviewSessions.Update(activeSession);
                            await unitOfWork.SaveChangesAsync(token);

                            failure = ApiResponse<SubmitReviewResultResponse>.Fail(
                                "Đã hết thời gian của phiên ôn tập.",
                                HttpStatusCode.Conflict,
                                errorCode: "SRS_SESSION_TIME_EXPIRED");
                            return;
                        }

                        var queueIds = DeserializeIntList(activeSession.QueueCardIdsJson);
                        var completedIds = DeserializeIntList(activeSession.CompletedCardIdsJson);
                        var retryIds = DeserializeIntList(activeSession.RetryCardIdsJson);

                        if (isPracticeSession && !queueIds.Contains(card.Id) && !retryIds.Contains(card.Id))
                        {
                            failure = ApiResponse<SubmitReviewResultResponse>.Fail(
                                "Thẻ này không thuộc phiên ôn tập tự do hiện tại.",
                                HttpStatusCode.BadRequest);
                            return;
                        }

                        processedStepKeys = DeserializeStringList(activeSession.ProcessedStepKeysJson);
                        if (idempotencyKey is not null && processedStepKeys.Contains(idempotencyKey, StringComparer.Ordinal))
                        {
                            var userReadDedup = await unitOfWork.UserAccounts.GetByIdAsync(userId, token);
                            response = new SubmitReviewResultResponse
                            {
                                ReviewCardId = card.Id,
                                QualitySubmitted = request.Quality,
                                RatingSubmitted = effectiveRating,
                                StudyMode = mode,
                                SessionKind = sessionKind,
                                ProgressMutated = false,
                                ShouldRepeatInSession = !isCorrect,
                                IsCorrect = isCorrect,
                                AutoEvaluated = autoEvaluated,
                                MatchedAgainst = string.IsNullOrEmpty(matchedAgainst) ? null : matchedAgainst,
                                ExpectedAnswer = string.IsNullOrWhiteSpace(snapshotDto.Meaning) ? snapshotDto.BackText : snapshotDto.Meaning,
                                ExpectedReading = snapshotDto.Reading,
                                NewEaseFactor = card.EaseFactor,
                                NewIntervalDays = card.IntervalDays,
                                Repetitions = card.Repetitions,
                                Lapses = card.Lapses,
                                LearningState = card.LearningState,
                                NextReviewDate = card.NextReviewDate,
                                CurrentStreakDays = userReadDedup?.CurrentStreak ?? 0,
                                LongestStreakDays = userReadDedup?.LongestStreak ?? 0,
                                Deduplicated = true,
                            };
                            return;
                        }

                        isInSessionRetryPass = retryIds.Contains(card.Id) && !completedIds.Contains(card.Id);
                    }

                    var pref = await GetOrBuildDefaultPreferenceAsync(userId, token);
                    var progressMutated = false;

                    Sm2Outcome outcome;
                    if (isPracticeSession)
                    {
                        // Ôn tập tự do (Free Practice): chấm điểm và ghi nhận tiến độ phiên nhưng tuyệt đối
                        // không thay đổi NextReviewDate, EaseFactor, IntervalDays, LearningState hoặc ReviewHistories.
                        outcome = new Sm2Outcome(
                            NewEaseFactor: card.EaseFactor,
                            NewIntervalDays: card.IntervalDays,
                            NewRepetitions: card.Repetitions,
                            NewLapses: card.Lapses,
                            NewState: card.LearningState,
                            NextReviewDateUtc: card.NextReviewDate,
                            ShouldRepeatInSession: !isCorrect,
                            NormalizedQuality: SrsSchedulingHelper.MapRatingToQuality(effectiveRating),
                            NormalizedRating: effectiveRating);
                    }
                    else if (isInSessionRetryPass)
                    {
                        // Phase 6.2 & 6.3: Thẻ đã đánh "Chưa nhớ" được lặp lại trong phiên để củng cố tại chỗ;
                        // không nhân đôi/tăng lịch dài hạn trong cùng một phiên.
                        outcome = new Sm2Outcome(
                            NewEaseFactor: card.EaseFactor,
                            NewIntervalDays: Math.Max(1, card.IntervalDays),
                            NewRepetitions: card.Repetitions,
                            NewLapses: card.Lapses,
                            NewState: isCorrect ? card.LearningState : SrsLearningState.Relearning,
                            NextReviewDateUtc: card.NextReviewDate,
                            ShouldRepeatInSession: !isCorrect,
                            NormalizedQuality: SrsSchedulingHelper.MapRatingToQuality(effectiveRating),
                            NormalizedRating: effectiveRating);
                    }
                    else
                    {
                        var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, pref.TimezoneId);
                        var utcWindowStart = studyToday.AddDays(-1);
                        var todayLogs = await unitOfWork.ReviewHistories.Query()
                            .AsNoTracking()
                            .Where(h => h.ReviewCard.UserId == userId
                                && !h.IsDeleted
                                && h.ReviewedAt >= utcWindowStart)
                            .Select(h => new { h.ReviewedAt, h.PreviousState })
                            .ToListAsync(token);

                        var todayInZone = todayLogs
                            .Where(h => SrsSchedulingHelper.GetStudyDateInZone(h.ReviewedAt, pref.TimezoneId) == studyToday)
                            .ToList();

                        var reviewedNewToday = todayInZone.Count(h => h.PreviousState == SrsLearningState.New);

                        if (card.LearningState == SrsLearningState.New && reviewedNewToday >= pref.DailyNewLimit)
                        {
                            failure = ApiResponse<SubmitReviewResultResponse>.Fail(
                                $"Bạn đã đạt giới hạn thẻ mới mỗi ngày ({pref.DailyNewLimit} thẻ). Các thẻ ôn tập cũ đến hạn vẫn có thể tiếp tục hoặc điều chỉnh giới hạn thẻ mới trong Cài đặt.",
                                HttpStatusCode.Conflict);
                            return;
                        }

                        outcome = SrsSchedulingHelper.CalculateSm2Outcome(card, effectiveRating, utcNow, pref.TimezoneId);
                        card.EaseFactor = outcome.NewEaseFactor;
                        card.IntervalDays = outcome.NewIntervalDays;
                        card.Repetitions = outcome.NewRepetitions;
                        card.Lapses = outcome.NewLapses;
                        card.LearningState = outcome.NewState;
                        if (outcome.NewState == SrsLearningState.Mastered && card.MasteredAt is null)
                            card.MasteredAt = utcNow;
                        if (outcome.NewState != SrsLearningState.Mastered)
                            card.MasteredAt = null;
                        if (isCorrect) card.CorrectCount++;
                        else card.IncorrectCount++;

                        card.NextReviewDate = outcome.NextReviewDateUtc;
                        card.LastReviewedAt = utcNow;
                        card.ModifiedDate = utcNow;
                        card.Revision++;
                        unitOfWork.ReviewCards.Update(card);

                        var log = new ReviewHistory
                        {
                            ReviewCardId = card.Id,
                            Rating = outcome.NormalizedRating,
                            Quality = outcome.NormalizedQuality,
                            ReviewedAt = utcNow,
                            StudyMode = mode,
                            UserAnswer = NormalizeOptional(request.UserAnswer),
                            IsCorrect = isCorrect,
                            ResponseTimeMs = request.ResponseTimeMs,
                            PreviousIntervalDays = previousInterval,
                            NewIntervalDays = outcome.NewIntervalDays,
                            PreviousEaseFactor = previousEf,
                            NewEaseFactor = outcome.NewEaseFactor,
                            PreviousState = previousState,
                            NewState = outcome.NewState,
                            IdempotencyKey = idempotencyKey,
                            SessionReference = request.SessionKey?.ToString(),
                        };
                        await unitOfWork.ReviewHistories.AddAsync(log, token);
                        progressMutated = true;
                    }

                    if (activeSession is not null && activeSession.Status == "Active")
                    {
                        var completedIds = DeserializeIntList(activeSession.CompletedCardIdsJson);
                        var retryIds = DeserializeIntList(activeSession.RetryCardIdsJson);
                        if (!isInSessionRetryPass)
                        {
                            activeSession.ReviewedCount++;
                            if (isCorrect) activeSession.RememberedCount++;
                            else activeSession.ForgotCount++;
                        }

                        if (isCorrect)
                        {
                            retryIds.Remove(card.Id);
                            if (!completedIds.Contains(card.Id))
                                completedIds.Add(card.Id);
                        }
                        else if (!retryIds.Contains(card.Id))
                        {
                            retryIds.Add(card.Id);
                        }

                        if (idempotencyKey is not null && !processedStepKeys.Contains(idempotencyKey, StringComparer.Ordinal))
                        {
                            processedStepKeys.Add(idempotencyKey);
                            if (processedStepKeys.Count > 250)
                                processedStepKeys.RemoveAt(0);
                            activeSession.ProcessedStepKeysJson = System.Text.Json.JsonSerializer.Serialize(processedStepKeys);
                        }

                        activeSession.CompletedCardIdsJson = System.Text.Json.JsonSerializer.Serialize(completedIds);
                        activeSession.RetryCardIdsJson = System.Text.Json.JsonSerializer.Serialize(retryIds);
                        activeSession.LastActivityAtUtc = utcNow;
                        activeSession.ModifiedDate = utcNow;
                        unitOfWork.SrsReviewSessions.Update(activeSession);
                    }

                    var (currentStreak, longestStreak) = isPracticeSession
                        ? await GetReadOnlyUserStreakAsync(userId, token)
                        : await UpdateUserStreakAsync(userId, pref.TimezoneId, utcNow, token);

                    await unitOfWork.SaveChangesAsync(token);
                    if (!isPracticeSession)
                    {
                        notifyDueChanged = true;
                        await ReconcileUnreadSrsRemindersIfCompletedAsync(userId, pref, utcNow, token);
                    }

                    response = new SubmitReviewResultResponse
                    {
                        ReviewCardId = card.Id,
                        QualitySubmitted = outcome.NormalizedQuality,
                        RatingSubmitted = outcome.NormalizedRating,
                        StudyMode = mode,
                        SessionKind = sessionKind,
                        ProgressMutated = progressMutated,
                        SessionExpired = false,
                        ShouldRepeatInSession = outcome.ShouldRepeatInSession,
                        IsCorrect = isCorrect,
                        AutoEvaluated = autoEvaluated,
                        MatchedAgainst = string.IsNullOrEmpty(matchedAgainst) ? null : matchedAgainst,
                        ExpectedAnswer = string.IsNullOrWhiteSpace(snapshotDto.Meaning) ? snapshotDto.BackText : snapshotDto.Meaning,
                        ExpectedReading = snapshotDto.Reading,
                        NewEaseFactor = outcome.NewEaseFactor,
                        NewIntervalDays = outcome.NewIntervalDays,
                        Repetitions = outcome.NewRepetitions,
                        Lapses = outcome.NewLapses,
                        LearningState = outcome.NewState,
                        NextReviewDate = outcome.NextReviewDateUtc,
                        CurrentStreakDays = currentStreak,
                        LongestStreakDays = longestStreak,
                        Deduplicated = false,
                    };
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                if (sidebarNotifier is not null && notifyDueChanged)
                    await sidebarNotifier.NotifyUsersAsync([userId], "srs_review_submitted", cancellationToken);

                return ApiResponse<SubmitReviewResultResponse>.Success(response!);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<SubmitReviewResultResponse>.Fail(
                    "Không thể lưu kết quả ôn tập.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        private async Task<(int CurrentStreak, int LongestStreak)> UpdateUserStreakAsync(
            int userId,
            string timezoneId,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var user = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, cancellationToken);
            if (user is null) return (0, 0);

            var localToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, timezoneId);
            var localYesterday = localToday.AddDays(-1);
            DateTime? lastLocalDay = user.LastStudyDate.HasValue
                ? SrsSchedulingHelper.GetStudyDateInZone(user.LastStudyDate.Value, timezoneId)
                : null;

            if (lastLocalDay == localToday)
            {
                user.CurrentStreak = Math.Max(1, user.CurrentStreak);
            }
            else if (lastLocalDay == localYesterday)
            {
                user.CurrentStreak = Math.Max(1, user.CurrentStreak + 1);
            }
            else
            {
                user.CurrentStreak = 1;
            }

            user.LongestStreak = Math.Max(user.LongestStreak, user.CurrentStreak);
            user.LastStudyDate = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            unitOfWork.UserAccounts.Update(user);
            return (user.CurrentStreak, user.LongestStreak);
        }

        private Task<(int Current, int Longest)> ComputeEffectiveStreakAsync(
            UserAccount? user,
            string timezoneId,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (user is null) return Task.FromResult((0, 0));
            var localToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, timezoneId);
            var localYesterday = localToday.AddDays(-1);
            if (!user.LastStudyDate.HasValue)
                return Task.FromResult((0, user.LongestStreak));

            var lastLocalDay = SrsSchedulingHelper.GetStudyDateInZone(user.LastStudyDate.Value, timezoneId);
            var current = (lastLocalDay == localToday || lastLocalDay == localYesterday)
                ? user.CurrentStreak
                : 0;
            return Task.FromResult((current, Math.Max(user.LongestStreak, current)));
        }

        private static bool EvaluateMcqAnswer(ReviewCard card, ReviewQueueItemResponse dto, string userAnswer)
        {
            var normUser = SrsSchedulingHelper.NormalizeMeaningKey(userAnswer);
            if (string.IsNullOrEmpty(normUser)) return false;

            var targets = new List<string> { dto.Meaning, dto.BackText };
            if (!string.IsNullOrWhiteSpace(card.Meaning)) targets.Add(card.Meaning);
            if (!string.IsNullOrWhiteSpace(card.BackText)) targets.Add(card.BackText);
            targets.AddRange(SrsSchedulingHelper.DeserializeStringList(card.SynonymsJson));

            return targets.Any(t =>
                !string.IsNullOrWhiteSpace(t)
                && string.Equals(normUser, SrsSchedulingHelper.NormalizeMeaningKey(t), StringComparison.Ordinal));
        }

        private sealed record ResolvedCardCandidate(
            string CardType,
            string SourceType,
            int? SourceId,
            int SourceVersion,
            string? SourceLabel,
            int? LessonId,
            int? VocabularyId,
            int? KanjiId,
            int? GrammarPatternId,
            int? LearnerNoteEntryId,
            int? SrsCardTemplateId,
            string FrontText,
            string BackText,
            string? Reading,
            string? Meaning,
            string? ExampleSentence,
            string? ExampleMeaning,
            string? ContextSnippet,
            string? JlptLevel,
            string? AudioUrl,
            string? PersonalNote,
            List<string> AcceptedReadings,
            List<string> Synonyms,
            string NormalizedKey);

        private async Task<ApiResponse<ResolvedCardCandidate>> ResolveCandidateAsync(
            int userId,
            EnrollSrsCardRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
                return ApiResponse<ResolvedCardCandidate>.Fail("Dữ liệu thẻ SRS không hợp lệ.");

            if (!string.IsNullOrWhiteSpace(request.JlptLevel)
                && SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel) is null)
            {
                return ApiResponse<ResolvedCardCandidate>.Fail("Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.", HttpStatusCode.BadRequest);
            }

            var lessonId = request.LessonId;
            string? lessonTitle = null;
            string? lessonLevel = null;
            if (lessonId.HasValue)
            {
                var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(lessonId.Value, cancellationToken);
                if (lesson is null)
                    return ApiResponse<ResolvedCardCandidate>.NotFound("Không tìm thấy bài học liên kết.");
                lessonTitle = lesson.Title;
                lessonLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(lesson.CertificateLevel?.Code);
            }

            if (request.SrsCardTemplateId.HasValue)
            {
                var template = await unitOfWork.SrsCardTemplates.GetDetailedByIdAsync(request.SrsCardTemplateId.Value, cancellationToken);
                if (template is null || template.Status != ContentStatus.Published)
                    return ApiResponse<ResolvedCardCandidate>.NotFound("Thẻ mẫu SRS không tồn tại hoặc chưa được xuất bản.");

                var jlpt = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel ?? template.JlptLevel ?? lessonLevel);
                var key = SrsSchedulingHelper.BuildNormalizedContentKey(
                    template.CardType,
                    template.VocabularyId,
                    template.KanjiId,
                    template.GrammarPatternId,
                    null,
                    template.Id,
                    template.FrontText,
                    template.Meaning ?? template.BackText);

                return ApiResponse<ResolvedCardCandidate>.Success(new ResolvedCardCandidate(
                    template.CardType,
                    string.IsNullOrWhiteSpace(request.SourceType) ? "AuthorTemplate" : request.SourceType.Trim(),
                    template.Id,
                    template.Version,
                    request.SourceLabel ?? lessonTitle ?? "Thẻ mẫu từ Content Author",
                    lessonId ?? template.LessonId,
                    template.VocabularyId,
                    template.KanjiId,
                    template.GrammarPatternId,
                    null,
                    template.Id,
                    template.FrontText,
                    template.BackText,
                    template.Reading,
                    template.Meaning ?? template.BackText,
                    template.ExampleSentence,
                    template.ExampleMeaning,
                    request.ContextSnippet ?? template.UsageNotes,
                    jlpt,
                    request.AudioUrl ?? template.AudioUrl,
                    NormalizeOptional(request.PersonalNote),
                    SrsSchedulingHelper.DeserializeStringList(template.AcceptedReadingsJson),
                    SrsSchedulingHelper.DeserializeStringList(template.SynonymsJson),
                    key));
            }

            if (request.VocabularyId.HasValue)
            {
                var vocab = await unitOfWork.Vocabularies.GetPublicByIdAsync(request.VocabularyId.Value, cancellationToken);
                if (vocab is null)
                    return ApiResponse<ResolvedCardCandidate>.NotFound("Từ vựng không tồn tại hoặc chưa xuất bản.");

                var jlpt = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel ?? lessonLevel);
                if (jlpt is null)
                {
                    var mappings = await unitOfWork.ContentLevelMappings.GetByVocabularyIdAsync(vocab.Id, cancellationToken);
                    jlpt = mappings.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code))
                        .FirstOrDefault(code => code is not null);
                }

                var key = SrsSchedulingHelper.BuildNormalizedContentKey("Vocabulary", vocab.Id, null, null, null, null, vocab.Word, vocab.Meaning);
                return ApiResponse<ResolvedCardCandidate>.Success(new ResolvedCardCandidate(
                    "Vocabulary",
                    string.IsNullOrWhiteSpace(request.SourceType) ? (lessonId.HasValue ? "Lesson" : "Vocabulary") : request.SourceType.Trim(),
                    request.SourceId ?? lessonId ?? vocab.Id,
                    vocab.Version,
                    request.SourceLabel ?? lessonTitle ?? "Ngân hàng Từ vựng",
                    lessonId,
                    vocab.Id,
                    null,
                    null,
                    request.LearnerNoteEntryId,
                    null,
                    vocab.Word,
                    vocab.Meaning,
                    vocab.Reading,
                    vocab.Meaning,
                    vocab.ExampleSentence ?? NormalizeOptional(request.ExampleSentence),
                    vocab.ExampleSentenceMeaning ?? NormalizeOptional(request.ExampleMeaning),
                    NormalizeOptional(request.ContextSnippet),
                    jlpt,
                    vocab.AudioUrl,
                    NormalizeOptional(request.PersonalNote),
                    request.AcceptedReadings ?? [],
                    request.Synonyms ?? [],
                    key));
            }

            if (request.KanjiId.HasValue)
            {
                var kanji = await unitOfWork.Kanjis.GetPublicByIdAsync(request.KanjiId.Value, cancellationToken);
                if (kanji is null)
                    return ApiResponse<ResolvedCardCandidate>.NotFound("Chữ Hán không tồn tại hoặc chưa xuất bản.");

                var jlpt = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel ?? lessonLevel);
                if (jlpt is null)
                {
                    var mappings = await unitOfWork.ContentLevelMappings.GetByKanjiIdAsync(kanji.Id, cancellationToken);
                    jlpt = mappings.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code))
                        .FirstOrDefault(code => code is not null);
                }

                var readings = new List<string>();
                if (!string.IsNullOrWhiteSpace(kanji.OnYomi)) readings.Add(kanji.OnYomi);
                if (!string.IsNullOrWhiteSpace(kanji.KunYomi)) readings.Add(kanji.KunYomi);
                if (!string.IsNullOrWhiteSpace(kanji.SinoVietnamese)) readings.Add(kanji.SinoVietnamese);
                var readingText = readings.Count > 0 ? string.Join(" · ", readings) : null;

                var key = SrsSchedulingHelper.BuildNormalizedContentKey("Kanji", null, kanji.Id, null, null, null, kanji.Character, kanji.Meaning);
                return ApiResponse<ResolvedCardCandidate>.Success(new ResolvedCardCandidate(
                    "Kanji",
                    string.IsNullOrWhiteSpace(request.SourceType) ? (lessonId.HasValue ? "Lesson" : "Kanji") : request.SourceType.Trim(),
                    request.SourceId ?? lessonId ?? kanji.Id,
                    kanji.Version,
                    request.SourceLabel ?? lessonTitle ?? "Ngân hàng Chữ Hán",
                    lessonId,
                    null,
                    kanji.Id,
                    null,
                    request.LearnerNoteEntryId,
                    null,
                    kanji.Character,
                    kanji.Meaning,
                    readingText,
                    kanji.Meaning,
                    NormalizeOptional(request.ExampleSentence),
                    NormalizeOptional(request.ExampleMeaning),
                    NormalizeOptional(request.ContextSnippet),
                    jlpt,
                    kanji.AudioUrl,
                    NormalizeOptional(request.PersonalNote),
                    readings,
                    request.Synonyms ?? [],
                    key));
            }

            if (request.GrammarPatternId.HasValue)
            {
                var grammar = await unitOfWork.GrammarPatterns.GetPublicByIdAsync(request.GrammarPatternId.Value, cancellationToken);
                if (grammar is null)
                    return ApiResponse<ResolvedCardCandidate>.NotFound("Mẫu ngữ pháp không tồn tại hoặc chưa xuất bản.");

                var jlpt = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel ?? lessonLevel);
                if (jlpt is null)
                {
                    var mappings = await unitOfWork.ContentLevelMappings.GetByGrammarPatternIdAsync(grammar.Id, cancellationToken);
                    jlpt = mappings.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code))
                        .FirstOrDefault(code => code is not null);
                }

                var meaning = !string.IsNullOrWhiteSpace(grammar.UsageNotes) ? grammar.UsageNotes : grammar.Structure;
                var key = SrsSchedulingHelper.BuildNormalizedContentKey("Grammar", null, null, grammar.Id, null, null, grammar.Title, meaning);
                return ApiResponse<ResolvedCardCandidate>.Success(new ResolvedCardCandidate(
                    "Grammar",
                    string.IsNullOrWhiteSpace(request.SourceType) ? (lessonId.HasValue ? "Lesson" : "Grammar") : request.SourceType.Trim(),
                    request.SourceId ?? lessonId ?? grammar.Id,
                    grammar.Version,
                    request.SourceLabel ?? lessonTitle ?? "Ngân hàng Ngữ pháp",
                    lessonId,
                    null,
                    null,
                    grammar.Id,
                    null,
                    null,
                    grammar.Title,
                    meaning,
                    grammar.Structure,
                    meaning,
                    grammar.ExampleSentence ?? NormalizeOptional(request.ExampleSentence),
                    grammar.ExampleSentenceMeaning ?? NormalizeOptional(request.ExampleMeaning),
                    NormalizeOptional(request.ContextSnippet),
                    jlpt,
                    null,
                    NormalizeOptional(request.PersonalNote),
                    request.AcceptedReadings ?? [grammar.Structure],
                    request.Synonyms ?? [],
                    key));
            }

            if (request.LearnerNoteEntryId.HasValue)
            {
                var note = await unitOfWork.LearnerNoteEntries.Query()
                    .Include(x => x.Vocabulary)
                    .Include(x => x.Kanji)
                    .Include(x => x.LearnerNote)
                    .Include(x => x.NotebookPage)
                    .FirstOrDefaultAsync(x => x.Id == request.LearnerNoteEntryId.Value && !x.IsDeleted, cancellationToken);

                if (note is null || note.LearnerNote is null || note.LearnerNote.IsDeleted || note.LearnerNote.UserId != userId)
                    return ApiResponse<ResolvedCardCandidate>.NotFound("Không tìm thấy mục Sổ tay.");

                if (note.VocabularyId.HasValue)
                {
                    request.VocabularyId = note.VocabularyId;
                    request.SourceType ??= "Notebook";
                    request.SourceLabel ??= note.NotebookPage?.Title ?? note.LearnerNote.Name;
                    return await ResolveCandidateAsync(userId, request, cancellationToken);
                }

                if (note.KanjiId.HasValue)
                {
                    request.KanjiId = note.KanjiId;
                    request.SourceType ??= "Notebook";
                    request.SourceLabel ??= note.NotebookPage?.Title ?? note.LearnerNote.Name;
                    return await ResolveCandidateAsync(userId, request, cancellationToken);
                }

                var front = note.ManualWord ?? string.Empty;
                var back = note.ManualMeaning ?? note.Note ?? string.Empty;
                if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back))
                    return ApiResponse<ResolvedCardCandidate>.Fail("Mục sổ tay cần có cả mặt trước và nghĩa để tạo thẻ SRS.");

                var key = SrsSchedulingHelper.BuildNormalizedContentKey("Custom", null, null, null, note.Id, null, front, back);
                return ApiResponse<ResolvedCardCandidate>.Success(new ResolvedCardCandidate(
                    "Custom",
                    string.IsNullOrWhiteSpace(request.SourceType) ? "Notebook" : request.SourceType.Trim(),
                    note.Id,
                    1,
                    request.SourceLabel ?? note.NotebookPage?.Title ?? note.LearnerNote.Name,
                    lessonId,
                    null,
                    null,
                    note.GrammarPatternId,
                    note.Id,
                    null,
                    front,
                    back,
                    note.ManualReading,
                    note.ManualMeaning ?? back,
                    note.ExampleSentence ?? NormalizeOptional(request.ExampleSentence),
                    note.ExampleMeaning ?? NormalizeOptional(request.ExampleMeaning),
                    note.ContextSnippet ?? NormalizeOptional(request.ContextSnippet),
                    SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel ?? lessonLevel),
                    NormalizeOptional(request.AudioUrl),
                    note.Note ?? NormalizeOptional(request.PersonalNote),
                    request.AcceptedReadings ?? [],
                    request.Synonyms ?? [],
                    key));
            }

            // Custom / Reading / Manual Card (never Test/Exam sources per Phase 0.1)
            var frontText = NormalizeOptional(request.FrontText);
            var meaningText = NormalizeOptional(request.Meaning) ?? NormalizeOptional(request.BackText);
            var backText = NormalizeOptional(request.BackText) ?? meaningText;
            if (string.IsNullOrWhiteSpace(frontText) || string.IsNullOrWhiteSpace(backText))
                return ApiResponse<ResolvedCardCandidate>.Fail("Mặt trước (Front) và mặt sau/nghĩa (Back/Meaning) là bắt buộc.");

            var cType = string.IsNullOrWhiteSpace(request.CardType)
                ? "Vocabulary"
                : (NormalizeLearnerEditableCardType(request.CardType) ?? request.CardType.Trim());
            var srcType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim();
            if (srcType.Contains("Test", StringComparison.OrdinalIgnoreCase)
                || srcType.Contains("Exam", StringComparison.OrdinalIgnoreCase)
                || srcType.Contains("Placement", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<ResolvedCardCandidate>.Fail(
                    "SRS chỉ hỗ trợ nguồn Lesson, Vocabulary, Kanji, Grammar, Notebook, Reading, AuthorTemplate và thẻ cá nhân.",
                    HttpStatusCode.BadRequest);
            }

            var normalizedKey = SrsSchedulingHelper.BuildNormalizedContentKey(cType, null, null, null, null, null, frontText, meaningText ?? backText);

            return ApiResponse<ResolvedCardCandidate>.Success(new ResolvedCardCandidate(
                cType,
                srcType,
                request.SourceId ?? lessonId,
                1,
                NormalizeOptional(request.SourceLabel) ?? lessonTitle ?? srcType,
                lessonId,
                null,
                null,
                null,
                null,
                null,
                frontText,
                backText,
                NormalizeOptional(request.Reading),
                meaningText ?? backText,
                NormalizeOptional(request.ExampleSentence),
                NormalizeOptional(request.ExampleMeaning),
                NormalizeOptional(request.ContextSnippet),
                SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel ?? lessonLevel),
                NormalizeOptional(request.AudioUrl),
                NormalizeOptional(request.PersonalNote),
                request.AcceptedReadings ?? [],
                request.Synonyms ?? [],
                normalizedKey));
        }

        private async Task<SrsEnrollOutcomeResponse> EnrollResolvedCandidateAsync(
            int userId,
            ResolvedCardCandidate c,
            bool reactivateIfArchived,
            CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var existing = await unitOfWork.ReviewCards.FindExistingCardAsync(
                userId,
                c.VocabularyId,
                c.KanjiId,
                c.GrammarPatternId,
                c.LearnerNoteEntryId,
                c.SrsCardTemplateId,
                c.NormalizedKey,
                ReviewMode.Recognition,
                cancellationToken);

            if (existing is not null)
            {
                string action;
                var updatedLinks = SrsSchedulingHelper.MergeSourceLinkJson(
                    existing.SourceLinksJson,
                    c.SourceType,
                    c.SourceId,
                    c.SourceVersion,
                    c.SourceLabel,
                    c.LessonId,
                    utcNow);

                var sourceAdded = !string.Equals(updatedLinks, existing.SourceLinksJson, StringComparison.Ordinal);
                existing.SourceLinksJson = updatedLinks;

                if (existing.IsDeleted || existing.Status == SrsCardStatus.Archived || existing.Status == SrsCardStatus.Suspended)
                {
                    if (reactivateIfArchived)
                    {
                        existing.IsDeleted = false;
                        existing.Status = SrsCardStatus.Active;
                        existing.SuspendedAt = null;
                        existing.ArchivedAt = null;
                        if (existing.NextReviewDate < utcNow.Date)
                            existing.NextReviewDate = utcNow.Date;
                        existing.ModifiedDate = utcNow;
                        existing.Revision++;
                        unitOfWork.ReviewCards.Update(existing);
                        action = "Reactivated";
                    }
                    else
                    {
                        action = "AlreadyExists";
                    }
                }
                else
                {
                    if (sourceAdded)
                    {
                        existing.ModifiedDate = utcNow;
                        unitOfWork.ReviewCards.Update(existing);
                        action = "LinkedSource";
                    }
                    else
                    {
                        action = "AlreadyExists";
                    }
                }

                return new SrsEnrollOutcomeResponse
                {
                    Action = action,
                    Card = MapToQueueItem(existing, utcNow, includePreviews: true),
                };
            }

            var newCard = new ReviewCard
            {
                UserId = userId,
                VocabularyId = c.VocabularyId,
                KanjiId = c.KanjiId,
                GrammarPatternId = c.GrammarPatternId,
                LearnerNoteEntryId = c.LearnerNoteEntryId,
                LessonId = c.LessonId,
                SrsCardTemplateId = c.SrsCardTemplateId,
                Mode = ReviewMode.Recognition,
                Status = SrsCardStatus.Active,
                LearningState = SrsLearningState.New,
                CardType = c.CardType,
                SourceType = c.SourceType,
                SourceId = c.SourceId,
                SourceVersion = c.SourceVersion,
                SourceLabel = c.SourceLabel,
                NormalizedContentKey = c.NormalizedKey,
                SourceLinksJson = SrsSchedulingHelper.MergeSourceLinkJson(null, c.SourceType, c.SourceId, c.SourceVersion, c.SourceLabel, c.LessonId, utcNow),
                FrontText = c.FrontText,
                BackText = c.BackText,
                Reading = c.Reading,
                Meaning = c.Meaning,
                ExampleSentence = c.ExampleSentence,
                ExampleMeaning = c.ExampleMeaning,
                ContextSnippet = c.ContextSnippet,
                JlptLevel = c.JlptLevel,
                AudioUrl = c.AudioUrl,
                PersonalNote = c.PersonalNote,
                AcceptedReadingsJson = SrsSchedulingHelper.SerializeStringList(c.AcceptedReadings),
                SynonymsJson = SrsSchedulingHelper.SerializeStringList(c.Synonyms),
                EaseFactor = SrsSchedulingHelper.DefaultEaseFactor,
                IntervalDays = 0,
                Repetitions = 0,
                NextReviewDate = utcNow.Date,
                CreatedDate = utcNow,
            };

            await GetOrBuildDefaultPreferenceAsync(userId, cancellationToken, persistIfMissing: true);
            await unitOfWork.ReviewCards.AddAsync(newCard, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new SrsEnrollOutcomeResponse
            {
                Action = "Created",
                Card = MapToQueueItem(newCard, utcNow, includePreviews: true),
            };
        }

        private async Task<UserSrsPreference> GetOrBuildDefaultPreferenceAsync(
            int userId,
            CancellationToken cancellationToken,
            bool persistIfMissing = false)
        {
            var existing = await unitOfWork.UserSrsPreferences.GetByUserIdAsync(userId, cancellationToken);
            if (existing is not null) return existing;

            var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
            var created = new UserSrsPreference
            {
                UserId = userId,
                EmailReminderEnabled = user?.EmailNotificationsEnabled ?? false,
                DailyReminderEnabled = user?.SystemNotificationsEnabled ?? true,
                TimezoneId = "Asia/Ho_Chi_Minh",
                PreferredLocale = "vi",
                CreatedDate = DateTime.UtcNow,
            };

            if (persistIfMissing && user is not null)
            {
                await unitOfWork.UserSrsPreferences.AddAsync(created, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return created;
        }

        private async Task ReconcileUnreadSrsRemindersIfCompletedAsync(
            int userId,
            UserSrsPreference pref,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var dueNowCards = await unitOfWork.ReviewCards.GetDueQueueAsync(
                userId,
                utcNow,
                null,
                null,
                null,
                cancellationToken);

            var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, pref.TimezoneId);
            var utcWindowStart = studyToday.AddDays(-1);
            var todayLogs = await unitOfWork.ReviewHistories.Query()
                .AsNoTracking()
                .Where(h => h.ReviewCard.UserId == userId
                    && !h.IsDeleted
                    && h.ReviewedAt >= utcWindowStart)
                .Select(h => new { h.ReviewedAt, h.PreviousState })
                .ToListAsync(cancellationToken);

            var todayInZone = todayLogs
                .Where(h => SrsSchedulingHelper.GetStudyDateInZone(h.ReviewedAt, pref.TimezoneId) == studyToday)
                .ToList();

            var remainingActionable = SrsSchedulingHelper.SelectActionableDueCards(
                dueNowCards,
                pref.DailyNewLimit,
                pref.DailyReviewLimit,
                todayInZone.Count(h => h.PreviousState == SrsLearningState.New),
                todayInZone.Count);

            if (remainingActionable.Count > 0)
                return;

            var unreadReminders = await unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId && !n.IsDeleted && !n.IsRead && n.Type == "SrsReminder")
                .ToListAsync(cancellationToken);

            if (unreadReminders.Count == 0)
                return;

            foreach (var notification in unreadReminders)
            {
                notification.IsRead = true;
                notification.ModifiedDate = utcNow;
                unitOfWork.Notifications.Update(notification);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static UserSrsPreferenceResponse MapPreference(
            UserSrsPreference p,
            bool emailProviderConfigured = false,
            bool emailVerified = true) => new()
        {
            AutoEnrollAfterLesson = p.AutoEnrollAfterLesson,
            DailyNewLimit = p.DailyNewLimit,
            DailyReviewLimit = p.DailyReviewLimit,
            PreferredReviewMode = p.PreferredReviewMode,
            AutoPlayAudio = p.AutoPlayAudio,
            ShowFuriganaHint = p.ShowFuriganaHint,
            DailyReminderEnabled = p.DailyReminderEnabled,
            ReminderHour = p.ReminderHour,
            ReminderMinute = p.ReminderMinute,
            ReminderFrequency = p.ReminderFrequency,
            ReminderDays = ParseIntList(p.ReminderDaysJson),
            EmailReminderEnabled = p.EmailReminderEnabled,
            EmailProviderConfigured = emailProviderConfigured,
            EmailVerified = emailVerified,
            TimezoneId = p.TimezoneId,
            PreferredLocale = p.PreferredLocale,
            LastReminderSentAtUtc = p.LastReminderSentAtUtc,
            LastReminderSentLocalDate = p.LastReminderSentLocalDate,
            UpdatedAtUtc = p.ModifiedDate ?? p.CreatedDate,
        };

        private static List<int> ParseIntList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<int>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static ReviewQueueItemResponse MapToQueueItem(ReviewCard item, DateTime utcNow, bool includePreviews)
        {
            var note = item.LearnerNoteEntry;
            var vocab = item.Vocabulary ?? note?.Vocabulary;
            var kanji = item.Kanji ?? note?.Kanji;
            var grammar = item.GrammarPattern;

            var term = !string.IsNullOrWhiteSpace(item.FrontText)
                ? item.FrontText
                : vocab?.Word
                  ?? kanji?.Character
                  ?? grammar?.Title
                  ?? note?.ManualWord
                  ?? string.Empty;

            var reading = !string.IsNullOrWhiteSpace(item.Reading)
                ? item.Reading
                : vocab?.Reading
                  ?? kanji?.OnYomi
                  ?? grammar?.Structure
                  ?? note?.ManualReading;

            var meaning = !string.IsNullOrWhiteSpace(item.Meaning)
                ? item.Meaning
                : vocab?.Meaning
                  ?? kanji?.Meaning
                  ?? grammar?.UsageNotes
                  ?? grammar?.Structure
                  ?? note?.ManualMeaning
                  ?? item.BackText;

            var backText = !string.IsNullOrWhiteSpace(item.BackText) ? item.BackText : meaning;

            var sourceUnpublished = (vocab is not null && (vocab.IsDeleted || vocab.Status != ContentStatus.Published))
                || (kanji is not null && (kanji.IsDeleted || kanji.Status != ContentStatus.Published))
                || (grammar is not null && (grammar.IsDeleted || grammar.Status != ContentStatus.Published))
                || (item.SrsCardTemplate is not null && (item.SrsCardTemplate.IsDeleted || item.SrsCardTemplate.Status != ContentStatus.Published));

            var contentType = ResolveContentType(item.CardType, vocab, kanji, grammar);

            return new ReviewQueueItemResponse
            {
                ReviewCardId = item.Id,
                ContentType = contentType,
                CardType = string.IsNullOrWhiteSpace(item.CardType) ? contentType.ToString() : item.CardType,
                Term = term,
                Reading = reading,
                Meaning = meaning ?? string.Empty,
                BackText = backText ?? string.Empty,
                ExampleSentence = item.ExampleSentence ?? vocab?.ExampleSentence ?? grammar?.ExampleSentence,
                ExampleMeaning = item.ExampleMeaning ?? vocab?.ExampleSentenceMeaning ?? grammar?.ExampleSentenceMeaning,
                ContextSnippet = item.ContextSnippet,
                Note = item.PersonalNote ?? note?.Note,
                JlptLevel = item.JlptLevel,
                AudioUrl = item.AudioUrl ?? vocab?.AudioUrl ?? kanji?.AudioUrl,
                Status = item.Status,
                LearningState = item.LearningState,
                SourceType = item.SourceType,
                SourceId = item.SourceId,
                SourceLabel = item.SourceLabel ?? item.Lesson?.Title,
                LessonId = item.LessonId,
                VocabularyId = item.VocabularyId ?? vocab?.Id,
                KanjiId = item.KanjiId ?? kanji?.Id,
                GrammarPatternId = item.GrammarPatternId ?? grammar?.Id,
                LearnerNoteEntryId = item.LearnerNoteEntryId,
                SrsCardTemplateId = item.SrsCardTemplateId,
                SourceUnpublished = sourceUnpublished,
                SourceLinks = SrsSchedulingHelper.DeserializeSourceLinks(item.SourceLinksJson),
                AcceptedReadings = SrsSchedulingHelper.DeserializeStringList(item.AcceptedReadingsJson),
                Synonyms = SrsSchedulingHelper.DeserializeStringList(item.SynonymsJson),
                EaseFactor = item.EaseFactor,
                IntervalDays = item.IntervalDays,
                Repetitions = item.Repetitions,
                Lapses = item.Lapses,
                CorrectCount = item.CorrectCount,
                IncorrectCount = item.IncorrectCount,
                NextReviewDate = item.NextReviewDate,
                LastReviewedAt = item.LastReviewedAt,
                CreatedAt = item.CreatedDate,
                IntervalPreviews = includePreviews ? BuildIntervalPreview(item, utcNow) : null,
            };
        }

        private static ReviewContentType ResolveContentType(string? cardType, Vocabulary? v, Kanji? k, GrammarPattern? g)
        {
            if (string.Equals(cardType, "Kanji", StringComparison.OrdinalIgnoreCase) || k is not null)
                return ReviewContentType.Kanji;
            if (string.Equals(cardType, "Grammar", StringComparison.OrdinalIgnoreCase) || g is not null)
                return ReviewContentType.Grammar;
            if (string.Equals(cardType, "Custom", StringComparison.OrdinalIgnoreCase))
                return ReviewContentType.Custom;
            return ReviewContentType.Vocabulary;
        }

        private static SrsIntervalPreviewResponse BuildIntervalPreview(ReviewCard card, DateTime utcNow)
        {
            var again = SrsSchedulingHelper.CalculateSm2Outcome(card, ReviewRating.Again, utcNow);
            var hard = SrsSchedulingHelper.CalculateSm2Outcome(card, ReviewRating.Hard, utcNow);
            var good = SrsSchedulingHelper.CalculateSm2Outcome(card, ReviewRating.Good, utcNow);
            var easy = SrsSchedulingHelper.CalculateSm2Outcome(card, ReviewRating.Easy, utcNow);

            return new SrsIntervalPreviewResponse
            {
                AgainDays = again.NewIntervalDays,
                HardDays = hard.NewIntervalDays,
                GoodDays = good.NewIntervalDays,
                EasyDays = easy.NewIntervalDays,
                AgainLabel = SrsSchedulingHelper.FormatIntervalLabel(again.NewIntervalDays),
                HardLabel = SrsSchedulingHelper.FormatIntervalLabel(hard.NewIntervalDays),
                GoodLabel = SrsSchedulingHelper.FormatIntervalLabel(good.NewIntervalDays),
                EasyLabel = SrsSchedulingHelper.FormatIntervalLabel(easy.NewIntervalDays),
            };
        }

        private static List<SrsMcqOptionResponse> BuildMcqOptions(
            ReviewQueueItemResponse target,
            List<ReviewCard> userCards,
            List<SrsCardTemplate> fallbackTemplates,
            List<Vocabulary> fallbackVocab,
            List<Kanji> fallbackKanji,
            List<GrammarPattern> fallbackGrammar,
            int deterministicSeed = 0)
        {
            var correctText = (string.IsNullOrWhiteSpace(target.Meaning) ? target.BackText : target.Meaning)?.Trim() ?? string.Empty;
            var normCorrect = SrsSchedulingHelper.NormalizeMeaningKey(correctText);
            if (string.IsNullOrWhiteSpace(target.Term) || string.IsNullOrEmpty(normCorrect))
                return [];

            var forbiddenNormalized = new HashSet<string>(StringComparer.Ordinal) { normCorrect };
            void AddForbidden(string? val)
            {
                var norm = SrsSchedulingHelper.NormalizeMeaningKey(val);
                if (!string.IsNullOrEmpty(norm))
                    forbiddenNormalized.Add(norm);
            }

            AddForbidden(target.Meaning);
            AddForbidden(target.BackText);
            AddForbidden(target.Term);
            AddForbidden(target.Reading);
            foreach (var syn in target.Synonyms) AddForbidden(syn);
            foreach (var rd in target.AcceptedReadings) AddForbidden(rd);

            var baseSeed = HashCode.Combine(deterministicSeed, target.ReviewCardId, 7919);
            var stepCounter = 0;
            var distractors = new List<string>(3);

            void CollectFromPool(IEnumerable<string?> rawPool)
            {
                if (distractors.Count >= 3) return;
                var tierCandidates = new List<string>();
                var tierSeen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var raw in rawPool)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var clean = raw.Trim();
                    if (clean.Length > 120) continue;
                    var norm = SrsSchedulingHelper.NormalizeMeaningKey(clean);
                    if (string.IsNullOrEmpty(norm) || forbiddenNormalized.Contains(norm))
                        continue;
                    if (tierSeen.Add(norm))
                        tierCandidates.Add(clean);
                }

                // Deterministic Fisher-Yates shuffle via SHA256 so different cards get different distractors
                // while F5 reload within the same session keeps the exact same options.
                stepCounter++;
                DeterministicShuffle(tierCandidates, HashCode.Combine(baseSeed, stepCounter));

                foreach (var candidate in tierCandidates)
                {
                    if (distractors.Count >= 3) break;
                    var norm = SrsSchedulingHelper.NormalizeMeaningKey(candidate);
                    if (forbiddenNormalized.Add(norm))
                        distractors.Add(candidate);
                }
            }

            static string? ResolveCardMeaning(ReviewCard c)
            {
                if (!string.IsNullOrWhiteSpace(c.Meaning)) return c.Meaning;
                if (!string.IsNullOrWhiteSpace(c.BackText)) return c.BackText;
                if (c.Vocabulary is not null && !string.IsNullOrWhiteSpace(c.Vocabulary.Meaning)) return c.Vocabulary.Meaning;
                if (c.Kanji is not null && !string.IsNullOrWhiteSpace(c.Kanji.Meaning)) return c.Kanji.Meaning;
                if (c.GrammarPattern is not null)
                    return !string.IsNullOrWhiteSpace(c.GrammarPattern.UsageNotes) ? c.GrammarPattern.UsageNotes : c.GrammarPattern.Structure;
                if (c.LearnerNoteEntry is not null)
                    return c.LearnerNoteEntry.ManualMeaning ?? c.LearnerNoteEntry.Vocabulary?.Meaning ?? c.LearnerNoteEntry.Kanji?.Meaning;
                return null;
            }

            var otherUserCards = userCards.Where(c => !c.IsDeleted && c.Id != target.ReviewCardId).ToList();

            // Tier 1: User's own cards matching the same content type
            CollectFromPool(
                otherUserCards
                    .Where(c => ResolveContentType(c.CardType, c.Vocabulary, c.Kanji, c.GrammarPattern) == target.ContentType
                        || (target.ContentType == ReviewContentType.Custom && ResolveContentType(c.CardType, c.Vocabulary, c.Kanji, c.GrammarPattern) == ReviewContentType.Vocabulary))
                    .Select(ResolveCardMeaning));

            // Tier 2: Published SrsCardTemplates & published Content Banks matching the content type
            if (target.ContentType == ReviewContentType.Kanji)
            {
                CollectFromPool(
                    fallbackTemplates
                        .Where(t => string.Equals(t.CardType, "Kanji", StringComparison.OrdinalIgnoreCase))
                        .Select(t => string.IsNullOrWhiteSpace(t.Meaning) ? t.BackText : t.Meaning)
                        .Concat(fallbackKanji.Select(k => k.Meaning)));
            }
            else if (target.ContentType == ReviewContentType.Grammar)
            {
                CollectFromPool(
                    fallbackTemplates
                        .Where(t => string.Equals(t.CardType, "Grammar", StringComparison.OrdinalIgnoreCase))
                        .Select(t => string.IsNullOrWhiteSpace(t.Meaning) ? t.BackText : t.Meaning)
                        .Concat(fallbackGrammar.Select(g => string.IsNullOrWhiteSpace(g.UsageNotes) ? g.Structure : g.UsageNotes)));
            }
            else
            {
                CollectFromPool(
                    fallbackTemplates
                        .Where(t => !string.Equals(t.CardType, "Kanji", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(t.CardType, "Grammar", StringComparison.OrdinalIgnoreCase))
                        .Select(t => string.IsNullOrWhiteSpace(t.Meaning) ? t.BackText : t.Meaning)
                        .Concat(fallbackVocab.Select(v => v.Meaning)));
            }

            // Tier 3: Other real user cards or published templates if still < 3 distractors
            if (distractors.Count < 3)
            {
                CollectFromPool(otherUserCards.Select(ResolveCardMeaning));
                CollectFromPool(fallbackTemplates.Select(t => string.IsNullOrWhiteSpace(t.Meaning) ? t.BackText : t.Meaning));
                CollectFromPool(fallbackVocab.Select(v => v.Meaning));
            }

            // V4.1 Phase 5.3: Never fabricate fake distractors. Require >= 3 real distinct distractors from DB.
            if (distractors.Count < 3)
                return [];

            var options = new List<SrsMcqOptionResponse>(4)
            {
                new() { OptionKey = "opt_a", Text = correctText },
            };
            for (var i = 0; i < 3; i++)
            {
                options.Add(new SrsMcqOptionResponse
                {
                    OptionKey = $"opt_{(char)('b' + i)}",
                    Text = distractors[i],
                });
            }

            DeterministicShuffle(options, HashCode.Combine(baseSeed, 997));
            for (var i = 0; i < options.Count; i++)
                options[i].OptionKey = $"choice_{i + 1}";

            return options;
        }

        private static void DeterministicShuffle<T>(IList<T> items, int seed)
        {
            if (items.Count <= 1) return;
            for (var i = items.Count - 1; i > 0; i--)
            {
                Span<byte> input = stackalloc byte[8];
                BitConverter.TryWriteBytes(input[..4], seed);
                BitConverter.TryWriteBytes(input[4..], i);
                Span<byte> hash = stackalloc byte[32];
                SHA256.HashData(input, hash);
                var val = BitConverter.ToUInt32(hash[..4]);
                var j = (int)(val % (uint)(i + 1));
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private static void AssignMixedModesDeterministically(IReadOnlyList<ReviewQueueItemResponse> items)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["flip"] = 0,
                ["typing"] = 0,
                ["mcq"] = 0,
            };
            string[] rotation = ["flip", "typing", "mcq"];

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var eligible = item.EligibleModes.Count > 0 ? item.EligibleModes : ["flip"];
                var preferredOrder = new[]
                {
                    rotation[i % 3],
                    rotation[(i + 1) % 3],
                    rotation[(i + 2) % 3],
                };

                var bestMode = preferredOrder
                    .Where(m => eligible.Contains(m, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(m => counts.GetValueOrDefault(m, 0))
                    .FirstOrDefault()
                    ?? eligible[0];

                item.AssignedStudyMode = bestMode;
                counts[bestMode] = counts.GetValueOrDefault(bestMode, 0) + 1;
            }
        }

        private static void RedactAnswerKeyForSessionStep(ReviewQueueItemResponse dto)
        {
            if (string.Equals(dto.AssignedStudyMode, "typing", StringComparison.OrdinalIgnoreCase))
            {
                dto.Meaning = string.Empty;
                dto.BackText = string.Empty;
                dto.Reading = null;
                dto.Note = null;
                dto.ContextSnippet = null;
                dto.Synonyms = [];
                dto.AcceptedReadings = [];
                dto.ExampleSentence = null;
                dto.ExampleMeaning = null;
                dto.McqOptions = [];
            }
            else if (string.Equals(dto.AssignedStudyMode, "mcq", StringComparison.OrdinalIgnoreCase))
            {
                dto.Meaning = string.Empty;
                dto.BackText = string.Empty;
                dto.Reading = null;
                dto.Note = null;
                dto.ContextSnippet = null;
                dto.Synonyms = [];
                dto.AcceptedReadings = [];
                dto.ExampleSentence = null;
                dto.ExampleMeaning = null;
            }
            else
            {
                dto.McqOptions = [];
            }
        }

        public async Task<ApiResponse<SrsReviewSessionResponse>> StartSessionAsync(
            int userId,
            StartSrsSessionRequest request,
            CancellationToken cancellationToken)
        {
            var normalizedKind = NormalizeSessionKind(request.SessionKind);
            if (normalizedKind == "Practice")
                return await StartPracticeSessionInternalAsync(userId, request, cancellationToken);

            var mode = NormalizeStudyMode(request.StudyMode);
            if (request.TimeLimitMinutes.HasValue && request.TimeLimitMinutes.Value is < 1 or > 180)
            {
                return ApiResponse<SrsReviewSessionResponse>.Fail(
                    "Giới hạn thời gian phiên ôn tập phải từ 1 đến 180 phút.",
                    HttpStatusCode.BadRequest);
            }

            var queueResult = await GetDailyReviewQueueAsync(
                userId,
                mode,
                request.CardType,
                request.JlptLevel,
                request.SourceType,
                cancellationToken);
            if (!queueResult.IsSuccess || queueResult.Result is null)
            {
                return ApiResponse<SrsReviewSessionResponse>.Fail(
                    queueResult.ErrorMessage ?? "Không thể khởi tạo phiên ôn tập.",
                    (HttpStatusCode)queueResult.StatusCode);
            }

            var queue = queueResult.Result;
            var maxCards = Math.Clamp(request.MaxCards, 1, 100);
            var selectedItems = queue.Items.Take(maxCards).ToList();
            if (selectedItems.Count == 0)
            {
                var emptyError = queue.EmptyReason switch
                {
                    "InsufficientMcqDistractors" => "Không đủ thẻ có đủ phương án nhiễu hợp lệ cho chế độ Trắc nghiệm.",
                    "InsufficientTypingData" => "Không có thẻ đến hạn nào đủ dữ liệu đáp án cho chế độ Gõ đáp án.",
                    "DailyLimitReached" => "Bạn đã đạt giới hạn thẻ mới hôm nay. Các thẻ ôn tập cũ đến hạn vẫn khả dụng hoặc bạn có thể tăng giới hạn thẻ mới trong Cài đặt.",
                    _ => "Không có thẻ đến hạn để bắt đầu phiên ôn tập.",
                };
                return ApiResponse<SrsReviewSessionResponse>.Fail(emptyError, HttpStatusCode.Conflict);
            }

            var utcNow = DateTime.UtcNow;
            // Selection always follows due-queue priority; shuffle only changes the order inside the chosen set,
            // so enabling it never swaps an overdue card for a later one.
            var cardIds = selectedItems.Select(x => x.ReviewCardId).ToList();
            if (request.Shuffle)
            {
                for (var i = cardIds.Count - 1; i > 0; i--)
                {
                    var j = RandomNumberGenerator.GetInt32(i + 1);
                    (cardIds[i], cardIds[j]) = (cardIds[j], cardIds[i]);
                }
            }
            DateTime? scheduledDeadlineAtUtc = request.TimeLimitMinutes.HasValue
                ? utcNow.AddMinutes(request.TimeLimitMinutes.Value)
                : null;
            SrsReviewSession? session = null;

            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                var activeCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, token);
                var activeIdSet = activeCards
                    .Where(c => !c.IsDeleted && c.Status == SrsCardStatus.Active && c.Mode == ReviewMode.Recognition)
                    .Select(c => c.Id)
                    .ToHashSet();

                var existingActive = await unitOfWork.SrsReviewSessions.GetActiveByUserIdAsync(userId, "Scheduled", token);
                if (existingActive is not null)
                {
                    var existingQueueIds = DeserializeIntList(existingActive.QueueCardIdsJson);
                    var existingCompletedSet = DeserializeIntList(existingActive.CompletedCardIdsJson).ToHashSet();
                    var existingRetryIds = DeserializeIntList(existingActive.RetryCardIdsJson);
                    var hasRemainingInExisting = existingQueueIds.Concat(existingRetryIds)
                        .Any(id => !existingCompletedSet.Contains(id) && activeIdSet.Contains(id));

                    // Prevent duplicate session creation on double-click or multi-tab start in the same mode
                    if (hasRemainingInExisting
                        && string.Equals(existingActive.StudyMode, mode, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existingActive.FilterCardType, NormalizeOptional(request.CardType), StringComparison.OrdinalIgnoreCase))
                    {
                        session = existingActive;
                        return;
                    }

                    existingActive.Status = "Abandoned";
                    existingActive.CompletedAtUtc = utcNow;
                    existingActive.ModifiedDate = utcNow;
                    unitOfWork.SrsReviewSessions.Update(existingActive);
                }

                session = new SrsReviewSession
                {
                    UserId = userId,
                    SessionKey = Guid.NewGuid(),
                    StudyMode = mode,
                    SessionKind = "Scheduled",
                    Status = "Active",
                    FilterCardType = NormalizeOptional(request.CardType),
                    FilterJlptLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel),
                    FilterSourceType = NormalizeOptional(request.SourceType),
                    ShuffleEnabled = request.Shuffle,
                    TimeLimitMinutes = request.TimeLimitMinutes,
                    DeadlineAtUtc = scheduledDeadlineAtUtc,
                    RequestedCount = maxCards,
                    TotalInitialCards = cardIds.Count,
                    QueueCardIdsJson = System.Text.Json.JsonSerializer.Serialize(cardIds),
                    CompletedCardIdsJson = "[]",
                    RetryCardIdsJson = "[]",
                    ProcessedStepKeysJson = "[]",
                    StartedAtUtc = utcNow,
                    LastActivityAtUtc = utcNow,
                    CreatedDate = utcNow,
                };

                await unitOfWork.SrsReviewSessions.AddAsync(session, token);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);

            var hydrated = await HydrateSessionAsync(session!, userId, cancellationToken);
            return ApiResponse<SrsReviewSessionResponse>.Success(hydrated);
        }

        private async Task<ApiResponse<SrsReviewSessionResponse>> StartPracticeSessionInternalAsync(
            int userId,
            StartSrsSessionRequest request,
            CancellationToken cancellationToken)
        {
            var mode = NormalizeStudyMode(request.StudyMode);
            if (request.TimeLimitMinutes.HasValue && request.TimeLimitMinutes.Value is < 1 or > 180)
            {
                return ApiResponse<SrsReviewSessionResponse>.Fail(
                    "Giới hạn thời gian ôn tập tự do phải từ 1 đến 180 phút.",
                    HttpStatusCode.BadRequest);
            }

            var scope = NormalizeOptional(request.SelectionScope)?.ToLowerInvariant() ?? "all";
            var explicitIds = request.CardIds?.Where(id => id > 0).Distinct().ToList() ?? [];
            if (scope == "selected" && explicitIds.Count == 0)
            {
                return ApiResponse<SrsReviewSessionResponse>.Fail(
                    "Vui lòng chọn ít nhất một thẻ trong thư viện để bắt đầu ôn tập tự do.",
                    HttpStatusCode.BadRequest);
            }

            var utcNow = DateTime.UtcNow;
            var allUserCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
            IEnumerable<ReviewCard> pool = allUserCards
                .Where(c => !c.IsDeleted && c.Status == SrsCardStatus.Active && c.Mode == ReviewMode.Recognition);

            if (scope == "selected")
            {
                var requestedSet = explicitIds.ToHashSet();
                pool = pool.Where(c => requestedSet.Contains(c.Id));
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.CardType))
                {
                    var ct = request.CardType.Trim();
                    pool = pool.Where(c => string.Equals(c.CardType, ct, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.JlptLevel))
                {
                    var level = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel);
                    if (!string.IsNullOrEmpty(level))
                        pool = pool.Where(c => string.Equals(c.JlptLevel, level, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.LearningState)
                    && Enum.TryParse<SrsLearningState>(request.LearningState.Trim(), ignoreCase: true, out var parsedState))
                {
                    pool = pool.Where(c => c.LearningState == parsedState);
                }

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var q = request.Search.Trim();
                    pool = pool.Where(c =>
                    {
                        var dto = MapToQueueItem(c, utcNow, includePreviews: false);
                        return dto.Term.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || dto.Meaning.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || (!string.IsNullOrWhiteSpace(dto.Reading) && dto.Reading.Contains(q, StringComparison.OrdinalIgnoreCase))
                            || (!string.IsNullOrWhiteSpace(dto.Note) && dto.Note.Contains(q, StringComparison.OrdinalIgnoreCase));
                    });
                }
            }

            var candidateCards = pool.ToList();
            if (candidateCards.Count == 0)
            {
                return ApiResponse<SrsReviewSessionResponse>.Fail(
                    "Không tìm thấy thẻ hợp lệ thuộc quyền sở hữu của bạn để ôn tập tự do.",
                    HttpStatusCode.Conflict);
            }

            List<SrsCardTemplate> fallbackTemplates = [];
            List<Vocabulary> fallbackVocab = [];
            List<Kanji> fallbackKanji = [];
            List<GrammarPattern> fallbackGrammar = [];
            if (mode is "mcq" or "mixed")
            {
                fallbackTemplates = await unitOfWork.SrsCardTemplates.Query().AsNoTracking().Where(t => !t.IsDeleted && t.Status == ContentStatus.Published).OrderBy(t => t.Id).Take(60).ToListAsync(cancellationToken);
                fallbackVocab = await unitOfWork.Vocabularies.Query().AsNoTracking().Where(v => !v.IsDeleted && v.Status == ContentStatus.Published).OrderBy(v => v.Id).Take(60).ToListAsync(cancellationToken);
                fallbackKanji = await unitOfWork.Kanjis.Query().AsNoTracking().Where(k => !k.IsDeleted && k.Status == ContentStatus.Published).OrderBy(k => k.Id).Take(40).ToListAsync(cancellationToken);
                fallbackGrammar = await unitOfWork.GrammarPatterns.Query().AsNoTracking().Where(g => !g.IsDeleted && g.Status == ContentStatus.Published).OrderBy(g => g.Id).Take(40).ToListAsync(cancellationToken);
            }

            var seed = RandomNumberGenerator.GetInt32(int.MaxValue);
            var eligibleItems = new List<ReviewQueueItemResponse>();
            var excludedMcqCount = 0;
            var excludedTypingCount = 0;

            foreach (var card in candidateCards)
            {
                var dto = MapToQueueItem(card, utcNow, includePreviews: true);
                var isFlipEligible = !string.IsNullOrWhiteSpace(dto.Term)
                    && (!string.IsNullOrWhiteSpace(dto.Meaning) || !string.IsNullOrWhiteSpace(dto.BackText));
                var typingElig = SrsSchedulingHelper.EvaluateTypingEligibility(card, dto.Term, dto.Reading, dto.Meaning, dto.BackText);
                var mcqOptions = mode is "mcq" or "mixed"
                    ? BuildMcqOptions(dto, allUserCards, fallbackTemplates, fallbackVocab, fallbackKanji, fallbackGrammar, seed)
                    : [];

                if (mode == "flip" && !isFlipEligible) continue;
                if (mode == "typing" && !typingElig.IsEligible) { excludedTypingCount++; continue; }
                if (mode == "mcq" && mcqOptions.Count < 4) { excludedMcqCount++; continue; }
                if (mode == "mixed" && !isFlipEligible && !typingElig.IsEligible && mcqOptions.Count < 4) continue;

                eligibleItems.Add(dto);
            }

            if (eligibleItems.Count == 0)
            {
                var msg = mode == "mcq" && excludedMcqCount > 0
                    ? "Các thẻ bạn chọn chưa đủ 4 phương án trắc nghiệm hợp lệ."
                    : mode == "typing" && excludedTypingCount > 0
                        ? "Các thẻ bạn chọn chưa đủ dữ liệu cách đọc hoặc nghĩa hợp lệ để gõ đáp án."
                        : "Không có thẻ nào đủ điều kiện cho chế độ ôn tập này.";
                return ApiResponse<SrsReviewSessionResponse>.Fail(msg, HttpStatusCode.Conflict);
            }

            if (request.Shuffle)
            {
                for (var i = eligibleItems.Count - 1; i > 0; i--)
                {
                    var j = RandomNumberGenerator.GetInt32(i + 1);
                    (eligibleItems[i], eligibleItems[j]) = (eligibleItems[j], eligibleItems[i]);
                }
            }

            var maxCards = Math.Clamp(request.MaxCards, 1, 100);
            var cardIds = eligibleItems.Take(maxCards).Select(x => x.ReviewCardId).ToList();
            DateTime? deadlineAtUtc = request.TimeLimitMinutes.HasValue
                ? utcNow.AddMinutes(request.TimeLimitMinutes.Value)
                : null;

            SrsReviewSession? session = null;
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                var existingPractice = await unitOfWork.SrsReviewSessions.GetActiveByUserIdAsync(userId, "Practice", token);
                if (existingPractice is not null)
                {
                    var existingExpired = existingPractice.DeadlineAtUtc.HasValue && utcNow > existingPractice.DeadlineAtUtc.Value;
                    var existingQueueIds = DeserializeIntList(existingPractice.QueueCardIdsJson);
                    var existingCompletedSet = DeserializeIntList(existingPractice.CompletedCardIdsJson).ToHashSet();
                    var existingRetryIds = DeserializeIntList(existingPractice.RetryCardIdsJson);
                    var hasRemaining = !existingExpired && existingQueueIds.Concat(existingRetryIds).Any(id => !existingCompletedSet.Contains(id));

                    // Idempotent anti-double-click guard only within quick 3-second launch window if same settings
                    var recentDuplicateClick = hasRemaining
                        && (utcNow - existingPractice.StartedAtUtc).TotalSeconds <= 3
                        && string.Equals(existingPractice.StudyMode, mode, StringComparison.OrdinalIgnoreCase)
                        && existingPractice.TotalInitialCards == cardIds.Count
                        && existingPractice.ShuffleEnabled == request.Shuffle
                        && existingPractice.TimeLimitMinutes == request.TimeLimitMinutes;

                    if (recentDuplicateClick)
                    {
                        session = existingPractice;
                        return;
                    }

                    existingPractice.Status = existingExpired ? "Completed" : "Abandoned";
                    existingPractice.CompletedAtUtc = existingExpired ? existingPractice.DeadlineAtUtc : utcNow;
                    existingPractice.ModifiedDate = utcNow;
                    unitOfWork.SrsReviewSessions.Update(existingPractice);
                }

                session = new SrsReviewSession
                {
                    UserId = userId,
                    SessionKey = Guid.NewGuid(),
                    StudyMode = mode,
                    SessionKind = "Practice",
                    Status = "Active",
                    FilterCardType = NormalizeOptional(request.CardType),
                    FilterJlptLevel = SrsSchedulingHelper.NormalizeSupportedJlptLevel(request.JlptLevel),
                    FilterSourceType = NormalizeOptional(request.SourceType),
                    FilterLearningState = NormalizeOptional(request.LearningState),
                    SelectionScope = scope,
                    ShuffleEnabled = request.Shuffle,
                    TimeLimitMinutes = request.TimeLimitMinutes,
                    DeadlineAtUtc = deadlineAtUtc,
                    RequestedCount = maxCards,
                    TotalInitialCards = cardIds.Count,
                    QueueCardIdsJson = System.Text.Json.JsonSerializer.Serialize(cardIds),
                    CompletedCardIdsJson = "[]",
                    RetryCardIdsJson = "[]",
                    ProcessedStepKeysJson = "[]",
                    StartedAtUtc = utcNow,
                    LastActivityAtUtc = utcNow,
                    CreatedDate = utcNow,
                };

                await unitOfWork.SrsReviewSessions.AddAsync(session, token);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);

            var hydrated = await HydrateSessionAsync(session!, userId, cancellationToken);
            return ApiResponse<SrsReviewSessionResponse>.Success(hydrated);
        }

        public async Task<ApiResponse<SrsReviewSessionResponse?>> GetActiveSessionAsync(
            int userId,
            string? sessionKind = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedKind = string.IsNullOrWhiteSpace(sessionKind)
                ? "Scheduled"
                : NormalizeSessionKind(sessionKind);
            var session = await unitOfWork.SrsReviewSessions.GetActiveByUserIdAsync(userId, normalizedKind, cancellationToken);
            if (session is null)
                return ApiResponse<SrsReviewSessionResponse?>.Success(null);

            var utcNow = DateTime.UtcNow;
            if (session.DeadlineAtUtc.HasValue && utcNow > session.DeadlineAtUtc.Value)
            {
                var finished = await FinishSessionAsync(userId, session.SessionKey, cancellationToken);
                return finished.IsSuccess
                    ? ApiResponse<SrsReviewSessionResponse?>.Success(finished.Result)
                    : ApiResponse<SrsReviewSessionResponse?>.Success(null);
            }

            var built = await HydrateSessionAsync(session, userId, cancellationToken);
            return ApiResponse<SrsReviewSessionResponse?>.Success(built);
        }

        public async Task<ApiResponse<SrsReviewSessionResponse>> FinishSessionAsync(
            int userId,
            Guid sessionKey,
            CancellationToken cancellationToken)
        {
            var session = await unitOfWork.SrsReviewSessions.GetBySessionKeyForUpdateAsync(sessionKey, userId, cancellationToken);
            if (session is null)
                return ApiResponse<SrsReviewSessionResponse>.NotFound("Không tìm thấy phiên ôn tập.");

            var utcNow = DateTime.UtcNow;
            if (session.Status == "Active")
            {
                session.Status = "Completed";
                session.CompletedAtUtc = session.DeadlineAtUtc.HasValue && utcNow > session.DeadlineAtUtc.Value
                    ? session.DeadlineAtUtc.Value
                    : utcNow;
                session.LastActivityAtUtc = utcNow;
                session.ModifiedDate = utcNow;
                unitOfWork.SrsReviewSessions.Update(session);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                if (!string.Equals(session.SessionKind, "Practice", StringComparison.OrdinalIgnoreCase))
                {
                    var pref = await GetOrBuildDefaultPreferenceAsync(userId, cancellationToken);
                    await ReconcileUnreadSrsRemindersIfCompletedAsync(userId, pref, utcNow, cancellationToken);
                }
            }

            var built = await HydrateSessionAsync(session, userId, cancellationToken);
            return ApiResponse<SrsReviewSessionResponse>.Success(built);
        }

        public async Task<ApiResponse<bool>> AbandonSessionAsync(
            int userId,
            Guid sessionKey,
            CancellationToken cancellationToken)
        {
            var session = await unitOfWork.SrsReviewSessions.GetBySessionKeyForUpdateAsync(sessionKey, userId, cancellationToken);
            if (session is null)
                return ApiResponse<bool>.NotFound("Không tìm thấy phiên ôn tập.");

            if (session.Status == "Active")
            {
                session.Status = "Abandoned";
                session.CompletedAtUtc = DateTime.UtcNow;
                session.ModifiedDate = DateTime.UtcNow;
                unitOfWork.SrsReviewSessions.Update(session);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return ApiResponse<bool>.Success(true);
        }

        public async Task<ApiResponse<List<SrsCalendarDayResponse>>> GetReviewCalendarAsync(
            int userId,
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            if (year is < 2020 or > 2100 || month is < 1 or > 12)
                return ApiResponse<List<SrsCalendarDayResponse>>.Fail("Tháng/năm không hợp lệ.", HttpStatusCode.BadRequest);

            var utcNow = DateTime.UtcNow;
            var pref = await GetOrBuildDefaultPreferenceAsync(userId, cancellationToken);
            var studyToday = SrsSchedulingHelper.GetStudyDateInZone(utcNow, pref.TimezoneId);
            var studyTodayDateOnly = DateOnly.FromDateTime(studyToday);

            var startDate = new DateOnly(year, month, 1);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var windowStartUtc = startDate.ToDateTime(TimeOnly.MinValue).AddDays(-2);
            var windowEndUtc = startDate.AddMonths(1).ToDateTime(TimeOnly.MinValue).AddDays(2);
            var todayStartUtc = studyToday.AddDays(-1);
            var minQueryUtc = todayStartUtc < windowStartUtc ? todayStartUtc : windowStartUtc;
            var maxQueryUtc = utcNow > windowEndUtc ? utcNow.AddDays(1) : windowEndUtc;

            var histories = await unitOfWork.ReviewHistories.Query()
                .AsNoTracking()
                .Where(h => h.ReviewCard.UserId == userId
                    && !h.IsDeleted
                    && h.ReviewedAt >= minQueryUtc
                    && h.ReviewedAt <= maxQueryUtc)
                .Select(h => new { h.ReviewedAt, h.IsCorrect, h.Rating, h.PreviousState })
                .ToListAsync(cancellationToken);

            var activeCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
            var activeScheduled = activeCards
                .Where(c => !c.IsDeleted && c.Status == SrsCardStatus.Active && c.Mode == ReviewMode.Recognition)
                .ToList();

            var histByDay = histories
                .GroupBy(h => DateOnly.FromDateTime(SrsSchedulingHelper.GetStudyDateInZone(h.ReviewedAt, pref.TimezoneId)))
                .ToDictionary(g => g.Key, g => g.ToList());

            histByDay.TryGetValue(studyTodayDateOnly, out var todayLogs);
            todayLogs ??= [];
            var reviewedNewToday = todayLogs.Count(h => h.PreviousState == SrsLearningState.New);
            var reviewedTotalToday = todayLogs.Count;

            var rawDueNowCards = activeScheduled.Where(c => c.NextReviewDate <= utcNow).ToList();
            var actionableDueNowCards = SrsSchedulingHelper.SelectActionableDueCards(
                rawDueNowCards,
                pref.DailyNewLimit,
                pref.DailyReviewLimit,
                reviewedNewToday,
                reviewedTotalToday);
            var dueNowCount = actionableDueNowCards.Count;

            var futureCards = activeScheduled.Where(c => c.NextReviewDate > utcNow).ToList();
            var futureByDay = futureCards
                .GroupBy(c => DateOnly.FromDateTime(SrsSchedulingHelper.GetStudyDateInZone(c.NextReviewDate, pref.TimezoneId)))
                .ToDictionary(
                    g => g.Key,
                    g => (Count: g.Count(), EarliestUtc: (DateTime?)g.Min(x => x.NextReviewDate)));

            var nextDueAt = futureCards
                .OrderBy(c => c.NextReviewDate)
                .Select(c => (DateTime?)c.NextReviewDate)
                .FirstOrDefault();

            var dueLaterTodayInfo = futureByDay.GetValueOrDefault(studyTodayDateOnly);
            var dueLaterTodayCount = dueLaterTodayInfo.Count;

            var result = Enumerable.Range(0, daysInMonth).Select(offset =>
            {
                var d = startDate.AddDays(offset);
                histByDay.TryGetValue(d, out var dayLogs);
                var revCount = dayLogs?.Count ?? 0;
                var remCount = dayLogs?.Count(l => l.IsCorrect ?? (l.Rating != ReviewRating.Again)) ?? 0;

                if (d == studyTodayDateOnly)
                {
                    var totalScheduledToday = dueNowCount + dueLaterTodayCount;
                    var earliestToday = dueNowCount > 0 ? utcNow : dueLaterTodayInfo.EarliestUtc;
                    var statusToday = dueNowCount > 0
                        ? "DueNow"
                        : dueLaterTodayCount > 0
                            ? "DueLaterToday"
                            : revCount > 0
                                ? "CompletedToday"
                                : "NoCardsDueToday";

                    return new SrsCalendarDayResponse
                    {
                        Date = d,
                        ReviewedCount = revCount,
                        RememberedCount = remCount,
                        DueNowCount = dueNowCount,
                        DueLaterCount = dueLaterTodayCount,
                        DueScheduledCount = totalScheduledToday,
                        EarliestDueAtUtc = earliestToday,
                        NextDueAtUtc = nextDueAt,
                        Status = statusToday,
                    };
                }

                if (d > studyTodayDateOnly)
                {
                    var fut = futureByDay.GetValueOrDefault(d);
                    return new SrsCalendarDayResponse
                    {
                        Date = d,
                        ReviewedCount = revCount,
                        RememberedCount = remCount,
                        DueNowCount = 0,
                        DueLaterCount = fut.Count,
                        DueScheduledCount = fut.Count,
                        EarliestDueAtUtc = fut.EarliestUtc,
                        NextDueAtUtc = fut.EarliestUtc,
                        Status = fut.Count > 0 ? "ScheduledFuture" : "None",
                    };
                }

                return new SrsCalendarDayResponse
                {
                    Date = d,
                    ReviewedCount = revCount,
                    RememberedCount = remCount,
                    DueNowCount = 0,
                    DueLaterCount = 0,
                    DueScheduledCount = 0,
                    EarliestDueAtUtc = null,
                    NextDueAtUtc = null,
                    Status = revCount > 0 ? "ReviewedPast" : "None",
                };
            }).ToList();

            return ApiResponse<List<SrsCalendarDayResponse>>.Success(result);
        }

        private async Task<SrsReviewSessionResponse> HydrateSessionAsync(
            SrsReviewSession session,
            int userId,
            CancellationToken cancellationToken)
        {
            var queueIds = DeserializeIntList(session.QueueCardIdsJson);
            var completedSet = DeserializeIntList(session.CompletedCardIdsJson).ToHashSet();
            var retryIds = DeserializeIntList(session.RetryCardIdsJson);
            var pendingOrderedIds = queueIds.Where(id => !completedSet.Contains(id) && !retryIds.Contains(id))
                .Concat(retryIds.Where(id => !completedSet.Contains(id)))
                .Distinct()
                .ToList();

            var utcNow = DateTime.UtcNow;
            var allUserCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId, cancellationToken);
            var cardMap = allUserCards.ToDictionary(c => c.Id);

            List<SrsCardTemplate> fallbackTemplates = [];
            List<Vocabulary> fallbackVocab = [];
            List<Kanji> fallbackKanji = [];
            List<GrammarPattern> fallbackGrammar = [];
            if (session.StudyMode is "mcq" or "mixed")
            {
                fallbackTemplates = await unitOfWork.SrsCardTemplates.Query().AsNoTracking().Where(t => !t.IsDeleted && t.Status == ContentStatus.Published).OrderBy(t => t.Id).Take(60).ToListAsync(cancellationToken);
                fallbackVocab = await unitOfWork.Vocabularies.Query().AsNoTracking().Where(v => !v.IsDeleted && v.Status == ContentStatus.Published).OrderBy(v => v.Id).Take(60).ToListAsync(cancellationToken);
                fallbackKanji = await unitOfWork.Kanjis.Query().AsNoTracking().Where(k => !k.IsDeleted && k.Status == ContentStatus.Published).OrderBy(k => k.Id).Take(40).ToListAsync(cancellationToken);
                fallbackGrammar = await unitOfWork.GrammarPatterns.Query().AsNoTracking().Where(g => !g.IsDeleted && g.Status == ContentStatus.Published).OrderBy(g => g.Id).Take(40).ToListAsync(cancellationToken);
            }

            var sessionSeed = session.SessionKey.GetHashCode();
            var allInitialItemsById = new Dictionary<int, ReviewQueueItemResponse>();
            var initialOrderedItems = new List<ReviewQueueItemResponse>();
            var excludedMcq = new List<ExcludedMcqCardReasonResponse>();
            var excludedTyping = new List<ExcludedMcqCardReasonResponse>();

            foreach (var id in queueIds.Distinct())
            {
                if (!cardMap.TryGetValue(id, out var card) || card.IsDeleted || card.Status != SrsCardStatus.Active)
                    continue;

                var dto = MapToQueueItem(card, utcNow, includePreviews: true);
                var eligibleModes = new List<string>();
                var isFlipEligible = !string.IsNullOrWhiteSpace(dto.Term)
                    && (!string.IsNullOrWhiteSpace(dto.Meaning) || !string.IsNullOrWhiteSpace(dto.BackText));
                if (isFlipEligible)
                    eligibleModes.Add("flip");

                var typingElig = SrsSchedulingHelper.EvaluateTypingEligibility(card, dto.Term, dto.Reading, dto.Meaning, dto.BackText);
                dto.TypingPromptKind = typingElig.PromptKind;
                if (typingElig.IsEligible)
                    eligibleModes.Add("typing");

                if (session.StudyMode is "mcq" or "mixed")
                {
                    var options = BuildMcqOptions(
                        dto,
                        allUserCards,
                        fallbackTemplates,
                        fallbackVocab,
                        fallbackKanji,
                        fallbackGrammar,
                        sessionSeed);
                    if (options.Count >= 4)
                    {
                        dto.McqOptions = options;
                        eligibleModes.Add("mcq");
                    }
                }

                dto.EligibleModes = eligibleModes;
                dto.AssignedStudyMode = session.StudyMode == "mixed" ? "flip" : session.StudyMode;
                allInitialItemsById[id] = dto;
                initialOrderedItems.Add(dto);
            }

            if (session.StudyMode == "mixed" && initialOrderedItems.Count > 0)
            {
                AssignMixedModesDeterministically(initialOrderedItems);
            }

            var activeModesInSession = initialOrderedItems
                .Select(x => x.AssignedStudyMode)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var pendingItems = new List<ReviewQueueItemResponse>();
            var removedCount = 0;

            foreach (var id in pendingOrderedIds)
            {
                if (!allInitialItemsById.TryGetValue(id, out var dto))
                {
                    removedCount++;
                    continue;
                }

                if (session.StudyMode == "mcq" && dto.McqOptions.Count < 4)
                {
                    excludedMcq.Add(new ExcludedMcqCardReasonResponse
                    {
                        ReviewCardId = dto.ReviewCardId,
                        Term = dto.Term,
                        Reason = "Chưa đủ phương án nhiễu hợp lệ từ học liệu đã xuất bản.",
                    });
                    continue;
                }

                if (session.StudyMode == "typing" && !dto.EligibleModes.Contains("typing", StringComparer.OrdinalIgnoreCase))
                {
                    excludedTyping.Add(new ExcludedMcqCardReasonResponse
                    {
                        ReviewCardId = dto.ReviewCardId,
                        Term = dto.Term,
                        Reason = "Thẻ chưa có dữ liệu cách đọc hoặc nghĩa hợp lệ để chấm tự động.",
                    });
                    continue;
                }

                RedactAnswerKeyForSessionStep(dto);
                pendingItems.Add(dto);
            }

            return BuildSessionResponse(session, pendingItems, excludedMcq, excludedTyping, activeModesInSession, removedCount);
        }

        private static SrsReviewSessionResponse BuildSessionResponse(
            SrsReviewSession session,
            List<ReviewQueueItemResponse> pendingItems,
            List<ExcludedMcqCardReasonResponse> excludedMcqCards,
            List<ExcludedMcqCardReasonResponse> excludedTypingCards,
            List<string> activeModesInSession,
            int removedMidSession)
        {
            var utcNow = DateTime.UtcNow;
            var effectiveEnd = session.CompletedAtUtc
                ?? (session.DeadlineAtUtc.HasValue && utcNow > session.DeadlineAtUtc.Value ? session.DeadlineAtUtc.Value : utcNow);
            var elapsedSeconds = Math.Max(0, (int)Math.Round((effectiveEnd - session.StartedAtUtc).TotalSeconds));
            var isExpired = session.DeadlineAtUtc.HasValue && utcNow >= session.DeadlineAtUtc.Value;
            var skippedCount = Math.Max(0, session.TotalInitialCards - Math.Min(session.TotalInitialCards, session.ReviewedCount));

            return new SrsReviewSessionResponse
            {
                SessionKey = session.SessionKey,
                StudyMode = session.StudyMode,
                SessionKind = string.IsNullOrWhiteSpace(session.SessionKind) ? "Scheduled" : session.SessionKind,
                Status = session.Status,
                FilterCardType = session.FilterCardType,
                FilterLearningState = session.FilterLearningState,
                SelectionScope = session.SelectionScope,
                ShuffleEnabled = session.ShuffleEnabled,
                TimeLimitMinutes = session.TimeLimitMinutes,
                DeadlineAtUtc = session.DeadlineAtUtc,
                ServerUtcNow = utcNow,
                ElapsedSeconds = elapsedSeconds,
                IsExpired = isExpired,
                TotalInitialCards = session.TotalInitialCards,
                ReviewedCount = session.ReviewedCount,
                RememberedCount = session.RememberedCount,
                ForgotCount = session.ForgotCount,
                SkippedCount = skippedCount,
                RemovedMidSessionCount = removedMidSession,
                QueueCardIds = DeserializeIntList(session.QueueCardIdsJson),
                CompletedCardIds = DeserializeIntList(session.CompletedCardIdsJson),
                RetryCardIds = DeserializeIntList(session.RetryCardIdsJson),
                ActiveModesInSession = activeModesInSession,
                PendingItems = pendingItems,
                ExcludedMcqCards = excludedMcqCards,
                ExcludedTypingCards = excludedTypingCards,
                StartedAtUtc = session.StartedAtUtc,
                LastActivityAtUtc = session.LastActivityAtUtc,
                CompletedAtUtc = session.CompletedAtUtc,
            };
        }

        private async Task<(int Current, int Longest)> GetReadOnlyUserStreakAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
            return (user?.CurrentStreak ?? 0, user?.LongestStreak ?? 0);
        }

        private static List<int> DeserializeIntList(string? json) => ParseIntList(json);

        private static List<string> DeserializeStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static string NormalizeSessionKind(string? kind)
            => string.Equals(kind?.Trim(), "Practice", StringComparison.OrdinalIgnoreCase) ? "Practice" : "Scheduled";

        private static string? NormalizeLearnerEditableCardType(string? cardType)
        {
            if (string.IsNullOrWhiteSpace(cardType)) return null;
            var trimmed = cardType.Trim();
            if (string.Equals(trimmed, "Vocabulary", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Vocab", StringComparison.OrdinalIgnoreCase))
            {
                return "Vocabulary";
            }
            if (string.Equals(trimmed, "Kanji", StringComparison.OrdinalIgnoreCase))
            {
                return "Kanji";
            }
            return null;
        }

        private static string NormalizeStudyMode(string? mode)
        {
            var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
            return m is "typing" or "mcq" or "mixed" ? m : "flip";
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
