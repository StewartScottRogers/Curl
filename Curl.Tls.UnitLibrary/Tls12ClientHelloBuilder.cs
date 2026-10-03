using System.Text;

namespace Curl.Tls;

/// <summary>
/// Builds the TLS 1.2 and below ClientHello from <see cref="Tls12ClientSettings" />, its
/// extensions in <see cref="Tls12ClientSettings.ExtensionOrder" />, then with
/// <see cref="Tls12ClientSettings.PadHello" /> <c>padding</c> by
/// <see cref="PaddingExtension.DataLengthFor" />'s rule, last, where OpenSSL sends it.
/// </summary>
internal static class Tls12ClientHelloBuilder
{
    // RFC 8422 section 5.1.2: one format, uncompressed.
    private static readonly TlsExtension UncompressedPointFormat = new(TlsExtensionType.EcPointFormats, [1, 0]);

    /// <summary>Returns the ClientHello with <paramref name="random" /> and <paramref name="sessionId" />, padded when <see cref="Tls12ClientSettings.PadHello" /> asks.</summary>
    public static ClientHello Build(Tls12ClientSettings settings, byte[] random, byte[] sessionId)
    {
        ClientHello hello = BuildUnpadded(settings, random, sessionId);
        return settings.PadHello && PaddingExtension.DataLengthFor(hello.Encode().Length) is { } length
            ? hello with { Extensions = [.. hello.Extensions, PaddingExtension.Encode(length)] }
            : hello;
    }

    /// <summary>Returns the ClientHello with <paramref name="random" /> and <paramref name="sessionId" />, never padded.</summary>
    public static ClientHello BuildUnpadded(Tls12ClientSettings settings, byte[] random, byte[] sessionId)
    {
        List<TlsExtension> extensions = [RenegotiationInfoExtension.Encode([])];
        if (settings.ServerName is not null)
        {
            extensions.Add(ServerNameExtension.EncodeHostName(settings.ServerName));
        }

        // OpenSSL sends srp after server_name (and max_fragment_length, which Curl never sends).
        if (settings.SrpCredentials is not null)
        {
            extensions.Add(SrpExtension.Encode(Encoding.UTF8.GetBytes(settings.SrpCredentials.UserName)));
        }

        // With no group to offer, OpenSSL sends neither ec_point_formats nor supported_groups
        // (measured with --tls-max 1.2 --curves '?bogus', BL-1094).
        if (settings.SupportedGroups.Count > 0)
        {
            extensions.Add(UncompressedPointFormat);
            extensions.Add(SupportedGroupsExtension.Encode(settings.SupportedGroups));
        }

        AddSessionAndStatusExtensions(settings, extensions);
        AddNegotiatedExtensions(settings, extensions);
        return new ClientHello((ushort)settings.MaximumVersion, random, sessionId, settings.OfferedCipherSuites, [0], InOrder(settings, extensions));
    }

    // The built extensions with the fixed ones in place of their types, in the settings' order,
    // and those whose type the order does not list after it.
    private static List<TlsExtension> InOrder(Tls12ClientSettings settings, List<TlsExtension> built)
    {
        List<TlsExtension> sent = [.. built.Where(extension => settings.FixedExtensions.All(fixedExtension => fixedExtension.Type != extension.Type)), .. settings.FixedExtensions];
        return
        [
            .. settings.ExtensionOrder.SelectMany(type => sent.Where(extension => extension.Type == type)),
            .. sent.Where(extension => !settings.ExtensionOrder.Contains(extension.Type)),
        ];
    }

    private static void AddSessionAndStatusExtensions(Tls12ClientSettings settings, List<TlsExtension> extensions)
    {
        if (settings.OfferSessionTicket)
        {
            extensions.Add(new TlsExtension(TlsExtensionType.SessionTicket, settings.SessionToResume?.Ticket ?? []));
        }

        if (settings.RequestOcspStatus)
        {
            extensions.Add(StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], [])));
        }

        if (settings.ApplicationProtocols.Count > 0)
        {
            extensions.Add(ApplicationLayerProtocolNegotiationExtension.Encode(settings.ApplicationProtocols));
        }
    }

    private static void AddNegotiatedExtensions(Tls12ClientSettings settings, List<TlsExtension> extensions)
    {
        if (settings.OfferEncryptThenMac)
        {
            extensions.Add(new TlsExtension(TlsExtensionType.EncryptThenMac, []));
        }

        if (settings.OfferExtendedMasterSecret)
        {
            extensions.Add(new TlsExtension(TlsExtensionType.ExtendedMasterSecret, []));
        }

        // RFC 5246 section 7.4.1.4.1: signature_algorithms exists from TLS 1.2 on.
        if (settings.MaximumVersion == TlsProtocolVersion.Tls12)
        {
            extensions.Add(SignatureAlgorithmsExtension.Encode(settings.SignatureAlgorithms));
        }
    }
}
