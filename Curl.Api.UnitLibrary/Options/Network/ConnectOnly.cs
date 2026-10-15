namespace Curl.Api.Options.Network;

/// <summary>
/// How far a transfer goes before it stops: libcurl's <c>CURLOPT_CONNECT_ONLY</c> values,
/// with the same numbers.
/// </summary>
public enum ConnectOnly
{
    /// <summary><c>0</c>: a full transfer (libcurl's default).</summary>
    Disabled = 0,

    /// <summary><c>1</c>: connect, including any proxy and TLS handshake, then stop.</summary>
    Connect = 1,

    /// <summary><c>2</c>: connect and complete the WebSocket upgrade, then stop.</summary>
    WebSocket = 2,
}
