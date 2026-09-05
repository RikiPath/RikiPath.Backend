namespace RikiPath.Application.IServices
{
    public record UploadedFileResult(string Url, string StoredFileName, long SizeBytes);
    //Upload supabase
    public interface IFileStorageService
    {
        Task<UploadedFileResult> UploadAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken);

        Task DeleteAsync(string storedFileName, string folder, CancellationToken cancellationToken);
    }
}
