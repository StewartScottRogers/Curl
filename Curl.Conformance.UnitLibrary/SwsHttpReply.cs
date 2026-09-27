namespace Curl.Conformance;

/// <summary>What the sws emulation sends back for one request, and whether it then closes the connection.</summary>
internal sealed class SwsHttpReply(byte[] bytes, bool closesConnection, bool isFromTestCase)
{
    /// <summary>The reply's bytes, sent as they are.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <summary>Whether the server closes the connection once the reply is sent.</summary>
    public bool ClosesConnection { get; } = closesConnection;

    /// <summary>
    /// Whether the reply is a part of the test case, so the case's <c>&lt;servercmd&gt;</c> and
    /// <c>&lt;postcmd&gt;</c> apply to it; false for sws's 404 document, sent before sws has
    /// read either.
    /// </summary>
    public bool IsFromTestCase { get; } = isFromTestCase;
}
