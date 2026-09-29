namespace Curl.Tls;

/// <summary>
/// What one TLS 1.3 client handshake offers and presents. The handshake builds the
/// ClientHello extensions it negotiates (<c>server_name</c>, <c>supported_groups</c>,
/// <c>key_share</c>, <c>supported_versions</c>, <c>signature_algorithms</c>, ALPN,
/// <c>cookie</c> and <c>padding</c>) and sends them in <see cref="ExtensionOrder" />;
/// any other extension goes out verbatim from <see cref="FixedExtensions" />.
/// </summary>
public sealed record Tls13ClientSettings
{
    /// <summary>Gets the extension order used when a caller names none.</summary>
    public static IReadOnlyList<TlsExtensionType> DefaultExtensionOrder { get; } =
    [
        TlsExtensionType.ServerName,
        TlsExtensionType.SupportedGroups,
        TlsExtensionType.SignatureAlgorithms,
        TlsExtensionType.ApplicationLayerProtocolNegotiation,
        TlsExtensionType.SupportedVersions,
        TlsExtensionType.KeyShare,
        TlsExtensionType.Cookie,
    ];

    /// <summary>Gets the host name sent in <c>server_name</c> and handed to the verifier, or <see langword="null" /> to send none (an IP address).</summary>
    public string? ServerName { get; init; }

    /// <summary>Gets the TLS 1.3 cipher suites offered, in preference order.</summary>
    public IReadOnlyList<ushort> CipherSuites { get; init; } =
        [Tls13CipherSuite.Aes256GcmSha384.Code, Tls13CipherSuite.ChaCha20Poly1305Sha256.Code, Tls13CipherSuite.Aes128GcmSha256.Code];

    /// <summary>Gets the groups offered in <c>supported_groups</c>, in preference order; each must be one <see cref="TlsNamedGroup.CanShare" /> accepts.</summary>
    public IReadOnlyList<ushort> SupportedGroups { get; init; } =
        [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1];

    /// <summary>Gets the groups the first ClientHello sends a key share for, each one of <see cref="SupportedGroups" />.</summary>
    public IReadOnlyList<ushort> KeyShareGroups { get; init; } = [TlsNamedGroup.X25519];

    /// <summary>Gets the signature schemes offered in <c>signature_algorithms</c>; the server's CertificateVerify must use one of them.</summary>
    public IReadOnlyList<ushort> SignatureAlgorithms { get; init; } =
    [
        TlsSignatureScheme.EcdsaSecp256r1Sha256,
        TlsSignatureScheme.EcdsaSecp384r1Sha384,
        TlsSignatureScheme.EcdsaSecp521r1Sha512,
        TlsSignatureScheme.Ed25519,
        TlsSignatureScheme.RsaPssRsaeSha256,
        TlsSignatureScheme.RsaPssRsaeSha384,
        TlsSignatureScheme.RsaPssRsaeSha512,
        TlsSignatureScheme.RsaPssPssSha256,
        TlsSignatureScheme.RsaPssPssSha384,
        TlsSignatureScheme.RsaPssPssSha512,
    ];

    /// <summary>Gets the ALPN protocols offered, in preference order; none sends no ALPN extension.</summary>
    public IReadOnlyList<string> ApplicationProtocols { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the ClientHello carries a random 32-byte
    /// <c>legacy_session_id</c> (middlebox compatibility mode, RFC 8446 appendix D.4)
    /// rather than an empty one, as QUIC requires.
    /// </summary>
    public bool SendLegacySessionId { get; init; }

    /// <summary>
    /// Gets the <c>legacy_record_version</c> of the record that carries the first
    /// ClientHello over a byte stream: <c>0x0301</c>, as OpenSSL and Schannel send and RFC
    /// 8446 section 5.1 allows; every later record carries <c>0x0303</c>. Unused inside QUIC.
    /// </summary>
    public ushort ClientHelloRecordVersion { get; init; } = 0x0301;

    /// <summary>
    /// Gets the order of the ClientHello's extensions. An extension the handshake builds is
    /// sent only when its type is listed; <c>cookie</c> only after a HelloRetryRequest that
    /// carried one; <c>padding</c>, when listed, pads a hello of 256 to 511 bytes to 512.
    /// </summary>
    public IReadOnlyList<TlsExtensionType> ExtensionOrder { get; init; } = DefaultExtensionOrder;

    /// <summary>
    /// Gets extensions sent as given (for example <c>psk_key_exchange_modes</c>,
    /// <c>record_size_limit</c> or QUIC's <c>quic_transport_parameters</c>): at their place
    /// in <see cref="ExtensionOrder" />, or after the listed ones when their type is not in it.
    /// </summary>
    public IReadOnlyList<TlsExtension> FixedExtensions { get; init; } = [];

    /// <summary>Gets the certificate presented when the server asks for one, or <see langword="null" /> to answer with an empty Certificate.</summary>
    public TlsClientCertificate? ClientCertificate { get; init; }

    /// <summary>Throws when the settings cannot drive a handshake.</summary>
    /// <exception cref="ArgumentException">A cipher suite is not TLS 1.3, a group cannot be shared, or a key share group is not offered.</exception>
    internal void Validate()
    {
        Require(CipherSuites.Count > 0 && CipherSuites.All(code => Tls13CipherSuite.Find(code) is not null), "Offer at least one cipher suite, and only TLS 1.3 suites.", nameof(CipherSuites));
        Require(SupportedGroups.All(TlsNamedGroup.CanShare), "Every supported group must be one the client can make a key share for.", nameof(SupportedGroups));
        Require(KeyShareGroups.All(SupportedGroups.Contains), "Every key share group must be one of the supported groups.", nameof(KeyShareGroups));
    }

    private static void Require(bool condition, string message, string parameterName)
    {
        if (!condition)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}
