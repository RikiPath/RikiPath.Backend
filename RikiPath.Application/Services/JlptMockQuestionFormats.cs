using System.Text.Json;
using RikiPath.Application.Responses.Content;

namespace RikiPath.Application.Services;

public sealed record JlptMockQuestionFormat(string Code, string Label, string Skill, IReadOnlyList<string> Levels);

public static class JlptMockQuestionFormats
{
    public const string VisualMultipleChoice = "VisualMultipleChoice";
    public const string SentenceComposition = "SentenceComposition";

    private static readonly string[] AllLevels = ["N5", "N4", "N3", "N2"];
    private static readonly string[] N4ToN2 = ["N4", "N3", "N2"];

    // https://www.jlpt.jp/e/guideline/testsections.html
    public static IReadOnlyList<JlptMockQuestionFormat> All { get; } =
    [
        new("KanjiReading", "Cách đọc chữ Hán", "Vocabulary", AllLevels),
        new("Orthography", "Chọn cách viết chữ", "Vocabulary", AllLevels),
        new("WordFormation", "Cấu tạo từ", "Vocabulary", ["N2"]),
        new("ContextVocabulary", "Từ vựng theo ngữ cảnh", "Vocabulary", AllLevels),
        new("Paraphrase", "Từ hoặc cách diễn đạt tương đương", "Vocabulary", AllLevels),
        new("WordUsage", "Cách dùng từ", "Vocabulary", N4ToN2),
        new("GrammarForm", "Chọn mẫu ngữ pháp", "Grammar", AllLevels),
        new(SentenceComposition, "Sắp xếp câu có dấu ★", "Grammar", AllLevels),
        new("TextGrammar", "Ngữ pháp trong đoạn văn", "Grammar", AllLevels),
        new("ReadingShort", "Đọc hiểu đoạn ngắn", "Reading", AllLevels),
        new("ReadingMedium", "Đọc hiểu đoạn vừa", "Reading", AllLevels),
        new("ReadingLong", "Đọc hiểu đoạn dài", "Reading", ["N3"]),
        new("ReadingIntegrated", "Đọc đối chiếu văn bản", "Reading", ["N2"]),
        new("ReadingThematic", "Đọc đoạn dài tìm chủ đề", "Reading", ["N2"]),
        new("ReadingInformation", "Tra cứu thông tin", "Reading", AllLevels),
        new("ListeningTask", "Nghe để chọn việc cần làm", "Listening", AllLevels),
        new("ListeningKeyPoints", "Nghe chi tiết chính", "Listening", AllLevels),
        new("ListeningOutline", "Nghe hiểu nội dung tổng quát", "Listening", ["N3", "N2"]),
        new("ListeningExpressions", "Nghe chọn cách nói theo tình huống", "Listening", ["N5", "N4", "N3"]),
        new("ListeningQuickResponse", "Nghe chọn câu đáp nhanh", "Listening", AllLevels),
        new("ListeningIntegrated", "Nghe hiểu tổng hợp", "Listening", ["N2"]),
    ];

    private static readonly string[] ChoiceTypes = ["SingleChoice", "Listening", "Reading", VisualMultipleChoice];
    private static readonly string[] OtherTypes = ["MultipleChoice", "Ordering", "FillBlank", "Matching", "ShortAnswer"];

    public static bool IsSingleChoice(string? type) => ChoiceTypes.Contains(type?.Trim(), StringComparer.OrdinalIgnoreCase);

