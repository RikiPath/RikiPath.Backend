using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Helpers;

public sealed record Sm2Outcome(
    double NewEaseFactor,
    int NewIntervalDays,
    int NewRepetitions,
    int NewLapses,
    SrsLearningState NewState,
    DateTime NextReviewDateUtc,
    bool ShouldRepeatInSession,
    int NormalizedQuality,
    ReviewRating NormalizedRating);

public sealed record SrsSourceLinkItem(
    string SourceType,
    int? SourceId,
    int SourceVersion,
    string? Label,
    int? LessonId,
    DateTime AddedAtUtc);

public sealed record TypingEvaluationResult(
    bool IsMatch,
    string MatchedAgainst,
    string NormalizedUserAnswer);

public sealed record TypingEligibilityResult(
    bool IsEligible,
    bool HasGradableReading,
    bool HasGradableMeaning,
    string PromptKind);

public static partial class SrsSchedulingHelper
{
    public const double MinEaseFactor = 1.3;
    public const double DefaultEaseFactor = 2.5;
    public const int MasteredMinRepetitions = 3;
    public const int MasteredMinIntervalDays = 21;
    public const double MasteredMinEaseFactor = 2.2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> SupportedJlptLevels =
        new(StringComparer.OrdinalIgnoreCase) { "N5", "N4", "N3", "N2" };

    public static readonly TimeZoneInfo StudyTimeZone = ResolveStudyTimeZone();

    private static TimeZoneInfo ResolveStudyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    public static bool TryResolveTimeZone(string? timezoneId, out string canonicalTimezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            canonicalTimezoneId = "Asia/Ho_Chi_Minh";
            return true;
        }

