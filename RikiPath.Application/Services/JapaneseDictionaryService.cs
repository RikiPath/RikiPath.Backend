using System.Text;
using System.Text.RegularExpressions;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.Application.Services;

public sealed class JapaneseDictionaryService(IKanjiRepository kanjis) : IJapaneseDictionaryService
{
    private static readonly Regex Separators = new(@"[\s,、;；/／|・]+", RegexOptions.Compiled);
    private static readonly Regex Annotation = new(@"[（(][^）)]*[）)]", RegexOptions.Compiled);
    private const int Limit = 20;

    public async Task<ApiResponse<List<JapaneseDictionaryCandidate>>> SearchAsync(
        string? readingKana, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(readingKana) || readingKana.Length > 64)
            return ApiResponse<List<JapaneseDictionaryCandidate>>.Fail("Cách đọc phải có từ 1 đến 64 ký tự Kana.");
        var query = NormalizeKana(readingKana.Trim());
        if (query.Length == 0 || query.Any(c => c is not (>= 'ぁ' and <= 'ゖ') and not 'ー'))
            return ApiResponse<List<JapaneseDictionaryCandidate>>.Fail("Vui lòng nhập cách đọc bằng Hiragana hoặc Katakana.");

        // Only the small dictionary projection is fetched. Readings are free-form admin data,
        // so matching handles their separators/notation in one place, without a writing queue.
        var entries = await kanjis.GetDictionaryEntriesAsync(cancellationToken);
        var matches = entries.SelectMany(entry => Readings(entry.OnYomi, entry.Character, false)
                .Concat(Readings(entry.KunYomi, entry.Character, true))
                .Where(reading => reading.Kana.StartsWith(query, StringComparison.Ordinal))
                .Select(reading => new JapaneseDictionaryCandidate(
                    entry.Id, reading.Surface, reading.Kana, entry.Meaning, entry.SinoVietnamese)))
            .OrderBy(candidate => candidate.ReadingKana == query ? 0 : 1)
            .ThenBy(candidate => candidate.ReadingKana.Length)
            .ThenBy(candidate => candidate.Surface, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Id)
            .DistinctBy(candidate => candidate.Surface)
            .Take(Limit)
            .ToList();
        return ApiResponse<List<JapaneseDictionaryCandidate>>.Success(matches);
    }

    private static IEnumerable<(string Kana, string Surface)> Readings(string? value, string character, bool kun)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(character)) yield break;
        foreach (var token in Separators.Split(Annotation.Replace(value.Normalize(NormalizationForm.FormKC), "")))
        {
            var reading = NormalizeKana(token.Replace("-", "").Replace("〜", "").Replace("～", ""));
            if (reading.Length == 0) continue;
            var dot = reading.IndexOf('.');
            if (kun && dot > 0)
            {
                var stem = reading[..dot];
                var suffix = reading[(dot + 1)..].Replace(".", "");
                yield return (stem, character);
                // Preserve okurigana when the user typed the complete reading: たべる -> 食べる.
                yield return (stem + suffix, character + suffix);
            }
            else yield return (reading.Replace(".", ""), character);
        }
    }

    private static string NormalizeKana(string value)
        => new(value.Normalize(NormalizationForm.FormKC)
            .Select(c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c).ToArray());
}
