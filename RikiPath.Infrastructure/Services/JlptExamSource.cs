using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using RikiPath.Application.IClients;

namespace RikiPath.Infrastructure.Services;

public sealed class JlptExamSource(HttpClient http) : IJlptExamSource
{
    public const string SourceUrl = "https://www.jlpt.jp/e/application/overseas_index.html";
    public async Task<IReadOnlyList<ImportedJlptExam>> FetchAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 1_000_000) throw new HttpRequestException("Nguồn lịch thi quá lớn.");
        await response.Content.LoadIntoBufferAsync(1_000_000);
        return Parse(await response.Content.ReadAsStringAsync(ct));
    }

    // Extract explicit published dates, including image alt text. Never guess dates from recurrence rules.
    public static IReadOnlyList<ImportedJlptExam> Parse(string html)
    {
        var pattern = @"\b(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}),?\s+(20\d{2})\b";
        return Regex.Matches(WebUtility.HtmlDecode(html), pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
            .Select(m => DateOnly.TryParseExact($"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}",
                "MMMM d yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (DateOnly?)d : null)
            .Where(d => d.HasValue).Select(d => d!.Value).Distinct().Order()
            .Select(d => new ImportedJlptExam(d, SourceUrl)).ToList();
    }
}
