using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Services;

public sealed class ContentAudioTranscriptionService(
    AppDbContext db, AppSettings settings, IFileStorageService files, IContentAudioTranscriber transcriber,
    ILogger<ContentAudioTranscriptionService> logger, IAwsAudioFileStorageService? awsAudio = null) : IContentAudioTranscriptionService
{
    public async Task<ListeningAudioUploadResponse> UploadAsync(
        Stream audio, string fileName, int authorId, CancellationToken ct)
    {
        if (authorId <= 0) throw new ArgumentException("Không xác định được người tải audio.");
        if (!Path.GetExtension(fileName).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Vui lòng chọn file MP3.");

        await using var buffered = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await audio.ReadAsync(buffer, ct)) > 0)
        {
            if (buffered.Length + count > ListeningAudioFilePolicy.MaxBytes)
                throw new ArgumentException("File MP3 không được vượt quá 500 MB.");
            await buffered.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        buffered.Position = 0;
        if (!await ListeningAudioFilePolicy.HasMp3HeaderAsync(buffered, ct))
            throw new ArgumentException("File nghe không có định dạng MP3 hợp lệ.");
        buffered.Position = 0;
        var uploaded = await files.UploadAsync(buffered, fileName, "audio/mpeg", "jlpt-listening", ct);
        try
        {
            var transcription = await QueueAsync(uploaded.Url, authorId, ct)
                ?? throw new InvalidOperationException("Audio chưa có đường dẫn lưu trữ hợp lệ.");
            return new(uploaded.Url, uploaded.StoredFileName, uploaded.SizeBytes,
                transcription.Id, transcription.Status);
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await files.DeleteAsync(uploaded.StoredFileName, "jlpt-listening", cleanup.Token); }
            catch { logger.LogWarning("Could not remove a listening upload after transcription registration failed."); }
            throw;
        }
    }

    public async Task<ContentAudioTranscriptionResponse?> QueueAsync(string audioUrl, int? authorId, CancellationToken ct)
    {
        var normalized = NormalizeUrl(audioUrl);
        if (normalized is null) return null;
        var hash = Hash(normalized);
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ContentAudioTranscriptions"
                ("Id", "AudioUrl", "AudioUrlHash", "CreatedByUserId", "Status", "LanguageCode",
                 "AttemptCount", "NextAttemptAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({id}, {normalized}, {hash}, {authorId}, 'Pending', 'ja-JP', 0, {now}, {now}, {now})
            ON CONFLICT ("AudioUrlHash") DO NOTHING;
            """, ct);
        return await GetAsync(normalized, ct);
    }

    public async Task QueuePayloadAsync(JsonElement payload, int authorId, CancellationToken ct)
    {
        foreach (var url in AudioReferences(payload).Distinct(StringComparer.Ordinal))
            await QueueAsync(url, authorId, ct);
    }

    private static IEnumerable<string> AudioReferences(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                foreach (var url in AudioReferences(item)) yield return url;
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in value.EnumerateObject())
            {
                if (field.Name.Equals("audioUrl", StringComparison.OrdinalIgnoreCase) &&
                    field.Value.ValueKind == JsonValueKind.String && field.Value.GetString() is { Length: > 0 } audio)
                    yield return audio;
                else
                    foreach (var url in AudioReferences(field.Value)) yield return url;
            }
        }
    }

    public async Task<ContentAudioTranscriptionResponse?> GetAsync(string audioUrl, CancellationToken ct)
    {
        var normalized = NormalizeUrl(audioUrl);
        if (normalized is null) return null;
        var hash = Hash(normalized);
        var item = await db.ContentAudioTranscriptions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AudioUrlHash == hash, ct);
        return item is null ? null : Response(item);
    }

    public async Task<IReadOnlyList<ContentAudioTranscriptionResponse>> ListMineAsync(int authorId, CancellationToken ct)
    {
        var items = await db.ContentAudioTranscriptions.AsNoTracking().Where(x => x.CreatedByUserId == authorId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(30).ToListAsync(ct);
        return items.Select(Response).ToList();
    }

    public async Task<ApiResponse<ContentAudioTranscriptionResponse>> RetryAsync(string audioUrl, CancellationToken ct)
    {
        var normalized = NormalizeUrl(audioUrl);
        if (normalized is null)
            return ApiResponse<ContentAudioTranscriptionResponse>.Fail("Audio phải là MP3 trong kho lưu trữ của hệ thống.");
        var hash = Hash(normalized);
        var now = DateTime.UtcNow;
        await db.ContentAudioTranscriptions.Where(x => x.AudioUrlHash == hash &&
                (x.Status == "Failed" || (x.Status == "Processing" && x.LeaseExpiresAtUtc <= now)))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Pending")
                .SetProperty(x => x.AttemptCount, 0).SetProperty(x => x.ErrorCode, (string?)null)
                .SetProperty(x => x.ErrorMessage, (string?)null).SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null).SetProperty(x => x.NextAttemptAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
        return ApiResponse<ContentAudioTranscriptionResponse>.Success((await QueueAsync(normalized, null, ct))!);
    }

    public async Task<ContentAudioTranscriptionResponse?> GetForGradingAsync(
        string audioUrl, JsonElement? interactionData, CancellationToken ct)
    {
        var response = await QueueAsync(audioUrl, null, ct);
        if (response is not { Status: "Completed" }) return response;
        if (interactionData is not { ValueKind: JsonValueKind.Object } fields ||
            (!fields.TryGetProperty("audioStartSeconds", out _) && !fields.TryGetProperty("audioEndSeconds", out _))) return response;
        var raw = await db.ContentAudioTranscriptions.Where(x => x.Id == response.Id)
            .Select(x => x.RawTranscriptJson).SingleAsync(ct);
        return ForQuestion(response, interactionData, raw);
    }

    private static ContentAudioTranscriptionResponse ForQuestion(
        ContentAudioTranscriptionResponse response, JsonElement? interactionData, string? raw)
    {
        if (interactionData is not { ValueKind: JsonValueKind.Object } data ||
            (!data.TryGetProperty("audioStartSeconds", out _) && !data.TryGetProperty("audioEndSeconds", out _)))
            return response;

        if (!data.TryGetProperty("audioStartSeconds", out var startField) || startField.ValueKind != JsonValueKind.Number ||
            !startField.TryGetDouble(out var start) || !data.TryGetProperty("audioEndSeconds", out var endField) ||
            endField.ValueKind != JsonValueKind.Number || !endField.TryGetDouble(out var end) ||
            !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start)
            return response with { Status = "Failed", Transcript = null, ErrorCode = "AUDIO_TRANSCRIPT_RANGE_INVALID",
                ErrorMessage = "Mốc bắt đầu và kết thúc câu nghe chưa hợp lệ." };

        var text = SliceTranscript(raw, start, end);
        return string.IsNullOrWhiteSpace(text)
            ? response with { Status = "Failed", Transcript = null, ErrorCode = "AUDIO_TRANSCRIPT_RANGE_EMPTY",
                ErrorMessage = "Chưa có lời thoại trong khoảng thời gian của câu nghe." }
            : response with { Transcript = text };
    }

    private static string? SliceTranscript(string? json, double start, double end)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.GetProperty("results").TryGetProperty("items", out var words)) return null;
        var text = new StringBuilder();
        var selected = false;
        foreach (var word in words.EnumerateArray())
        {
            if (word.TryGetProperty("start_time", out var time))
            {
                selected = double.TryParse(time.GetString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds >= start && seconds < end;
            }
            if (selected && word.TryGetProperty("alternatives", out var choices) && choices.GetArrayLength() > 0)
                text.Append(choices[0].GetProperty("content").GetString());
        }
        return text.ToString();
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!transcriber.IsConfigured) return false;
        var now = DateTime.UtcNow;
        var leaseId = Guid.NewGuid();
        var expires = now.AddSeconds(transcriber.TimeoutSeconds + 120);
        var rows = await db.ContentAudioTranscriptions.FromSqlInterpolated($"""
            UPDATE "ContentAudioTranscriptions" SET "Status" = 'Processing',
                "LeaseId" = {leaseId}, "LeaseExpiresAtUtc" = {expires},
                "AttemptCount" = "AttemptCount" + 1, "UpdatedAtUtc" = {now}
            WHERE "Id" = (
                SELECT "Id" FROM "ContentAudioTranscriptions"
                WHERE ("Status" = 'Pending' AND "NextAttemptAtUtc" <= {now})
                   OR ("Status" = 'Processing' AND "LeaseExpiresAtUtc" <= {now})
                ORDER BY "NextAttemptAtUtc", "Id" FOR UPDATE SKIP LOCKED LIMIT 1
            ) RETURNING *;
            """).AsNoTracking().ToListAsync(ct);
        var item = rows.SingleOrDefault();
        if (item is null)
        {
            // A draft may be approved while its transcription finishes. Catch questions published after that commit.
            var completedAudio = await db.ContentAudioTranscriptions.AsNoTracking().Where(audio => audio.Status == "Completed" &&
                    db.MockQuestions.Any(question => !question.IsDeleted && question.AudioUrl == audio.AudioUrl && question.AudioTranscript == null))
                .OrderBy(audio => audio.CompletedAtUtc).FirstOrDefaultAsync(ct);
            if (completedAudio is null) return false;
            await SynchronizeQuestionsAsync(completedAudio.AudioUrl, Response(completedAudio), completedAudio.RawTranscriptJson, ct);
            return true;
        }

        IDbContextTransaction? transaction = null;
        try
        {
            var result = await transcriber.TranscribeListeningAsync(item.AudioUrl, ct);
            if (string.IsNullOrWhiteSpace(result.TranscribedText))
                throw new InvalidOperationException("No speech recognized.");
            now = DateTime.UtcNow;
            transaction = await db.Database.BeginTransactionAsync(ct);
            var completed = await db.ContentAudioTranscriptions.Where(x => x.Id == item.Id && x.LeaseId == leaseId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Completed")
                    .SetProperty(x => x.Transcript, result.TranscribedText)
                    .SetProperty(x => x.RawTranscriptJson, result.RawResponseJson)
                    .SetProperty(x => x.Confidence, result.Confidence)
                    .SetProperty(x => x.CompletedAtUtc, now).SetProperty(x => x.UpdatedAtUtc, now)
                    .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null)
                    .SetProperty(x => x.ErrorCode, (string?)null).SetProperty(x => x.ErrorMessage, (string?)null), ct);
            if (completed == 1)
            {
                var response = Response(item) with { Status = "Completed", Transcript = result.TranscribedText };
                await SynchronizeQuestionsAsync(item.AudioUrl, response, result.RawResponseJson, ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RollbackQuietlyAsync(transaction);
            using var release = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await db.ContentAudioTranscriptions.Where(x => x.Id == item.Id && x.LeaseId == leaseId)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Pending")
                        .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null),
                        release.Token);
            }
            catch { logger.LogWarning("Content audio lease {TranscriptionId} will recover after expiry.", item.Id); }
            throw;
        }
        catch (Exception exception)
        {
            await RollbackQuietlyAsync(transaction);
            var code = exception is AmazonServiceException aws &&
                aws.ErrorCode is "AccessDenied" or "AccessDeniedException" or "UnauthorizedException"
                ? "AUDIO_TRANSCRIPTION_ACCESS_DENIED" : "AUDIO_TRANSCRIPTION_FAILED";
            logger.LogWarning("Content audio transcription {TranscriptionId} failed on attempt {AttemptCount} ({ErrorCode}).",
                item.Id, item.AttemptCount, code);
            now = DateTime.UtcNow;
            await db.ContentAudioTranscriptions.Where(x => x.Id == item.Id && x.LeaseId == leaseId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, item.AttemptCount < 3 ? "Pending" : "Failed")
                    .SetProperty(x => x.ErrorCode, code)
                    .SetProperty(x => x.ErrorMessage, code == "AUDIO_TRANSCRIPTION_ACCESS_DENIED"
                        ? "AWS chưa cấp đủ quyền đọc audio hoặc nhận dạng tiếng Nhật."
                        : "Chưa nhận dạng được audio. Kiểm tra file MP3 và cấu hình AWS rồi thử lại.")
                    .SetProperty(x => x.NextAttemptAtUtc, now.AddSeconds(30 * item.AttemptCount))
                    .SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.LeaseId, (Guid?)null)
                    .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null), ct);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
        return true;
    }

    private static async Task RollbackQuietlyAsync(IDbContextTransaction? transaction)
    {
        if (transaction is null) return;
        try
        {
            // Cleanup must not reuse an already-cancelled request/worker token.
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Disposed by caller's finally block.
        }
    }

    private async Task SynchronizeQuestionsAsync(string audioUrl, ContentAudioTranscriptionResponse response, string? raw, CancellationToken ct)
    {
        var questions = await db.MockQuestions.Where(question => !question.IsDeleted && question.AudioUrl == audioUrl && question.AudioTranscript == null)
            .ToListAsync(ct);
        foreach (var question in questions)
        {
            var data = string.IsNullOrWhiteSpace(question.InteractionDataJson) ? (JsonElement?)null
                : JsonSerializer.Deserialize<JsonElement>(question.InteractionDataJson);
            var selected = ForQuestion(response, data, raw);
            question.AudioTranscript = selected.Status == "Completed" ? selected.Transcript : string.Empty;
        }
        await db.SaveChangesAsync(ct);
    }

    private ContentAudioTranscriptionResponse Response(ContentAudioTranscription item)
    {
        var waiting = item.Status == "Pending" && !transcriber.IsConfigured;
        return new(item.Id, item.AudioUrl, waiting ? "WaitingConfiguration" : item.Status, item.LanguageCode,
            item.Transcript, item.Confidence, waiting ? "AUDIO_TRANSCRIPTION_NOT_CONFIGURED" : item.ErrorCode,
            waiting ? "Đang chờ cấu hình dịch vụ lưu và nhận dạng tiếng Nhật." : item.ErrorMessage,
            item.CompletedAtUtc is { } completed ? BusinessDateHelper.EnsureUtc(completed) : null);
    }

    private string? NormalizeUrl(string audioUrl)
    {
        if (Application.Helpers.AllowedMediaUrlPolicy.IsSafeLocalMediaPath(audioUrl))
            return audioUrl.Trim();
        if (awsAudio?.TryGetContentReference(audioUrl, out var canonical) == true) return canonical;
        if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !Uri.TryCreate(settings.Supabase?.Url, UriKind.Absolute, out var storage) ||
            uri.GetLeftPart(UriPartial.Authority) != storage.GetLeftPart(UriPartial.Authority) ||
            string.IsNullOrWhiteSpace(settings.Supabase?.Bucket) || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsoluteUri.Length > 2048 ||
            !uri.AbsolutePath.StartsWith("/storage/v1/object/public/" +
                Uri.EscapeDataString(settings.Supabase.Bucket) + "/", StringComparison.Ordinal) ||
            !uri.AbsolutePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return null;
        return uri.AbsoluteUri;
    }

    private static string Hash(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
}
