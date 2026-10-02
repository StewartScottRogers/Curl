namespace Curl.Tls;

/// <summary>
/// What one TLS 1.2, 1.1 or 1.0 client handshake offers and presents. The ClientHello
/// carries <c>renegotiation_info</c> (empty: Curl never renegotiates), <c>server_name</c>,
/// <c>srp</c> with <see cref="SrpCredentials" />, <c>ec_point_formats</c>, <c>supported_groups</c>, <c>session_ticket</c>,
/// <c>status_request</c>, ALPN, <c>encrypt_then_mac</c>, <c>extended_master_secret</c> and,
/// when TLS 1.2 is offered, <c>signature_algorithms</c>, in <see cref="ExtensionOrder" />
/// (OpenSSL's by default), with any of <see cref="FixedExtensions" /> sent verbatim in their place.
/// </summary>
public sealed record Tls12ClientSettings
{
    /// <summary>Gets the order OpenSSL sends the ClientHello's extensions in, the default <see cref="ExtensionOrder" />.</summary>
    public static IReadOnlyList<TlsExtensionType> DefaultExtensionOrder { get; } =
    [
        TlsExtensionType.RenegotiationInfo, TlsExtensionType.ServerName, TlsExtensionType.Srp, TlsExtensionType.EcPointFormats,
        TlsExtensionType.SupportedGroups, TlsExtensionType.SessionTicket, TlsExtensionType.StatusRequest,
        TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.EncryptThenMac, TlsExtensionType.ExtendedMasterSecret,
        TlsExtensionType.SignatureAlgorithms,
    ];

    /// <summary>
    /// Gets the order the ClientHello's extensions are sent in. An extension the settings
    /// call for whose type is not listed goes after the listed ones; a listed type the
    /// settings do not call for is skipped.
    /// </summary>
    public IReadOnlyList<TlsExtensionType> ExtensionOrder { get; init; } = DefaultExtensionOrder;

    /// <summary>
    /// Gets extensions sent verbatim: each replaces the one of its type the settings would
    /// build, or is added when they build none, and goes at its type's place in
    /// <see cref="ExtensionOrder" />. A platform profile sends its own <c>ec_point_formats</c>
    /// and <c>status_request</c> this way. A fixed <c>status_request</c> asks for a staple
    /// without <see cref="RequestOcspStatus" />'s check of it.
    /// </summary>
    public IReadOnlyList<TlsExtension> FixedExtensions { get; init; } = [];

    /// <summary>Gets the host name sent in <c>server_name</c> and handed to the verifier, or <see langword="null" /> to send none (an IP address).</summary>
    public string? ServerName { get; init; }

    /// <summary>Gets the lowest version the client accepts.</summary>
    public TlsProtocolVersion MinimumVersion { get; init; } = TlsProtocolVersion.Tls12;

    /// <summary>Gets the highest version the client offers, the ClientHello's <c>client_version</c>.</summary>
    public TlsProtocolVersion MaximumVersion { get; init; } = TlsProtocolVersion.Tls12;

    /// <summary>
    /// Gets the version in the header of the records written before the server picks one,
    /// the ClientHello's among them: TLS 1.0 by default, as OpenSSL sends it.
    /// </summary>
    public TlsProtocolVersion ClientHelloRecordVersion { get; init; } = TlsProtocolVersion.Tls10;

    /// <summary>
    /// Gets the cipher suites offered, in preference order: codes <see cref="Tls12CipherSuite.Find" />
    /// knows, and optionally <see cref="Tls12CipherSuite.EmptyRenegotiationInfoScsv" />.
    /// </summary>
    public IReadOnlyList<ushort> CipherSuites { get; init; } =
    [
        0xc02c, 0xc030, 0x009f, 0xcca9, 0xcca8, 0xccaa, 0xc02b, 0xc02f, 0x009e, 0xc024, 0xc028, 0x006b, 0xc023, 0xc027, 0x0067,
        0xc00a, 0xc014, 0x0039, 0xc009, 0xc013, 0x0033, 0x009d, 0x009c, 0x003d, 0x003c, 0x0035, 0x002f,
    ];

    /// <summary>
    /// Gets the ECDHE groups offered in <c>supported_groups</c>: by default X25519 and the
    /// NIST curves; x448 and the brainpool curves may be added
    /// (<see cref="TlsNamedGroup.IsTls12EcdheGroup" />).
    /// </summary>
    public IReadOnlyList<ushort> SupportedGroups { get; init; } =
        [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1];

    /// <summary>
    /// Gets the signature schemes offered in TLS 1.2's <c>signature_algorithms</c>; the
    /// server's ServerKeyExchange must use one, and the client signs with one.
    /// </summary>
    public IReadOnlyList<ushort> SignatureAlgorithms { get; init; } =
    [
        TlsSignatureScheme.EcdsaSecp256r1Sha256,
        TlsSignatureScheme.EcdsaSecp384r1Sha384,
        TlsSignatureScheme.EcdsaSecp521r1Sha512,
        TlsSignatureScheme.Ed25519,
        TlsSignatureScheme.RsaPssRsaeSha256,
        TlsSignatureScheme.RsaPssRsaeSha384,
        TlsSignatureScheme.RsaPssRsaeSha512,
        TlsSignatureScheme.RsaPkcs1Sha256,
        TlsSignatureScheme.RsaPkcs1Sha384,
        TlsSignatureScheme.RsaPkcs1Sha512,
        TlsSignatureScheme.EcdsaSha1,
        TlsSignatureScheme.RsaPkcs1Sha1,
    ];

