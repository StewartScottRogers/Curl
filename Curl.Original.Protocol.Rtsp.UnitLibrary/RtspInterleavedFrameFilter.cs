using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Splits interleaved <c>$</c> RTP frames out of the bytes an RTSP transfer reads outside a reply
/// and hands each one whole to an <see cref="IRtspInterleaveSink" />, as curl 8.22.0's
/// <c>rtsp_filter_rtp</c> and <c>rtp_client_write</c> do (BL-2477, ADR-0612).
/// </summary>
/// <remarks>
/// A frame is <c>$</c>, a channel byte, a 16-bit big-endian payload length and the payload. One
/// not yet received whole is left unconsumed, so the caller joins it with the next read before
/// the sink sees it. Only a channel a reply's <c>Transport</c> set up is a frame (<c>rtp_channel_mask</c>, BL-2787): a
/// <c>$</c> on any other channel is a stray byte. Any stray byte is skipped, except, before a reply, one where <c>RTSP/</c> may
/// begin. A sink that takes fewer bytes than the frame fails the transfer with 23.
/// </remarks>
/// <param name="sink">Takes each frame.</param>
/// <param name="isChannelSetUp">Whether a channel byte names a channel set up for interleaved RTP.</param>
internal sealed class RtspInterleavedFrameFilter(IRtspInterleaveSink sink, Func<byte, bool> isChannelSetUp)
{
    /// <summary>The exit 23 message for a sink that took fewer bytes than its frame, curl 8.22.0's text.</summary>
    internal const string WriteFailed = "Failed writing RTP data";

    /// <summary>The longest frame: its four header bytes and a payload of 65535.</summary>
    internal const int MaximumFrameLength = FrameHeaderLength + ushort.MaxValue;

    private const int FrameHeaderLength = 4;

    private const int ChannelOffset = 1;

    private const byte FrameStart = (byte)'$';

    /// <summary>Gets how many frame bytes the sink has taken so far.</summary>
    internal long BytesHanded { get; private set; }

    /// <summary>
    /// Determines whether <paramref name="received" /> matches <c>RTSP/</c> as far as it goes, so a
    /// reply may begin there.
    /// </summary>
    internal static bool CouldStartReply(ReadOnlySpan<byte> received)
    {
        ReadOnlySpan<byte> prefix = "RTSP/"u8;
        int length = Math.Min(received.Length, prefix.Length);
        return received[..length].SequenceEqual(prefix[..length]);
    }

    /// <summary>
    /// Hands every whole frame at the start of <paramref name="received" /> to the sink, skipping
    /// stray bytes between them, and stops at a frame not yet received whole or, when
    /// <paramref name="stopAtReply" />, where a reply may begin.
    /// </summary>
    /// <param name="received">The bytes read and not yet consumed.</param>
    /// <param name="stopAtReply">Whether a reply may follow, so <c>RTSP/</c> ends the frames.</param>
    /// <param name="cancellationToken">Cancels the sink's writes.</param>
    /// <returns>How many bytes from the start of <paramref name="received" /> were consumed.</returns>
    /// <exception cref="RtspTransferException">The sink took fewer bytes than a frame: exit 23.</exception>
    internal async ValueTask<int> ConsumeAsync(ReadOnlyMemory<byte> received, bool stopAtReply, CancellationToken cancellationToken)
    {
        int position = 0;
        while (position < received.Length)
        {
            int frameLength = WholeFrameLength(received.Span[position..]);
            if (frameLength > 0)
            {
                await HandAsync(received.Slice(position, frameLength), cancellationToken).ConfigureAwait(false);
                position += frameLength;
            }
            else if (frameLength == 0 || (stopAtReply && CouldStartReply(received.Span[position..])))
            {
                break;
            }
            else
            {
                position++;
            }
        }

        return position;
    }

    /// <summary>Measures the frame at the start of <paramref name="received" />.</summary>
    /// <returns>Its length when it is whole, 0 when it is not yet, or -1 when no frame starts there.</returns>
    private int WholeFrameLength(ReadOnlySpan<byte> received)
    {
        if (received[0] != FrameStart)
        {
            return -1;
        }

        if (received.Length <= ChannelOffset)
        {
            return 0;
        }

        if (!isChannelSetUp(received[ChannelOffset]))
        {
            return -1;
        }

        if (received.Length < FrameHeaderLength)
        {
            return 0;
        }

        int length = FrameHeaderLength + ((received[2] << 8) | received[3]);
        return received.Length < length ? 0 : length;
    }

    private async ValueTask HandAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        long taken = await sink.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        if (taken != frame.Length)
        {
            throw new RtspTransferException(CurlExitCode.WriteError, WriteFailed);
        }

        BytesHanded += taken;
    }
}
