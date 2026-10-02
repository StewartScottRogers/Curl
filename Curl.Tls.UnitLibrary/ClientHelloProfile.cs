namespace Curl.Tls;

/// <summary>
/// A ClientHello profile (ADR-0140, "Default ClientHello: the platform curl's, as
/// measured"): the record version, cipher suites, extension order and the lists the
/// extensions carry, copied from one curl build's captured hello. <see cref="Build" />
/// fills in what differs on every connection - the host name, random, legacy session ID
/// and key shares - so the hello is that build's byte for byte. Options change a
/// profile's lists with a <see langword="with" /> expression, never its extension order.
/// </summary>
/// <param name="RecordVersion">The version in the header of the record that carries the hello.</param>
/// <param name="CipherSuites">The cipher suite code points, in the order sent.</param>
/// <param name="ExtensionOrder">The extensions, in the order sent.</param>
/// <param name="SupportedVersions">The <c>supported_versions</c> list.</param>
/// <param name="SupportedGroups">The <c>supported_groups</c> list.</param>
/// <param name="KeyShareGroups">The groups the build sends a key share for, in the order sent.</param>
/// <param name="SignatureAlgorithms">The <c>signature_algorithms</c> list.</param>
/// <param name="ApplicationProtocols">The ALPN protocol names.</param>
/// <param name="EcPointFormats">The <c>ec_point_formats</c> list.</param>
/// <param name="PskKeyExchangeModes">The <c>psk_key_exchange_modes</c> list.</param>
/// <param name="CertificateCompressionAlgorithms">The <c>compress_certificate</c> list.</param>
public sealed record ClientHelloProfile(
    ushort RecordVersion,
    IReadOnlyList<ushort> CipherSuites,
    IReadOnlyList<TlsExtensionType> ExtensionOrder,
    IReadOnlyList<ushort> SupportedVersions,
    IReadOnlyList<ushort> SupportedGroups,
    IReadOnlyList<ushort> KeyShareGroups,
    IReadOnlyList<ushort> SignatureAlgorithms,
    IReadOnlyList<string> ApplicationProtocols,
    byte[] EcPointFormats,
    byte[] PskKeyExchangeModes,
    IReadOnlyList<ushort> CertificateCompressionAlgorithms)
{
    private const ushort Tls12Version = 0x0303;

    private const ushort Tls10Version = 0x0301;

    private const byte HandshakeContentType = 22;

    private static readonly Dictionary<TlsExtensionType, Func<ClientHelloProfile, string, IReadOnlyList<KeyShareEntry>, TlsExtension>> ExtensionEncoders = new()
    {
        [TlsExtensionType.ServerName] = (_, hostName, _) => ServerNameExtension.EncodeHostName(hostName),
        [TlsExtensionType.StatusRequest] = (_, _, _) => StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], [])),
        [TlsExtensionType.SupportedGroups] = (profile, _, _) => SupportedGroupsExtension.Encode(profile.SupportedGroups),
        [TlsExtensionType.EcPointFormats] = (profile, _, _) => EcPointFormatsExtension.Encode(profile.EcPointFormats),
        [TlsExtensionType.SignatureAlgorithms] = (profile, _, _) => SignatureAlgorithmsExtension.Encode(profile.SignatureAlgorithms),
        [TlsExtensionType.ApplicationLayerProtocolNegotiation] = (profile, _, _) => ApplicationLayerProtocolNegotiationExtension.Encode(profile.ApplicationProtocols),
        [TlsExtensionType.EncryptThenMac] = (_, _, _) => EncryptThenMacExtension.Encode(),
        [TlsExtensionType.ExtendedMasterSecret] = (_, _, _) => ExtendedMasterSecretExtension.Encode(),
        [TlsExtensionType.CompressCertificate] = (profile, _, _) => CompressCertificateExtension.Encode(profile.CertificateCompressionAlgorithms),
        [TlsExtensionType.SessionTicket] = (_, _, _) => SessionTicketExtension.Encode([]),
        [TlsExtensionType.SupportedVersions] = (profile, _, _) => SupportedVersionsExtension.EncodeOffered(profile.SupportedVersions),
        [TlsExtensionType.PskKeyExchangeModes] = (profile, _, _) => PskKeyExchangeModesExtension.Encode(profile.PskKeyExchangeModes),
        [TlsExtensionType.PostHandshakeAuth] = (_, _, _) => PostHandshakeAuthExtension.Encode(),
        [TlsExtensionType.KeyShare] = (_, _, keyShares) => KeyShareExtension.EncodeClientShares(keyShares),
        [TlsExtensionType.RenegotiationInfo] = (_, _, _) => RenegotiationInfoExtension.Encode([]),
    };

    /// <summary>
    /// The hello of curl 8.21.0 with Schannel on Windows 11, the Windows reference build
    /// (ADR-0018): used over TCP on Windows.
    /// </summary>
    public static ClientHelloProfile Schannel { get; } = new(
        Tls10Version,
        [
            0x1302, 0x1301, 0xc02c, 0xc02b, 0xc030, 0xc02f, 0xc024, 0xc023, 0xc028, 0xc027,
            0xc00a, 0xc009, 0xc014, 0xc013, 0x009d, 0x009c, 0x003d, 0x003c, 0x0035, 0x002f,
        ],
        [
            TlsExtensionType.ServerName, TlsExtensionType.StatusRequest, TlsExtensionType.SupportedVersions,
            TlsExtensionType.SignatureAlgorithms, TlsExtensionType.SessionTicket, TlsExtensionType.SupportedGroups,
            TlsExtensionType.EcPointFormats, TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.KeyShare,
            TlsExtensionType.PostHandshakeAuth, TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.RenegotiationInfo,
            TlsExtensionType.PskKeyExchangeModes,
        ],
        [0x0304, 0x0303],
        [0x001d, 0x0017, 0x0018],
        [0x001d, 0x0017, 0x0018],
        [0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0201, 0x0403, 0x0503, 0x0203, 0x0202, 0x0601, 0x0603],
        ["http/1.1"],
        [0],
        [1],
        [])
    {
        // Measured with --tls-max 1.2 and --tls-max 1.0 (BL-941): Schannel moves
        // supported_groups and ec_point_formats ahead of signature_algorithms.
        Tls12ExtensionOrder =
        [
            TlsExtensionType.ServerName, TlsExtensionType.StatusRequest, TlsExtensionType.SupportedGroups,
            TlsExtensionType.EcPointFormats, TlsExtensionType.SignatureAlgorithms, TlsExtensionType.SessionTicket,
            TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.RenegotiationInfo,
        ],
        Tls12RecordVersionIsTheCeiling = true,
    };

    /// <summary>
    /// The hello of Ubuntu's curl 8.18.0 with OpenSSL 3.5.5: used over TCP on Linux and
    /// macOS, and for QUIC there.
    /// </summary>
    public static ClientHelloProfile OpenSsl { get; } = new(
        Tls10Version,
        [
            0x1302, 0x1303, 0x1301, 0xc02c, 0xc030, 0x009f, 0xcca9, 0xcca8, 0xccaa, 0xc02b,
            0xc02f, 0x009e, 0xc024, 0xc028, 0x006b, 0xc023, 0xc027, 0x0067, 0xc00a, 0xc014,
            0x0039, 0xc009, 0xc013, 0x0033, 0x009d, 0x009c, 0x003d, 0x003c, 0x0035, 0x002f,
        ],
        [
            TlsExtensionType.RenegotiationInfo, TlsExtensionType.ServerName, TlsExtensionType.EcPointFormats,
            TlsExtensionType.SupportedGroups, TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.EncryptThenMac,
            TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.PostHandshakeAuth, TlsExtensionType.SignatureAlgorithms,
            TlsExtensionType.SupportedVersions, TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.KeyShare,
            TlsExtensionType.CompressCertificate,
        ],
        [0x0304, 0x0303],
        [0x11ec, 0x001d, 0x0017, 0x001e, 0x0018, 0x0019, 0x0100, 0x0101],
        [0x11ec, 0x001d],
        [
            0x0905, 0x0906, 0x0904, 0x0403, 0x0503, 0x0603, 0x0807, 0x0808, 0x081a, 0x081b,
            0x081c, 0x0809, 0x080a, 0x080b, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601,
            0x0303, 0x0301, 0x0302, 0x0402, 0x0502, 0x0602,
        ],
        ["h2", "http/1.1"],
        [0, 1, 2],
        [1],
        [0x0001, 0x0003])
    {
        PadsTcpHello = true,
    };

    /// <summary>
    /// The hello of curl.se's official Windows build (curl 8.18.0, LibreSSL 4.2.1): used
    /// for QUIC on Windows, where that build is the curl that does HTTP/3.
    /// </summary>
    public static ClientHelloProfile LibreSsl { get; } = new(
        Tls12Version,
        [
            0x1302, 0x1303, 0x1301, 0xc030, 0xc02c, 0xc028, 0xc024, 0xc014, 0xc00a, 0x009f,
            0x006b, 0x0039, 0xcca9, 0xcca8, 0xccaa, 0x00c4, 0x0088, 0x009d, 0x003d, 0x0035,
            0x00c0, 0x0084, 0xc02f, 0xc02b, 0xc027, 0xc023, 0xc013, 0xc009, 0x009e, 0x0067,
            0x0033, 0x00be, 0x0045, 0x009c, 0x003c, 0x002f, 0x00ba, 0x0041, 0xc011, 0xc007,
            0x0005, 0xc012, 0xc008, 0x0016, 0x000a, 0x00ff,
        ],
        [
            TlsExtensionType.SignatureAlgorithms, TlsExtensionType.ServerName, TlsExtensionType.EcPointFormats,
            TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.SupportedGroups,
            TlsExtensionType.ApplicationLayerProtocolNegotiation,
        ],
        [0x0304, 0x0303],
        [0x001d, 0x0017, 0x0018, 0x0019],
        [0x001d],
        [0x0806, 0x0601, 0x0603, 0x0805, 0x0501, 0x0503, 0x0804, 0x0401, 0x0403, 0x0201, 0x0203],
        ["h2", "http/1.1"],
        [0],
        [],
        []);

    /// <summary>
    /// Gets the extensions, in the order sent, of the hello the build sends when its version
    /// range's ceiling is below TLS 1.3. By default <see cref="ExtensionOrder" />: the OpenSSL
    /// build sends the same order and leaves out the TLS 1.3 extensions (measured, BL-941).
    /// </summary>
    public IReadOnlyList<TlsExtensionType> Tls12ExtensionOrder
    {
        get => field ?? ExtensionOrder;
        init;
    }

    /// <summary>
    /// Gets a value indicating whether the hello the build sends below a TLS 1.3 ceiling is in
    /// a record carrying that ceiling, as Schannel's is (<c>0x0303</c> under <c>--tls-max 1.2</c>,
    /// <c>0x0301</c> under <c>--tls-max 1.0</c>); otherwise it carries <see cref="RecordVersion" />,
    /// as OpenSSL's does (measured, BL-1152).
    /// </summary>
    public bool Tls12RecordVersionIsTheCeiling { get; init; }

    /// <summary>
    /// Gets a value indicating whether the build ends its TLS 1.3 and TLS 1.2 ClientHellos
    /// over TCP with <c>padding</c>, bringing a hello of 256 to 511 bytes up to 512 (OpenSSL's
    /// <c>tls_construct_ctos_padding</c>, measured under <c>--curves X25519</c>, BL-1048 and
    /// BL-1156). QUIC's hello, built from <see cref="ExtensionOrder" />, is not padded, as
    /// OpenSSL 3.5's QUIC client pads none (measured, BL-1156).
    /// </summary>
    public bool PadsTcpHello { get; init; }

    /// <summary>Builds the profile's ClientHello for one connection.</summary>
    /// <param name="hostName">The host name sent in <c>server_name</c>.</param>
    /// <param name="random">The 32-byte client random.</param>
    /// <param name="legacySessionId">The legacy session ID.</param>
    /// <param name="keyShares">The key shares, one per <see cref="KeyShareGroups" /> entry in its order.</param>
    /// <returns>The ClientHello.</returns>
    /// <exception cref="InvalidOperationException"><see cref="ExtensionOrder" /> names an extension a profile cannot build.</exception>
    public ClientHello Build(string hostName, byte[] random, byte[] legacySessionId, IReadOnlyList<KeyShareEntry> keyShares)
    {
        TlsExtension[] extensions = [.. ExtensionOrder.Select(type => EncodeExtension(type, hostName, keyShares))];
        return new ClientHello(Tls12Version, random, legacySessionId, CipherSuites, [0], extensions);
    }

    /// <summary>Returns the handshake record that carries <paramref name="hello" />, with the profile's record version.</summary>
    /// <param name="hello">The ClientHello.</param>
    /// <returns>The record header and the encoded hello.</returns>
    public byte[] EncodeRecord(ClientHello hello)
    {
        ArgumentNullException.ThrowIfNull(hello);
        TlsWriter writer = new();
        writer.WriteUInt8(HandshakeContentType);
        writer.WriteUInt16(RecordVersion);
        writer.WriteOpaque(2, hello.Encode());
        return writer.ToArray();
    }

    private TlsExtension EncodeExtension(TlsExtensionType type, string hostName, IReadOnlyList<KeyShareEntry> keyShares) =>
        ExtensionEncoders.TryGetValue(type, out Func<ClientHelloProfile, string, IReadOnlyList<KeyShareEntry>, TlsExtension>? encode)
            ? encode(this, hostName, keyShares)
            : throw new InvalidOperationException($"A ClientHello profile cannot build the {type} extension.");
}
