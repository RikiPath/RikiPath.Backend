using System.Text.RegularExpressions;

namespace RikiPath.Application.Helpers;

/// <summary>Chính sách đường dẫn media an toàn (HTTPS Supabase/S3, đường dẫn local Dev/QA hoặc audio proxy đã cấu hình).</summary>
public static class AllowedMediaUrlPolicy
{
    public static readonly HashSet<string> SupportedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".webm"
    };

    private static volatile Uri? _configuredAudioPublicOrigin;

    private static readonly Regex LocalMediaPathRegex = new(
        @"^/api/FileStorage/local/(?:(?<folder>[a-z0-9_-]{1,40})/)?(?<file>[a-f0-9]{32}\.(?:jpg|jpeg|png|webp|gif|pdf|mp3|wav|m4a|ogg|webm))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ContentAudioPathRegex = new(
        @"^/api/audio/content/[1-9][0-9]{0,9}/[a-f0-9]{32}\.(?:mp3|wav|m4a|mp4|webm|ogg|flac|amr)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static void ConfigureApprovedAudioBaseUrl(string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            _configuredAudioPublicOrigin = null;
            return;
        }

        if (Uri.TryCreate(publicBaseUrl.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment))
        {
            _configuredAudioPublicOrigin = uri;
            return;
        }

        _configuredAudioPublicOrigin = null;
    }

    public static bool IsSafeLocalMediaPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return !trimmed.Contains("..", StringComparison.Ordinal) &&
               !trimmed.Contains('\\', StringComparison.Ordinal) &&
               !trimmed.Contains('%', StringComparison.Ordinal) &&
               LocalMediaPathRegex.IsMatch(trimmed);
    }

    public static bool IsSafeContentAudioPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return !trimmed.Contains("..", StringComparison.Ordinal) &&
               !trimmed.Contains('\\', StringComparison.Ordinal) &&
               !trimmed.Contains('%', StringComparison.Ordinal) &&
               !trimmed.Contains('?', StringComparison.Ordinal) &&
               !trimmed.Contains('#', StringComparison.Ordinal) &&
               ContentAudioPathRegex.IsMatch(trimmed);
    }

    public static bool IsApprovedMediaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        if (IsSafeLocalMediaPath(trimmed) || IsSafeContentAudioPath(trimmed)) return true;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var isLoopbackOrLocalhost = uri.IsLoopback ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        if (IsSafeLocalMediaPath(uri.AbsolutePath) && isLoopbackOrLocalhost)
        {
            return true;
        }

        if (IsSafeContentAudioPath(uri.AbsolutePath) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment) &&
            (isLoopbackOrLocalhost ||
             (_configuredAudioPublicOrigin is { } origin &&
              string.Equals(uri.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase) &&
              string.Equals(uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
              uri.Port == origin.Port)))
        {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        return host == "supabase.co" || host.EndsWith(".supabase.co", StringComparison.Ordinal) ||
               host == "amazonaws.com" || host.EndsWith(".amazonaws.com", StringComparison.Ordinal);
    }

    public static bool HasSupportedAudioExtension(string? urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath)) return false;
        var trimmed = urlOrPath.Trim();
        var path = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.AbsolutePath : trimmed.Split('?', '#')[0];
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && SupportedAudioExtensions.Contains(ext);
    }
}
