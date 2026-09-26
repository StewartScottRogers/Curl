namespace Curl.Conformance;

/// <summary>What the sws emulation sends back for one request, and whether it then closes the connection.</summary>
internal sealed class SwsHttpReply(byte[] bytes, bool closesConnection)
{
    /// <summary>The reply's bytes, sent as they are.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <summary>Whether the server closes the connection once the reply is sent.</summary>
    public bool ClosesConnection { get; } = closesConnection;
}
