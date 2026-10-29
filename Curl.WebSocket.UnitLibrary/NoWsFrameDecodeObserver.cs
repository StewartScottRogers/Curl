namespace Curl.WebSocket;

/// <summary>An <see cref="IWsFrameDecodeObserver" /> that ignores everything it is told.</summary>
internal sealed class NoWsFrameDecodeObserver : IWsFrameDecodeObserver
{
    /// <summary>Gets the one instance.</summary>
    public static NoWsFrameDecodeObserver Instance { get; } = new();

    /// <inheritdoc />
    public void FrameOpened(WsOpcode opcode, bool isFinal, long payloadLength)
    {
    }

    /// <inheritdoc />
    public void HeadEndedRead(WsOpcode opcode, bool isFinal, long payloadLength)
    {
    }

    /// <inheritdoc />
    public void PayloadPassed(WsOpcode opcode, bool isFinal, long passed, long payloadOffset, long payloadLength)
    {
    }

    /// <inheritdoc />
    public void CloseReceived(ReadOnlySpan<byte> payload)
    {
    }
}
