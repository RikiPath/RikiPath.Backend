namespace RikiPath.Application.Services;

public static class ListeningAudioFilePolicy
{
    public const int MaxBytes = 25 * 1024 * 1024;

    public static async Task<bool> HasMp3HeaderAsync(Stream audio, CancellationToken ct)
    {
        var header = new byte[4];
        if (await audio.ReadAtLeastAsync(header, header.Length, false, ct) < header.Length)
            return false;
        var hasId3 = header[0] == 'I' && header[1] == 'D' && header[2] == '3' && header[3] is >= 2 and <= 4;
        var hasFrame = header[0] == 0xff && (header[1] & 0xe6) == 0xe2 &&
            (header[1] & 0x18) != 0x08 && (header[2] & 0xf0) != 0xf0 && (header[2] & 0x0c) != 0x0c;
        return hasId3 || hasFrame;
    }
}
