namespace Curl.Tls;

/// <summary>
/// What one TLS 1.3 client handshake offers and presents. The handshake builds the
/// ClientHello extensions it negotiates (<c>server_name</c>, <c>supported_groups</c>,
/// <c>key_share</c>, <c>supported_versions</c>, <c>signature_algorithms</c>, ALPN,
/// <c>cookie</c>, <c>status_request</c> with <see cref="RequestOcspStatus" />,
/// <c>compress_certificate</c> with <see cref="CertificateCompressionAlgorithms" />,
/// <c>post_handshake_auth</c>, which lets the server ask for <see cref="ClientCertificate" />
/// after the handshake, and <c>padding</c>) and sends them in <see cref="ExtensionOrder" />;
/// any other extension goes out verbatim from <see cref="FixedExtensions" />.
/// </summary>
public sealed record Tls13ClientSettings
{
    /// <summary>Gets the extension order used when a caller names none.</summary>
    public static IReadOnlyList<TlsExtensionType> DefaultExtensionOrder { get; } =
    [
        TlsExtensionType.ServerName,
        TlsExtensionType.SupportedGroups,
        TlsExtensionType.StatusRequest,
        TlsExtensionType.SignatureAlgorithms,
        TlsExtensionType.ApplicationLayerProtocolNegotiation,
        TlsExtensionType.SupportedVersions,
        TlsExtensionType.KeyShare,
        TlsExtensionType.CompressCertificate,
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

    /// <summary>
    /// Gets a value indicating whether the client asks for a stapled OCSP response
    /// (<c>status_request</c>, <c>--cert-status</c>) and fails the handshake with
    /// <c>bad_certificate_status_response</c> unless <see cref="OcspStapleVerifier" /> finds it good.
    /// <c>status_request</c> must then be in <see cref="ExtensionOrder" />.
    /// </summary>
    public bool RequestOcspStatus { get; init; }

    /// <summary>
    /// Gets the certificate compression algorithms offered in <c>compress_certificate</c>
    /// (RFC 8879), in preference order, each one <see cref="CertificateCompressionAlgorithm.CanDecompress" />
    /// accepts; none sends no extension and refuses a CompressedCertificate. With any,
    /// the server may send a CompressedCertificate in place of its Certificate, and
    /// <c>compress_certificate</c> must be in <see cref="ExtensionOrder" />.
    /// </summary>
    public IReadOnlyList<ushort> CertificateCompressionAlgorithms { get; init; } = [];

    /// <summary>
    /// Gets the clock a stapled OCSP response's <c>thisUpdate</c> and <c>nextUpdate</c>, a
    /// ticket's age and lifetime, and a received ticket's arrival are judged against.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>
    /// Gets the session to resume (RFC 8446 section 2.2), or <see langword="null" /> for a
    /// full handshake. The ClientHello offers its ticket in <c>pre_shared_key</c> with a
    /// binder, for <c>psk_dhe_ke</c>, when <see cref="TlsSessionRecord.CanResumeAt" /> holds
    /// at <see cref="TimeProvider" />'s time, one offered suite shares its suite's hash, and
    /// its host name is <see cref="ServerName" />; otherwise the handshake is a full one.
    /// <c>psk_key_exchange_modes</c> must then be in <see cref="ExtensionOrder" />.
    /// </summary>
    public TlsSessionRecord? ResumptionSession { get; init; }

    /// <summary>
    /// Gets a value indicating whether the ClientHello offers 0-RTT early data (RFC 8446
    /// section 4.2.10) when it resumes <see cref="ResumptionSession" /> and the ticket allows
    /// early data, and the session's ALPN protocol, if any, is offered. <c>early_data</c>
    /// must then be in <see cref="ExtensionOrder" />.
    /// </summary>
    public bool OfferEarlyData { get; init; }

    /// <summary>
    /// Gets a value indicating whether accepted early data ends with an EndOfEarlyData
    /// message, as over TCP; QUIC turns it off (RFC 9001 section 8.3).
    /// </summary>
    public bool SendEndOfEarlyData { get; init; } = true;

    /// <summary>
    /// Gets the server's <c>ECHConfigList</c> (<c>--ech ecl:</c>, or an HTTPS record's
    /// <c>ech</c>), or <see langword="null" /> to offer no Encrypted Client Hello. When its
    /// <see cref="EchConfigList.SupportedConfig" /> is not <see langword="null" />, the
    /// ClientHello sent is an outer hello naming that config's public name and carrying the
    /// inner hello, which names <see cref="ServerName" />, sealed with HPKE (RFC 9849); no
    /// <see cref="ResumptionSession" /> is offered then. <c>encrypted_client_hello</c> must be
    /// in <see cref="ExtensionOrder" />.
    /// </summary>
    public EchConfigList? EncryptedClientHelloConfigs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the ClientHello carries a GREASE
    /// <c>encrypted_client_hello</c> (RFC 9849 section 6.2, <c>--ech grease</c>) when no
    /// supported config is offered. <c>encrypted_client_hello</c> must then be in
    /// <see cref="ExtensionOrder" />.
    /// </summary>
    public bool SendEncryptedClientHelloGrease { get; init; }

    /// <summary>
    /// Gets the TLS 1.2-and-below offer the same ClientHello also carries, or
    /// <see langword="null" /> to offer TLS 1.3 alone (<see cref="TlsClientConnection" />):
    /// its versions follow TLS 1.3 in <c>supported_versions</c>, its suites follow the TLS 1.3
    /// ones, its signature schemes follow those of <see cref="SignatureAlgorithms" />, and its
    /// extensions whose types <see cref="ExtensionOrder" /> and <see cref="FixedExtensions" />
    /// do not name are sent after the listed ones. The TLS 1.3 handshake still accepts only
    /// a TLS 1.3 ServerHello.
    /// </summary>
    internal Tls12ClientSettings? LowerVersions { get; init; }

    /// <summary>Throws when the settings cannot drive a handshake.</summary>
    /// <exception cref="ArgumentException">
    /// A cipher suite is not TLS 1.3, a group cannot be shared, a key share group is not
    /// offered, an OCSP status is asked for with no place for <c>status_request</c>, or a
    /// certificate compression algorithm cannot be decompressed or has no place for
    /// <c>compress_certificate</c>.
    /// </exception>
    internal void Validate()
    {
        ValidateExtensionPlaces();
        ValidateOffers();
    }

    private static void Require(bool condition, string message, string parameterName)
    {
        if (!condition)
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private void ValidateExtensionPlaces()
    {
        Require(!RequestOcspStatus || ExtensionOrder.Contains(TlsExtensionType.StatusRequest), "Asking for OCSP status needs status_request in the extension order.", nameof(ExtensionOrder));
        Require(CertificateCompressionAlgorithms.Count == 0 || ExtensionOrder.Contains(TlsExtensionType.CompressCertificate), "Offering certificate compression needs compress_certificate in the extension order.", nameof(ExtensionOrder));
        Require(ResumptionSession is null || ExtensionOrder.Contains(TlsExtensionType.PskKeyExchangeModes), "Resuming a session needs psk_key_exchange_modes in the extension order.", nameof(ExtensionOrder));
        Require(!OfferEarlyData || ExtensionOrder.Contains(TlsExtensionType.EarlyData), "Offering early data needs early_data in the extension order.", nameof(ExtensionOrder));
        Require(
            (EncryptedClientHelloConfigs is null && !SendEncryptedClientHelloGrease) || ExtensionOrder.Contains(TlsExtensionType.EncryptedClientHello),
            "Offering Encrypted Client Hello or its GREASE needs encrypted_client_hello in the extension order.",
            nameof(ExtensionOrder));
    }

    private void ValidateOffers()
    {
        Require(CertificateCompressionAlgorithms.All(CertificateCompressionAlgorithm.CanDecompress), "Offer only certificate compression algorithms the client can decompress.", nameof(CertificateCompressionAlgorithms));
        Require(CipherSuites.Count > 0 && CipherSuites.All(code => Tls13CipherSuite.Find(code) is not null), "Offer at least one cipher suite, and only TLS 1.3 suites.", nameof(CipherSuites));
        Require(SupportedGroups.All(TlsNamedGroup.CanShare), "Every supported group must be one the client can make a key share for.", nameof(SupportedGroups));
        Require(KeyShareGroups.All(SupportedGroups.Contains), "Every key share group must be one of the supported groups.", nameof(KeyShareGroups));
    }
}
