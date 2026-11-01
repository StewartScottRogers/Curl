namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// An output <see cref="MemoryStream" /> that calls <paramref name="written" /> after every write, as
/// a libcurl write callback that unpauses the upload with <c>curl_easy_pause(CURLPAUSE_CONT)</c> does.
/// </summary>
/// <param name="written">What to call after each write.</param>
public sealed class WriteNotifyingStream(Action written) : MemoryStream
{
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        base.Write(buffer, offset, count);
        written();
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        base.Write(buffer);
        written();
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        written();
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        written();
    }
}
