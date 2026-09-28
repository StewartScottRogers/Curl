namespace Curl.Tls;

/// <summary>
/// The TLS extension code points (IANA "TLS ExtensionType Values") the client sends or
/// meets. An extension of any other code point still decodes, as a
/// <see cref="TlsExtension" /> holding a value outside this list.
/// </summary>
public enum TlsExtensionType : ushort
{
    /// <summary><c>server_name</c> (RFC 6066).</summary>
    ServerName = 0,

    /// <summary><c>status_request</c> (RFC 6066).</summary>
    StatusRequest = 5,

    /// <summary><c>supported_groups</c> (RFC 8422, RFC 7919).</summary>
    SupportedGroups = 10,

    /// <summary><c>ec_point_formats</c> (RFC 8422).</summary>
    EcPointFormats = 11,

    /// <summary><c>srp</c> (RFC 5054).</summary>
    Srp = 12,

    /// <summary><c>signature_algorithms</c> (RFC 8446).</summary>
    SignatureAlgorithms = 13,

    /// <summary><c>application_layer_protocol_negotiation</c> (RFC 7301).</summary>
    ApplicationLayerProtocolNegotiation = 16,

    /// <summary><c>padding</c> (RFC 7685).</summary>
    Padding = 21,

    /// <summary><c>encrypt_then_mac</c> (RFC 7366).</summary>
    EncryptThenMac = 22,

    /// <summary><c>extended_master_secret</c> (RFC 7627).</summary>
    ExtendedMasterSecret = 23,

    /// <summary><c>compress_certificate</c> (RFC 8879).</summary>
    CompressCertificate = 27,

    /// <summary><c>record_size_limit</c> (RFC 8449).</summary>
    RecordSizeLimit = 28,

    /// <summary><c>session_ticket</c> (RFC 5077).</summary>
    SessionTicket = 35,

    /// <summary><c>pre_shared_key</c> (RFC 8446).</summary>
    PreSharedKey = 41,

    /// <summary><c>early_data</c> (RFC 8446).</summary>
    EarlyData = 42,

    /// <summary><c>supported_versions</c> (RFC 8446).</summary>
    SupportedVersions = 43,

    /// <summary><c>cookie</c> (RFC 8446).</summary>
    Cookie = 44,

    /// <summary><c>psk_key_exchange_modes</c> (RFC 8446).</summary>
    PskKeyExchangeModes = 45,

    /// <summary><c>certificate_authorities</c> (RFC 8446).</summary>
    CertificateAuthorities = 47,

    /// <summary><c>post_handshake_auth</c> (RFC 8446).</summary>
    PostHandshakeAuth = 49,

    /// <summary><c>signature_algorithms_cert</c> (RFC 8446).</summary>
    SignatureAlgorithmsCert = 50,

    /// <summary><c>key_share</c> (RFC 8446).</summary>
    KeyShare = 51,

    /// <summary><c>quic_transport_parameters</c> (RFC 9001).</summary>
    QuicTransportParameters = 57,

    /// <summary><c>ech_outer_extensions</c> (RFC 9849).</summary>
    EchOuterExtensions = 0xfd00,

    /// <summary><c>encrypted_client_hello</c> (RFC 9849).</summary>
    EncryptedClientHello = 0xfe0d,

    /// <summary><c>renegotiation_info</c> (RFC 5746).</summary>
    RenegotiationInfo = 0xff01,
}
