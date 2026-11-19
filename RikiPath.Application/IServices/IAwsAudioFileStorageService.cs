namespace RikiPath.Application.IServices;

public interface IAwsAudioFileStorageService
{
    bool IsConfigured { get; }
    bool IsAudio(string fileName, string? contentType);
    bool TryGetObjectKey(string reference, out string key);
    bool TryGetContentReference(string reference, out string canonicalUrl);
    bool IsRecordingOwnedBy(string reference, int ownerId);
    Task<UploadedFileResult> UploadAsync(Stream audio, string fileName, int? ownerId, bool content, CancellationToken ct);
    Task<PrivateVerificationReadResult> OpenAsync(string reference, CancellationToken ct);
    Task DeleteAsync(string reference, CancellationToken ct);
    Task<string> CreateSignedReadUrlAsync(string reference, CancellationToken ct);
    string ContentReference(int ownerId, string fileName);
    string S3Reference(string key);
}
