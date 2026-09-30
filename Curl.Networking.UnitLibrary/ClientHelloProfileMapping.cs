using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Turns a measured <see cref="ClientHelloProfile" /> into what the hand-built client's
/// settings take (BL-820, ADR-0140 "Default ClientHello"): the profile's extension order,
/// the extensions the handshake does not build sent verbatim from it, and its lists cut to
/// what each client can honour, so a server never picks a capability the client lacks.
/// </summary>
internal static class ClientHelloProfileMapping
{
    // An OCSP request naming no responders and no extensions, as both builds send it.
    private static readonly TlsExtension OcspStatusRequest = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));

    /// <summary>
    /// Returns the profile's extension order, with <c>status_request</c> added after
    /// <c>supported_groups</c>, where OpenSSL sends it, when <c>--cert-status</c> asks for a
    /// stapled response and the profile does not already send one.
    /// </summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <param name="requestOcspStatus">Whether <c>--cert-status</c> is on.</param>
    /// <returns>The extension order.</returns>
    public static IReadOnlyList<TlsExtensionType> ExtensionOrder(ClientHelloProfile profile, bool requestOcspStatus)
    {
        if (!requestOcspStatus || profile.ExtensionOrder.Contains(TlsExtensionType.StatusRequest))
        {
            return profile.ExtensionOrder;
        }

        int afterGroups = profile.ExtensionOrder.ToList().IndexOf(TlsExtensionType.SupportedGroups) + 1;
        return [.. profile.ExtensionOrder.Take(afterGroups), TlsExtensionType.StatusRequest, .. profile.ExtensionOrder.Skip(afterGroups)];
    }

    /// <summary>
    /// Returns the profile's extensions the TLS 1.3 handshake does not build, as the profile
    /// sends them: <c>renegotiation_info</c>, <c>ec_point_formats</c>, <c>session_ticket</c>,
    /// <c>encrypt_then_mac</c>, <c>extended_master_secret</c>, <c>status_request</c> and
    /// <c>psk_key_exchange_modes</c>, each only when the profile sends it.
    /// </summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <returns>The extensions, for <see cref="Tls13ClientSettings.FixedExtensions" />.</returns>
    public static IReadOnlyList<TlsExtension> FixedExtensions(ClientHelloProfile profile)
    {
        TlsExtension[] candidates =
        [
            RenegotiationInfoExtension.Encode([]),
            EcPointFormatsExtension.Encode(profile.EcPointFormats),
            SessionTicketExtension.Encode([]),
            EncryptThenMacExtension.Encode(),
            ExtendedMasterSecretExtension.Encode(),
            OcspStatusRequest,
            PskKeyExchangeModesExtension.Encode(profile.PskKeyExchangeModes),
        ];
        return [.. candidates.Where(extension => profile.ExtensionOrder.Contains(extension.Type))];
    }

    /// <summary>
    /// Returns the profile's signature schemes that a TLS 1.3 CertificateVerify or a TLS 1.2
    /// signature can be checked with, in the profile's order.
    /// </summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <returns>The schemes the ClientHello offers.</returns>
    public static IReadOnlyList<ushort> CheckableSignatureAlgorithms(ClientHelloProfile profile) =>
        [.. profile.SignatureAlgorithms.Where(scheme => TlsSignatureScheme.IsCertificateVerifyScheme(scheme) || TlsSignatureScheme.IsTls12Scheme(scheme))];

    /// <summary>Returns the profile's signature schemes TLS 1.2 can check, in its order.</summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <returns>The schemes for <see cref="Tls12ClientSettings.SignatureAlgorithms" />.</returns>
    public static IReadOnlyList<ushort> Tls12SignatureAlgorithms(ClientHelloProfile profile) =>
        [.. profile.SignatureAlgorithms.Where(TlsSignatureScheme.IsTls12Scheme)];

    /// <summary>Returns the profile's groups TLS 1.2 agrees ECDHE on, in its order.</summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <returns>The groups for <see cref="Tls12ClientSettings.SupportedGroups" />.</returns>
    public static IReadOnlyList<ushort> Tls12SupportedGroups(ClientHelloProfile profile) =>
        [.. profile.SupportedGroups.Where(TlsNamedGroup.IsTls12EcdheGroup)];

    /// <summary>Returns whether the profile sends the extension.</summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <param name="type">The extension type.</param>
    /// <returns><see langword="true" /> when <see cref="ClientHelloProfile.ExtensionOrder" /> lists it.</returns>
    public static bool Sends(ClientHelloProfile profile, TlsExtensionType type) => profile.ExtensionOrder.Contains(type);
}
