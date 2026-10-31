namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Takes each interleaved RTP frame an RTSP transfer reads - <c>$</c>, the channel byte, the
/// 16-bit big-endian length and the payload, whole - as curl 8.22.0's <c>rtp_client_write</c>
/// hands it to <c>CURLOPT_INTERLEAVEFUNCTION</c> (BL-2477, ADR-0612).
/// </summary>
public interface IRtspInterleaveSink
{
    /// <summary>Takes one whole frame.</summary>
    /// <param name="frame">The frame, header included.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// How many bytes were taken; anything other than the frame's length fails the transfer with
    /// 23, <c>Failed writing RTP data</c>.
    /// </returns>
    ValueTask<long> WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken);
}
