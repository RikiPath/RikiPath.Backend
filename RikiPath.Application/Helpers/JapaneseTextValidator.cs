using System.Text;
using System.Text.RegularExpressions;

namespace RikiPath.Application.Helpers;

public sealed record JapaneseValidationResult(
    bool IsValid,
    string NormalizedText,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// Shared Japanese text sanitizer and validator enforcing clean ja -> vi selection translation.
/// Rejects Vietnamese, English, pure numbers/punctuation/emojis, URLs, emails, isolated single kana,
/// and clearly Simplified-Chinese-only spans while preserving single valid Kanji words, half-width Katakana,
/// mixed Japanese sentences with numerals/Latin terms, and multi-line reading passages.
/// </summary>
public static partial class JapaneseTextValidator
{
    public const int MaxTranslationLength = 500;

    private const string SimplifiedChineseUniqueChars =
        "这为国学说们个时来没里发对经动现关开点问机电车东长书买卖听见给让从进还应该边过运远连选错钟钱门间难题飞饭鱼鸟黄龙你他她它吗呢吧哪么很着过师认识试请诉谁课谈论谢议记读许证评调变资费赚赠赢买赔质赞赖赶起";

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[\u200B-\u200F\u202A-\u202E\uFEFF\u00AD]", RegexOptions.Compiled)]
    private static partial Regex ZeroWidthCharsRegex();

    [GeneratedRegex(@"[ \t\u00A0]+", RegexOptions.Compiled)]
    private static partial Regex HorizontalWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex ExcessiveNewlinesRegex();

    [GeneratedRegex(@"(?:https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public static string SanitizeInput(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        var text = rawText.Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);
        text = ZeroWidthCharsRegex().Replace(text, string.Empty);
        text = HtmlTagRegex().Replace(text, string.Empty);
        text = HorizontalWhitespaceRegex().Replace(text, " ");
        text = Regex.Replace(text, @" *\n *", "\n");
        text = ExcessiveNewlinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    public static JapaneseValidationResult Validate(string? rawText, int maxLength = MaxTranslationLength)
    {
        var normalized = SanitizeInput(rawText);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new JapaneseValidationResult(
                false,
                string.Empty,
                "TRANSLATION_EMPTY",
                "Vui lòng chọn một từ hoặc đoạn tiếng Nhật cần dịch.");
        }

        if (normalized.Length > maxLength)
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_TOO_LONG",
                $"Đoạn văn bản vượt quá giới hạn {maxLength} ký tự. Vui lòng chọn đoạn ngắn hơn.");
        }

        if (EmailRegex().IsMatch(normalized) || (UrlRegex().IsMatch(normalized) && !ContainsAnyJapaneseScript(normalized)))
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_NON_JAPANESE",
                "Chỉ hỗ trợ dịch văn bản tiếng Nhật sang tiếng Việt.");
        }

        var stats = AnalyzeScripts(normalized);

        if (stats.TotalJapanese == 0)
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_NON_JAPANESE",
                "Chỉ hỗ trợ dịch văn bản tiếng Nhật sang tiếng Việt.");
        }

        // Single Hiragana/Katakana character (no Kanji, no second kana) lacks sentence/word context to translate meaningfully.
        if (stats.KanaCount == 1 && stats.KanjiCount == 0 && stats.LatinCount == 0 && stats.DigitCount == 0)
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_INSUFFICIENT_CONTEXT",
                "Ký tự kana đơn lẻ chưa đủ ngữ cảnh để dịch. Hãy chọn cả từ hoặc câu tiếng Nhật.");
        }

        // Reject text containing Vietnamese tone/diacritic characters unless Japanese strongly dominates.
        if (stats.VietnameseDiacriticCount > 0 && stats.TotalJapanese < Math.Max(4, stats.LatinCount * 2))
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_NON_JAPANESE",
                "Nội dung tiếng Việt không cần kích hoạt trình dịch Nhật → Việt.");
        }

        // Pure Kanji without Kana: block clearly Simplified-Chinese-only text.
        if (stats.KanaCount == 0 && stats.SimplifiedChineseUniqueCount > 0)
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_NON_JAPANESE",
                "Đoạn chữ được chọn có dấu hiệu tiếng Trung giản thể, không phải tiếng Nhật.");
        }

        var substantive = stats.TotalJapanese + stats.LatinCount;
        if (substantive > 0 && (double)stats.TotalJapanese / substantive < 0.30)
        {
            return new JapaneseValidationResult(
                false,
                normalized,
                "TRANSLATION_NON_JAPANESE",
                "Vùng văn bản được chọn chủ yếu không phải tiếng Nhật.");
        }

        return new JapaneseValidationResult(true, normalized, null, null);
    }

    private static bool ContainsAnyJapaneseScript(string text)
    {
        foreach (var ch in text)
        {
            if (IsHiragana(ch) || IsKatakana(ch) || IsKanjiOrJapaneseSymbol(ch)) return true;
        }
        return false;
    }

    private static (int HiraganaCount, int KatakanaCount, int KanaCount, int KanjiCount, int TotalJapanese, int LatinCount, int DigitCount, int VietnameseDiacriticCount, int SimplifiedChineseUniqueCount)
        AnalyzeScripts(string text)
    {
        int hira = 0, kata = 0, kanji = 0, latin = 0, digits = 0, vn = 0, sc = 0;

        foreach (var ch in text)
        {
            if (IsHiragana(ch))
            {
                hira++;
            }
            else if (IsKatakana(ch))
            {
                kata++;
            }
            else if (IsKanjiOrJapaneseSymbol(ch))
            {
                kanji++;
                if (SimplifiedChineseUniqueChars.Contains(ch)) sc++;
            }
            else if (char.IsAsciiDigit(ch) || (ch >= '\uFF10' && ch <= '\uFF19'))
            {
                digits++;
            }
            else if (IsVietnameseDiacritic(ch))
            {
                vn++;
                latin++;
            }
            else if (char.IsLetter(ch) && ch <= '\u024F')
            {
                latin++;
            }
        }

        var kana = hira + kata;
        return (hira, kata, kana, kanji, kana + kanji, latin, digits, vn, sc);
    }

    private static bool IsHiragana(char c) => c is >= '\u3040' and <= '\u309F';

    private static bool IsKatakana(char c) =>
        (c is >= '\u30A0' and <= '\u30FF') ||
        (c is >= '\u31F0' and <= '\u31FF') ||
        (c is >= '\uFF66' and <= '\uFF9F') ||
        c == '\u30FC';

    private static bool IsKanjiOrJapaneseSymbol(char c) =>
        (c is >= '\u4E00' and <= '\u9FFF') ||
        (c is >= '\u3400' and <= '\u4DBF') ||
        (c is >= '\uF900' and <= '\uFAFF') ||
        c is '々' or '〆' or 'ヵ' or 'ヶ';

    private static bool IsVietnameseDiacritic(char c) =>
        "àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ"
            .Contains(char.ToLowerInvariant(c));
}
