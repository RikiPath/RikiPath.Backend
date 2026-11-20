using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.SharedBanks;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.SharedBanks;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần thêm cột Vocabulary.AudioUrl (string?), Kanji.SinoVietnamese (string?),
    // Kanji.StrokeOrderUrl (string?), Kanji.AudioUrl (string?) nếu chưa có.
    // Cần các hàm repo paged-search (filter + keyword, trả về (items, totalCount)):
    //   IVocabularyRepository.SearchAsync(jlptLevelId, keyword, page, pageSize)
    //   IKanjiRepository.SearchAsync(jlptLevelId, keyword, minStroke, maxStroke, page, pageSize)
    //   IGrammarPatternRepository.SearchAsync(jlptLevelId, keyword, page, pageSize)
    public class SharedBankSearchService(IUnitOfWork unitOfWork) : ISharedBankSearchService
    {
        private const int MaxPageSize = 100;

        public async Task<ApiResponse<PagedResult<VocabularyCardResponse>>> SearchVocabularyAsync(
            VocabularySearchFilter filter, CancellationToken cancellationToken)
        {
            try
            {
                Normalize(filter);

                // SharedBank Vocabulary luôn dùng truy vấn công khai; client không tự quyết định trạng thái hiển thị.
                var (items, total) = await unitOfWork.Vocabularies.SearchPublicAsync(
                    filter.JlptLevelId, filter.Keyword, filter.Page, filter.PageSize, cancellationToken);

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
                // Dùng cùng quy tắc với danh sách để không thể đoán ID và truy cập Vocabulary ẩn.
                var entry = await unitOfWork.Vocabularies.GetPublicByIdAsync(id, cancellationToken);
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

                // SharedBank Kanji dùng truy vấn công khai riêng để không ảnh hưởng luồng luyện viết.
                var (items, total) = await unitOfWork.Kanjis.SearchPublicAsync(
                    filter.JlptLevelId, filter.Keyword, filter.MinStrokeCount, filter.MaxStrokeCount,
                    filter.Page, filter.PageSize, cancellationToken);

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
                // Dùng cùng quy tắc với danh sách để không thể đoán ID và truy cập Kanji ẩn.
                var entry = await unitOfWork.Kanjis.GetPublicByIdAsync(id, cancellationToken);
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

                // SharedBank Grammar luôn dùng truy vấn công khai; client không tự quyết định trạng thái hiển thị.
                var (items, total) = await unitOfWork.GrammarPatterns.SearchPublicAsync(
                    filter.JlptLevelId, filter.Keyword, filter.Page, filter.PageSize, cancellationToken);

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
                // Dùng cùng quy tắc với danh sách để không thể đoán ID và truy cập Grammar ẩn.
                var entry = await unitOfWork.GrammarPatterns.GetPublicByIdAsync(id, cancellationToken);
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

        public async Task<ApiResponse<PagedResult<QuestionBankCardResponse>>> SearchQuestionsAsync(
            QuestionBankSearchFilter filter, CancellationToken cancellationToken)
        {
            try
            {
                Normalize(filter);
                var query = unitOfWork.MockQuestions.Query()
                    .AsNoTracking()
                    .Include(q => q.Options)
                    .Include(q => q.MockTestSection)
                        .ThenInclude(s => s.LanguageSkill)
                    .Include(q => q.MockTestSection)
                        .ThenInclude(s => s.MockTest)
                            .ThenInclude(t => t.CertificateLevel)
                    .Where(q => !q.IsDeleted
                        && !q.MockTestSection.IsDeleted
                        && !q.MockTestSection.MockTest.IsDeleted);

                if (filter.JlptLevelId.HasValue)
                    query = query.Where(q => q.MockTestSection.MockTest.CertificateLevelId == filter.JlptLevelId.Value);

                if (filter.LanguageSkillId.HasValue)
                    query = query.Where(q => q.MockTestSection.LanguageSkillId == filter.LanguageSkillId.Value);

                if (!string.IsNullOrWhiteSpace(filter.QuestionType))
                {
                    var qt = filter.QuestionType.Trim();
                    query = query.Where(q => q.QuestionType == qt);
                }

                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                {
                    var kw = filter.Keyword.Trim().ToLower();
                    query = query.Where(q =>
                        q.QuestionText.ToLower().Contains(kw)
                        || (q.Explanation != null && q.Explanation.ToLower().Contains(kw))
                        || q.MockTestSection.MockTest.Title.ToLower().Contains(kw)
                        || q.MockTestSection.Title.ToLower().Contains(kw));
                }

                var total = await query.CountAsync(cancellationToken);
                var rows = await query
                    .OrderByDescending(q => q.ModifiedDate ?? q.CreatedDate)
                    .ThenByDescending(q => q.Id)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync(cancellationToken);

                return ApiResponse<PagedResult<QuestionBankCardResponse>>.Success(new PagedResult<QuestionBankCardResponse>
                {
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = total,
                    Items = rows.Select(MapQuestion).ToList(),
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResult<QuestionBankCardResponse>>.Fail(
                    "Không thể tải ngân hàng câu hỏi.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<QuestionBankCardResponse>> GetQuestionDetailAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                var row = await unitOfWork.MockQuestions.Query()
                    .AsNoTracking()
                    .Include(q => q.Options)
                    .Include(q => q.MockTestSection)
                        .ThenInclude(s => s.LanguageSkill)
                    .Include(q => q.MockTestSection)
                        .ThenInclude(s => s.MockTest)
                            .ThenInclude(t => t.CertificateLevel)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted && !q.MockTestSection.IsDeleted && !q.MockTestSection.MockTest.IsDeleted, cancellationToken);

                if (row is null)
                    return ApiResponse<QuestionBankCardResponse>.NotFound("Không tìm thấy câu hỏi.");

                return ApiResponse<QuestionBankCardResponse>.Success(MapQuestion(row));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionBankCardResponse>.Fail(
                    "Không thể tải chi tiết câu hỏi.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private static void Normalize(Requests.SharedBanks.PagedFilterBase filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < 1) filter.PageSize = 20;
            if (filter.PageSize > MaxPageSize) filter.PageSize = MaxPageSize;
        }

        private static string GetLevelNames(IEnumerable<ContentLevelMapping>? mappings)
        {
            if (mappings == null || !mappings.Any()) return string.Empty;

            var levelCodes = mappings
                .Where(m => !m.IsDeleted && m.CertificateLevel != null)
                .Select(m => m.CertificateLevel.Code);

            return string.Join(", ", levelCodes);
        }

        private static VocabularyCardResponse MapVocabulary(Vocabulary e) => new()
        {
            Id = e.Id,
            Word = e.Word,
            Reading = e.Reading,
            Meaning = e.Meaning,
            ExampleSentence = e.ExampleSentence,
            AudioUrl = e.AudioUrl,
            JlptLevelName = GetLevelNames(e.ContentLevelMappings)
        };

        private static KanjiCardResponse MapKanji(Kanji k) => new()
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
            JlptLevelName = GetLevelNames(k.ContentLevelMappings)
        };

        private static GrammarCardResponse MapGrammar(GrammarPattern g) => new()
        {
            Id = g.Id,
            Title = g.Title,
            Structure = g.Structure,
            Explanation = g.UsageNotes,
            ExampleSentence = g.ExampleSentence,
            JlptLevelName = GetLevelNames(g.ContentLevelMappings)
        };

        private static QuestionBankCardResponse MapQuestion(MockQuestion q)
        {
            var options = (q.Options ?? [])
                .Where(o => !o.IsDeleted)
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Id)
                .Select(o => new QuestionBankOptionResponse
                {
                    Id = o.Id,
                    OptionText = o.OptionText,
                    ImageUrl = o.ImageUrl,
                    IsCorrect = o.IsCorrect,
                    SortOrder = o.SortOrder,
                })
                .ToList();

            var correctTexts = options.Where(o => o.IsCorrect && !string.IsNullOrWhiteSpace(o.OptionText))
                .Select(o => o.OptionText)
                .ToList();

            var summary = correctTexts.Count > 0
                ? string.Join(", ", correctTexts)
                : (!string.IsNullOrWhiteSpace(q.AnswerKeyJson) ? q.AnswerKeyJson : null);

            return new QuestionBankCardResponse
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                Explanation = q.Explanation,
                AudioUrl = q.AudioUrl,
                ImageUrl = q.ImageUrl,
                JlptLevelName = q.MockTestSection?.MockTest?.CertificateLevel?.Code ?? string.Empty,
                CertificateLevelId = q.MockTestSection?.MockTest?.CertificateLevelId,
                SkillName = q.MockTestSection?.LanguageSkill?.Name ?? string.Empty,
                LanguageSkillId = q.MockTestSection?.LanguageSkillId,
                MockTestTitle = q.MockTestSection?.MockTest?.Title ?? string.Empty,
                MockTestId = q.MockTestSection?.MockTestId ?? 0,
                SectionTitle = q.MockTestSection?.Title ?? string.Empty,
                AnswerSummary = summary,
                Options = options,
            };
        }

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
