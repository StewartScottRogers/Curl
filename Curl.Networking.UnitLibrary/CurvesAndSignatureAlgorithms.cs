using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Applies <c>--curves</c> and <c>--sigalgs</c> to the platform curl's ClientHello profile
/// for the hand-built client, on every platform (ADR-0151, ADR-0284): the groups, key shares
/// and signature schemes they name replace the profile's, and the rest of the ClientHello
/// stays the profile's. Failures come in the OpenSSL build's order (measured, BL-709): a
/// refused <c>--curves</c> value, then a refused <c>--sigalgs</c> value (both exit 59), then
/// no group left, then no signature scheme left, then - when TLS 1.3 is offered - no key
/// share left (all three exit 35, BL-1082). Under a TLS 1.2 ceiling the one failure is no
/// scheme TLS 1.2 can check, with OpenSSL's <c>no ciphers available</c>; no group left is no
/// failure there, the ClientHello leaving the group extensions out (BL-1094).
/// </summary>
internal static class CurvesAndSignatureAlgorithms
{
    /// <summary>Applies the options to <paramref name="profile" />.</summary>
    /// <param name="profile">The platform curl's profile.</param>
    /// <param name="options">The connection's TLS options.</param>
    /// <returns>The profile to offer, or the failure that stops the handshake before it starts.</returns>
    public static (ClientHelloProfile? Profile, ConnectResult? Failure) Apply(ClientHelloProfile profile, TlsClientOptions options)
    {
        var groups = ReadCurves(profile, options.Curves);
        if (groups is null)
        {
            return (null, ConnectResult.Failed(CurlExitCode.SslCipher, TlsFailureMessages.CurvesListRefused(options.Curves!)));
        }

        var schemes = ReadSignatureAlgorithms(profile, options.SignatureAlgorithms);
        if (schemes is null)
        {
            return (null, ConnectResult.Failed(CurlExitCode.SslCipher, TlsFailureMessages.SignatureAlgorithmsRefused(options.SignatureAlgorithms!)));
        }

        return Offer(profile with { SupportedGroups = groups.Groups, KeyShareGroups = groups.KeyShares, SignatureAlgorithms = schemes }, ReachesTls13(options));
    }

    private static OfferedGroups? ReadCurves(ClientHelloProfile profile, string? curves) =>
        curves is null ? new OfferedGroups(profile.SupportedGroups, profile.KeyShareGroups) : OpenSslGroupList.Parse(curves, profile);

    private static IReadOnlyList<ushort>? ReadSignatureAlgorithms(ClientHelloProfile profile, string? signatureAlgorithms) =>
        signatureAlgorithms is null ? profile.SignatureAlgorithms : OpenSslSignatureAlgorithmList.Parse(signatureAlgorithms);

    private static bool ReachesTls13(TlsClientOptions options) => options.MaximumVersion is TlsVersion.SystemDefault or TlsVersion.Tls13;

    private static (ClientHelloProfile? Profile, ConnectResult? Failure) Offer(ClientHelloProfile applied, bool reachesTls13) =>
        reachesTls13 ? OfferWithTls13(applied) : OfferBelowTls13(applied);

    // Offering TLS 1.3, the profile, unless it leaves no group TLS 1.3 can use (even when
    // TLS 1.2 is offered too), no signature scheme the client can check, or no key share.
    private static (ClientHelloProfile? Profile, ConnectResult? Failure) OfferWithTls13(ClientHelloProfile applied) =>
        !applied.SupportedGroups.Any(TlsNamedGroup.CanShare) ? Failed(TlsFailureMessages.OpenSslNoSuitableGroups)
        : ClientHelloProfileMapping.CheckableSignatureAlgorithms(applied).Count == 0 ? Failed(TlsFailureMessages.OpenSslNoSuitableSignatureAlgorithm)
        : applied.KeyShareGroups.Count == 0 ? Failed(TlsFailureMessages.OpenSslNoSuitableKeyShare)
        : (applied, null);

    // Under a TLS 1.2 ceiling no group is no failure - the ClientHello leaves the group
    // extensions out - but no scheme TLS 1.2 can check leaves no suite to run (measured,
    // BL-1094).
    private static (ClientHelloProfile? Profile, ConnectResult? Failure) OfferBelowTls13(ClientHelloProfile applied) =>
        ClientHelloProfileMapping.Tls12SignatureAlgorithms(applied).Count == 0 ? Failed(TlsFailureMessages.OpenSslNoCiphersAvailable) : (applied, null);

    private static (ClientHelloProfile? Profile, ConnectResult? Failure) Failed(string message) =>
        (null, ConnectResult.Failed(CurlExitCode.SslConnectError, message));
}
