namespace RikiPath.Application.Responses.Content;

public sealed record ContentAudioTranscriptionResponse(
    Guid Id, string AudioUrl, string Status, string LanguageCode, string? Transcript, double? Confidence,
    string? ErrorCode, string? ErrorMessage, DateTime? CompletedAtUtc);

public sealed record ListeningAudioUploadResponse(
    string Url, string StoredFileName, long SizeBytes, Guid TranscriptionId, string TranscriptionStatus);
