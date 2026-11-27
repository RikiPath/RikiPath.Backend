using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RikiPath.Application.IServices;
using RikiPath.Domain;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Services;

public sealed class MentorCalendarService(AppDbContext db, IHttpClientFactory clients,
    IDataProtectionProvider protection, IConfiguration config, AppSettings settings) : IMentorCalendarService
{
    private string ClientId => config["GoogleCalendar:ClientId"] ?? "";
    private string ClientSecret => config["GoogleCalendar:ClientSecret"] ?? "";
    private string RedirectUri => config["GoogleCalendar:RedirectUri"] ?? "";
    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0
        && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var u) && (u.Scheme == "https" || (u.Scheme == "http" && u.IsLoopback));
    private IDataProtector Protector => protection.CreateProtector("RikiPath.MentorCalendar.RefreshToken.v1");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed record SyncedEvent(string Hash, DateTime EndsAt);

    public async Task<object> StatusAsync(int mentorId, CancellationToken ct)
    {
        if (!IsConfigured) return new { Configured = false, Connected = false, LastSyncedAt = (DateTime?)null };
        var row = await db.Set<MentorCalendarConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.MentorId == mentorId, ct);
        return new { Configured = true, Connected = row?.ProtectedRefreshToken is not null, row?.LastSyncedAt };
    }

    public async Task<string> ConnectAsync(int mentorId, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Google Calendar chưa được cấu hình.");
        var row = await db.Set<MentorCalendarConnection>().SingleOrDefaultAsync(x => x.MentorId == mentorId, ct);
        if (row is null) { row = new() { MentorId = mentorId }; db.Add(row); }
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        row.StateHash = Hash(state); row.StateExpiresAt = DateTime.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync(ct);
        return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new Dictionary<string, string> {
            ["client_id"] = ClientId, ["redirect_uri"] = RedirectUri, ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/calendar.events.owned", ["access_type"] = "offline",
            ["prompt"] = "consent", ["state"] = state }.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
    }

    public async Task CompleteAsync(int mentorId, string state, string code, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrEmpty(state) || state.Length > 256 || string.IsNullOrEmpty(code))
            throw new InvalidOperationException("Kết nối Google không hợp lệ hoặc đã hết hạn.");
        var hash = Hash(state);
        var row = await db.Set<MentorCalendarConnection>().SingleOrDefaultAsync(x => x.MentorId == mentorId && x.StateHash == hash && x.StateExpiresAt > DateTime.UtcNow, ct)
            ?? throw new InvalidOperationException("Phiên kết nối Google đã hết hạn.");
        // Atomically consume state before exchanging a code; callbacks cannot replay it.
        var used = await db.Set<MentorCalendarConnection>().Where(x => x.Id == row.Id && x.StateHash == hash)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.StateHash, (string?)null).SetProperty(r => r.StateExpiresAt, (DateTime?)null), ct);
        if (used != 1) throw new InvalidOperationException("Phiên kết nối đã được sử dụng.");
        var token = await TokenAsync(new() { ["code"] = code, ["grant_type"] = "authorization_code", ["redirect_uri"] = RedirectUri }, ct);
        if (!token.TryGetProperty("refresh_token", out var refresh) || string.IsNullOrEmpty(refresh.GetString()))
            throw new InvalidOperationException("Google chưa cấp quyền đồng bộ nền. Hãy kết nối lại và đồng ý cấp quyền.");
        row.StateHash = null; row.StateExpiresAt = null; row.ProtectedRefreshToken = Protector.Protect(refresh.GetString()!);
        await db.SaveChangesAsync(ct);
    }

    private async Task<JsonElement> TokenAsync(Dictionary<string, string> values, CancellationToken ct)
    {
        values["client_id"] = ClientId; values["client_secret"] = ClientSecret;
        using var response = await clients.CreateClient("MentorCalendar").PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values), ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Google từ chối cấp quyền Calendar. Cần kết nối lại.");
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public async Task DisconnectAsync(int mentorId, CancellationToken ct)
    {
        var row = await db.Set<MentorCalendarConnection>().SingleOrDefaultAsync(x => x.MentorId == mentorId, ct);
        if (row is null) return;
        row.ProtectedRefreshToken = null; row.StateHash = null; row.StateExpiresAt = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task SyncAsync(int mentorId, CancellationToken ct)
    {
        if (!IsConfigured) return;
        var row = await db.Set<MentorCalendarConnection>().SingleOrDefaultAsync(x => x.MentorId == mentorId, ct);
        if (row?.ProtectedRefreshToken is null) return;
        var token = await TokenAsync(new() { ["refresh_token"] = Protector.Unprotect(row.ProtectedRefreshToken), ["grant_type"] = "refresh_token" }, ct);
        var client = clients.CreateClient("MentorCalendar");
        var auth = new HttpClientWrapper(client, token.GetProperty("access_token").GetString()!);
        var now = DateTime.UtcNow;
        var slots = await db.MentorAvailabilities.AsNoTracking().Where(s => s.MentorId == mentorId && !s.IsDeleted && s.IsApproved && s.RejectedAt == null && s.EndTime > now
            && (s.MentorBookings.Any(b => !b.IsDeleted && b.UserSubscription.PaymentStatus == RikiPath.Domain.Enums.PaymentStatus.Paid
                && (b.Status == RikiPath.Domain.Enums.MentorStatus.Assigned || b.Status == RikiPath.Domain.Enums.MentorStatus.Accepted))
                || db.StudyCohorts.Any(c => c.Status == "Published" && c.Sessions.Any(t => t.AvailabilityId == s.Id))
                || db.StudyCourses.Any(c => c.PaymentStatus == "Paid" && c.Status == "Active" && c.Sessions.Any(t => t.AvailabilityId == s.Id && t.Status == "Scheduled"))))
            .ToListAsync(ct);
        var previous = JsonSerializer.Deserialize<Dictionary<string, SyncedEvent>>(row.SyncedEventsJson) ?? [];
        var next = new Dictionary<string, SyncedEvent>();
        foreach (var slot in slots)
        {
            var eventId = Hash($"rikipath:{slot.RoomId}");
            var room = settings.FrontendBaseUrl.TrimEnd('/') + $"/meeting-room/{slot.RoomId}";
            var body = new { id = eventId, summary = "RikiPath · Buổi dạy cùng Giáo viên", description = "Lịch do RikiPath quản lý. Mọi thay đổi lịch cần Quản trị viên phê duyệt.\n" + room,
                location = room, start = new { dateTime = DateTime.SpecifyKind(slot.StartTime, DateTimeKind.Utc).ToString("O"), timeZone = "Asia/Ho_Chi_Minh" },
                end = new { dateTime = DateTime.SpecifyKind(slot.EndTime, DateTimeKind.Utc).ToString("O"), timeZone = "Asia/Ho_Chi_Minh" } };
            var digest = Hash(JsonSerializer.Serialize(body));
            if (!previous.TryGetValue(eventId, out var old) || old.Hash != digest)
            {
                using var put = await auth.SendAsync(HttpMethod.Put, eventId, body, ct);
                if (put.StatusCode == HttpStatusCode.NotFound)
                {
                    using var post = await auth.SendAsync(HttpMethod.Post, "", body, ct);
                    if (post.StatusCode != HttpStatusCode.Conflict) post.EnsureSuccessStatusCode();
                }
                else put.EnsureSuccessStatusCode();
            }
            next[eventId] = new(digest, slot.EndTime);
        }
        foreach (var old in previous.Where(x => !next.ContainsKey(x.Key)))
        {
            if (old.Value.EndsAt <= now) continue; // Keep past teaching records in the personal calendar.
            using var deleted = await auth.SendAsync(HttpMethod.Delete, old.Key, null, ct);
            if (deleted.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone)) deleted.EnsureSuccessStatusCode();
        }
        row.SyncedEventsJson = JsonSerializer.Serialize(next); row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private sealed class HttpClientWrapper(HttpClient client, string token)
    {
        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string id, object? body, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, "https://www.googleapis.com/calendar/v3/calendars/primary/events" + (id.Length > 0 ? "/" + id : ""));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request, ct);
        }

    }
}
