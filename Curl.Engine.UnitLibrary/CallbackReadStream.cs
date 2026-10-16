using Curl.Api.Callbacks;

namespace Curl.Engine;

/// <summary>
/// A read-only <see cref="Stream" /> over an easy handle's <c>CURLOPT_READFUNCTION</c>
/// (<see cref="ReadCallback" />), the upload source a handler reads (ADR-0542): each read asks the
/// callback to fill the buffer, and a return of zero is the end of the upload, as libcurl takes it.
/// A return below zero or past the buffer's length - libcurl's <c>CURL_READFUNC_ABORT</c> and
/// <c>CURL_READFUNC_PAUSE</c> among them, which the engine does not honour yet - fails the read with
/// libcurl's <c>CURLE_READ_ERROR</c> text.
/// </summary>
/// <param name="callback">The read callback.</param>
internal sealed class CallbackReadStream(ReadCallback callback) : Stream
{
    /// <summary>libcurl's text for a read callback that returned more than it was asked for.</summary>
    internal const string FunnyValueText = "read function returned funny value";

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
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        long read = callback(buffer);
        return read >= 0 && read <= buffer.Length
            ? (int)read
            : throw new IOException(FunnyValueText);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
