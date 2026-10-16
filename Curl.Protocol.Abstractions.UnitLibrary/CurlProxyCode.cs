namespace Curl.Protocol.Abstractions;

/// <summary>
/// libcurl's <c>CURLproxycode</c>: why a SOCKS proxy handshake failed, the value of
/// <c>CURLINFO_PROXY_ERROR</c> after a transfer that ended with exit 97
/// (<see cref="CurlExitCode.Proxy" />). Each member's number is upstream's <c>CURLPX_*</c>
/// value, so it is reported as it is.
/// </summary>
public enum CurlProxyCode
{
    /// <summary><c>CURLPX_OK</c>: no proxy handshake failed.</summary>
    Ok = 0,

    /// <summary><c>CURLPX_BAD_ADDRESS_TYPE</c>: the SOCKS5 reply named an unknown address type.</summary>
    BadAddressType = 1,

    /// <summary><c>CURLPX_BAD_VERSION</c>: a reply carried the wrong SOCKS version.</summary>
    BadVersion = 2,

    /// <summary><c>CURLPX_CLOSED</c>: the proxy closed the connection before its reply was complete.</summary>
    Closed = 3,

    /// <summary><c>CURLPX_GSSAPI</c>: the SOCKS5 GSS-API negotiation failed.</summary>
    Gssapi = 4,

    /// <summary><c>CURLPX_GSSAPI_PERMSG</c>: the proxy picked GSS-API, which was not enabled.</summary>
    GssapiPerMessage = 5,

    /// <summary><c>CURLPX_GSSAPI_PROTECTION</c>: the GSS-API protection level was refused.</summary>
    GssapiProtection = 6,

    /// <summary><c>CURLPX_IDENTD</c>: the SOCKS4 proxy could not reach identd on the client (reply 92).</summary>
    Identd = 7,

    /// <summary><c>CURLPX_IDENTD_DIFFER</c>: the client and identd reported different user IDs (reply 93).</summary>
    IdentdDiffer = 8,

    /// <summary><c>CURLPX_LONG_HOSTNAME</c>: the host name is too long to send to the proxy.</summary>
    LongHostname = 9,

    /// <summary><c>CURLPX_LONG_PASSWD</c>: the password is too long to send to the proxy.</summary>
    LongPassword = 10,

    /// <summary><c>CURLPX_LONG_USER</c>: the user name is too long to send to the proxy.</summary>
    LongUser = 11,

    /// <summary><c>CURLPX_NO_AUTH</c>: no authentication method both sides allow was picked.</summary>
    NoAuthentication = 12,

    /// <summary><c>CURLPX_RECV_ADDRESS</c>: receiving the bound address failed.</summary>
    ReceiveAddress = 13,

    /// <summary><c>CURLPX_RECV_AUTH</c>: receiving the authentication reply failed.</summary>
    ReceiveAuthentication = 14,

    /// <summary><c>CURLPX_RECV_CONNECT</c>: receiving the connect reply failed.</summary>
    ReceiveConnect = 15,

    /// <summary><c>CURLPX_RECV_REQACK</c>: receiving the request acknowledgement failed.</summary>
    ReceiveRequestAcknowledgement = 16,

    /// <summary><c>CURLPX_REPLY_ADDRESS_TYPE_NOT_SUPPORTED</c>: SOCKS5 reply 8.</summary>
    ReplyAddressTypeNotSupported = 17,

    /// <summary><c>CURLPX_REPLY_COMMAND_NOT_SUPPORTED</c>: SOCKS5 reply 7.</summary>
    ReplyCommandNotSupported = 18,

    /// <summary><c>CURLPX_REPLY_CONNECTION_REFUSED</c>: SOCKS5 reply 5.</summary>
    ReplyConnectionRefused = 19,

    /// <summary><c>CURLPX_REPLY_GENERAL_SERVER_FAILURE</c>: SOCKS5 reply 1.</summary>
    ReplyGeneralServerFailure = 20,

    /// <summary><c>CURLPX_REPLY_HOST_UNREACHABLE</c>: SOCKS5 reply 4.</summary>
    ReplyHostUnreachable = 21,

    /// <summary><c>CURLPX_REPLY_NETWORK_UNREACHABLE</c>: SOCKS5 reply 3.</summary>
    ReplyNetworkUnreachable = 22,

    /// <summary><c>CURLPX_REPLY_NOT_ALLOWED</c>: SOCKS5 reply 2.</summary>
    ReplyNotAllowed = 23,

    /// <summary><c>CURLPX_REPLY_TTL_EXPIRED</c>: SOCKS5 reply 6.</summary>
    ReplyTtlExpired = 24,

    /// <summary><c>CURLPX_REPLY_UNASSIGNED</c>: a SOCKS5 reply above 8.</summary>
    ReplyUnassigned = 25,

    /// <summary><c>CURLPX_REQUEST_FAILED</c>: the SOCKS4 proxy rejected the request (reply 91).</summary>
    RequestFailed = 26,

    /// <summary><c>CURLPX_RESOLVE_HOST</c>: the host could not be resolved to an address the proxy takes.</summary>
    ResolveHost = 27,

    /// <summary><c>CURLPX_SEND_AUTH</c>: sending the authentication request failed.</summary>
    SendAuthentication = 28,

    /// <summary><c>CURLPX_SEND_CONNECT</c>: sending the connect request failed.</summary>
    SendConnect = 29,

    /// <summary><c>CURLPX_SEND_REQUEST</c>: sending the request failed.</summary>
    SendRequest = 30,

    /// <summary><c>CURLPX_UNKNOWN_FAIL</c>: the SOCKS4 proxy answered with a code curl does not know.</summary>
    UnknownFail = 31,

    /// <summary><c>CURLPX_UNKNOWN_MODE</c>: the SOCKS5 proxy picked a method curl does not know.</summary>
    UnknownMode = 32,

    /// <summary><c>CURLPX_USER_REJECTED</c>: the SOCKS5 proxy rejected the user name and password.</summary>
    UserRejected = 33,
}
