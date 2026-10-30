namespace Curl.Extension.Har;

/// <summary>
/// A write-only stream that passes every byte of the response body on to the transfer's output unchanged and
/// keeps a copy for the HAR entry's <c>content</c> (HAR 1.2, "content").
/// </summary>
/// <param name="output">Where the bytes go on to.</param>
internal sealed class BodyCopyingStream(Stream output) : Stream
{
    private readonly MemoryStream _copy = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Gives a copy of every byte written so far.</summary>
    /// <returns>The bytes, in the order they were written.</returns>
    internal byte[] Copied() => _copy.ToArray();

    public override void Write(byte[] buffer, int offset, int count)
    {
        output.Write(buffer, offset, count);
        _copy.Write(buffer, offset, count);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _copy.Write(buffer.Span);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => output.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _copy.Dispose();
        base.Dispose(disposing);
    }
}
