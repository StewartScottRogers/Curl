using Curl.Core;
using Curl.Original.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// A read-only view of a <c>-T</c> or <c>-F</c> upload that holds its asynchronous reads at
/// <c>--limit-rate</c> with a <see cref="RateLimitedStream" />, as curl 8.22.0 holds the request
/// body at the same rate it holds the response (<c>CURLOPT_MAX_SEND_SPEED_LARGE</c>): a
/// <c>-F f=@f.txt --limit-rate 10</c> POST sends its head and then 10 body bytes a second
/// (AF-0300, BL-2938, ADR-0745). Seeking, the length and the position pass through to the wrapped
/// stream, so a resend can still rewind it; a synchronous read passes through unlimited, since
/// waiting there would block a thread. It still tells the HTTP handler whether the wrapped stream
/// could be rewound (<see cref="CanRewind" />). The wrapped stream is not owned.
/// </summary>
/// <param name="inner">The upload source.</param>
/// <param name="bytesPerSecond">The <c>--limit-rate</c>, in bytes per second; at least 1.</param>
/// <param name="timeProvider">The clock the waits are measured and taken on.</param>
internal sealed class RateLimitedUploadStream(Stream inner, long bytesPerSecond, TimeProvider timeProvider)
    : Stream, IRewindableUpload
{
    private readonly RateLimitedStream limiter = new(inner, bytesPerSecond, timeProvider);

    /// <summary>
    /// Gets a value indicating whether the wrapped stream could be rewound for a resend: unless it
    /// says itself, as <see cref="StandardInputUploadStream" /> does, that it cannot.
    /// </summary>
    public bool CanRewind => inner is not IRewindableUpload { CanRewind: false };

    /// <inheritdoc />
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => inner.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    /// <inheritdoc />
    /// <remarks>Does nothing; the stream is read-only.</remarks>
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        limiter.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        limiter.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the stream is read-only.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the stream is read-only.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
