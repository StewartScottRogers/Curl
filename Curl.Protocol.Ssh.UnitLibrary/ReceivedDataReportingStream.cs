using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <summary>
/// A write-only view of a transfer's output that reports each write as received data
/// before passing it on, which gives <c>-v</c> its <c>{ [N bytes data]</c> line and
/// <c>--trace</c> its <c>&lt;= Recv data</c> dump for an <c>scp</c> or <c>sftp</c>
/// download (ADR-0262). The output itself is never disposed here.
/// </summary>
/// <param name="output">The transfer's output.</param>
/// <param name="events">Where each write is reported.</param>
internal sealed class ReceivedDataReportingStream(Stream output, ITransferEvents events) : Stream
{
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
    public override void Flush() => output.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        events.ReportDataReceived(buffer.AsSpan(offset, count));
        output.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        events.ReportDataReceived(buffer.Span);
        return output.WriteAsync(buffer, cancellationToken);
    }
}
