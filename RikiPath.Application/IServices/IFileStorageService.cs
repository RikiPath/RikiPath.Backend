namespace RikiPath.Application.IServices
{
    public record UploadedFileResult(string Url, string StoredFileName, long SizeBytes);
    public record PrivateVerificationReadResult(Stream Content, string ContentType);
    //Upload supabase
    public interface IFileStorageService
    {
        Task<UploadedFileResult> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken);

        Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken);
        Task<string> CreateSignedReadUrlAsync(string storedUrl, CancellationToken cancellationToken);
        Task<UploadedFileResult> UploadPrivateVerificationAsync(
            Stream fileStream, string contentType, int ownerId, CancellationToken cancellationToken);
        Task VerifyPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken);
        Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(string storedUrl, CancellationToken cancellationToken);
    }
}
