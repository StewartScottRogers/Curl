namespace Curl.Http2;

/// <summary>
/// Shared helpers for the frame-layer tests: frames to wire bytes and back, and the
/// connection a test drives.
/// </summary>
internal static class Http2Test
{
    /// <summary>Serializes the frames one after another, as a peer would send them.</summary>
    public static byte[] Wire(params Http2Frame[] frames) => [.. frames.SelectMany(Http2FrameCodec.Serialize)];

    /// <summary>Reads every frame in <paramref name="bytes" />.</summary>
    public static async Task<List<Http2Frame>> FramesIn(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        var frames = new List<Http2Frame>();
        while (await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.LargestMaximumFrameSize, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>Asserts two frames carry the same header fields and payload bytes.</summary>
    public static void AssertFrame(Http2Frame expected, Http2Frame actual)
    {
        Assert.AreEqual(expected.Type, actual.Type);
        Assert.AreEqual(expected.Flags, actual.Flags);
        Assert.AreEqual(expected.StreamId, actual.StreamId);
        CollectionAssert.AreEqual(expected.Payload.ToArray(), actual.Payload.ToArray());
    }

    /// <summary>Returns the error code of the <see cref="Http2ProtocolException" /> <paramref name="action" /> throws.</summary>
    public static Http2ErrorCode ErrorOf(Action action) =>
        Assert.ThrowsExactly<Http2ProtocolException>(action).ErrorCode;
}

/// <summary>
/// A connection's stream in a test: reads come from the peer's scripted bytes, at most
/// <c>readSize</c> at a time so frames arrive split, and writes are recorded.
/// </summary>
internal sealed class PeerStream(byte[] fromPeer, int readSize = int.MaxValue) : Stream
{
    private readonly MemoryStream input = new(fromPeer);

    /// <summary>Gets everything the connection wrote.</summary>
    public MemoryStream Written { get; } = new();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, Math.Min(count, readSize));

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

    /// <summary>Reads the frames the connection wrote.</summary>
    public Task<List<Http2Frame>> WrittenFrames() => Http2Test.FramesIn(Written.ToArray());
}
