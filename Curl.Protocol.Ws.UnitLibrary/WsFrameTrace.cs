using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Writes curl 8.21.0's <c>--trace-config ws</c> lines, <c>[WS] ...</c>, as the WebSocket
/// transfer steps (BL-1164): the chunk size and the upload reader after the <c>101</c>, each
/// frame decoded and each run of its payload passed on, the pong answering a ping, the upload
/// frame encoded, and the connection established, through the transfer's
/// <see cref="ITransferEvents.ReportInfo" /> so they fall among the <c>-v</c> lines where curl's do.
/// </summary>
/// <param name="events">The transfer's events.</param>
/// <param name="enabled">Whether the lines are written; <see langword="false" /> writes nothing.</param>
/// <remarks>
/// Measured with <c>Record-CurlExchange.ps1</c> on 2026-10-02 for text, binary, fragmented,
/// close, ping and empty-ping frames, an upload and <c>-I</c> (BL-1164 Notes).
/// </remarks>
internal sealed class WsFrameTrace(ITransferEvents events, bool enabled)
{
    /// <summary>The chunk size curl 8.21.0 reports for its WebSocket buffers.</summary>
    private const int ChunkSize = 65535;

    /// <summary>Gets a trace that writes nothing.</summary>
    public static WsFrameTrace Off { get; } = new(NoTransferEvents.Instance, enabled: false);

    /// <summary>Writes the line curl writes as it sets the WebSocket up, after the <c>101</c>.</summary>
    public void UsingChunkSize() =>
        Write(string.Create(CultureInfo.InvariantCulture, $"WS, using chunk size {ChunkSize}"));

    /// <summary>Writes the line curl writes when a <c>-T</c> upload is to be sent as a frame.</summary>
    public void UploadReaderAdded() => Write("UPLOAD set, add ws-encode reader");

    /// <summary>Writes the line curl writes once the bytes that came with the <c>101</c> are handled.</summary>
    public void Established() => Write("websocket established, callback mode");

    /// <summary>Writes the line for a frame head decoded, before any of its payload.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    public void FrameDecoded(WsOpcode opcode, bool isFinal, long payloadLength) =>
        Write("decoded decoded " + Frame(opcode, isFinal, 0, payloadLength));

    /// <summary>Writes the line for a ping that is to be answered with a pong.</summary>
    /// <param name="payloadLength">The ping's payload length.</param>
    public void AutoPong(long payloadLength) =>
        Write("auto PONG to " + Frame(WsOpcode.Ping, isFinal: true, 0, payloadLength));

    /// <summary>Writes the lines for a run of a frame's payload passed on.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="passed">How many payload bytes this run passed on.</param>
    /// <param name="payloadOffset">How many of the frame's payload bytes have been passed on so far.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    public void PayloadPassed(WsOpcode opcode, bool isFinal, long passed, long payloadOffset, long payloadLength)
    {
        Write(string.Create(CultureInfo.InvariantCulture, $"passed {passed} bytes payload, {payloadLength - payloadOffset} remain"));
        Passing(opcode, isFinal, payloadOffset, payloadLength);
    }

    /// <summary>Writes the line for a frame whose payload is being passed on.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="payloadOffset">How many of the frame's payload bytes have been passed on so far.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    public void Passing(WsOpcode opcode, bool isFinal, long payloadOffset, long payloadLength) =>
        Write("decoded passing " + Frame(opcode, isFinal, payloadOffset, payloadLength));

    /// <summary>Writes the two lines for a frame curl encodes to send, before it is sent.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    public void FrameEncoded(WsOpcode opcode, long payloadLength)
    {
        Write("WS-ENC: sending " + Frame(opcode, isFinal: true, 0, payloadLength));
        Write("WS-ENC: buffered " + Frame(opcode, isFinal: true, payloadLength, payloadLength));
    }

    /// <summary>Writes the line for a pong frame flushed to the connection.</summary>
    /// <param name="frameLength">The frame's length, head included.</param>
    public void Flushed(long frameLength) =>
        Write(string.Create(CultureInfo.InvariantCulture, $"flushed {frameLength} bytes"));

    /// <summary>Names an opcode as curl's trace does: <c>CONT</c>, <c>TEXT</c>, <c>BIN</c>, <c>CLOSE</c>, <c>PING</c>, <c>PONG</c>.</summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns>The name.</returns>
    internal static string Name(WsOpcode opcode) => opcode switch
    {
        WsOpcode.Continuation => "CONT",
        WsOpcode.Binary => "BIN",
        _ => opcode.ToString().ToUpperInvariant(),
    };

    private static string Frame(WsOpcode opcode, bool isFinal, long payloadOffset, long payloadLength) =>
        string.Create(CultureInfo.InvariantCulture, $"[{Name(opcode)}{(isFinal ? string.Empty : " NON-FINAL")} payload={payloadOffset}/{payloadLength}]");

    private void Write(string line)
    {
        if (enabled)
        {
            events.ReportInfo("[WS] " + line);
        }
    }
}
