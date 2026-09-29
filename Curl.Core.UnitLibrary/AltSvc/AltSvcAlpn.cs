namespace Curl.Core.AltSvc;

/// <summary>
/// An HTTP version an alt-svc entry names, by the ALPN token curl 8.21.0 reads and writes
/// for it: <c>h1</c>, <c>h2</c> or <c>h3</c>, lower case only.
/// </summary>
public enum AltSvcAlpn
{
    /// <summary>HTTP/1.1, written <c>h1</c>.</summary>
    H1,

    /// <summary>HTTP/2, written <c>h2</c>.</summary>
    H2,

    /// <summary>HTTP/3, written <c>h3</c>.</summary>
    H3,
}
