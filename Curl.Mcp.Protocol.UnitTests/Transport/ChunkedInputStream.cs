namespace Curl.Mcp.Protocol.Transport;

/// <summary>
/// An in-memory pipe's reading end: each read returns the next chunk whole, and once the chunks run out a read
/// either ends the input or waits until it is cancelled, as a pipe whose writer is still open does.
/// </summary>
internal sealed class ChunkedInputStream(IEnumerable<byte[]> chunks, bool waitAfterLastChunk) : Stream
{
    private readonly Queue<byte[]> remaining = new(chunks);

    public int ReadCount { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        if (remaining.TryDequeue(out byte[]? chunk))
        {
            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        if (waitAfterLastChunk)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return 0;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
