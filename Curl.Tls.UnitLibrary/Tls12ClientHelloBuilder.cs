namespace Curl.Tls;

/// <summary>
/// Builds the TLS 1.2 and below ClientHello from <see cref="Tls12ClientSettings" />, its
/// extensions in the order OpenSSL sends them.
/// </summary>
internal static class Tls12ClientHelloBuilder
{
    // RFC 8422 section 5.1.2: one format, uncompressed.
    private static readonly TlsExtension UncompressedPointFormat = new(TlsExtensionType.EcPointFormats, [1, 0]);

    /// <summary>Returns the ClientHello with <paramref name="random" /> and <paramref name="sessionId" />.</summary>
    public static ClientHello Build(Tls12ClientSettings settings, byte[] random, byte[] sessionId)
    {
        List<TlsExtension> extensions = [RenegotiationInfoExtension.Encode([])];
        if (settings.ServerName is not null)
        {
            extensions.Add(ServerNameExtension.EncodeHostName(settings.ServerName));
        }

        extensions.Add(UncompressedPointFormat);
        extensions.Add(SupportedGroupsExtension.Encode(settings.SupportedGroups));
        AddSessionAndStatusExtensions(settings, extensions);
        AddNegotiatedExtensions(settings, extensions);
        return new ClientHello((ushort)settings.MaximumVersion, random, sessionId, settings.OfferedCipherSuites, [0], extensions);
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
