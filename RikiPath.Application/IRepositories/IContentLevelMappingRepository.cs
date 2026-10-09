using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories
{
    public interface IContentLevelMappingRepository : IGenericRepository<ContentLevelMapping>
    {
        /// <summary>Tất cả mapping của một level (kèm Kanji/Vocabulary/GrammarPattern).</summary>
        Task<IReadOnlyList<ContentLevelMapping>> GetByCertificateLevelIdAsync(int certificateLevelId, CancellationToken ct = default);

        /// <summary>Các level mà một Kanji thuộc về (kèm CertificateLevel).</summary>
        Task<IReadOnlyList<ContentLevelMapping>> GetByKanjiIdAsync(int kanjiId, CancellationToken ct = default);

        /// <summary>Các level mà một Vocabulary thuộc về (kèm CertificateLevel).</summary>
        Task<IReadOnlyList<ContentLevelMapping>> GetByVocabularyIdAsync(int vocabularyId, CancellationToken ct = default);

        /// <summary>Các level mà một GrammarPattern thuộc về (kèm CertificateLevel).</summary>
        Task<IReadOnlyList<ContentLevelMapping>> GetByGrammarPatternIdAsync(int grammarPatternId, CancellationToken ct = default);

        /// <summary>Kiểm tra đã gán nội dung này vào level này chưa (truyền đúng MỘT trong 3 Id nội dung).</summary>
        Task<bool> ExistsAsync(int certificateLevelId, int? kanjiId, int? vocabularyId, int? grammarPatternId, CancellationToken ct = default);

        /// <summary>Danh sách Kanji thuộc một level. Mặc định chỉ lấy bản đã Published.</summary>
        Task<IReadOnlyList<Kanji>> GetKanjisByLevelAsync(int certificateLevelId, bool publishedOnly = true, CancellationToken ct = default);

        /// <summary>Danh sách Vocabulary thuộc một level. Mặc định chỉ lấy bản đã Published.</summary>
        Task<IReadOnlyList<Vocabulary>> GetVocabulariesByLevelAsync(int certificateLevelId, bool publishedOnly = true, CancellationToken ct = default);

        /// <summary>Danh sách GrammarPattern thuộc một level. Mặc định chỉ lấy bản đã Published.</summary>
        Task<IReadOnlyList<GrammarPattern>> GetGrammarPatternsByLevelAsync(int certificateLevelId, bool publishedOnly = true, CancellationToken ct = default);
    }
}