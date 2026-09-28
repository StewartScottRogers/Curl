using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Picks one SASL mechanism from a mail server's list the way curl 8.21.0 does: the first,
/// in curl's preference order, that the server offers, the login options allow and the
/// request can use (ADR-0121, ADR-0123).
/// </summary>
/// <remarks>
/// GSSAPI, DIGEST-MD5, CRAM-MD5 and NTLM hold their places in the order but are not built
/// yet (BL-537, BL-538), so they are treated as not offered.
/// </remarks>
internal static class SaslMechanismRanking
{
    /// <summary>The SASL name of the EXTERNAL mechanism (RFC 4422 appendix A).</summary>
    internal const string External = "EXTERNAL";

    /// <summary>The SASL name of the OAUTHBEARER mechanism (RFC 7628).</summary>
    internal const string OAuthBearer = "OAUTHBEARER";

    /// <summary>The SASL name of Google's XOAUTH2 mechanism.</summary>
    internal const string XOAuth2 = "XOAUTH2";

    /// <summary>The SASL name of the PLAIN mechanism (RFC 4616).</summary>
    internal const string Plain = "PLAIN";

    /// <summary>The SASL name of the LOGIN mechanism (draft-murchison-sasl-login).</summary>
    internal const string Login = "LOGIN";

    /// <summary>The login option value that allows any mechanism.</summary>
    private const string AnyMechanism = "*";

    // Each mechanism in curl's preference order, with whether the request can use it:
    // EXTERNAL only when AUTH=EXTERNAL names it and the password is empty; the bearer
    // mechanisms only with a token; PLAIN and LOGIN only with a user and no token (measured,
    // ADR-0123). The mechanisms not built yet are never usable.
    private static readonly (string Name, Func<SaslRequest, bool> CanUse)[] CurlPreferenceOrder =
    [
        (External, CanUseExternal),
        ("GSSAPI", NotBuilt),
        ("DIGEST-MD5", NotBuilt),
        ("CRAM-MD5", NotBuilt),
        ("NTLM", NotBuilt),
        (OAuthBearer, HasBearerToken),
        (XOAuth2, HasBearerToken),
        (Plain, HasUserAndNoBearerToken),
        (Login, HasUserAndNoBearerToken),
    ];

    /// <summary>
    /// Gets the mechanism curl picks from <paramref name="offeredMechanisms" />.
    /// </summary>
    /// <param name="request">What the transfer may authenticate with.</param>
    /// <param name="offeredMechanisms">The server's mechanisms, compared case-insensitively.</param>
    /// <returns>The chosen mechanism's SASL name, or <see langword="null" /> when none is usable.</returns>
    internal static string? PickFirst(SaslRequest request, IReadOnlyList<string> offeredMechanisms) =>
        Array.Find(
            CurlPreferenceOrder,
            mechanism => IsAllowed(mechanism.Name, request.RequiredMechanism)
                && offeredMechanisms.Contains(mechanism.Name, StringComparer.OrdinalIgnoreCase)
                && mechanism.CanUse(request)).Name;

    private static bool IsAllowed(string mechanism, string? requiredMechanism) =>
        requiredMechanism is null or AnyMechanism || string.Equals(mechanism, requiredMechanism, StringComparison.OrdinalIgnoreCase);

    private static bool CanUseExternal(SaslRequest request) =>
        string.Equals(request.RequiredMechanism, External, StringComparison.OrdinalIgnoreCase)
            && request.Credential is { } credential
            && credential.Password.Length == 0;

    private static bool HasBearerToken(SaslRequest request) => request.BearerToken is not null;

    private static bool HasUserAndNoBearerToken(SaslRequest request) => request.BearerToken is null && request.Credential is not null;

    private static bool NotBuilt(SaslRequest _) => false;
}
