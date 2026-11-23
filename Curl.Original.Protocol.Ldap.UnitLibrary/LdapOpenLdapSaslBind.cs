using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Binds as curl's OpenLDAP build does when the auth mask or <c>--login-options AUTH=</c> asks for
/// SASL instead of a simple bind (BL-5435): reads <c>supportedSASLMechanisms</c> from the rootDSE,
/// binds with <c>DIGEST-MD5</c> when the server offers it and the options allow it, and otherwise
/// fails with exit 67 <c>Login denied</c>. The password never crosses the wire in clear.
/// </summary>
internal static class LdapOpenLdapSaslBind
{
    private const string AuthPrefix = "AUTH=";

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

    /// <summary>Binds with SASL as described on the type; <see langword="null" /> once bound, otherwise the failed result.</summary>
    public static async ValueTask<TransferResult?> BindAsync(LdapExchange exchange, NetworkCredential credentials, string host, bool allowsDigest, CancellationToken cancellationToken)
    {
        if (await WinLdapLogonBind.ReadRootDseAsync(exchange, WinLdapLogonBind.SupportedSaslMechanisms, cancellationToken).ConfigureAwait(false) is not { } offered)
        {
            return await LdapBind.OpenLdapBindFailedAsync(exchange, new LdapBindReply(LdapBindReplyStatus.Closed, 0), cancellationToken).ConfigureAwait(false);
        }

        if (!allowsDigest || !offered.Contains(LdapDigestMd5Bind.Mechanism, StringComparer.OrdinalIgnoreCase))
        {
            return await LdapBind.OpenLdapLoginDeniedAsync(exchange, cancellationToken).ConfigureAwait(false);
        }

        LdapBindReply reply = await LdapAuthenticatedBind.DigestAsync(exchange, credentials, host, cancellationToken, acceptsQopAuth: true).ConfigureAwait(false);
        return reply.IsSuccess ? null : await SaslBindFailedAsync(exchange, reply, cancellationToken).ConfigureAwait(false);
    }

    // The OpenLDAP SASL bind (ADR-1342): a server that closed is exit 7; a challenge the client cannot answer (Local Error, as under a
    // server minssf) is exit 67 "Authentication cancelled"; every other failed reply is exit 67 "Login denied", never exit 38.
    private static ValueTask<TransferResult?> SaslBindFailedAsync(LdapExchange exchange, LdapBindReply reply, CancellationToken cancellationToken) =>
        reply.Status == LdapBindReplyStatus.Closed
            ? LdapBind.OpenLdapBindFailedAsync(exchange, reply, cancellationToken)
            : reply.Status == LdapBindReplyStatus.Answered && reply.ResultCode == WinLdapResultText.LocalError
                ? LdapBind.UnbindAndFailAsync(exchange, TransferResult.Failure(CurlExitCode.LoginDenied, "Authentication cancelled"), cancellationToken)
                : LdapBind.OpenLdapLoginDeniedAsync(exchange, cancellationToken);

    private static IEnumerable<string> SplitOptions(string? loginOptions) =>
        (loginOptions ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
