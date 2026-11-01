using Curl.Original.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// A read-only stream over standard input for <c>-T -</c> and <c>-T .</c> that cannot seek, so a
/// protocol handler never learns its size, whatever standard input is: curl 8.21.0 never sizes
/// standard input, so over HTTP/1.1 it sends the upload chunked with <c>Expect: 100-continue</c>
/// and under <c>-0</c> refuses it with exit 25, even when a file is redirected into it
/// (GF-0007, BL-1800). The bytes pass through unchanged. The wrapped stream is not owned.
/// It still tells the HTTP handler whether curl's seek callback could rewind it
/// (<see cref="CanRewind" />).
/// </summary>
/// <param name="inner">Standard input.</param>
internal sealed class StandardInputUploadStream(Stream inner) : Stream, IRewindableUpload
{
    /// <summary>
    /// Gets a value indicating whether the C runtime's <c>lseek</c> succeeds on standard input
    /// even when it cannot seek, as the Windows C runtime's does on a pipe; elsewhere it fails
    /// there. By default it does on Windows only (measured, BL-2337 Notes).
    /// </summary>
    internal bool SeekOfUnseekableInputSucceeds { get; init; } = OperatingSystem.IsWindows();

    /// <summary>
    /// Gets a value indicating whether curl could rewind standard input for a resend: when the
    /// wrapped stream can seek, as a file redirected into it can, or when
    /// <see cref="SeekOfUnseekableInputSucceeds" />. Off Windows, stdin read through a pipe cannot
    /// be rewound, so curl 8.22.0 fails a chunked resend of it to an HTTP/1.0 server with exit 65
    /// rather than 25 (upstream test1072 and test1073 as GF-0082 runs them, ADR-0524, BL-2337).
    /// </summary>
    public bool CanRewind => inner.CanSeek || SeekOfUnseekableInputSucceeds;

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

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
