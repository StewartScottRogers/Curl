using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes the <c>[HTTP/2]</c> lines curl 8.21.0's nghttp2 layer writes under
/// <c>-v --trace-config http/2</c> (BL-1167, ADR-0373): <c>[0] created h2 session</c> as the
/// preface goes out, <c>[&lt;stream&gt;] -&gt; FRAME[...]</c> for each frame sent and
/// <c>[&lt;stream&gt;] &lt;- FRAME[...]</c> for each frame received, as curl's <c>fr_print</c>
/// describes them, the server's <c>MAX_CONCURRENT_STREAMS</c> and <c>ENABLE_PUSH</c> after each
/// SETTINGS it sends, and <c>[&lt;stream&gt;] CLOSED</c> as a response ends. Measured with curl
/// 8.18.0's OpenSSL build (nghttp2 1.68.0); see BL-1167's Notes.
/// </summary>
/// <param name="events">The transfer's events, which the lines are reported through as info lines.</param>
/// <remarks>
/// nghttp2 hands curl a header block whole, so a CONTINUATION frame is never named. curl's lines
/// about its own buffering (<c>ingress</c>, <c>cf_send</c>, <c>cf_recv</c>, window
/// arithmetic), whose count varies from run to run, are not written.
/// </remarks>
internal sealed class Http2FrameTrace(ITransferEvents events) : IHttp2FrameObserver
{
    /// <summary>The most GOAWAY debug bytes curl copies into its <c>reason</c>.</summary>
    private const int MaximumReasonLength = 127;

    /// <summary>The server's SETTINGS_MAX_CONCURRENT_STREAMS: nghttp2's unlimited until it sends one.</summary>
    private uint maxConcurrentStreams = uint.MaxValue;

    /// <summary>The server's SETTINGS_ENABLE_PUSH: on until it sends 0, as nghttp2 keeps it.</summary>
    private bool enablePush = true;

    /// <summary>Reports <c>[HTTP/2] [0] created h2 session</c>, before the preface's frames.</summary>
    internal void SessionCreated() => Report(0, "created h2 session");

    /// <summary>Reports <c>[HTTP/2] [&lt;stream&gt;] CLOSED</c> once the response on a stream has ended.</summary>
    /// <param name="streamId">The stream.</param>
    internal void StreamClosed(int streamId) => Report(streamId, "CLOSED");

    /// <inheritdoc />
    public void FrameSent(Http2Frame frame)
    {
        if (frame.Type != Http2FrameType.Continuation)
        {
            Report(frame.StreamId, "-> " + Describe(frame));
        }
    }

    /// <inheritdoc />
    public void FrameReceived(Http2Frame frame)
    {
        if (frame.Type == Http2FrameType.Continuation)
        {
            return;
        }

        Report(frame.StreamId, "<- " + Describe(frame));
        if (frame.Type == Http2FrameType.Settings && !frame.HasFlag(Http2FrameFlags.Acknowledgement))
        {
            ApplySettings(frame);
            Report(0, string.Create(CultureInfo.InvariantCulture, $"MAX_CONCURRENT_STREAMS: {unchecked((int)maxConcurrentStreams)}"));
            Report(0, "ENABLE_PUSH: " + (enablePush ? "TRUE" : "false"));
        }
    }

    /// <summary>Describes a frame as curl's <c>fr_print</c> does: <c>FRAME[HEADERS, len=28, hend=1, eos=1]</c>.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The description.</returns>
    internal static string Describe(Http2Frame frame)
    {
        int length = frame.Payload.Length;
        ReadOnlySpan<byte> payload = frame.Payload.Span;
        return frame.Type switch
        {
            Http2FrameType.Data => Text($"FRAME[DATA, len={length}, eos={Bit(frame, Http2FrameFlags.EndStream)}, padlen={PaddingLength(frame)}]"),
            Http2FrameType.Headers => Text($"FRAME[HEADERS, len={length}, hend={Bit(frame, Http2FrameFlags.EndHeaders)}, eos={Bit(frame, Http2FrameFlags.EndStream)}]"),
            Http2FrameType.Priority => Text($"FRAME[PRIORITY, len={length}, flags={frame.Flags}]"),
            Http2FrameType.RstStream => Text($"FRAME[RST_STREAM, len={length}, flags={frame.Flags}, error={BinaryPrimitives.ReadUInt32BigEndian(payload)}]"),
            Http2FrameType.Settings => frame.HasFlag(Http2FrameFlags.Acknowledgement) ? "FRAME[SETTINGS, ack=1]" : Text($"FRAME[SETTINGS, len={length}]"),
            Http2FrameType.PushPromise => Text($"FRAME[PUSH_PROMISE, len={length}, hend={Bit(frame, Http2FrameFlags.EndHeaders)}]"),
            Http2FrameType.Ping => Text($"FRAME[PING, len={length}, ack={Bit(frame, Http2FrameFlags.Acknowledgement)}]"),
            Http2FrameType.GoAway => DescribeGoAway(payload),
            Http2FrameType.WindowUpdate => Text($"FRAME[WINDOW_UPDATE, incr={BinaryPrimitives.ReadUInt32BigEndian(payload) & 0x7FFFFFFF}]"),
            _ => Text($"FRAME[{(byte)frame.Type}, len={length}, flags={frame.Flags}]"),
        };
    }

    /// <summary>
    /// Describes a GOAWAY: its error code, its debug data up to the first NUL and at most 127
    /// bytes as <c>reason</c>, and its last stream.
    /// </summary>
    private static string DescribeGoAway(ReadOnlySpan<byte> payload)
    {
        int lastStreamId = (int)(BinaryPrimitives.ReadUInt32BigEndian(payload) & 0x7FFFFFFF);
        int errorCode = unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[4..]));
        ReadOnlySpan<byte> debugData = payload[8..];
        debugData = debugData[..Math.Min(debugData.Length, MaximumReasonLength)];
        int end = debugData.IndexOf((byte)0);
        string reason = Encoding.Latin1.GetString(end < 0 ? debugData : debugData[..end]);
        return Text($"FRAME[GOAWAY, error={errorCode}, reason='{reason}', last_stream={lastStreamId}]");
    }

    /// <summary>nghttp2's <c>padlen</c>: the padding and its one-byte length field, 0 when unpadded.</summary>
    private static int PaddingLength(Http2Frame frame) =>
        frame.HasFlag(Http2FrameFlags.Padded) && !frame.Payload.IsEmpty ? frame.Payload.Span[0] + 1 : 0;

    private static int Bit(Http2Frame frame, byte flag) => frame.HasFlag(flag) ? 1 : 0;

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private void ApplySettings(Http2Frame frame)
    {
        ReadOnlySpan<byte> payload = frame.Payload.Span;
        for (int offset = 0; offset + 6 <= payload.Length; offset += 6)
        {
            ushort identifier = BinaryPrimitives.ReadUInt16BigEndian(payload[offset..]);
            uint value = BinaryPrimitives.ReadUInt32BigEndian(payload[(offset + 2)..]);
            if (identifier == (ushort)Http2SettingIdentifier.MaxConcurrentStreams)
            {
                maxConcurrentStreams = value;
            }
            else if (identifier == (ushort)Http2SettingIdentifier.EnablePush)
            {
                enablePush = value != 0;
            }
        }
    }

    private void Report(int streamId, string text) =>
        events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"[HTTP/2] [{streamId}] {text}"));
}
