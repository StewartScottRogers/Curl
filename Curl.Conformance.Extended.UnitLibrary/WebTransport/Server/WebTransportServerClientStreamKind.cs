namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>What a client stream turned out to be once its first bytes arrived.</summary>
internal enum WebTransportServerClientStreamKind
{
    /// <summary>Too few bytes yet to tell.</summary>
    Unknown,

    /// <summary>An HTTP/3 request stream: the extended CONNECT that opens the session.</summary>
    Connect,

    /// <summary>The client's HTTP/3 control stream.</summary>
    Control,

    /// <summary>A WebTransport bidirectional stream (signal 0x41) or unidirectional stream (type 0x54).</summary>
    WebTransport,

    /// <summary>Another unidirectional stream (QPACK or unknown), read and ignored.</summary>
    Ignored,
}
