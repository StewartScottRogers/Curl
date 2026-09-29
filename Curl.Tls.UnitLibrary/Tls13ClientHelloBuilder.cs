namespace Curl.Tls;

/// <summary>
/// Builds the TLS 1.3 ClientHello from <see cref="Tls13ClientSettings" />: the extensions
/// in the settings' order, and the <c>padding</c> rule that brings a hello of 256 to 511
/// bytes up to 512 (the rule of RFC 7685's F5 workaround, as NSS and BoringSSL apply it).
/// </summary>
internal sealed class Tls13ClientHelloBuilder(Tls13ClientSettings settings, byte[] random, byte[] legacySessionId)
{
    /// <summary>The TLS 1.3 version code point.</summary>
    public const ushort Tls13Version = 0x0304;

    /// <summary>The <c>legacy_version</c> every TLS 1.3 hello carries.</summary>
    public const ushort LegacyVersion = 0x0303;

    private const int PaddingFloor = 0x100;
    private const int PaddingTarget = 0x200;
    private const int ExtensionHeaderLength = 4;

    /// <summary>Returns the ClientHello offering <paramref name="shares" /> and echoing <paramref name="cookie" />.</summary>
    /// <param name="shares">The key shares to send.</param>
    /// <param name="cookie">The HelloRetryRequest's cookie, or <see langword="null" />.</param>
    /// <returns>The ClientHello.</returns>
    public ClientHello Build(IReadOnlyList<KeyShareEntry> shares, byte[]? cookie)
    {
        List<TlsExtension> extensions = [];
        int paddingIndex = -1;
        foreach (TlsExtensionType type in settings.ExtensionOrder)
        {
            if (type == TlsExtensionType.Padding)
            {
                paddingIndex = extensions.Count;
                continue;
            }

            if (BuildExtension(type, shares, cookie) is { } extension)
            {
                extensions.Add(extension);
            }
        }

        extensions.AddRange(LowerVersionExtensions());
        extensions.AddRange(settings.FixedExtensions.Where(fixedExtension => !settings.ExtensionOrder.Contains(fixedExtension.Type)));
        ClientHello hello = Create(extensions);
        return paddingIndex < 0 ? hello : Pad(hello, extensions, paddingIndex);
    }

    private static int? PaddingLength(int unpaddedLength)
    {
        if (unpaddedLength < PaddingFloor || unpaddedLength >= PaddingTarget)
        {
            return null;
        }

        int padding = PaddingTarget - unpaddedLength;
        return padding > ExtensionHeaderLength ? padding - ExtensionHeaderLength : 1;
    }

    private ClientHello Pad(ClientHello hello, List<TlsExtension> extensions, int paddingIndex)
    {
        if (PaddingLength(hello.Encode().Length) is not { } length)
        {
            return hello;
        }

        extensions.Insert(paddingIndex, PaddingExtension.Encode(length));
        return Create(extensions);
    }

    private ClientHello Create(List<TlsExtension> extensions) =>
        new(LegacyVersion, random, legacySessionId, [.. settings.CipherSuites, .. settings.LowerVersions?.CipherSuites ?? []], [0], [.. extensions]);

    /// <summary>TLS 1.3, then with <see cref="Tls13ClientSettings.LowerVersions" /> every version from its ceiling down to its minimum.</summary>
    private ushort[] OfferedVersions()
    {
        Tls12ClientSettings? lower = settings.LowerVersions;
        if (lower is null)
        {
            return [Tls13Version];
        }

        int count = lower.MaximumVersion - lower.MinimumVersion + 1;
        return [Tls13Version, .. Enumerable.Range(0, count).Select(step => (ushort)((ushort)lower.MaximumVersion - step))];
    }

    /// <summary>The TLS 1.2-and-below extensions (<c>renegotiation_info</c>, <c>ec_point_formats</c> and the rest) the TLS 1.3 settings build no counterpart of.</summary>
    private IEnumerable<TlsExtension> LowerVersionExtensions() =>
        settings.LowerVersions is not { } lower
            ? []
            : Tls12ClientHelloBuilder.Build(lower, random, legacySessionId).Extensions.Where(extension =>
                !settings.ExtensionOrder.Contains(extension.Type) && settings.FixedExtensions.All(fixedExtension => fixedExtension.Type != extension.Type));

    private TlsExtension? BuildExtension(TlsExtensionType type, IReadOnlyList<KeyShareEntry> shares, byte[]? cookie) => type switch
    {
        TlsExtensionType.SupportedGroups => SupportedGroupsExtension.Encode(settings.SupportedGroups),
        TlsExtensionType.KeyShare => KeyShareExtension.EncodeClientShares(shares),
        TlsExtensionType.SupportedVersions => SupportedVersionsExtension.EncodeOffered(OfferedVersions()),
        _ => BuildSignatureOrCookieExtension(type, cookie),
    };

    private TlsExtension? BuildSignatureOrCookieExtension(TlsExtensionType type, byte[]? cookie) => type switch
    {
        TlsExtensionType.SignatureAlgorithms => SignatureAlgorithmsExtension.Encode([.. settings.SignatureAlgorithms.Union(settings.LowerVersions?.SignatureAlgorithms ?? [])]),
        TlsExtensionType.Cookie => EchoCookie(cookie),
        TlsExtensionType.CompressCertificate => BuildCompressCertificate(),
        _ => BuildOptionalExtension(type),
    };

    private static TlsExtension? EchoCookie(byte[]? cookie) => cookie is null ? null : CookieExtension.Encode(cookie);

    private TlsExtension? BuildOptionalExtension(TlsExtensionType type) => type switch
    {
        TlsExtensionType.ServerName => settings.ServerName is null ? null : ServerNameExtension.EncodeHostName(settings.ServerName),
        TlsExtensionType.ApplicationLayerProtocolNegotiation => settings.ApplicationProtocols.Count == 0
            ? null
            : ApplicationLayerProtocolNegotiationExtension.Encode(settings.ApplicationProtocols),
        TlsExtensionType.StatusRequest => BuildStatusRequest(),
        TlsExtensionType.PostHandshakeAuth => PostHandshakeAuthExtension.Encode(),
        _ => FindFixedExtension(type),
    };

    /// <summary>With <see cref="Tls13ClientSettings.CertificateCompressionAlgorithms" />, the algorithms the client decompresses (RFC 8879).</summary>
    private TlsExtension? BuildCompressCertificate() => settings.CertificateCompressionAlgorithms.Count > 0
        ? CompressCertificateExtension.Encode(settings.CertificateCompressionAlgorithms)
        : FindFixedExtension(TlsExtensionType.CompressCertificate);

    /// <summary>With <see cref="Tls13ClientSettings.RequestOcspStatus" />, an OCSP request naming no responders and no extensions, as OpenSSL sends.</summary>
    private TlsExtension? BuildStatusRequest() => settings.RequestOcspStatus
        ? StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []))
        : FindFixedExtension(TlsExtensionType.StatusRequest);

    private TlsExtension? FindFixedExtension(TlsExtensionType type) =>
        settings.FixedExtensions.FirstOrDefault(fixedExtension => fixedExtension.Type == type);
}
