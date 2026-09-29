namespace Curl.Tls;

/// <summary>
/// The server's messages in a full TLS 1.2 and below handshake, in the order RFC 5246
/// section 7.3 and RFC 6066 section 8 fix: each arrives after every earlier one, and only
/// the ones the suite and the ServerHello call for.
/// </summary>
internal enum Tls12ServerFlightStep
{
    /// <summary>The ServerHello; also where any message outside the flight lands, which can then never follow.</summary>
    ServerHello,

    /// <summary>The Certificate, unless the suite is anonymous.</summary>
    Certificate,

    /// <summary>The CertificateStatus, only when the ServerHello echoed <c>status_request</c>, and even then optional.</summary>
    CertificateStatus,

    /// <summary>The ServerKeyExchange, for ECDHE and DHE and never for RSA key exchange.</summary>
    ServerKeyExchange,

    /// <summary>The optional CertificateRequest, unless the suite is anonymous.</summary>
    CertificateRequest,

    /// <summary>The ServerHelloDone.</summary>
    ServerHelloDone,
}
