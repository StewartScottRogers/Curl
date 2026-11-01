using Curl.Api.EngineContracts.WebSocket;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

/// <summary>
/// Maps between RFC 6455 frames and libcurl's <c>CURLWS_*</c> bits as <c>lib/ws.c</c> does (ADR-0616): a received
/// frame's opcode and FIN bit to the bits <c>curl_ws_recv</c> reports, a continuation fragment carrying its message's
/// <c>CURLWS_TEXT</c> or <c>CURLWS_BINARY</c>; and the bits given to <c>curl_ws_send</c> to the frame it writes.
/// </summary>
internal static class WebSocketFrameFlags
{
    /// <summary>The bits that name a data message's type.</summary>
    internal const CurlWebSocketFlags MessageTypes = CurlWebSocketFlags.Text | CurlWebSocketFlags.Binary;

    /// <summary>The opcode of each control frame bit.</summary>
    private static readonly Dictionary<CurlWebSocketFlags, WsOpcode> ControlOpcodes = new()
    {
        [CurlWebSocketFlags.Close] = WsOpcode.Close,
        [CurlWebSocketFlags.Ping] = WsOpcode.Ping,
        [CurlWebSocketFlags.Pong] = WsOpcode.Pong,
    };

    /// <summary>
    /// The bits <c>curl_ws_recv</c> reports for <paramref name="run" />: its opcode's bit, or for a continuation
    /// fragment the type of the message it continues, with <c>CURLWS_CONT</c> while more of the message follows.
    /// </summary>
    /// <param name="run">The received run of payload.</param>
    /// <param name="messageType">The type of the data message in progress, from its first fragment.</param>
    /// <returns>The frame's bits.</returns>
    internal static CurlWebSocketFlags Of(WsFrameRun run, CurlWebSocketFlags messageType)
    {
        CurlWebSocketFlags type = run.Opcode switch
        {
            WsOpcode.Text => CurlWebSocketFlags.Text,
            WsOpcode.Binary => CurlWebSocketFlags.Binary,
            WsOpcode.Close => CurlWebSocketFlags.Close,
            WsOpcode.Ping => CurlWebSocketFlags.Ping,
            WsOpcode.Pong => CurlWebSocketFlags.Pong,
            _ => messageType,
        };
        return run.IsFinal ? type : type | CurlWebSocketFlags.Continuation;
    }

    /// <summary>
    /// The frame <c>curl_ws_send</c> writes for <paramref name="flags" />, as <c>ws_frame_flags2firstbyte</c> chooses
    /// it: <c>CURLWS_CONT</c> clears the FIN bit; a data bit starts a message, or continues the one in progress; no
    /// bit, or <c>CURLWS_CONT</c> alone, continues the message in progress; a control bit sends an unfragmented control
    /// frame. Anything else - no message to continue, a fragmented control frame, two type bits - is refused.
    /// </summary>
    /// <param name="flags">The bits given, without <c>CURLWS_OFFSET</c>.</param>
    /// <param name="continuesMessage">Whether a fragmented data message sent earlier is still unfinished.</param>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <returns><see langword="false" /> when libcurl refuses the bits with <c>CURLE_BAD_FUNCTION_ARGUMENT</c>.</returns>
    internal static bool TryFrameOf(CurlWebSocketFlags flags, bool continuesMessage, out WsOpcode opcode, out bool isFinal)
    {
        isFinal = (flags & CurlWebSocketFlags.Continuation) == 0;
        CurlWebSocketFlags type = flags & ~CurlWebSocketFlags.Continuation;
        WsOpcode? chosen = (type & MessageTypes) == type
            ? DataOpcodeOf(type, continuesMessage)
            : ControlOpcodeOf(type, isFinal);
        opcode = chosen.GetValueOrDefault();
        return chosen.HasValue;
    }

    /// <summary>The opcode of a data frame: a continuation inside a message, else the message type's.</summary>
    /// <param name="type">No bit, <c>CURLWS_TEXT</c>, <c>CURLWS_BINARY</c>, or both.</param>
    /// <param name="continuesMessage">Whether a fragmented message is unfinished.</param>
    /// <returns>The opcode, or <see langword="null" /> when there is no message to continue or both type bits are set.</returns>
    private static WsOpcode? DataOpcodeOf(CurlWebSocketFlags type, bool continuesMessage) => type switch
    {
        _ when continuesMessage && type != MessageTypes => WsOpcode.Continuation,
        CurlWebSocketFlags.Text => WsOpcode.Text,
        CurlWebSocketFlags.Binary => WsOpcode.Binary,
        _ => null,
    };

    /// <summary>The opcode of an unfragmented control frame.</summary>
    /// <param name="type">The bits other than <c>CURLWS_CONT</c>.</param>
    /// <param name="isFinal">Whether <c>CURLWS_CONT</c> was not given.</param>
    /// <returns>The opcode, or <see langword="null" /> for a fragmented control frame or bits naming no single frame.</returns>
    private static WsOpcode? ControlOpcodeOf(CurlWebSocketFlags type, bool isFinal) =>
        isFinal && ControlOpcodes.TryGetValue(type, out WsOpcode opcode) ? opcode : null;
}