    /// <summary>Gets the ALPN protocols offered, in preference order; none sends no ALPN extension.</summary>
    public IReadOnlyList<string> ApplicationProtocols { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the client asks for a stapled OCSP response
    /// (<c>status_request</c>, <c>--cert-status</c>) and fails a full handshake with
    /// <c>bad_certificate_status_response</c> unless <see cref="OcspStapleVerifier" /> finds it good.
    /// </summary>
    public bool RequestOcspStatus { get; init; }

    /// <summary>Gets the clock a stapled OCSP response's <c>thisUpdate</c> and <c>nextUpdate</c> are judged against.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Gets a value indicating whether the client offers <c>session_ticket</c> (RFC 5077).</summary>
    public bool OfferSessionTicket { get; init; } = true;

    /// <summary>Gets a value indicating whether the client offers <c>extended_master_secret</c> (RFC 7627).</summary>
    public bool OfferExtendedMasterSecret { get; init; } = true;

    /// <summary>Gets a value indicating whether the client offers <c>encrypt_then_mac</c> (RFC 7366).</summary>
    public bool OfferEncryptThenMac { get; init; } = true;

    /// <summary>
    /// Gets the session to resume, or <see langword="null" /> for a full handshake: its
    /// ticket goes in <c>session_ticket</c> with a fresh random session ID, otherwise its
    /// session ID is offered.
    /// </summary>
    public Tls12Session? SessionToResume { get; init; }

    /// <summary>Gets the certificate presented when the server asks for one, or <see langword="null" /> to answer with an empty Certificate.</summary>
    public TlsClientCertificate? ClientCertificate { get; init; }

    /// <summary>
    /// Gets a value indicating whether a TLS 1.0 CBC connection writes an empty application
    /// data record before each write, OpenSSL's BEAST countermeasure; <see langword="false" />
    /// for <c>--ssl-allow-beast</c>. Only <see cref="Tls12ClientConnection" /> reads it.
    /// </summary>
    public bool InsertEmptyFragment { get; init; } = true;

    /// <summary>
    /// Gets the user name and password for TLS-SRP (RFC 5054), or <see langword="null" />: with
    /// them the ClientHello carries the <c>srp</c> extension and the SRP suites in
    /// <see cref="CipherSuites" />; without them, as in OpenSSL, it offers no SRP suite.
    /// </summary>
    public TlsSrpCredentials? SrpCredentials { get; init; }

    /// <summary>
    /// Gets the suites the ClientHello carries: <see cref="CipherSuites" /> without those that
    /// need TLS 1.2 when <see cref="MaximumVersion" /> is below it, as OpenSSL leaves out a
    /// suite the highest offered version cannot run, and without the SRP suites when there are
    /// no <see cref="SrpCredentials" />.
    /// </summary>
    internal IReadOnlyList<ushort> OfferedCipherSuites => [.. CipherSuites.Where(IsOffered)];

    private bool IsOffered(ushort code)
    {
        Tls12CipherSuite? suite = Tls12CipherSuite.Find(code);
        return suite is null
            || ((MaximumVersion == TlsProtocolVersion.Tls12 || !suite.RequiresTls12) && (SrpCredentials is not null || suite.KeyExchange != Tls12KeyExchange.Srp));
    }

    /// <summary>Throws when the settings cannot drive a handshake.</summary>
    /// <exception cref="ArgumentException">
    /// The version range is empty, a suite is unknown, a group is not an ECDHE group, a
    /// signature scheme is not a TLS 1.2 one, or the session to resume is outside what is offered.
    /// </exception>
    internal void Validate()
    {
        Require(HasVersionRange(), "The versions must be TLS 1.0, 1.1 or 1.2, the minimum not above the maximum.", nameof(MinimumVersion));
        Require(HasSuites(), "Offer at least one cipher suite the maximum version can run, and only TLS 1.2 and below suites.", nameof(CipherSuites));
        Require(
            SupportedGroups.All(TlsNamedGroup.IsTls12EcdheGroup),
            "Every supported group must be x25519, x448, secp256r1, secp384r1, secp521r1, brainpoolP256r1, brainpoolP384r1 or brainpoolP512r1.",
            nameof(SupportedGroups));
        Require(SignatureAlgorithms.All(TlsSignatureScheme.IsTls12Scheme), "Every signature algorithm must be one TLS 1.2 can check.", nameof(SignatureAlgorithms));
        Require(SessionToResume is null || CanResume(SessionToResume), "The session to resume must have an offered version and suite.", nameof(SessionToResume));
    }

    private static bool IsOfferable(ushort code) => code == Tls12CipherSuite.EmptyRenegotiationInfoScsv || Tls12CipherSuite.Find(code) is not null;

    private bool HasVersionRange() => Enum.IsDefined(MinimumVersion) && Enum.IsDefined(MaximumVersion) && MinimumVersion <= MaximumVersion;

    private bool HasSuites() => OfferedCipherSuites.Any(code => Tls12CipherSuite.Find(code) is not null) && CipherSuites.All(IsOfferable);

    private static void Require(bool condition, string message, string parameterName)
    {
        if (!condition)
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private bool CanResume(Tls12Session session) =>
        session.Version >= MinimumVersion && session.Version <= MaximumVersion && CipherSuites.Contains(session.CipherSuite);
}
