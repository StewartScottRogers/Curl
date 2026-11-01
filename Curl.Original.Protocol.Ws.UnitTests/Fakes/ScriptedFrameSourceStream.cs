namespace Curl.Original.Protocol.Ws.Fakes;

/// <summary>
/// A read callback's upload as <see cref="WsProtocolHandler" /> sees it: an <see cref="IWsUploadFrameSource" /> stream
/// whose reads each run the next scripted step, which fills the buffer and answers its count (or throws), and
/// answer 0 once the steps run out. It records the length each read asked for.
/// </summary>
/// <param name="uploadFrames">The frames the handler sends the reads in, or <see langword="null" /> to have each read filled, as any other upload.</param>
/// <param name="steps">The reads, in order.</param>
internal sealed class ScriptedFrameSourceStream(WsUploadFrames? uploadFrames, params Func<Memory<byte>, int>[] steps) : Stream, IWsUploadFrameSource
{
    private readonly Queue<Func<Memory<byte>, int>> pending = new(steps);

    /// <inheritdoc />
    public WsUploadFrames? UploadFrames => uploadFrames;

    /// <summary>Gets the buffer length each read asked for, in order.</summary>
    public List<int> AskedLengths { get; } = [];

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>A step that copies <paramref name="text" />'s Latin-1 bytes into the buffer.</summary>
    /// <param name="text">The bytes to give.</param>
    /// <returns>The step.</returns>
    public static Func<Memory<byte>, int> Gives(string text) => buffer =>
    {
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(text);
        bytes.CopyTo(buffer);
        return bytes.Length;
    };

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Next(buffer.AsMemory(offset, count));

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Next(buffer));

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Next(Memory<byte> buffer)
    {
        AskedLengths.Add(buffer.Length);
        return pending.TryDequeue(out Func<Memory<byte>, int>? step) ? step(buffer) : 0;
    }
}
