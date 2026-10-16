using System.Globalization;

using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A write-only stream that hands every write to a libcurl write or header callback, as libcurl
/// hands each chunk it receives to <c>CURLOPT_WRITEFUNCTION</c> or <c>CURLOPT_HEADERFUNCTION</c>.
/// A callback that returns anything but the chunk's length fails the write with an
/// <see cref="OutputWriteFailedException" /> carrying what it returned, which ends the transfer with
/// <c>CURLE_WRITE_ERROR</c> and the runner's exit 23 text,
/// <c>Failure writing output to destination, passed N returned M</c>, as libcurl does.
/// </summary>
/// <param name="callback">Receives each chunk and returns how many bytes it took.</param>
internal sealed class CallbackWriteStream(Func<ReadOnlySpan<byte>, long> callback) : Stream
{
    /// <summary>Creates the stream over a write callback.</summary>
    /// <param name="callback">The <c>CURLOPT_WRITEFUNCTION</c>.</param>
    /// <returns>The stream.</returns>
    internal static CallbackWriteStream Of(WriteCallback callback) => new(data => callback(data));

    /// <summary>Creates the stream over a header callback.</summary>
    /// <param name="callback">The <c>CURLOPT_HEADERFUNCTION</c>.</param>
    /// <returns>The stream.</returns>
    internal static CallbackWriteStream Of(HeaderCallback callback) => new(header => callback(header));

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
        long returned = callback(buffer);
        if (returned != buffer.Length)
        {
            throw new OutputWriteFailedException(
                (int)Math.Clamp(returned, 0, int.MaxValue),
                string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {buffer.Length} returned {returned}"));
        }
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
