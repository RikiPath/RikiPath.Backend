namespace RikiPath.Application.Responses;

public sealed record JapaneseDictionaryCandidate(
    int Id, string Surface, string ReadingKana, string Meaning, string? SinoVietnamese);
