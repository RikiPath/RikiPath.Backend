namespace RikiPath.Application.Responses.SpeechToText;

/// <summary>Short-lived capability for a single Transcribe WebSocket connection; never log the URL.</summary>
public sealed record SpeechStreamingSessionResponse(
    string Provider, string WebSocketUrl, string Language, int SampleRate,
    string MediaEncoding, int ExpiresInSeconds);
