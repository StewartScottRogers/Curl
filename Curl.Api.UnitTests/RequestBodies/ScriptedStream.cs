namespace Curl.Api.RequestBodies;

/// <summary>
/// A stream over fixed bytes that can be made non-seekable, unreadable, or to throw on read or seek,
/// for the request body tests.
/// </summary>
internal sealed class ScriptedStream(byte[] bytes) : Stream
{
    private readonly MemoryStream _inner = new(bytes);

    public bool Seekable { get; init; } = true;

    public bool Readable { get; init; } = true;

    public bool ThrowOnRead { get; init; }

    public bool ThrowOnSeek { get; init; }

    public override bool CanRead => Readable;

    public override bool CanSeek => Seekable;

    public override bool CanWrite => false;

    public override long Length => Seekable ? _inner.Length : throw new NotSupportedException();

    public override long Position
    {
        get => Seekable ? _inner.Position : throw new NotSupportedException();
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) =>
        ThrowOnRead ? throw new IOException("read failed") : _inner.Read(buffer);

    public override long Seek(long offset, SeekOrigin origin) =>
        ThrowOnSeek ? throw new IOException("seek failed") : _inner.Seek(offset, origin);

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _inner.Dispose();
        base.Dispose(disposing);
    }
}
