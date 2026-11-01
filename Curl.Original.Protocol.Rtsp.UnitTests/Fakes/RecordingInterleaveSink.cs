namespace Curl.Original.Protocol.Rtsp.Fakes;

/// <summary>
/// An <see cref="IRtspInterleaveSink" /> that records every frame it is handed and takes
/// <paramref name="shortBy" /> bytes fewer than each frame's length.
/// </summary>
/// <param name="shortBy">How many bytes fewer than each frame it says it took.</param>
public sealed class RecordingInterleaveSink(long shortBy = 0) : IRtspInterleaveSink
{
    private readonly List<byte[]> frames = [];

    /// <summary>Gets every frame handed so far, in order.</summary>
    public IReadOnlyList<byte[]> Frames => frames;

    /// <inheritdoc />
    public ValueTask<long> WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        frames.Add(frame.ToArray());
        return ValueTask.FromResult(frame.Length - shortBy);
    }
}
