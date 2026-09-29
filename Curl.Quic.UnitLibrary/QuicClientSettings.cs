using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// What one QUIC client handshake offers (ADR-0144 section 5): the TLS 1.3 ClientHello
/// profile, the transport parameters, the length of the connection IDs and an
/// address-validation token from an earlier connection. <see cref="QuicClientHandshake" />
/// adds the <c>quic_transport_parameters</c> extension to the TLS settings itself.
/// </summary>
public sealed record QuicClientSettings
{
    /// <summary>The length of every connection ID the client chooses, as curl's ngtcp2 build uses.</summary>
    public const int CurlConnectionIdLength = 20;

    /// <summary>The ALPN protocols curl's build offers over QUIC, in order.</summary>
    public static IReadOnlyList<string> CurlApplicationProtocols { get; } = ["h3", "h3-29"];

    /// <summary>Gets the TLS 1.3 client settings; their <c>quic_transport_parameters</c> extension is added by the handshake.</summary>
    public required Tls13ClientSettings Tls { get; init; }

    /// <summary>Gets the transport parameters to declare; the handshake sets their <c>initial_source_connection_id</c>.</summary>
    public QuicTransportParameters TransportParameters { get; init; } = QuicTransportParameters.CurlClientDefaults;

    /// <summary>Gets the length of the connection IDs the client chooses, 8 to 20 bytes (RFC 9000 section 7.2 asks for at least 8 for the first destination).</summary>
    public int ConnectionIdLength { get; init; } = CurlConnectionIdLength;

    /// <summary>Gets an address-validation token a NEW_TOKEN frame gave on an earlier connection to this server, sent in the first Initial; empty for none.</summary>
    public ReadOnlyMemory<byte> Token { get; init; }

    /// <summary>Gets the congestion controller: CUBIC, as curl's build runs, unless a test asks for RFC 9002's NewReno.</summary>
    public QuicCongestionControlAlgorithm CongestionControl { get; init; } = QuicCongestionControlAlgorithm.Cubic;

    /// <summary>
    /// Returns the TLS settings of curl.se's ngtcp2 build's ClientHello as ADR-0144 section 5
    /// measured it (LibreSSL 4.2.1): suites <c>1302 1303 1301</c>, groups <c>001d 0017 0018
    /// 0019</c> with an X25519 share, its nine signature algorithms, <c>ec_point_formats</c>
    /// uncompressed, ALPN <c>h3</c> then <c>h3-29</c>, and the extensions in the measured order.
    /// </summary>
    /// <param name="serverName">The host name for <c>server_name</c>, or <see langword="null" /> for an IP address.</param>
    /// <returns>The settings.</returns>
    public static Tls13ClientSettings CreateCurlTlsSettings(string? serverName) => new()
    {
        ServerName = serverName,
        CipherSuites = [0x1302, 0x1303, 0x1301],
        SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1],
        KeyShareGroups = [TlsNamedGroup.X25519],
        SignatureAlgorithms = [0x0806, 0x0601, 0x0603, 0x0805, 0x0501, 0x0503, 0x0804, 0x0401, 0x0403],
        ApplicationProtocols = CurlApplicationProtocols,
        ExtensionOrder =
        [
            TlsExtensionType.QuicTransportParameters,
            TlsExtensionType.ServerName,
            TlsExtensionType.EcPointFormats,
            TlsExtensionType.SupportedGroups,
            TlsExtensionType.KeyShare,
            TlsExtensionType.ApplicationLayerProtocolNegotiation,
            TlsExtensionType.SupportedVersions,
            TlsExtensionType.SignatureAlgorithms,
        ],
        FixedExtensions = [new TlsExtension(TlsExtensionType.EcPointFormats, [0x01, 0x00])],
    };
}
