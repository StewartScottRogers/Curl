using System.Net.Http.Headers;

namespace Curl.Api;

/// <summary>
/// The response body <see cref="CurlHttpMessageHandler"/> gives its content: reads pass through to the
/// <see cref="CurlResponse.Body"/>, and the first read that finds its end copies the response's trailer
/// fields into the message's trailing headers. Read-only and not seekable; disposing it disposes the body.
/// </summary>
/// <param name="body">The response body.</param>
/// <param name="trailers">The response's trailer fields, complete once the body has ended.</param>
/// <param name="trailingHeaders">The message's trailing headers, filled at the body's end.</param>
internal sealed class TrailerFillingStream(Stream body, IReadOnlyList<KeyValuePair<string, string>> trailers, HttpHeaders trailingHeaders) : Stream
{
    /// <summary>Whether the trailers have been copied, so a read after the end does not add them twice.</summary>
    private bool _filled;

    /// <inheritdoc/>
    public override bool CanRead => body.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        FillAtEnd(await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false), buffer.Length);

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => FillAtEnd(body.Read(buffer, offset, count), count);

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask DisposeAsync() => body.DisposeAsync();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        body.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Copies the trailers the first time a read of <paramref name="asked"/> bytes found the body's end.</summary>
    /// <param name="read">The bytes the read gave.</param>
    /// <param name="asked">The bytes the read asked for.</param>
    /// <returns><paramref name="read"/>.</returns>
    private int FillAtEnd(int read, int asked)
    {
        if (read == 0 && asked > 0 && !_filled)
        {
            _filled = true;
            foreach ((string name, string value) in trailers)
            {
                _ = trailingHeaders.TryAddWithoutValidation(name, value);
            }
        }

        return read;
    }
}
