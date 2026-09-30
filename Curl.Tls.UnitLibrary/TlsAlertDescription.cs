namespace Curl.Tls;

/// <summary>
/// The TLS alert descriptions (RFC 8446 section 6, with the TLS 1.2 values TLS 1.3 keeps).
/// A codec that rejects bytes names the alert the client sends for them.
/// </summary>
public enum TlsAlertDescription : byte
{
    /// <summary><c>close_notify</c>: the sender will send no more data.</summary>
    CloseNotify = 0,

    /// <summary><c>unexpected_message</c>: a message arrived that is not valid here, such as an unknown handshake type.</summary>
    UnexpectedMessage = 10,

    /// <summary><c>bad_record_mac</c>: a record failed to decrypt or authenticate.</summary>
    BadRecordMac = 20,

    /// <summary><c>record_overflow</c>: a record is longer than the limit.</summary>
    RecordOverflow = 22,

    /// <summary><c>handshake_failure</c>: no acceptable set of parameters.</summary>
    HandshakeFailure = 40,

    /// <summary><c>bad_certificate</c>: a certificate was corrupt or failed verification.</summary>
    BadCertificate = 42,

    /// <summary><c>unsupported_certificate</c>: a certificate of an unsupported type.</summary>
    UnsupportedCertificate = 43,

    /// <summary><c>certificate_revoked</c>: a certificate was revoked.</summary>
    CertificateRevoked = 44,

    /// <summary><c>certificate_expired</c>: a certificate has expired or is not yet valid.</summary>
    CertificateExpired = 45,

    /// <summary><c>certificate_unknown</c>: a certificate could not be accepted for another reason.</summary>
    CertificateUnknown = 46,

    /// <summary><c>illegal_parameter</c>: a field is well formed but its value is not allowed, such as a repeated extension.</summary>
    IllegalParameter = 47,

    /// <summary><c>unknown_ca</c>: the chain does not lead to a trusted anchor.</summary>
    UnknownCa = 48,

    /// <summary><c>access_denied</c>: the peer's identity is valid but not allowed.</summary>
    AccessDenied = 49,

    /// <summary><c>decode_error</c>: a message could not be decoded, such as a length running past its end.</summary>
    DecodeError = 50,

    /// <summary><c>decrypt_error</c>: a handshake cryptographic check failed.</summary>
    DecryptError = 51,

    /// <summary><c>protocol_version</c>: the peer's version is not supported.</summary>
    ProtocolVersion = 70,

    /// <summary><c>insufficient_security</c>: the peer requires stronger parameters.</summary>
    InsufficientSecurity = 71,

    /// <summary><c>internal_error</c>: a local failure unrelated to the peer.</summary>
    InternalError = 80,

    /// <summary><c>inappropriate_fallback</c>: a fallback retry the server refuses (RFC 7507).</summary>
    InappropriateFallback = 86,

    /// <summary><c>user_canceled</c>: the handshake is being cancelled.</summary>
    UserCanceled = 90,

    /// <summary><c>missing_extension</c>: a required extension is absent.</summary>
    MissingExtension = 109,

    /// <summary><c>unsupported_extension</c>: an extension appeared where it is not allowed.</summary>
    UnsupportedExtension = 110,

    /// <summary><c>unrecognized_name</c>: the server has no identity for the offered name.</summary>
    UnrecognizedName = 112,

    /// <summary><c>bad_certificate_status_response</c>: an invalid OCSP response was stapled.</summary>
    BadCertificateStatusResponse = 113,

    /// <summary><c>unknown_psk_identity</c>: no acceptable pre-shared key identity.</summary>
    UnknownPskIdentity = 115,

    /// <summary><c>certificate_required</c>: a client certificate was required and none was sent.</summary>
    CertificateRequired = 116,

    /// <summary><c>no_application_protocol</c>: no ALPN protocol in common.</summary>
    NoApplicationProtocol = 120,

    /// <summary><c>ech_required</c> (RFC 9849 section 11.2): the server rejected Encrypted Client Hello, so the client ends the connection it authenticated under the public name.</summary>
    EchRequired = 121,
}
