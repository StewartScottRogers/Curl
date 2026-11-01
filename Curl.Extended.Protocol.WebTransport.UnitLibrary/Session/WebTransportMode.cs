namespace Curl.Extended.Protocol.WebTransport.Session;

/// <summary>How a <c>webtransport://</c> transfer carries its data (ADR-1089).</summary>
internal enum WebTransportMode
{
    /// <summary>On a client bidirectional stream; the server's answer on the same stream is the output (section 4.3).</summary>
    Bidirectional,

    /// <summary>On a client unidirectional stream; the first server unidirectional stream of the session is the output (section 4.2).</summary>
    Unidirectional,

    /// <summary>In one HTTP/3 datagram; the first datagram of the session from the server is the output (section 4.5, RFC 9297).</summary>
    Datagram,
}
