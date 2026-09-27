namespace Curl.Conformance;

/// <summary>
/// One thing the sws emulation sends on a connection, and when, measured from the connection's
/// opening: some reply bytes, or the close of the server's side.
/// </summary>
internal sealed class SwsServerSend(byte[] bytes, TimeSpan sentAt, bool isClose)
{
    /// <summary>The bytes sent; empty for a close.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <summary>When the client can read them.</summary>
    public TimeSpan SentAt { get; } = sentAt;

    /// <summary>Whether this is the server closing its side of the connection.</summary>
    public bool IsClose { get; } = isClose;
}
