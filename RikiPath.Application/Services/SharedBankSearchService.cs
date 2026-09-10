using RikiPath.Domain.Entities;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.SharedBanks;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SharedBanks;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần thêm cột VocabularyEntry.AudioUrl (string?), KanjiEntry.SinoVietnamese (string?),
    // KanjiEntry.StrokeOrderUrl (string?), KanjiEntry.AudioUrl (string?) nếu chưa có.
    // Cần các hàm repo paged-search (filter + keyword, trả về (items, totalCount)):
    //   IVocabularyEntryRepository.SearchAsync(jlptLevelId, keyword, page, pageSize)
    //   IKanjiEntryRepository.SearchAsync(jlptLevelId, keyword, minStroke, maxStroke, page, pageSize)
    //   IGrammarPointRepository.SearchAsync(jlptLevelId, keyword, page, pageSize)
    public class SharedBankSearchService(IUnitOfWork unitOfWork) : ISharedBankSearchService
    {
        private const int MaxPageSize = 100;

        public async Task<ApiResponse<PagedResult<VocabularyCardResponse>>> SearchVocabularyAsync(
            VocabularySearchFilter filter, CancellationToken cancellationToken)
        {
            try
            {
                Normalize(filter);

                var (items, total) = await unitOfWork.VocabularyEntries.SearchAsync(
                    filter.JlptLevelId, null, filter.Keyword, filter.Page, filter.PageSize);

                var result = new PagedResult<VocabularyCardResponse>
                {
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = total,
                    Items = items.Select(MapVocabulary).ToList(),
                };

                return ApiResponse<PagedResult<VocabularyCardResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResult<VocabularyCardResponse>>.Fail(
                    "Không thể tìm kiếm từ vựng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<VocabularyCardResponse>> GetVocabularyDetailAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                var entry = await unitOfWork.VocabularyEntries.GetByIdAsync(id);
                if (entry is null)
                    return ApiResponse<VocabularyCardResponse>.NotFound($"Không tìm thấy từ vựng Id = {id}.");

                return ApiResponse<VocabularyCardResponse>.Success(MapVocabulary(entry));
            }
            catch (Exception ex)
            {
                return ApiResponse<VocabularyCardResponse>.Fail(
                    "Không thể tải chi tiết từ vựng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<PagedResult<KanjiCardResponse>>> SearchKanjiAsync(
            KanjiSearchFilter filter, CancellationToken cancellationToken)
        {
            try
            {
                Normalize(filter);

                var (items, total) = await unitOfWork.KanjiEntries.SearchAsync(
                    filter.JlptLevelId, null, filter.Keyword, filter.MinStrokeCount, filter.MaxStrokeCount,
                    filter.Page, filter.PageSize);

                var result = new PagedResult<KanjiCardResponse>
                {
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = total,
                    Items = items.Select(MapKanji).ToList(),
                };

                return ApiResponse<PagedResult<KanjiCardResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResult<KanjiCardResponse>>.Fail(
                    "Không thể tìm kiếm Kanji.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<KanjiCardResponse>> GetKanjiDetailAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                var entry = await unitOfWork.KanjiEntries.GetByIdAsync(id);
                if (entry is null)
                    return ApiResponse<KanjiCardResponse>.NotFound($"Không tìm thấy Kanji Id = {id}.");

                return ApiResponse<KanjiCardResponse>.Success(MapKanji(entry));
            }
            catch (Exception ex)
            {
                return ApiResponse<KanjiCardResponse>.Fail(
                    "Không thể tải chi tiết Kanji.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<PagedResult<GrammarCardResponse>>> SearchGrammarAsync(
            GrammarSearchFilter filter, CancellationToken cancellationToken)
        {
            try
            {
                Normalize(filter);

                var (items, total) = await unitOfWork.GrammarPoints.SearchAsync(
                    filter.JlptLevelId, null, filter.Keyword, filter.Page, filter.PageSize);

                var result = new PagedResult<GrammarCardResponse>
                {
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = total,
                    Items = items.Select(MapGrammar).ToList(),
                };

                return ApiResponse<PagedResult<GrammarCardResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResult<GrammarCardResponse>>.Fail(
                    "Không thể tìm kiếm ngữ pháp.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<GrammarCardResponse>> GetGrammarDetailAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                var entry = await unitOfWork.GrammarPoints.GetByIdAsync(id);
                if (entry is null)
                    return ApiResponse<GrammarCardResponse>.NotFound($"Không tìm thấy ngữ pháp Id = {id}.");

                return ApiResponse<GrammarCardResponse>.Success(MapGrammar(entry));
            }
            catch (Exception ex)
            {
                return ApiResponse<GrammarCardResponse>.Fail(
                    "Không thể tải chi tiết ngữ pháp.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private static void Normalize(Requests.SharedBanks.PagedFilterBase filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < 1) filter.PageSize = 20;
            if (filter.PageSize > MaxPageSize) filter.PageSize = MaxPageSize;
        }

        private static VocabularyCardResponse MapVocabulary(VocabularyEntry e) => new()
        {
            Id = e.Id,
            Word = e.Word,
            Reading = e.Reading,
            Meaning = e.Meaning,
            ExampleSentence = e.ExampleSentence,
            AudioUrl = e.AudioUrl,
            JlptLevelName = e.JlptLevel?.Name ?? string.Empty,
        };

        private static KanjiCardResponse MapKanji(KanjiEntry k) => new()
        {
            Id = k.Id,
            Character = k.Character,
            OnYomi = k.OnYomi,
            KunYomi = k.KunYomi,
            SinoVietnamese = k.SinoVietnamese,
            Meaning = k.Meaning,
            StrokeCount = k.StrokeCount,
            StrokeOrderUrl = k.StrokeOrderImageUrl,
            AudioUrl = k.AudioUrl,
            JlptLevelName = k.JlptLevel?.Name ?? string.Empty,
        };

        private static GrammarCardResponse MapGrammar(GrammarPoint g) => new()
        {
            Id = g.Id,
            Title = g.Title,
            Structure = g.Structure,
            Explanation = g.UsageNotes,
            ExampleSentence = g.ExampleSentence,
            JlptLevelName = g.JlptLevel?.Name ?? string.Empty,
        };

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
