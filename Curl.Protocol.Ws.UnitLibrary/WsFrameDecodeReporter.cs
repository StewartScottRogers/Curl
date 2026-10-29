using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Turns what <see cref="WsFrameDecoder" /> decodes into the <c>ws</c> diagnostic log lines
/// (<see cref="WsTransferLog" />) and curl's <c>--trace-config ws</c> lines (<see cref="WsFrameTrace" />).
/// </summary>
/// <param name="diagnosticLog">
/// Receives each frame's opcode, FIN bit and length, and a close frame with an unexpected code;
/// nothing is written when it is <see langword="null" />.
/// </param>
/// <param name="trace">Receives the trace lines; <see langword="null" /> writes none.</param>
internal sealed class WsFrameDecodeReporter(IDiagnosticLog? diagnosticLog, WsFrameTrace? trace) : IWsFrameDecodeObserver
{
    private readonly WsTransferLog log = new(diagnosticLog ?? NoDiagnosticLog.Instance);

    private readonly WsFrameTrace trace = trace ?? WsFrameTrace.Off;

    /// <inheritdoc />
    public void FrameOpened(WsOpcode opcode, bool isFinal, long payloadLength)
    {
        log.Frame("received", opcode, isFinal, payloadLength);
        trace.FrameDecoded(opcode, isFinal, payloadLength);
        if (opcode == WsOpcode.Ping)
        {
            trace.AutoPong(payloadLength);
        }
    }

    /// <inheritdoc />
    public void HeadEndedRead(WsOpcode opcode, bool isFinal, long payloadLength) =>
        trace.Passing(opcode, isFinal, 0, payloadLength);

    /// <inheritdoc />
    public void PayloadPassed(WsOpcode opcode, bool isFinal, long passed, long payloadOffset, long payloadLength) =>
        trace.PayloadPassed(opcode, isFinal, passed, payloadOffset, payloadLength);

    /// <inheritdoc />
    public void CloseReceived(ReadOnlySpan<byte> payload) => log.CloseReceived(payload);
}
