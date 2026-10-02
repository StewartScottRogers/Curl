using System.Globalization;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes one transfer's HTTP/2 or HTTP/3 frames to Curl's own diagnostic log (ADR-0222,
/// ADR-0345, BL-1073), under component <c>http2</c> or <c>http3</c>: each frame sent or
/// received, by type, stream and length, as <c>verbose</c>; the server's SETTINGS as
/// <c>info</c>; and the server's GOAWAY and stream resets as <c>warning</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <param name="component">The <see cref="DiagnosticLogComponents" /> name every line carries.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message
/// (ADR-0222, decision 8). A frame's content is never logged: a HEADERS frame is named by its
/// type, stream and length alone, so no header value reaches the log.
/// </remarks>
internal sealed class HttpFrameLog(IDiagnosticLog log, string component)
{
    /// <summary>Gets a log that writes nothing, for a stream no transfer gave a log.</summary>
    internal static HttpFrameLog Silent { get; } = new(NoDiagnosticLog.Instance, DiagnosticLogComponents.Http2);

    /// <summary>Gets the component every line carries.</summary>
    internal string Component => component;

    /// <summary>
    /// Makes the frame log of a transfer on a session speaking <paramref name="versionName" />,
    /// or <see cref="Silent" /> when the transfer gave no log.
    /// </summary>
    /// <param name="log">The transfer's log, or <see langword="null" /> for none.</param>
    /// <param name="versionName">The session's version name, <c>HTTP/2</c> or <c>HTTP/3</c>.</param>
    /// <returns>The frame log.</returns>
    internal static HttpFrameLog For(IDiagnosticLog? log, string versionName) =>
        log is null ? Silent : new(log, HttpExchangeLog.ComponentOf(versionName));

    /// <summary>Logs, at <c>verbose</c>, a frame sent: <c>HEADERS sent on stream 1, 23 bytes</c>.</summary>
    /// <param name="frameType">The frame's type as RFC 9113 or RFC 9114 names it.</param>
    /// <param name="streamId">The stream it was sent on.</param>
    /// <param name="length">Its payload's length.</param>
    internal void FrameSent(string frameType, long streamId, int length) => WriteFrame(frameType, "sent", streamId, length);

    /// <summary>Logs, at <c>verbose</c>, a frame received: <c>DATA received on stream 1, 5 bytes</c>.</summary>
    /// <param name="frameType">The frame's type as RFC 9113 or RFC 9114 names it.</param>
    /// <param name="streamId">The stream it arrived on.</param>
    /// <param name="length">Its payload's length; a header block's whole length for HEADERS.</param>
    internal void FrameReceived(string frameType, long streamId, int length) => WriteFrame(frameType, "received", streamId, length);

    /// <summary>Logs, at <c>info</c>, the HTTP/2 server's first SETTINGS as they stand once applied.</summary>
    /// <param name="settings">The peer's settings.</param>
    internal void SettingsReceived(Http2Settings settings)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            string maxConcurrentStreams = settings.MaxConcurrentStreams?.ToString(CultureInfo.InvariantCulture) ?? "unlimited";
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"SETTINGS received: max concurrent streams {maxConcurrentStreams}, initial window {settings.InitialWindowSize}, max frame {settings.MaxFrameSize}, header table {settings.HeaderTableSize}"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, the HTTP/2 server's GOAWAY: its last stream and error code.</summary>
    /// <param name="goAway">The GOAWAY's payload.</param>
    internal void GoAwayReceived(Http2GoAwayPayload goAway)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"GOAWAY received: last stream {goAway.LastStreamId}, error {HttpTransferMessages.Http2ErrorName(goAway.ErrorCode)} ({(uint)goAway.ErrorCode})"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, the HTTP/2 server's RST_STREAM: its stream and error code.</summary>
    /// <param name="reset">The reset.</param>
    internal void ResetReceived(Http2StreamResetException reset)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"RST_STREAM received on stream {reset.StreamId}: error {HttpTransferMessages.Http2ErrorName(reset.ErrorCode)} ({(uint)reset.ErrorCode})"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, the HTTP/3 server's reset of a request stream (QUIC RESET_STREAM) with its application error code.</summary>
    /// <param name="streamId">The stream reset.</param>
    /// <param name="errorCode">The HTTP/3 application error code.</param>
    internal void StreamResetReceived(long streamId, long errorCode)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"RESET_STREAM received on stream {streamId}: error 0x{errorCode:x}"));
        }
    }

    private void WriteFrame(string frameType, string direction, long streamId, int length)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(
                CultureInfo.InvariantCulture,
                $"{frameType} {direction} on stream {streamId}, {length} bytes"));
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, component, message);
}
