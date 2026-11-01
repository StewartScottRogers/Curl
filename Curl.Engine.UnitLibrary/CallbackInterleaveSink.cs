using Curl.Api.Callbacks;
using Curl.Original.Protocol.Rtsp;

namespace Curl.Engine;

/// <summary>
/// The RTSP handler's interleave sink over a handle's <c>CURLOPT_INTERLEAVEFUNCTION</c> (ADR-0619): each whole
/// <c>$</c> frame, header included, goes to the callback as curl 8.22.0's <c>rtp_client_write</c> hands it over, and
/// the callback's answer is the count taken, anything but the frame's length failing the transfer with 23.
/// </summary>
/// <param name="interleave">The handle's interleave callback.</param>
internal sealed class CallbackInterleaveSink(InterleaveCallback interleave) : IRtspInterleaveSink
{
    /// <summary>
    /// Gives a sink over <paramref name="interleave" /> when it is set, else <see langword="null" />, so the RTSP
    /// handler writes the frames to the body output as libcurl does with no interleave function.
    /// </summary>
    /// <param name="interleave">The handle's interleave callback, or <see langword="null" />.</param>
    /// <returns>The sink, or <see langword="null" />.</returns>
    internal static IRtspInterleaveSink? Of(InterleaveCallback? interleave) =>
        interleave is null ? null : new CallbackInterleaveSink(interleave);

    /// <inheritdoc />
    public ValueTask<long> WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) =>
        ValueTask.FromResult(interleave(frame.Span));
}
