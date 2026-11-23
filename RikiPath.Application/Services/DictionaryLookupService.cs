using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RikiPath.Application.DTOs.Dictionary;
using RikiPath.Application.Exceptions;
using RikiPath.Application.Helpers;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public class DictionaryLookupService(
    IUnitOfWork unitOfWork,
    IMemoryCache memoryCache,
    IAiTranslationClient? translationClient = null,
    ILogger<DictionaryLookupService>? logger = null) : IDictionaryLookupService
{
    public async Task<ApiResponse<DictionaryLookupResponse>> LookupAsync(
        int? userId,
        string? query,
        string? jlptLevel,
        CancellationToken cancellationToken = default)
    {
        var clean = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return ApiResponse<DictionaryLookupResponse>.Success(new DictionaryLookupResponse { Query = string.Empty });

        string? levelCode = null;
        if (!string.IsNullOrWhiteSpace(jlptLevel))
        {
            levelCode = SrsSchedulingHelper.NormalizeSupportedJlptLevel(jlptLevel);
            if (levelCode is null)
                return ApiResponse<DictionaryLookupResponse>.Fail("Cấp độ JLPT chỉ hỗ trợ N5, N4, N3, N2.", HttpStatusCode.BadRequest);
        }

        var lower = clean.ToLower();
        var vocabMatches = await unitOfWork.Vocabularies.Query()
            .AsNoTracking()
            .Where(v => !v.IsDeleted && v.Status == ContentStatus.Published
                && (levelCode == null || (v.ContentLevelMappings != null && v.ContentLevelMappings.Any(m => !m.IsDeleted && m.CertificateLevel.Code == levelCode)))
                && (v.Word.ToLower().Contains(lower) || v.Reading.ToLower().Contains(lower) || v.Meaning.ToLower().Contains(lower)))
            .Include(v => v.ContentLevelMappings!.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.CertificateLevel)
            .OrderBy(v => v.Word == clean ? 0 : 1)
            .ThenBy(v => v.Word.Length)
            .Take(8)
            .ToListAsync(cancellationToken);

        var kanjiMatches = await unitOfWork.Kanjis.Query()
            .AsNoTracking()
            .Where(k => !k.IsDeleted && k.Status == ContentStatus.Published
                && (levelCode == null || (k.ContentLevelMappings != null && k.ContentLevelMappings.Any(m => !m.IsDeleted && m.CertificateLevel.Code == levelCode)))
                && (k.Character.Contains(clean) || k.Meaning.ToLower().Contains(lower) || (k.SinoVietnamese != null && k.SinoVietnamese.ToLower().Contains(lower))))
            .Include(k => k.ContentLevelMappings!.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.CertificateLevel)
            .OrderBy(k => k.Character == clean ? 0 : 1)
            .Take(6)
            .ToListAsync(cancellationToken);

        var grammarMatches = await unitOfWork.GrammarPatterns.Query()
            .AsNoTracking()
            .Where(g => !g.IsDeleted && g.Status == ContentStatus.Published
                && (levelCode == null || (g.ContentLevelMappings != null && g.ContentLevelMappings.Any(m => !m.IsDeleted && m.CertificateLevel.Code == levelCode)))
                && (g.Title.ToLower().Contains(lower) || g.Structure.ToLower().Contains(lower) || (g.UsageNotes != null && g.UsageNotes.ToLower().Contains(lower))))
            .Include(g => g.ContentLevelMappings!.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.CertificateLevel)
            .OrderBy(g => g.Title == clean ? 0 : 1)
            .Take(6)
            .ToListAsync(cancellationToken);

        Dictionary<int, int> vocabCards = [];
        Dictionary<int, int> kanjiCards = [];
        Dictionary<int, int> grammarCards = [];
        if (userId.HasValue)
        {
            var userCards = await unitOfWork.ReviewCards.GetAllForUserAsync(userId.Value, cancellationToken);
            foreach (var c in userCards.Where(c => !c.IsDeleted && c.Status != SrsCardStatus.Archived))
            {
                if (c.VocabularyId.HasValue) vocabCards[c.VocabularyId.Value] = c.Id;
                if (c.KanjiId.HasValue) kanjiCards[c.KanjiId.Value] = c.Id;
                if (c.GrammarPatternId.HasValue) grammarCards[c.GrammarPatternId.Value] = c.Id;
            }
        }

        var items = new List<DictionaryLookupItemResponse>();
        foreach (var v in vocabMatches)
        {
            var lvl = v.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null);
            items.Add(new DictionaryLookupItemResponse
            {
                EntryType = "Vocabulary",
                EntryId = v.Id,
                Term = v.Word,
                Reading = v.Reading,
                Meaning = v.Meaning,
                ExampleSentence = v.ExampleSentence,
                ExampleMeaning = v.ExampleSentenceMeaning,
                JlptLevel = lvl,
                AudioUrl = v.AudioUrl,
                AlreadyEnrolledInSrs = vocabCards.ContainsKey(v.Id),
                ExistingReviewCardId = vocabCards.TryGetValue(v.Id, out var cid) ? cid : null,
            });
        }

        foreach (var k in kanjiMatches)
        {
            var lvl = k.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null);
            items.Add(new DictionaryLookupItemResponse
            {
                EntryType = "Kanji",
                EntryId = k.Id,
                Term = k.Character,
                Reading = string.Join(" · ", new[] { k.OnYomi, k.KunYomi }.Where(x => !string.IsNullOrWhiteSpace(x))),
                Meaning = k.Meaning,
                SinoVietnamese = k.SinoVietnamese,
                JlptLevel = lvl,
                AudioUrl = k.AudioUrl,
                AlreadyEnrolledInSrs = kanjiCards.ContainsKey(k.Id),
                ExistingReviewCardId = kanjiCards.TryGetValue(k.Id, out var cid) ? cid : null,
            });
        }

        foreach (var g in grammarMatches)
        {
            var lvl = g.ContentLevelMappings?.Select(m => SrsSchedulingHelper.NormalizeSupportedJlptLevel(m.CertificateLevel?.Code)).FirstOrDefault(x => x is not null);
            items.Add(new DictionaryLookupItemResponse
            {
                EntryType = "Grammar",
                EntryId = g.Id,
                Term = g.Title,
                Reading = g.Structure,
                Meaning = string.IsNullOrWhiteSpace(g.UsageNotes) ? g.Structure : g.UsageNotes,
                ExampleSentence = g.ExampleSentence,
                ExampleMeaning = g.ExampleSentenceMeaning,
                JlptLevel = lvl,
                AlreadyEnrolledInSrs = grammarCards.ContainsKey(g.Id),
                ExistingReviewCardId = grammarCards.TryGetValue(g.Id, out var cid) ? cid : null,
            });
        }

        return ApiResponse<DictionaryLookupResponse>.Success(new DictionaryLookupResponse
        {
            Query = clean,
            Items = items,
        });
    }

    public async Task<ApiResponse<TranslateSelectionResponse>> TranslateAsync(
        int userId,
        TranslateSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var cleanText = (request?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanText))
            return ApiResponse<TranslateSelectionResponse>.Fail("Vui lòng chọn đoạn văn bản cần dịch.");

        var targetLang = string.Equals(request?.TargetLanguage, "ja", StringComparison.OrdinalIgnoreCase) ? "ja" : "vi";
        var normContext = (request?.ContextSentence ?? string.Empty).Trim();

        // 1. Approved Bank exact match first (never invent data, prefer canonical curriculum definitions)
        if (targetLang == "vi")
        {
            var exactVocab = await unitOfWork.Vocabularies.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(v => !v.IsDeleted && v.Status == ContentStatus.Published
                    && (v.Word == cleanText || v.Reading == cleanText), cancellationToken);
            if (exactVocab is not null)
            {
                return ApiResponse<TranslateSelectionResponse>.Success(new TranslateSelectionResponse
                {
                    Query = cleanText,
                    Translation = exactVocab.Meaning,
                    Reading = exactVocab.Reading,
                    Source = "ApprovedBank",
                    SuggestedCardType = "Vocabulary",
                    BankEntryId = exactVocab.Id,
                    ExampleSentence = exactVocab.ExampleSentence,
                    ExampleMeaning = exactVocab.ExampleSentenceMeaning,
                });
            }

            if (cleanText.Length <= 2)
            {
                var exactKanji = await unitOfWork.Kanjis.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => !k.IsDeleted && k.Status == ContentStatus.Published
                        && k.Character == cleanText, cancellationToken);
                if (exactKanji is not null)
                {
                    return ApiResponse<TranslateSelectionResponse>.Success(new TranslateSelectionResponse
                    {
                        Query = cleanText,
                        Translation = exactKanji.Meaning,
                        Reading = string.Join(" · ", new[] { exactKanji.OnYomi, exactKanji.KunYomi, exactKanji.SinoVietnamese }.Where(x => !string.IsNullOrWhiteSpace(x))),
                        GrammarNote = !string.IsNullOrWhiteSpace(exactKanji.SinoVietnamese) ? $"Hán Việt: {exactKanji.SinoVietnamese}" : null,
                        Source = "ApprovedBank",
                        SuggestedCardType = "Kanji",
                        BankEntryId = exactKanji.Id,
                    });
                }
            }

            var exactGrammar = await unitOfWork.GrammarPatterns.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(g => !g.IsDeleted && g.Status == ContentStatus.Published
                    && g.Title == cleanText, cancellationToken);
            if (exactGrammar is not null)
            {
                return ApiResponse<TranslateSelectionResponse>.Success(new TranslateSelectionResponse
                {
                    Query = cleanText,
                    Translation = string.IsNullOrWhiteSpace(exactGrammar.UsageNotes) ? exactGrammar.Structure : exactGrammar.UsageNotes,
                    Reading = exactGrammar.Structure,
                    Source = "ApprovedBank",
                    SuggestedCardType = "Grammar",
                    BankEntryId = exactGrammar.Id,
                    ExampleSentence = exactGrammar.ExampleSentence,
                    ExampleMeaning = exactGrammar.ExampleSentenceMeaning,
                });
            }
        }

        // 2. Check 24h IMemoryCache
        var cacheKey = $"dict-translate:{targetLang}:{cleanText}:{normContext}";
        if (memoryCache.TryGetValue<TranslateSelectionResponse>(cacheKey, out var cached) && cached is not null)
        {
            return ApiResponse<TranslateSelectionResponse>.Success(new TranslateSelectionResponse
            {
                Query = cached.Query,
                Translation = cached.Translation,
                Reading = cached.Reading,
                GrammarNote = cached.GrammarNote,
                Source = cached.Source,
                FromCache = true,
                SuggestedCardType = cached.SuggestedCardType,
                Breakdown = cached.Breakdown,
            });
        }

        // 3. Real OpenAI translation (honest error if unconfigured, rate-limited, or unavailable)
        if (translationClient is null)
        {
            return ApiResponse<TranslateSelectionResponse>.Fail(
                "Dịch vụ dịch AI chưa được kích hoạt.",
                HttpStatusCode.ServiceUnavailable);
        }

        try
        {
            var aiResult = await translationClient.TranslateJapaneseAsync(
                userId, cleanText, normContext, targetLang, cancellationToken);

            var built = new TranslateSelectionResponse
            {
                Query = cleanText,
                Translation = aiResult.Translation.Trim(),
                Reading = aiResult.Reading,
                GrammarNote = aiResult.GrammarNote,
                Source = "AiTranslation",
                FromCache = false,
                Breakdown = (aiResult.Breakdown ?? [])
                    .Select(b => new TranslateBreakdownItem
                    {
                        Surface = b.Surface,
                        Reading = b.Reading,
                        Meaning = b.Meaning,
                    }).ToList(),
            };

            memoryCache.Set(cacheKey, built, TimeSpan.FromHours(24));
            return ApiResponse<TranslateSelectionResponse>.Success(built);
        }
        catch (AiQuotaExceededException ex)
        {
            return ApiResponse<TranslateSelectionResponse>.Fail(ex.Message, HttpStatusCode.TooManyRequests);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "AI translation failed for user {UserId}", userId);
            return ApiResponse<TranslateSelectionResponse>.Fail(
                "Dịch vụ dịch tạm thời gián đoạn. Bạn vẫn có thể tra cứu trong Ngân hàng đã duyệt hoặc thử lại sau.",
                HttpStatusCode.ServiceUnavailable);
        }
    }
}
