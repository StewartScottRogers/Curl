using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.Options.Http;

namespace Curl.Engine;

/// <summary>
/// The body of a <c>curl_mime_data_cb</c> part (<see cref="EasyMimeCallback" />) in a <c>CURLOPT_MIMEPOST</c> body: a
/// <see cref="CallbackReadStream" /> over the part's read callback, so <c>CURL_READFUNC_ABORT</c> marks it
/// <see cref="CallbackReadStream.Aborted" /> and the perform ends with <c>CURLE_ABORTED_BY_CALLBACK</c>. A part with a
/// seek callback can seek, so the multipart body around it can be rewound for a resend: setting
/// <see cref="Position" /> to 0 once the read callback has given bytes calls the seek callback with offset 0 from
/// <see cref="SeekOrigin.Begin" />, as libcurl 8.22.0's <c>mime_part_rewind</c> does, and any result but
/// <see cref="SeekResult.Ok" /> fails the rewind with libcurl's <c>seek callback returned error &lt;n&gt;</c>.
/// </summary>
/// <param name="part">The part's data.</param>
/// <param name="pauseGate">The handle's pause state, so <c>CURL_READFUNC_PAUSE</c> waits for <c>curl_easy_pause</c> (upstream tests 670 to 673, BL-2631), or <see langword="null" /> when the transfer cannot pause.</param>
internal sealed class MimeCallbackReadStream(EasyMimeCallback part, EasyPauseGate? pauseGate = null) : CallbackReadStream(part.Read, pauseGate)
{
    /// <inheritdoc />
    public override bool CanSeek => part.Seek is not null;

    /// <summary>
    /// Gets a value indicating whether the part has a known size and the read callback has given all of it, so it is
    /// not asked again, as curl 8.22.0's <c>read_part_content</c> spares the read once <c>offset &gt;= datasize</c>
    /// (upstream test 554, BL-2613).
    /// </summary>
    private bool AtSizedEnd => part.Size >= 0 && BytesReadSinceRewind >= part.Size;

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => AtSizedEnd ? 0 : base.Read(buffer);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        AtSizedEnd ? ValueTask.FromResult(0) : base.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">The part's size is unknown (-1) or it has no seek callback.</exception>
    public override long Length => CanSeek && part.Size >= 0 ? part.Size : throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">The part has no seek callback.</exception>
    /// <exception cref="IOException">The seek callback did not return <see cref="SeekResult.Ok" />.</exception>
    public override long Position
    {
        get => CanSeek ? BytesReadSinceRewind : throw new NotSupportedException();
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">The part has no seek callback.</exception>
    /// <exception cref="IOException">The seek callback did not return <see cref="SeekResult.Ok" />.</exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        SeekCallback seek = part.Seek ?? throw new NotSupportedException();
        if (offset == BytesReadSinceRewind && origin == SeekOrigin.Begin)
        {
            return offset;
        }

        SeekResult result = seek(offset, origin);
        if (result != SeekResult.Ok)
        {
            throw new IOException(SeekingCallbackReadStream.SeekFailedPrefix + ((int)result).ToString(CultureInfo.InvariantCulture));
        }

        BytesReadSinceRewind = offset;
        return offset;
    }
}
