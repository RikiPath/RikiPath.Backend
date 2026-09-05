using RikiPath.Application.Common;
using RikiPath.Application.Requests.SharedBanks;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SharedBanks;

namespace RikiPath.Application.IServices
{
    public interface ISharedBankSearchService
    {
        Task<ApiResponse<PagedResult<VocabularyCardResponse>>> SearchVocabularyAsync(
            VocabularySearchFilter filter, CancellationToken cancellationToken);

        Task<ApiResponse<VocabularyCardResponse>> GetVocabularyDetailAsync(int id, CancellationToken cancellationToken);

        Task<ApiResponse<PagedResult<KanjiCardResponse>>> SearchKanjiAsync(
            KanjiSearchFilter filter, CancellationToken cancellationToken);

        Task<ApiResponse<KanjiCardResponse>> GetKanjiDetailAsync(int id, CancellationToken cancellationToken);

        Task<ApiResponse<PagedResult<GrammarCardResponse>>> SearchGrammarAsync(
            GrammarSearchFilter filter, CancellationToken cancellationToken);

        Task<ApiResponse<GrammarCardResponse>> GetGrammarDetailAsync(int id, CancellationToken cancellationToken);
    }
}
