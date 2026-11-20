using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Enums;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Services;

public sealed class FileStorageService(
    SupabaseFileStorageService supabase, IAwsAudioFileStorageService audio, IClaimService claims,
    AppSettings settings) : IFileStorageService
{
    public Task<UploadedFileResult> UploadAsync(
        Stream stream, string fileName, string contentType, string folder, CancellationToken ct)
    {
        if (!audio.IsAudio(fileName, contentType)) return supabase.UploadAsync(stream, fileName, contentType, folder, ct);
        var actor = claims.GetUserClaim();
        return audio.UploadAsync(stream, fileName, actor.Id, actor.Role is Role.ContentAuthor or Role.Admin, ct);
    }

    public Task<UploadedFileResult> UploadPrivateVerificationAsync(
        Stream stream, string contentType, int ownerId, CancellationToken ct) =>
        contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            ? audio.UploadAsync(stream, "recording" + (HomeworkAudioFormat.Extension(contentType)
                ?? throw new ArgumentException("Định dạng bản thu không hợp lệ.")), ownerId, false, ct)
            : supabase.UploadPrivateVerificationAsync(stream, contentType, ownerId, ct);

    public Task DeleteAsync(string reference, string folder, CancellationToken ct)
    {
        if (!audio.TryGetObjectKey(reference, out var key)) return supabase.DeleteAsync(reference, folder, ct);
        var actor = claims.GetUserClaim();
        if (actor.Role != Role.Admin && !key.StartsWith(settings.AwsAudio.ContentPrefix.Trim('/') + "/" + actor.Id + "/",
                StringComparison.Ordinal) && !key.StartsWith(settings.AwsAudio.RecordingPrefix.Trim('/') + "/" + actor.Id + "/",
                StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Bạn không có quyền xóa audio này.");
        return audio.DeleteAsync(reference, ct);
    }

    public Task<string> CreateSignedReadUrlAsync(string reference, CancellationToken ct) =>
        audio.TryGetObjectKey(reference, out _) ? audio.CreateSignedReadUrlAsync(reference, ct) : supabase.CreateSignedReadUrlAsync(reference, ct);

    public async Task VerifyPrivateVerificationAsync(string reference, CancellationToken ct)
    {
        if (audio.TryGetObjectKey(reference, out _)) _ = await audio.CreateSignedReadUrlAsync(reference, ct);
        else await supabase.VerifyPrivateVerificationAsync(reference, ct);
    }

    public Task<PrivateVerificationReadResult> OpenPrivateVerificationAsync(string reference, CancellationToken ct) =>
        audio.TryGetObjectKey(reference, out _) ? audio.OpenAsync(reference, ct) : supabase.OpenPrivateVerificationAsync(reference, ct);
}
