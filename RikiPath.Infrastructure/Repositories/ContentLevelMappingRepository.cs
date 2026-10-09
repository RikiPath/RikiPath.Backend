using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class ContentLevelMappingRepository(AppDbContext context)
        : GenericRepository<ContentLevelMapping>(context), IContentLevelMappingRepository
    {
        public async Task<IReadOnlyList<ContentLevelMapping>> GetByCertificateLevelIdAsync(int certificateLevelId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CertificateLevelId == certificateLevelId)
                .Include(x => x.Kanji)
                .Include(x => x.Vocabulary)
                .Include(x => x.GrammarPattern)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ContentLevelMapping>> GetByKanjiIdAsync(int kanjiId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.KanjiId == kanjiId)
                .Include(x => x.CertificateLevel)
                .OrderBy(x => x.CertificateLevel.SortOrder)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ContentLevelMapping>> GetByVocabularyIdAsync(int vocabularyId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.VocabularyId == vocabularyId)
                .Include(x => x.CertificateLevel)
                .OrderBy(x => x.CertificateLevel.SortOrder)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ContentLevelMapping>> GetByGrammarPatternIdAsync(int grammarPatternId, CancellationToken ct = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.GrammarPatternId == grammarPatternId)
                .Include(x => x.CertificateLevel)
                .OrderBy(x => x.CertificateLevel.SortOrder)
                .ToListAsync(ct);
        }

        public async Task<bool> ExistsAsync(int certificateLevelId, int? kanjiId, int? vocabularyId, int? grammarPatternId, CancellationToken ct = default)
        {
            // So sánh với null được EF dịch thành "IS NULL" nên truyền đúng 1 Id nội dung, 2 Id còn lại để null.
            return await _dbSet.AnyAsync(x =>
                !x.IsDeleted
                && x.CertificateLevelId == certificateLevelId
                && x.KanjiId == kanjiId
                && x.VocabularyId == vocabularyId
                && x.GrammarPatternId == grammarPatternId, ct);
        }

        public async Task<IReadOnlyList<Kanji>> GetKanjisByLevelAsync(int certificateLevelId, bool publishedOnly = true, CancellationToken ct = default)
        {
            var query = _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CertificateLevelId == certificateLevelId && x.KanjiId != null)
                .Select(x => x.Kanji!)
                .Where(k => !k.IsDeleted);

            if (publishedOnly)
                query = query.Where(k => k.Status == ContentStatus.Published);

            return await query.OrderBy(k => k.Id).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Vocabulary>> GetVocabulariesByLevelAsync(int certificateLevelId, bool publishedOnly = true, CancellationToken ct = default)
        {
            var query = _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CertificateLevelId == certificateLevelId && x.VocabularyId != null)
                .Select(x => x.Vocabulary!)
                .Where(v => !v.IsDeleted);

            if (publishedOnly)
                query = query.Where(v => v.Status == ContentStatus.Published);

            return await query.OrderBy(v => v.Id).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<GrammarPattern>> GetGrammarPatternsByLevelAsync(int certificateLevelId, bool publishedOnly = true, CancellationToken ct = default)
        {
            var query = _dbSet.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CertificateLevelId == certificateLevelId && x.GrammarPatternId != null)
                .Select(x => x.GrammarPattern!)
                .Where(g => !g.IsDeleted);

            if (publishedOnly)
                query = query.Where(g => g.Status == ContentStatus.Published);

            return await query.OrderBy(g => g.Id).ToListAsync(ct);
        }
    }
}