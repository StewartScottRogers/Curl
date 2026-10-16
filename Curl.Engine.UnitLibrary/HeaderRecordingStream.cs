using System.Text;

namespace Curl.Engine;

/// <summary>
/// The header output of a libcurl handle's HTTP transfer: hands every complete line written to
/// <paramref name="store" /> (its bytes read as Latin-1, its line ending dropped) and then the whole write to
/// <paramref name="inner" />, the header callback's stream, when there is one (ADR-0603). A line split across
/// writes is recorded once its line feed arrives.
/// </summary>
/// <param name="store">The handle's headers.</param>
/// <param name="inner">The header callback's stream, or <see langword="null" /> when none is set.</param>
internal sealed class HeaderRecordingStream(TransferHeaderStore store, Stream? inner) : Stream, IConnectReplyHeadMarking
{
    private readonly List<byte> pendingLine = [];

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

    /// <inheritdoc />
    public void MarkConnectReplyHead() => store.MarkConnectReplyHead();

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
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Record(buffer);
        inner?.Write(buffer);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Record(buffer.Span);
        if (inner is not null)
        {
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Record(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> rest = bytes;
        int lineFeed;
        while ((lineFeed = rest.IndexOf((byte)'\n')) >= 0)
        {
            pendingLine.AddRange(rest[..lineFeed]);
            store.RecordLine(Encoding.Latin1.GetString([.. pendingLine]).TrimEnd('\r'));
            pendingLine.Clear();
            rest = rest[(lineFeed + 1)..];
        }

        pendingLine.AddRange(rest);
    }
}
