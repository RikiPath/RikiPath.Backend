using System.Text.Json;

namespace RikiPath.Application.Services;

internal static class SentenceOrdering
{
    public const string QuestionType = "Ordering";
    public const string InvalidContentMessage =
        "Câu sắp xếp cần ít nhất 2 cụm từ trong InteractionDataJson.tokens. AnswerKeyJson phải chứa đúng các cụm từ theo thứ tự đáp án.";

    public static bool IsType(string? type) => string.Equals(type, QuestionType, StringComparison.OrdinalIgnoreCase);

    public static bool TryRead(string? data, string? answerKey, out string[] tokens, out string[] answer)
    {
        tokens = [];
        answer = [];
        if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(answerKey)) return false;

        try
        {
            return TryRead(JsonSerializer.Deserialize<JsonElement>(data), JsonSerializer.Deserialize<JsonElement>(answerKey),
                out tokens, out answer);
        }
        catch (JsonException) { return false; }
    }

    public static bool TryRead(JsonElement? data, JsonElement? answerKey, out string[] tokens, out string[] answer)
    {
        tokens = [];
        answer = [];
        if (data is not { ValueKind: JsonValueKind.Object } interaction || !interaction.TryGetProperty("tokens", out var tokenData))
            return false;
        return TryReadSequence(tokenData, out tokens) && tokens.Length >= 2 &&
            TryReadSequence(answerKey, out answer) && answer.Length == tokens.Length && IsSubsequenceOfChoices(tokens, answer);
    }

    public static bool TryReadSequence(JsonElement? value, out string[] sequence)
    {
        sequence = [];
        if (value is not { ValueKind: JsonValueKind.Array } array) return false;
        var items = array.EnumerateArray().ToArray();
        if (items.Any(item => item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))) return false;
        sequence = items.Select(item => item.GetString()!).ToArray();
        return true;
    }

    // Token values may repeat. Each response can use at most the published number of copies.
    public static bool IsSubsequenceOfChoices(string[] choices, string[] response)
    {
        var available = choices.GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
        return response.GroupBy(value => value).All(group => group.Count() <= available.GetValueOrDefault(group.Key));
    }
}
