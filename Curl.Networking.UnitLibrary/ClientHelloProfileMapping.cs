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
    public static IReadOnlyList<TlsExtensionType> ExtensionOrder(ClientHelloProfile profile, bool requestOcspStatus) =>
        WithStatusRequest(profile.ExtensionOrder, requestOcspStatus);

    /// <summary>
    /// Returns the profile's extension order for a version range whose ceiling is below TLS
    /// 1.3 (BL-941), with <c>srp</c> after <c>server_name</c>, where OpenSSL sends it, and
    /// <c>status_request</c> placed as <see cref="ExtensionOrder" /> places it.
    /// </summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <param name="requestOcspStatus">Whether <c>--cert-status</c> is on.</param>
    /// <returns>The order for <see cref="Tls12ClientSettings.ExtensionOrder" />.</returns>
    public static IReadOnlyList<TlsExtensionType> Tls12ExtensionOrder(ClientHelloProfile profile, bool requestOcspStatus) =>
        WithStatusRequest(InsertAfter(profile.Tls12ExtensionOrder, TlsExtensionType.ServerName, TlsExtensionType.Srp), requestOcspStatus);

    /// <summary>
    /// Returns the profile's extensions the TLS 1.2 handshake builds differently, as the
    /// profile sends them: its <c>ec_point_formats</c> list, when it offers an ECDHE group,
    /// and <c>status_request</c>, each only when the profile sends it.
    /// </summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <returns>The extensions, for <see cref="Tls12ClientSettings.FixedExtensions" />.</returns>
    public static IReadOnlyList<TlsExtension> Tls12FixedExtensions(ClientHelloProfile profile)
    {
        TlsExtension[] candidates = Tls12SupportedGroups(profile).Count > 0
            ? [EcPointFormatsExtension.Encode(profile.EcPointFormats), OcspStatusRequest]
            : [OcspStatusRequest];
        return [.. candidates.Where(extension => profile.Tls12ExtensionOrder.Contains(extension.Type))];
    }

    private static IReadOnlyList<TlsExtensionType> WithStatusRequest(IReadOnlyList<TlsExtensionType> order, bool requestOcspStatus) =>
        requestOcspStatus ? InsertAfter(order, TlsExtensionType.SupportedGroups, TlsExtensionType.StatusRequest) : order;

    // The order with the type inserted after the anchor, unless the order already lists it.
    private static IReadOnlyList<TlsExtensionType> InsertAfter(IReadOnlyList<TlsExtensionType> order, TlsExtensionType anchor, TlsExtensionType type)
    {
        if (order.Contains(type))
        {
            return order;
        }

        int afterAnchor = order.ToList().IndexOf(anchor) + 1;
        return [.. order.Take(afterAnchor), type, .. order.Skip(afterAnchor)];
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