    internal static string? NormalizeQuestionType(string? type) => string.IsNullOrWhiteSpace(type)
        ? "SingleChoice" : ChoiceTypes.Concat(OtherTypes).FirstOrDefault(value => value.Equals(type.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static JlptMockQuestionFormat? Find(string? code) =>
        All.FirstOrDefault(format => format.Code.Equals(code?.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static string? ReadString(JsonElement? data, string key) =>
        data is { ValueKind: JsonValueKind.Object } fields && fields.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static bool HasAudioOptions(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } fields && fields.TryGetProperty("optionsInAudio", out var value) &&
        value.ValueKind == JsonValueKind.True;

    internal static int? StarPosition(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } fields && fields.TryGetProperty("starPosition", out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var position) ? position : null;

    internal static bool IsSentenceComposition(JsonElement? data) => string.Equals(ReadString(data, "jlptItemType"),
        SentenceComposition, StringComparison.OrdinalIgnoreCase);

    internal static bool IsValidSentenceResponse(JsonElement? data, JsonElement? response) =>
        data is { ValueKind: JsonValueKind.Object } fields && fields.TryGetProperty("tokens", out var tokenData) &&
        SentenceOrdering.TryReadSequence(tokenData, out var tokens) &&
        SentenceOrdering.TryReadSequence(response, out var submitted) &&
        SentenceOrdering.IsSubsequenceOfChoices(tokens, submitted);

    internal static string? Validate(MockQuestionPayloadResponse question, string? level, string? skill)
    {
        if (string.IsNullOrWhiteSpace(question.QuestionText) ||
            question.QuestionText.Contains("Câu hỏi mẫu #", StringComparison.OrdinalIgnoreCase) ||
            question.QuestionText.Contains("(Chính xác)", StringComparison.OrdinalIgnoreCase))
            return "Câu hỏi mẫu hoặc nội dung tiết lộ đáp án không thể gửi duyệt.";
        if (question.Options.Any(option =>
                string.IsNullOrWhiteSpace(option.OptionText) ||
                option.OptionText.Contains("(Chính xác)", StringComparison.OrdinalIgnoreCase) ||
                option.OptionText.Contains("(Correct)", StringComparison.OrdinalIgnoreCase)))
            return "Lựa chọn có nhãn tiết lộ đáp án hoặc thiếu nội dung.";
        if (IsSingleChoice(question.QuestionType) &&
            (question.Options.Count < 2 || question.Options.Count(option => option.IsCorrect) != 1 ||
             question.Options.Select(option => option.OptionText.Trim())
                 .Distinct(StringComparer.OrdinalIgnoreCase).Count() != question.Options.Count))
            return "Câu chọn một đáp án cần ít nhất hai lựa chọn khác nhau và đúng một đáp án.";
        if (JlptSkillAssessmentPolicy.Classify(skill) == JlptStudySkill.Listening &&
            string.IsNullOrWhiteSpace(question.AudioUrl))
            return "Câu nghe cần tệp audio trước khi gửi duyệt.";
        var data = question.InteractionData;
        if (string.Equals(question.QuestionType, VisualMultipleChoice, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(question.ImageUrl) && !question.Options.Any(option => !string.IsNullOrWhiteSpace(option.ImageUrl)))
            return "Trắc nghiệm hình ảnh cần ảnh câu hỏi hoặc ảnh ở ít nhất một lựa chọn.";
        if (data is not { ValueKind: JsonValueKind.Object } fields) return null;
        if (fields.TryGetProperty("optionsInAudio", out var audioSetting) &&
            audioSetting.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return "OptionsInAudio chỉ nhận true hoặc false.";
        foreach (var key in new[] { "passage", "secondPassage" })
            if (fields.TryGetProperty(key, out var passage) && passage.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                return "Ngữ liệu đọc phải là văn bản.";
        if (fields.TryGetProperty("audioStartSeconds", out _) || fields.TryGetProperty("audioEndSeconds", out _))
        {
            if (!fields.TryGetProperty("audioStartSeconds", out var start) || start.ValueKind != JsonValueKind.Number ||
                !start.TryGetDouble(out var from) || !fields.TryGetProperty("audioEndSeconds", out var end) ||
                end.ValueKind != JsonValueKind.Number || !end.TryGetDouble(out var to) ||
                !double.IsFinite(from) || !double.IsFinite(to) || from < 0 || to <= from)
                return "Khoảng audio cần có giây bắt đầu từ 0 và giây kết thúc lớn hơn giây bắt đầu.";
        }
        var isListening = JlptSkillAssessmentPolicy.Classify(skill) == JlptStudySkill.Listening;
        if (HasAudioOptions(data) && (!isListening || !IsSingleChoice(question.QuestionType) ||
            question.Options.Any(option => !string.IsNullOrWhiteSpace(option.ImageUrl))))
            return "Lựa chọn chỉ phát trong audio áp dụng cho câu nghe chọn một đáp án, dùng các lựa chọn đánh số.";

        if (!fields.TryGetProperty("jlptItemType", out var rawCode)) return null;
        var format = rawCode.ValueKind == JsonValueKind.String ? Find(rawCode.GetString()) : null;
        if (format is null) return "JlptItemType không hợp lệ.";
        if (!format.Levels.Contains(level?.Trim(), StringComparer.OrdinalIgnoreCase))
            return $"Dạng '{format.Label}' không có trong JLPT {level}.";
        if (JlptSkillAssessmentPolicy.Classify(format.Skill) != JlptSkillAssessmentPolicy.Classify(skill))
            return $"Dạng '{format.Label}' cần nằm trong phần {JlptSkillAssessmentPolicy.Label(Enum.Parse<JlptStudySkill>(format.Skill))}.";

        if (format.Code == SentenceComposition)
        {
            if (!SentenceOrdering.IsType(question.QuestionType) ||
                !SentenceOrdering.TryRead(data, question.AnswerKey, out var choices, out var correct) ||
                choices.Length != 4 || choices.Distinct(StringComparer.Ordinal).Count() < 2 ||
                StarPosition(data) is not (>= 1 and <= 4))
                return "Câu sắp xếp JLPT cần 4 cụm từ, thứ tự đúng đầy đủ và vị trí dấu ★ từ 1 đến 4.";
            if (choices.SequenceEqual(correct)) return "Hãy đảo thứ tự các cụm hiển thị để khác thứ tự đáp án.";
            return null;
        }

        if (!IsSingleChoice(question.QuestionType)) return $"Dạng '{format.Label}' cần chọn một đáp án.";
        var hasPassage = !string.IsNullOrWhiteSpace(ReadString(data, "passage"));
        var hasImage = !string.IsNullOrWhiteSpace(question.ImageUrl);
        if ((format.Skill == "Reading" || format.Code == "TextGrammar") && !hasPassage && !hasImage)
            return "Hãy thêm ngữ liệu ở Passage hoặc ảnh trong ImageUrl.";
        if (format.Code == "ReadingIntegrated" && !hasImage && string.IsNullOrWhiteSpace(ReadString(data, "secondPassage")))
            return "Dạng đọc đối chiếu cần văn bản thứ hai hoặc ảnh thể hiện các văn bản cần đối chiếu.";
        if (format.Code == "ListeningExpressions" && !hasImage)
            return "Dạng nghe chọn cách nói theo tình huống cần ảnh câu hỏi.";
        return null;
    }
}
