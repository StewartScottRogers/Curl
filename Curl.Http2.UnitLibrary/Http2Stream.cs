namespace Curl.Http2;

/// <summary>
/// One client-initiated stream's state on an <see cref="Http2Connection" />: its two
/// flow-control windows and which ends have finished (RFC 9113 section 5.1).
/// </summary>
internal sealed class Http2Stream
{
    public Http2Stream(int sendWindowSize, int receiveWindowSize)
    {
        SendWindow = new Http2FlowControlWindow(sendWindowSize);
        ReceiveWindow = new Http2FlowControlWindow(receiveWindowSize);
        ReceiveWindowTarget = receiveWindowSize;
    }

    /// <summary>Gets how much this endpoint may still send.</summary>
    public Http2FlowControlWindow SendWindow { get; }

    /// <summary>Gets how much the peer may still send.</summary>
    public Http2FlowControlWindow ReceiveWindow { get; }

    /// <summary>Gets or sets the size the receive window is topped back up to.</summary>
    public long ReceiveWindowTarget { get; set; }

    /// <summary>Gets or sets whether this endpoint has sent the stream's HEADERS, which starts it.</summary>
    public bool IsHeadersSent { get; set; }

    /// <summary>Gets or sets whether this endpoint has sent END_STREAM.</summary>
    public bool IsLocalEnded { get; set; }

    /// <summary>Gets or sets whether the peer has sent END_STREAM.</summary>
    public bool IsRemoteEnded { get; set; }

    /// <summary>Gets whether both ends have finished: the stream is closed.</summary>
    public bool IsClosed => IsLocalEnded && IsRemoteEnded;
}
