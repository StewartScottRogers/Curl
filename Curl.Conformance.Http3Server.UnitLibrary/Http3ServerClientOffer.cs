using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// The parts of a QUIC client's ClientHello the HTTP/3 stand-in negotiates from: its groups,
/// key shares, signature schemes and transport parameters. <see cref="Parse" /> refuses a
/// hello that cannot run TLS 1.3 over QUIC with the alert RFC 8446 and RFC 9001 prescribe.
/// </summary>
/// <param name="Groups">The client's <c>supported_groups</c>.</param>
/// <param name="KeyShares">The client's <c>key_share</c> entries.</param>
/// <param name="SignatureSchemes">The client's <c>signature_algorithms</c>.</param>
/// <param name="QuicTransportParameters">The client's encoded <c>quic_transport_parameters</c>.</param>
internal sealed record Http3ServerClientOffer(
    IReadOnlyList<ushort> Groups,
    IReadOnlyList<KeyShareEntry> KeyShares,
    IReadOnlyList<ushort> SignatureSchemes,
    byte[] QuicTransportParameters)
{
    private const ushort Tls13 = 0x0304;

    /// <summary>
    /// Reads <paramref name="hello" />: without TLS 1.3 in <c>supported_versions</c> it is
    /// <c>protocol_version</c>, without ALPN <c>h3</c> <c>no_application_protocol</c> (RFC 9001
    /// section 8.1), without groups, key shares, signature schemes or transport parameters
    /// <c>missing_extension</c>, and a malformed extension is its decoder's alert.
    /// </summary>
    /// <param name="hello">The ClientHello.</param>
    /// <returns>The offer.</returns>
    /// <exception cref="TlsAlertException">The hello cannot be served; the alert says why.</exception>
    public static Http3ServerClientOffer Parse(ClientHello hello)
    {
        IReadOnlyList<ushort> versions = Decode(hello, TlsExtensionType.SupportedVersions, SupportedVersionsExtension.DecodeOffered, TlsAlertDescription.ProtocolVersion);
        Require(versions.Contains(Tls13), TlsAlertDescription.ProtocolVersion);
        IReadOnlyList<string> protocols = Decode(hello, TlsExtensionType.ApplicationLayerProtocolNegotiation, ApplicationLayerProtocolNegotiationExtension.Decode, TlsAlertDescription.NoApplicationProtocol);
        Require(protocols.Contains(Http3ServerTlsSettings.ApplicationProtocol), TlsAlertDescription.NoApplicationProtocol);
        return new(
            Decode(hello, TlsExtensionType.SupportedGroups, SupportedGroupsExtension.Decode, TlsAlertDescription.MissingExtension),
            Decode(hello, TlsExtensionType.KeyShare, KeyShareExtension.DecodeClientShares, TlsAlertDescription.MissingExtension),
            Decode(hello, TlsExtensionType.SignatureAlgorithms, SignatureAlgorithmsExtension.Decode, TlsAlertDescription.MissingExtension),
            Decode(hello, TlsExtensionType.QuicTransportParameters, QuicTransportParametersExtension.Decode, TlsAlertDescription.MissingExtension));
    }

    /// <summary>Throws <paramref name="alert" /> unless <paramref name="condition" /> holds.</summary>
    /// <param name="condition">What must hold.</param>
    /// <param name="alert">The alert when it does not.</param>
    /// <exception cref="TlsAlertException"><paramref name="condition" /> is false.</exception>
    public static void Require(bool condition, TlsAlertDescription alert)
    {
        if (!condition)
        {
            throw new TlsAlertException(alert, isFromServer: true);
        }
    }

    private static T Decode<T>(ClientHello hello, TlsExtensionType type, Func<byte[], TlsDecodeResult<T>> decode, TlsAlertDescription missing)
    {
        TlsExtension? extension = hello.Extensions.FirstOrDefault(candidate => candidate.Type == type);
        Require(extension is not null, missing);
        TlsDecodeResult<T> decoded = decode(extension!.Data);
        Require(decoded.Succeeded, decoded.Alert.GetValueOrDefault());
        return decoded.Value;
    }
}
