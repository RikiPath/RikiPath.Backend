namespace RikiPath.Application.IClients;

public sealed record AiTranslationPayload(
    string Translation,
    string? Reading,
    string? GrammarNote,
    List<AiTranslationSegment> Breakdown);

public sealed record AiTranslationSegment(
    string Surface,
    string? Reading,
    string Meaning);

public interface IAiTranslationClient
{
    Task<AiTranslationPayload> TranslateJapaneseAsync(
        int userId,
        string selectedText,
        string? contextSentence,
        string targetLanguage,
        CancellationToken cancellationToken);
}
