namespace Curl.Conformance;

/// <summary>
/// One thing the sws emulation sends on a connection, and when, measured from the connection's
/// opening: some reply bytes, or, with no bytes, the close of the server's side.
/// </summary>
/// <remarks>
/// A reply's bytes are never queued empty, so an empty send is always a close, and it is always
/// the last one queued: reading it returns 0, the end of the connection (AF-0420).
/// </remarks>
internal sealed class SwsServerSend(byte[] bytes, TimeSpan sentAt)
{
    /// <summary>The bytes sent; empty for a close.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <summary>When the client can read them.</summary>
    public TimeSpan SentAt { get; } = sentAt;
}
