using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using RikiPath.Infrastructure.Options;

namespace RikiPath.Infrastructure.Clients;

/// <summary>SigV4 query signing following the Amazon Transcribe WebSocket protocol.</summary>
public static class TranscribeWebSocketSigner
{
    public static string Sign(ImmutableCredentials credentials, AmazonTranscribeSettings settings, DateTimeOffset now)
    {
        if (!Regex.IsMatch(settings.Region, @"^[a-z]{2}(-[a-z]+)+-\d$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Invalid Amazon Transcribe region.");
        if (settings.Language != "ja-JP" || settings.SampleRate != 16000)
            throw new InvalidOperationException("RikiPath speech requires ja-JP, mono PCM16 at 16000 Hz.");
        if (settings.SessionUrlLifetimeSeconds is < 15 or > 300)
            throw new InvalidOperationException("The session URL must expire within 15–300 seconds.");
        var date = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var timestamp = now.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var scope = $"{date}/{settings.Region}/transcribe/aws4_request";
        var host = $"transcribestreaming.{settings.Region}.amazonaws.com:8443";
        const string path = "/stream-transcription-websocket";
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{credentials.AccessKey}/{scope}",
            ["X-Amz-Date"] = timestamp,
            ["X-Amz-Expires"] = settings.SessionUrlLifetimeSeconds.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host",
            ["language-code"] = settings.Language,
            ["media-encoding"] = "pcm",
            ["sample-rate"] = settings.SampleRate.ToString(CultureInfo.InvariantCulture),
        };
        if (credentials.UseToken) query["X-Amz-Security-Token"] = credentials.Token;
        // AWS Transcribe requires equals signs in values to be encoded twice.
        var canonicalQuery = string.Join('&', query.Select(x => $"{Encode(x.Key)}={Encode(x.Value)}"));
        var canonicalRequest = $"GET\n{path}\n{canonicalQuery}\nhost:{host}\n\nhost\n{Hash(string.Empty)}";
        var toSign = $"AWS4-HMAC-SHA256\n{timestamp}\n{scope}\n{Hash(canonicalRequest)}";
        var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + credentials.SecretKey), date);
        key = Hmac(Hmac(Hmac(key, settings.Region), "transcribe"), "aws4_request");
        var signature = Convert.ToHexString(Hmac(key, toSign)).ToLowerInvariant();
        return $"wss://{host}{path}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    private static string Encode(string value) => Uri.EscapeDataString(value).Replace("%3D", "%253D", StringComparison.Ordinal);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static byte[] Hmac(byte[] key, string value) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
}
