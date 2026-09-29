using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Picks one SASL mechanism from a mail server's list the way curl 8.21.0 does: the first,
/// in curl's preference order, that the server offers, the login options allow and the
/// request can use (ADR-0121, ADR-0123, ADR-0184).
/// </summary>
internal static class SaslMechanismRanking
{
    /// <summary>The SASL name of the EXTERNAL mechanism (RFC 4422 appendix A).</summary>
    internal const string External = "EXTERNAL";

    /// <summary>The SASL name of the GSSAPI mechanism (RFC 4752).</summary>
    internal const string Gssapi = "GSSAPI";

    /// <summary>The SASL name of the DIGEST-MD5 mechanism (RFC 2831).</summary>
    internal const string DigestMd5 = "DIGEST-MD5";

    /// <summary>The SASL name of the CRAM-MD5 mechanism (RFC 2195).</summary>
    internal const string CramMd5 = "CRAM-MD5";

    /// <summary>The SASL name of Microsoft's NTLM mechanism.</summary>
    internal const string Ntlm = "NTLM";

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

    // Each mechanism in curl's preference order, with whether the request can use it and
    // whether it runs on a security context: EXTERNAL only when AUTH=EXTERNAL names it and the
    // password is empty; GSSAPI only with a user name naming a domain or realm, or an empty one
    // (curl's Curl_auth_user_contains_domain); the bearer mechanisms only with a token; the
    // others only with a user and no token (measured, ADR-0123, ADR-0139).
    private static readonly (string Name, Func<SaslRequest, bool> CanUse, bool RunsOnSecurityContext)[] CurlPreferenceOrder =
    [
        (External, CanUseExternal, false),
        (Gssapi, CanUseGssapi, true),
        (DigestMd5, HasUserAndNoBearerToken, false),
        (CramMd5, HasUserAndNoBearerToken, false),
        (Ntlm, HasUserAndNoBearerToken, true),
        (OAuthBearer, HasBearerToken, false),
        (XOAuth2, HasBearerToken, false),
        (Plain, HasUserAndNoBearerToken, false),
        (Login, HasUserAndNoBearerToken, false),
    ];

    /// <summary>
    /// Gets the mechanism curl picks from <paramref name="offeredMechanisms" />.
    /// </summary>
    /// <param name="request">What the transfer may authenticate with.</param>
    /// <param name="offeredMechanisms">The server's mechanisms, compared case-insensitively.</param>
    /// <param name="securityContextsAvailable">
    /// Whether GSSAPI and NTLM can be answered, which needs an <see cref="ISecurityContextFactory" />;
    /// without one they are treated as not offered.
    /// </param>
    /// <returns>The chosen mechanism's SASL name, or <see langword="null" /> when none is usable.</returns>
    internal static string? PickFirst(SaslRequest request, IReadOnlyList<string> offeredMechanisms, bool securityContextsAvailable) =>
        Array.Find(
            CurlPreferenceOrder,
            mechanism => (securityContextsAvailable || !mechanism.RunsOnSecurityContext)
                && IsAllowed(mechanism.Name, request.RequiredMechanism)
                && offeredMechanisms.Contains(mechanism.Name, StringComparer.OrdinalIgnoreCase)
                && mechanism.CanUse(request)).Name;

    private static bool IsAllowed(string mechanism, string? requiredMechanism) =>
        requiredMechanism is null or AnyMechanism || string.Equals(mechanism, requiredMechanism, StringComparison.OrdinalIgnoreCase);

    private static bool CanUseExternal(SaslRequest request) =>
        string.Equals(request.RequiredMechanism, External, StringComparison.OrdinalIgnoreCase)
            && request.Credential is { } credential
            && credential.Password.Length == 0;

    // curl's Curl_auth_user_contains_domain: a '\', '/' or '@' that is neither the first nor
    // the last character; an empty user name passes too, the default credentials.
    private static bool CanUseGssapi(SaslRequest request)
    {
        if (!HasUserAndNoBearerToken(request))
        {
            return false;
        }

        string user = request.Credential!.UserName;
        int separator = user.IndexOfAny(['\\', '/', '@']);
        return user.Length == 0 || (separator > 0 && separator < user.Length - 1);
    }

    private static bool HasBearerToken(SaslRequest request) => request.BearerToken is not null;

    private static bool HasUserAndNoBearerToken(SaslRequest request) => request.BearerToken is null && request.Credential is not null;
}