        var trimmed = timezoneId.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _))
        {
            canonicalTimezoneId = trimmed;
            return true;
        }

        canonicalTimezoneId = "Asia/Ho_Chi_Minh";
        return false;
    }

    public static TimeZoneInfo ResolveTimeZoneOrDefault(string? timezoneId)
    {
        if (!string.IsNullOrWhiteSpace(timezoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezoneId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return StudyTimeZone;
    }

    public static DateTime GetStudyDateInZone(DateTime utcNow, string? timezoneId = null)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var tz = ResolveTimeZoneOrDefault(timezoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz).Date;
    }

    public static string? NormalizeSupportedJlptLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;
        var normalized = level.Trim().ToUpperInvariant();
        return SupportedJlptLevels.Contains(normalized) ? normalized : null;
    }

    public static ReviewRating ResolveRating(ReviewRating? explicitRating, int? quality, string? studyMode)
    {
        if (explicitRating.HasValue)
            return explicitRating.Value;

        var q = quality ?? 4;
        var mode = (studyMode ?? string.Empty).Trim().ToLowerInvariant();
        // V4.1 2-button Flashcards contract: 0/1/2 => Again ("Chưa nhớ"), 4 => Good ("Đã nhớ").
        // Legacy callers sending 3 (Hard) or 5 (Easy) are mapped cleanly for backward compatibility.
        if (mode is "flip" or "typing" or "mcq" or "mixed" && q is >= 0 and <= 2)
            return ReviewRating.Again;

        return q switch
        {
            <= 2 => ReviewRating.Again,
            3 => ReviewRating.Hard,
            4 => ReviewRating.Good,
            _ => ReviewRating.Easy,
        };
    }

    public static int MapRatingToQuality(ReviewRating rating) => rating switch
    {
        ReviewRating.Again => 0,
        ReviewRating.Hard => 3,
        ReviewRating.Good => 4,
        ReviewRating.Easy => 5,
        _ => 4,
    };

    public static Sm2Outcome CalculateSm2Outcome(
        ReviewCard card,
        ReviewRating rating,
        DateTime utcNow,
        string? timezoneId = null)
    {
        var quality = MapRatingToQuality(rating);
        var currentEf = card.EaseFactor <= 0 ? DefaultEaseFactor : card.EaseFactor;
        var currentInterval = Math.Max(0, card.IntervalDays);
        var currentReps = Math.Max(0, card.Repetitions);
        var currentLapses = Math.Max(0, card.Lapses);

        double newEf;
        int newInterval;
        int newReps;
        int newLapses = currentLapses;
        bool repeatInSession = false;
        SrsLearningState newState;

        if (rating == ReviewRating.Again)
        {
            newReps = 0;
            newInterval = 1;
            if (currentReps > 0 || card.LastReviewedAt.HasValue)
                newLapses++;
            newEf = Math.Max(MinEaseFactor, Math.Round(currentEf - 0.20, 4));
            newState = currentReps > 0 || card.LastReviewedAt.HasValue
                ? SrsLearningState.Relearning
                : SrsLearningState.Learning;
            repeatInSession = true;
        }
        else if (rating == ReviewRating.Hard)
        {
            newReps = Math.Max(1, currentReps + 1);
            newInterval = currentReps switch
            {
                0 => 1,
                1 => 3,
                _ => Math.Max(currentInterval + 1, (int)Math.Round(currentInterval * 1.2)),
            };
            newEf = Math.Max(MinEaseFactor, Math.Round(currentEf - 0.14, 4));
            newState = ResolvePositiveLearningState(newReps, newInterval, newEf);
        }
        else if (rating == ReviewRating.Good)
        {
            newReps = currentReps + 1;
            newInterval = currentReps switch
            {
                0 => 1,
                1 => 6,
                _ => Math.Max(currentInterval + 1, (int)Math.Round(currentInterval * currentEf)),
            };
            newEf = Math.Max(MinEaseFactor, Math.Round(currentEf + 0.00, 4));
            newState = ResolvePositiveLearningState(newReps, newInterval, newEf);
        }
        else // Easy
        {
            newReps = currentReps + 1;
            var goodBase = currentReps switch
            {
                0 => 1,
                1 => 6,
                _ => Math.Max(currentInterval + 1, (int)Math.Round(currentInterval * currentEf)),
            };
            newInterval = currentReps == 0
                ? 4
                : Math.Max(goodBase + 1, (int)Math.Round(goodBase * 1.3));
            newEf = Math.Max(MinEaseFactor, Math.Round(currentEf + 0.15, 4));
            newState = ResolvePositiveLearningState(newReps, newInterval, newEf);
        }

        newInterval = Math.Clamp(newInterval, 1, 3650);
        var studyToday = GetStudyDateInZone(utcNow, timezoneId);
        var nextDateUtc = DateTime.SpecifyKind(studyToday.AddDays(newInterval), DateTimeKind.Utc);

        return new Sm2Outcome(
            NewEaseFactor: Math.Round(newEf, 2),
            NewIntervalDays: newInterval,
            NewRepetitions: newReps,
            NewLapses: newLapses,
            NewState: newState,
            NextReviewDateUtc: nextDateUtc,
            ShouldRepeatInSession: repeatInSession,
            NormalizedQuality: quality,
            NormalizedRating: rating);
    }

    /// <summary>
    /// Selects actionable due cards enforcing DailyNewLimit as a strict hard cap on New cards,
    /// while treating DailyReviewLimit ("Mục tiêu ôn mỗi ngày") as a flexible soft goal so learners
    /// are never locked out of reviewing overdue or active cards once they meet their daily goal.
    /// </summary>
    public static List<ReviewCard> SelectActionableDueCards(
        IEnumerable<ReviewCard> dueNowCards,
        int dailyNewLimit,
        int dailyReviewLimit,
        int reviewedNewToday,
        int reviewedTotalToday)
    {
        _ = dailyReviewLimit;
        _ = reviewedTotalToday;

        var ordered = dueNowCards
            .Where(c => !c.IsDeleted && c.Status == SrsCardStatus.Active && c.Mode == ReviewMode.Recognition)
            .OrderBy(c => c.LearningState == SrsLearningState.Relearning ? 0
                : c.LearningState == SrsLearningState.Learning ? 1
                : c.LearningState == SrsLearningState.New ? 2 : 3)
            .ThenBy(c => c.NextReviewDate)
            .ThenBy(c => c.Id);

        var remainingNewBudget = Math.Max(0, dailyNewLimit - reviewedNewToday);

        var selected = new List<ReviewCard>();
        var newPicked = 0;
        foreach (var card in ordered)
        {
            if (card.LearningState == SrsLearningState.New)
            {
                if (newPicked >= remainingNewBudget)
                    continue;
                newPicked++;
            }

            selected.Add(card);
        }

        return selected;
    }

    private static SrsLearningState ResolvePositiveLearningState(int reps, int intervalDays, double easeFactor)
    {
        if (reps >= MasteredMinRepetitions && intervalDays >= MasteredMinIntervalDays && easeFactor >= MasteredMinEaseFactor)
            return SrsLearningState.Mastered;

        return reps >= 2 ? SrsLearningState.Review : SrsLearningState.Learning;
    }

    public static string BuildNormalizedContentKey(
        string cardType,
        int? vocabularyId,
        int? kanjiId,
        int? grammarPatternId,
        int? noteEntryId,
        int? templateId,
        string frontText,
        string backTextOrMeaning)
    {
        if (vocabularyId.HasValue) return $"vocabulary:{vocabularyId.Value}";
        if (kanjiId.HasValue) return $"kanji:{kanjiId.Value}";
        if (grammarPatternId.HasValue) return $"grammar:{grammarPatternId.Value}";
        if (templateId.HasValue) return $"template:{templateId.Value}";
        if (noteEntryId.HasValue) return $"noteentry:{noteEntryId.Value}";

        var type = string.IsNullOrWhiteSpace(cardType) ? "Custom" : cardType.Trim().ToLowerInvariant();
        var front = NormalizeForComparison(frontText);
        var meaning = NormalizeMeaningKey(backTextOrMeaning);
        var raw = $"{type}:{front}|{meaning}";
        if (raw.Length <= 260) return raw;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return $"{type}:sha256:{hash[..32]}";
    }

    public static List<SrsSourceLinkItem> DeserializeSourceLinks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<SrsSourceLinkItem>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string MergeSourceLinkJson(
        string? existingJson,
        string sourceType,
        int? sourceId,
        int sourceVersion,
        string? label,
        int? lessonId,
        DateTime utcNow)
    {
        var items = DeserializeSourceLinks(existingJson);
        var normalizedType = string.IsNullOrWhiteSpace(sourceType) ? "Manual" : sourceType.Trim();
        var exists = items.Any(item =>
            string.Equals(item.SourceType, normalizedType, StringComparison.OrdinalIgnoreCase)
            && item.SourceId == sourceId
            && item.LessonId == lessonId);

        if (!exists)
        {
            items.Add(new SrsSourceLinkItem(
                normalizedType,
                sourceId,
                Math.Max(1, sourceVersion),
                string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                lessonId,
                utcNow));
        }

        if (items.Count > 15)
            items = items.TakeLast(15).ToList();

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string? SerializeStringList(IEnumerable<string>? values)
    {
        if (values is null) return null;
        var clean = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean, JsonOptions);
    }

    public static TypingEligibilityResult EvaluateTypingEligibility(
        ReviewCard card,
        string? term,
        string? reading,
        string? meaning,
        string? backText)
    {
        var promptTerm = !string.IsNullOrWhiteSpace(term) ? term.Trim() : (card.FrontText?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(promptTerm))
            return new TypingEligibilityResult(false, false, false, "none");

        var normPrompt = NormalizeForComparison(promptTerm);
        var normPromptMeaning = NormalizeMeaningKey(promptTerm);

        var readingCandidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(reading))
            readingCandidates.AddRange(SplitMultiValueField(reading));
        if (!string.IsNullOrWhiteSpace(card.Reading))
            readingCandidates.AddRange(SplitMultiValueField(card.Reading));
        readingCandidates.AddRange(DeserializeStringList(card.AcceptedReadingsJson));

        var hasGradableReading = readingCandidates.Any(r =>
        {
            var norm = NormalizeForComparison(r);
            return !string.IsNullOrEmpty(norm)
                && norm.Length <= 80
                && !string.Equals(norm, normPrompt, StringComparison.Ordinal);
        });

        var meaningCandidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(meaning))
        {
            meaningCandidates.Add(meaning);
            meaningCandidates.AddRange(SplitMultiValueField(meaning));
        }
        if (!string.IsNullOrWhiteSpace(backText))
        {
            meaningCandidates.Add(backText);
            meaningCandidates.AddRange(SplitMultiValueField(backText));
        }
        if (!string.IsNullOrWhiteSpace(card.Meaning))
        {
            meaningCandidates.Add(card.Meaning);
            meaningCandidates.AddRange(SplitMultiValueField(card.Meaning));
        }
        if (!string.IsNullOrWhiteSpace(card.BackText))
        {
            meaningCandidates.Add(card.BackText);
            meaningCandidates.AddRange(SplitMultiValueField(card.BackText));
        }
        meaningCandidates.AddRange(DeserializeStringList(card.SynonymsJson));

        var hasGradableMeaning = meaningCandidates.Any(m =>
        {
            var norm = NormalizeMeaningKey(m);
            return !string.IsNullOrEmpty(norm)
                && norm.Length <= 120
                && !string.Equals(norm, normPromptMeaning, StringComparison.Ordinal);
        });

        if (!hasGradableReading && !hasGradableMeaning)
            return new TypingEligibilityResult(false, false, false, "none");

        var promptKind = hasGradableReading && hasGradableMeaning
            ? "reading_or_meaning"
            : hasGradableMeaning
                ? "meaning_only"
                : "reading_only";

        return new TypingEligibilityResult(true, hasGradableReading, hasGradableMeaning, promptKind);
    }

    public static TypingEvaluationResult EvaluateTypingAnswer(ReviewCard card, string? userAnswer)
    {
        var normalizedInput = NormalizeForComparison(userAnswer);
        if (string.IsNullOrEmpty(normalizedInput))
            return new TypingEvaluationResult(false, string.Empty, string.Empty);

        var normPrompt = NormalizeForComparison(card.FrontText);
        var normPromptMeaning = NormalizeMeaningKey(card.FrontText);
        var inputRomaji = ConvertKanaToRomaji(normalizedInput);
        var canonicalInputRomaji = NormalizeRomajiKey(inputRomaji);

        var readingCandidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(card.Reading))
        {
            readingCandidates.Add(card.Reading);
            readingCandidates.AddRange(SplitMultiValueField(card.Reading));
        }
        readingCandidates.AddRange(DeserializeStringList(card.AcceptedReadingsJson));

        var rawUserAnswer = (userAnswer ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim();
        var userRomajiDirect = IsAsciiRomajiInput(rawUserAnswer) ? NormalizeRomajiKey(rawUserAnswer) : string.Empty;

        foreach (var candidate in readingCandidates)
        {
            var strippedCandidate = ParentheticalRegex().Replace(candidate, string.Empty).Trim();
            foreach (var variant in new[] { candidate, strippedCandidate })
            {
                var normCand = NormalizeForComparison(variant);
                if (string.IsNullOrEmpty(normCand)) continue;
                // Do not let typing the visible question prompt itself count as a reading match
                if (!string.IsNullOrEmpty(normPrompt) && string.Equals(normCand, normPrompt, StringComparison.Ordinal))
                    continue;

                if (string.Equals(normalizedInput, normCand, StringComparison.Ordinal))
                    return new TypingEvaluationResult(true, candidate, normalizedInput);

                var candRomaji = ConvertKanaToRomaji(normCand);
                if (!string.IsNullOrEmpty(candRomaji))
                {
                    if (string.Equals(inputRomaji, candRomaji, StringComparison.Ordinal))
                        return new TypingEvaluationResult(true, candidate, normalizedInput);

                    var canonicalCandRomaji = NormalizeRomajiKey(candRomaji);
                    if (!string.IsNullOrEmpty(canonicalCandRomaji)
                        && (string.Equals(canonicalInputRomaji, canonicalCandRomaji, StringComparison.Ordinal)
                            || (!string.IsNullOrEmpty(userRomajiDirect) && string.Equals(userRomajiDirect, canonicalCandRomaji, StringComparison.Ordinal))))
                    {
                        return new TypingEvaluationResult(true, candidate, normalizedInput);
                    }
                }
            }
        }

        var meaningCandidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(card.Meaning))
        {
            meaningCandidates.Add(card.Meaning);
            meaningCandidates.AddRange(SplitMultiValueField(card.Meaning));
        }
        if (!string.IsNullOrWhiteSpace(card.BackText))
        {
            meaningCandidates.Add(card.BackText);
            meaningCandidates.AddRange(SplitMultiValueField(card.BackText));
        }
        meaningCandidates.AddRange(DeserializeStringList(card.SynonymsJson));

        var normUserMeaning = NormalizeMeaningKey(userAnswer);
        if (!string.IsNullOrEmpty(normUserMeaning))
        {
            foreach (var candidate in meaningCandidates)
            {
                var strippedCandidate = ParentheticalRegex().Replace(candidate, string.Empty).Trim();
                foreach (var variant in new[] { candidate, strippedCandidate })
                {
                    var normMeaning = NormalizeMeaningKey(variant);
                    if (string.IsNullOrEmpty(normMeaning)) continue;
                    if (!string.IsNullOrEmpty(normPromptMeaning) && string.Equals(normMeaning, normPromptMeaning, StringComparison.Ordinal))
                        continue;

                    if (string.Equals(normUserMeaning, normMeaning, StringComparison.Ordinal))
                    {
                        return new TypingEvaluationResult(true, candidate, normalizedInput);
                    }
                }
            }
        }

        return new TypingEvaluationResult(false, string.Empty, normalizedInput);
    }

    private static bool IsAsciiRomajiInput(string input)
        => !string.IsNullOrEmpty(input) && input.All(ch => ch <= 0x017F);

    private static string NormalizeRomajiKey(string? romaji)
    {
        if (string.IsNullOrWhiteSpace(romaji)) return string.Empty;
        var s = romaji.Trim().ToLowerInvariant();
        s = PunctuationRegex().Replace(s, string.Empty);
        s = s
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("\u2019", string.Empty, StringComparison.Ordinal)
            .Replace("ō", "ou", StringComparison.Ordinal)
            .Replace("ū", "uu", StringComparison.Ordinal)
            .Replace("ā", "aa", StringComparison.Ordinal)
            .Replace("ī", "ii", StringComparison.Ordinal)
            .Replace("ē", "ee", StringComparison.Ordinal)
            .Replace("sya", "sha", StringComparison.Ordinal)
            .Replace("syu", "shu", StringComparison.Ordinal)
            .Replace("syo", "sho", StringComparison.Ordinal)
            .Replace("tya", "cha", StringComparison.Ordinal)
            .Replace("tyu", "chu", StringComparison.Ordinal)
            .Replace("tyo", "cho", StringComparison.Ordinal)
            .Replace("cya", "cha", StringComparison.Ordinal)
            .Replace("cyu", "chu", StringComparison.Ordinal)
            .Replace("cyo", "cho", StringComparison.Ordinal)
            .Replace("jya", "ja", StringComparison.Ordinal)
            .Replace("jyu", "ju", StringComparison.Ordinal)
            .Replace("jyo", "jo", StringComparison.Ordinal)
            .Replace("zya", "ja", StringComparison.Ordinal)
            .Replace("zyu", "ju", StringComparison.Ordinal)
            .Replace("zyo", "jo", StringComparison.Ordinal)
            .Replace("dya", "ja", StringComparison.Ordinal)
            .Replace("dyu", "ju", StringComparison.Ordinal)
            .Replace("dyo", "jo", StringComparison.Ordinal)
            .Replace("si", "shi", StringComparison.Ordinal)
            .Replace("ti", "chi", StringComparison.Ordinal)
            .Replace("tu", "tsu", StringComparison.Ordinal)
            .Replace("hu", "fu", StringComparison.Ordinal)
            .Replace("zi", "ji", StringComparison.Ordinal)
            .Replace("di", "ji", StringComparison.Ordinal)
            .Replace("du", "zu", StringComparison.Ordinal)
            .Replace("mb", "nb", StringComparison.Ordinal)
            .Replace("mp", "np", StringComparison.Ordinal)
            .Replace("mm", "nm", StringComparison.Ordinal)
            .Replace("nn", "n", StringComparison.Ordinal);
        return s;
    }

    [GeneratedRegex(@"\([^)]*\)|（[^）]*）")]
    private static partial Regex ParentheticalRegex();

    private static IEnumerable<string> SplitMultiValueField(string raw)
        => raw.Split(['/', ';', '、', '；', ',', '|', '\u00b7', '\u30fb', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string NormalizeForComparison(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Normalize(NormalizationForm.FormKC).Trim();
        s = PunctuationRegex().Replace(s, string.Empty);
        s = MultiSpaceRegex().Replace(s, string.Empty).ToLowerInvariant();
        return ConvertKatakanaToHiragana(s);
    }

    public static string NormalizeMeaningKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        s = PunctuationRegex().Replace(s, " ");
        s = MultiSpaceRegex().Replace(s, " ").Trim();
        return s;
    }

    private static string ConvertKatakanaToHiragana(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch is >= '\u30A1' and <= '\u30F6')
                sb.Append((char)(ch - 0x60));
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string ConvertKanaToRomaji(string hiraganaInput)
    {
        if (string.IsNullOrEmpty(hiraganaInput)) return string.Empty;
        var s = ConvertKatakanaToHiragana(hiraganaInput);
        var sb = new StringBuilder(s.Length * 2);

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == 'っ' && i + 1 < s.Length)
            {
                var nextRomaji = MapKanaToken(s, i + 1, out _);
                if (!string.IsNullOrEmpty(nextRomaji))
                {
                    sb.Append(nextRomaji[0]);
                    continue;
                }
            }

            if (ch == 'ー' && sb.Length > 0)
            {
                sb.Append(sb[^1]);
                continue;
            }

            var mapped = MapKanaToken(s, i, out var consumed);
            sb.Append(mapped);
            i += consumed - 1;
        }

        return sb.ToString();
    }

    private static string MapKanaToken(string s, int index, out int consumed)
    {
        if (index + 1 < s.Length && TwoCharKanaRomaji.TryGetValue(s.Substring(index, 2), out var pair))
        {
            consumed = 2;
            return pair;
        }

        consumed = 1;
        var one = s.Substring(index, 1);
        return SingleKanaRomaji.TryGetValue(one, out var single) ? single : one;
    }

    [GeneratedRegex(@"[。、．，.,;:！？!?・･()（）\[\]「」『』~〜\-‐\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    private static readonly Dictionary<string, string> TwoCharKanaRomaji = new(StringComparer.Ordinal)
    {
        ["きゃ"] = "kya", ["きゅ"] = "kyu", ["きょ"] = "kyo",
        ["しゃ"] = "sha", ["しゅ"] = "shu", ["しょ"] = "sho", ["しぇ"] = "she",
        ["ちゃ"] = "cha", ["ちゅ"] = "chu", ["ちょ"] = "cho", ["ちぇ"] = "che",
        ["にゃ"] = "nya", ["にゅ"] = "nyu", ["にょ"] = "nyo",
        ["ひゃ"] = "hya", ["ひゅ"] = "hyu", ["ひょ"] = "hyo",
        ["みゃ"] = "mya", ["みゅ"] = "myu", ["みょ"] = "myo",
        ["りゃ"] = "rya", ["りゅ"] = "ryu", ["りょ"] = "ryo",
        ["ぎゃ"] = "gya", ["ぎゅ"] = "gyu", ["ぎょ"] = "gyo",
        ["じゃ"] = "ja", ["じゅ"] = "ju", ["じょ"] = "jo", ["じぇ"] = "je",
        ["びゃ"] = "bya", ["びゅ"] = "byu", ["びょ"] = "byo",
        ["ぴゃ"] = "pya", ["ぴゅ"] = "pyu", ["ぴょ"] = "pyo",
        ["ふぁ"] = "fa", ["ふぃ"] = "fi", ["ふぇ"] = "fe", ["ふぉ"] = "fo",
        ["てぃ"] = "ti", ["でぃ"] = "di",
        ["うぃ"] = "wi", ["うぇ"] = "we", ["うぉ"] = "wo",
    };

    private static readonly Dictionary<string, string> SingleKanaRomaji = new(StringComparer.Ordinal)
    {
        ["あ"] = "a", ["い"] = "i", ["う"] = "u", ["え"] = "e", ["お"] = "o",
        ["か"] = "ka", ["き"] = "ki", ["く"] = "ku", ["け"] = "ke", ["こ"] = "ko",
        ["さ"] = "sa", ["し"] = "shi", ["す"] = "su", ["せ"] = "se", ["そ"] = "so",
        ["た"] = "ta", ["ち"] = "chi", ["つ"] = "tsu", ["て"] = "te", ["と"] = "to",
        ["な"] = "na", ["に"] = "ni", ["ぬ"] = "nu", ["ね"] = "ne", ["の"] = "no",
        ["は"] = "ha", ["ひ"] = "hi", ["ふ"] = "fu", ["へ"] = "he", ["ほ"] = "ho",
        ["ま"] = "ma", ["み"] = "mi", ["む"] = "mu", ["め"] = "me", ["も"] = "mo",
        ["や"] = "ya", ["ゆ"] = "yu", ["よ"] = "yo",
        ["ら"] = "ra", ["り"] = "ri", ["る"] = "ru", ["れ"] = "re", ["ろ"] = "ro",
        ["わ"] = "wa", ["を"] = "o", ["ん"] = "n",
        ["が"] = "ga", ["ぎ"] = "gi", ["ぐ"] = "gu", ["げ"] = "ge", ["ご"] = "go",
        ["ざ"] = "za", ["じ"] = "ji", ["ず"] = "zu", ["ぜ"] = "ze", ["ぞ"] = "zo",
        ["だ"] = "da", ["ぢ"] = "ji", ["づ"] = "zu", ["で"] = "de", ["ど"] = "do",
        ["ば"] = "ba", ["び"] = "bi", ["ぶ"] = "bu", ["べ"] = "be", ["ぼ"] = "bo",
        ["ぱ"] = "pa", ["ぴ"] = "pi", ["ぷ"] = "pu", ["ぺ"] = "pe", ["ぽ"] = "po",
        ["ぁ"] = "a", ["ぃ"] = "i", ["ぅ"] = "u", ["ぇ"] = "e", ["ぉ"] = "o",
        ["ゃ"] = "ya", ["ゅ"] = "yu", ["ょ"] = "yo",
    };

    public static string FormatIntervalLabel(int days) => days switch
    {
        <= 1 => "1 ngày",
        < 30 => $"{days} ngày",
        < 365 => $"{Math.Round(days / 30.0, 1):0.#} tháng",
        _ => $"{Math.Round(days / 365.0, 1):0.#} năm",
    };
}
