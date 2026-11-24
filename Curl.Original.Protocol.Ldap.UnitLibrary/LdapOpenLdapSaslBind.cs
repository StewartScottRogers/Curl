using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Binds as curl's OpenLDAP build does when the auth mask or <c>--login-options AUTH=</c> asks for
/// SASL instead of a simple bind (BL-5435): reads <c>supportedSASLMechanisms</c> from the rootDSE,
/// binds with <c>DIGEST-MD5</c>, else <c>PLAIN</c>, when the server offers it and the options allow it, and otherwise
/// fails with exit 67 <c>Login denied</c>. The password never crosses the wire in clear.
/// </summary>
internal static class LdapOpenLdapSaslBind
{
    private const string AuthPrefix = "AUTH=";
    private const string PlainMechanism = "PLAIN";

    private static readonly string[] KnownMechanisms = ["EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "XOAUTH2", "OAUTHBEARER", "PLAIN", "LOGIN"];

    /// <summary>
    /// Whether <paramref name="loginOptions" /> are what curl's <c>Curl_sasl_parse_url_auth_option</c> accepts (STD-184): empty, or
    /// <c>;</c>-separated items that are each exactly <c>AUTH=</c> and a known SASL mechanism or <c>*</c>. Anything else, an empty item included, is exit 49.
    /// </summary>
    public static bool AreWellFormed(string? loginOptions) =>
        string.IsNullOrEmpty(loginOptions)
        || loginOptions.Split(';').All(item =>
            item.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase)
            && (item[AuthPrefix.Length..] == "*" || KnownMechanisms.Contains(item[AuthPrefix.Length..], StringComparer.OrdinalIgnoreCase)));

    /// <summary>Whether <paramref name="loginOptions" /> carry an <c>AUTH=</c> option, which selects SASL.</summary>
    public static bool HasAuthOption(string? loginOptions) =>
        SplitOptions(loginOptions).Any(option => option.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether DIGEST-MD5 is allowed: by an <c>AUTH=DIGEST-MD5</c> or <c>AUTH=*</c> option, else by the mask holding Digest.</summary>
    public static bool AllowsDigest(string? loginOptions, HttpAuthSchemes schemes)
    {
        string[] mechanisms = [.. SplitOptions(loginOptions)
            .Where(option => option.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(option => option[AuthPrefix.Length..])];
        return mechanisms.Length == 0
            ? schemes.HasFlag(HttpAuthSchemes.Digest)
            : mechanisms.Any(mechanism => mechanism == "*" || mechanism.Equals(LdapDigestMd5Bind.Mechanism, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether PLAIN is allowed: by an <c>AUTH=PLAIN</c> or <c>AUTH=*</c> option, else by the mask holding Basic (STD-185).</summary>
    public static bool AllowsPlain(string? loginOptions, HttpAuthSchemes schemes)
    {
        string[] mechanisms = [.. SplitOptions(loginOptions)
            .Where(option => option.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(option => option[AuthPrefix.Length..])];
        return mechanisms.Length == 0
            ? schemes.HasFlag(HttpAuthSchemes.Basic)
            : mechanisms.Any(mechanism => mechanism == "*" || mechanism.Equals(PlainMechanism, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Binds with SASL as described on the type; <see langword="null" /> once bound, otherwise the failed result.</summary>
    public static async ValueTask<TransferResult?> BindAsync(LdapExchange exchange, NetworkCredential credentials, string host, bool allowsDigest, bool allowsPlain, CancellationToken cancellationToken)
    {
        if (await WinLdapLogonBind.ReadRootDseAsync(exchange, WinLdapLogonBind.SupportedSaslMechanisms, cancellationToken).ConfigureAwait(false) is not { } offered)
        {
            return await LdapBind.OpenLdapBindFailedAsync(exchange, new LdapBindReply(LdapBindReplyStatus.Closed, 0), cancellationToken).ConfigureAwait(false);
        }

        LdapBindReply? reply = await BindWithOfferedMechanismAsync(exchange, credentials, host, allowsDigest, allowsPlain, offered, cancellationToken).ConfigureAwait(false);
        return reply is null
            ? await LdapBind.OpenLdapLoginDeniedAsync(exchange, cancellationToken).ConfigureAwait(false)
            : reply.Value.IsSuccess ? null : await SaslBindFailedAsync(exchange, reply.Value, cancellationToken).ConfigureAwait(false);
    }

    // DIGEST-MD5 first, then PLAIN; null when no allowed mechanism is offered.
    private static async ValueTask<LdapBindReply?> BindWithOfferedMechanismAsync(LdapExchange exchange, NetworkCredential credentials, string host, bool allowsDigest, bool allowsPlain, IReadOnlyCollection<string> offered, CancellationToken cancellationToken)
    {
        if (allowsDigest && offered.Contains(LdapDigestMd5Bind.Mechanism, StringComparer.OrdinalIgnoreCase))
        {
            return await LdapAuthenticatedBind.DigestAsync(exchange, credentials, host, cancellationToken, acceptsQopAuth: true).ConfigureAwait(false);
        }

        return allowsPlain && offered.Contains(PlainMechanism, StringComparer.OrdinalIgnoreCase)
            ? await exchange.SaslBindAsync(PlainMechanism, System.Text.Encoding.UTF8.GetBytes("\0" + credentials.UserName + "\0" + credentials.Password), cancellationToken).ConfigureAwait(false)
            : null;
    }
    // The OpenLDAP SASL bind (ADR-1343): a server that closed is exit 7; a challenge the client cannot answer (Local Error, as under a
    // server minssf) is exit 67 "Authentication cancelled"; every other failed reply is exit 67 "Login denied", never exit 38.
    private static ValueTask<TransferResult?> SaslBindFailedAsync(LdapExchange exchange, LdapBindReply reply, CancellationToken cancellationToken) =>
        reply.Status == LdapBindReplyStatus.Closed
            ? LdapBind.OpenLdapBindFailedAsync(exchange, reply, cancellationToken)
            : reply.ResultCode == WinLdapResultText.LocalError
                ? LdapBind.UnbindAndFailAsync(exchange, TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled"), cancellationToken)
                : LdapBind.OpenLdapLoginDeniedAsync(exchange, cancellationToken);

    private static IEnumerable<string> SplitOptions(string? loginOptions) =>
        (loginOptions ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
