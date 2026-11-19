namespace RikiPath.Application.Services;

public static class HomeworkAudioFormat
{
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12) return null;
        if (header.StartsWith("RIFF"u8) && header[8..].StartsWith("WAVE"u8)) return "audio/wav";
        if (header.StartsWith("ID3"u8) || (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)) return "audio/mpeg";
        if (header[4..].StartsWith("ftyp"u8)) return "audio/mp4";
        if (header[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 })) return "audio/webm";
        return null;
    }

    public static string? Extension(string? mediaType) => mediaType switch
    {
        "audio/wav" => ".wav",
        "audio/mpeg" => ".mp3",
        "audio/mp4" => ".m4a",
        "audio/webm" => ".webm",
        _ => null
    };
}
