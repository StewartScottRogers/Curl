namespace Curl.Tls;

/// <summary>
/// What <see cref="OcspStapleVerifier" /> found in a stapled OCSP response (RFC 6960), one
/// value per failure curl's OpenSSL build tells apart for <c>--cert-status</c>. Only
/// <see cref="Good" /> passes; every other value is exit 91 <c>CURLE_SSL_INVALIDCERTSTATUS</c>.
/// </summary>
public enum OcspStapleStatus
{
    /// <summary>The responder says the certificate is good, and the response is signed, matching and current.</summary>
    Good,

    /// <summary>The responder says the certificate is revoked; <see cref="OcspStapleOutcome.Code" /> is the CRL reason, or -1 when none was given.</summary>
    Revoked,

    /// <summary>The responder does not know the certificate.</summary>
    Unknown,

    /// <summary>The server stapled no response.</summary>
    NoResponse,

    /// <summary>The response does not parse as a DER <c>OCSPResponse</c> carrying a <c>BasicOCSPResponse</c>.</summary>
    Malformed,

    /// <summary>The <c>responseStatus</c> is not <c>successful</c>; <see cref="OcspStapleOutcome.Code" /> is its value.</summary>
    Unsuccessful,

    /// <summary>The server's chain holds no certificate that issued its leaf, so the response cannot be matched or checked.</summary>
    IssuerNotFound,

    /// <summary>The response's signature does not verify with the responder's key, or its algorithm is not one the client knows.</summary>
    SignatureInvalid,

    /// <summary>
    /// The responder is neither the issuer nor a certificate the issuer signed for OCSP
    /// signing (<c>id-kp-OCSPSigning</c>), or no certificate matches its <c>ResponderID</c>.
    /// </summary>
    ResponderNotAuthorised,

    /// <summary>The response carries no <c>SingleResponse</c> for the server's leaf certificate.</summary>
    CertificateNotFound,

    /// <summary>The matching response's <c>thisUpdate</c> is in the future or its <c>nextUpdate</c> has passed, beyond five minutes' leeway.</summary>
    Expired,
}
