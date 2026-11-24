using System.Threading.Channels;

namespace RikiPath.Infrastructure.Services;

/// <summary>
/// Wakes the transcription worker only when a caller queues or retries an audio item.
/// The bounded channel coalesces concurrent signals so one upload cannot create an
/// unbounded number of worker wake-ups.
/// </summary>
public sealed class ContentAudioTranscriptionWakeup
{
    private readonly Channel<bool> signals = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public void Signal() => signals.Writer.TryWrite(true);

    public ValueTask<bool> WaitAsync(CancellationToken cancellationToken) =>
        signals.Reader.ReadAsync(cancellationToken);
}
