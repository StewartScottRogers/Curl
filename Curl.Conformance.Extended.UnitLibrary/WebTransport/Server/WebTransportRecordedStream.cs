namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>A WebTransport stream the client opened: its session id, the bytes after its header, and whether its FIN arrived.</summary>
/// <param name="streamId">The QUIC stream id.</param>
/// <param name="sessionId">The session id the stream header named.</param>
public sealed class WebTransportRecordedStream(ulong streamId, long sessionId)
{
    private readonly List<byte> bytes = [];

    /// <summary>Gets the QUIC stream id.</summary>
    public ulong StreamId => streamId;

    /// <summary>Gets the session id the stream header named.</summary>
    public long SessionId => sessionId;

    /// <summary>Gets the bytes after the stream header, as they arrived so far.</summary>
    public byte[] Bytes => [.. bytes];

    /// <summary>Gets a value indicating whether the client's FIN arrived.</summary>
    public bool IsFinished { get; private set; }

    internal void Append(byte[] payload) => bytes.AddRange(payload);

    internal void Finish() => IsFinished = true;
}
