namespace Curl.Mcp;

/// <summary>
/// A write-only stream that keeps, in memory, only as many bytes as a tool can return - the
/// <see cref="McpResponseCap" /> and one byte more, so a cut can be moved off a split character -
/// and counts every byte written, so the cut can be stated (ADR-0569).
/// </summary>
/// <param name="cap">The most bytes a tool returns.</param>
public sealed class McpCappedCaptureStream(McpResponseCap cap) : Stream
{
    private readonly MemoryStream kept = new();

    private readonly int keepLimit = cap.MaxBytes == int.MaxValue ? int.MaxValue : cap.MaxBytes + 1;

    /// <summary>How many bytes were written, kept or not.</summary>
    public long TotalBytes { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>The captured output encoded for a tool result, cut to the cap with the cut stated.</summary>
    /// <returns>The returned bytes.</returns>
    public McpReturnedBytes ToReturnedBytes() =>
        McpReturnedBytes.Encode(kept.GetBuffer().AsSpan(0, (int)kept.Length), (int)Math.Min(TotalBytes, int.MaxValue), cap);

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        TotalBytes += buffer.Length;
        int room = keepLimit - (int)kept.Length;
        kept.Write(buffer[..Math.Min(room, buffer.Length)]);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            kept.Dispose();
        }

        base.Dispose(disposing);
    }
}
