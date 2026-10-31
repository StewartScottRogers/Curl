namespace Curl.WebSocket;

/// <summary>
/// Told by <see cref="WsFrameDecoder" /> what it decodes, so a protocol can write its diagnostic
/// log and trace lines without the decoder knowing either.
/// </summary>
public interface IWsFrameDecodeObserver
{
    /// <summary>A frame's head has been read whole.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    void FrameOpened(WsOpcode opcode, bool isFinal, long payloadLength);

    /// <summary>A frame's head with a payload to come ended the bytes of one decode call.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    void HeadEndedRead(WsOpcode opcode, bool isFinal, long payloadLength);

    /// <summary>A run of a frame's payload has been passed on.</summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="passed">How many payload bytes this run passed on.</param>
    /// <param name="payloadOffset">How many of the frame's payload bytes have been passed on so far.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    void PayloadPassed(WsOpcode opcode, bool isFinal, long passed, long payloadOffset, long payloadLength);

    /// <summary>A close frame has been read whole.</summary>
    /// <param name="payload">The close frame's payload.</param>
    void CloseReceived(ReadOnlySpan<byte> payload);
}
