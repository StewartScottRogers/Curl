using System.Text;
using Curl.Extended.Protocol.OciRegistry.Http;

namespace Curl.Extended.Protocol.OciRegistry.Auth;

/// <summary>
/// Builds the token request a <see cref="OciBearerChallenge"/> asks for (Docker/Distribution token authentication
/// specification, "Requesting a Token"): <c>GET</c> the realm with <c>service</c> and <c>scope</c> query parameters, with
/// HTTP Basic credentials when <c>-u</c> gave some (RFC 7617).
/// </summary>
public static class OciTokenRequestBuilder
{
    /// <summary>Builds the request.</summary>
    /// <param name="challenge">The registry's challenge.</param>
    /// <param name="userAndPassword"><c>-u</c>'s <c>user:password</c>, or <see langword="null"/> for an anonymous token.</param>
    /// <returns>The request, or <see langword="null"/> when the realm is not an absolute <c>http</c> or <c>https</c> URL (a bare path such as <c>/token</c> parses as an absolute <c>file</c> URL on Linux and macOS).</returns>
    /// <exception cref="Client.OciTransferException">
    /// <paramref name="userAndPassword"/> is set and the realm is plain <c>http</c>: the registry is always reached over
    /// HTTPS, so sending the credentials there would downgrade them to clear text (the downgrade curl refuses on a
    /// redirect, CVE-2022-27774).
    /// </exception>
    public static OciHttpRequest? Build(OciBearerChallenge challenge, string? userAndPassword)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (!Uri.TryCreate(challenge.Realm, UriKind.Absolute, out var realm) || realm.Scheme is not ("http" or "https"))
        {
            return null;
        }

        List<KeyValuePair<string, string>> headers = [new("Accept", "application/json")];
        if (userAndPassword is not null)
        {
            headers.Add(BasicCredentialsFor(realm, challenge.Realm, userAndPassword));
        }

        return new OciHttpRequest("GET", AddQuery(realm, challenge), headers, null);
    }

    private static KeyValuePair<string, string> BasicCredentialsFor(Uri realm, string realmText, string userAndPassword)
    {
        if (realm.Scheme == Uri.UriSchemeHttp)
        {
            throw new Client.OciTransferException(
                Curl.Protocol.Abstractions.CurlExitCode.LoginDenied,
                $"Refusing to send credentials to the non-HTTPS token realm {realmText}.");
        }

        return new("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userAndPassword)));
    }

    private static Uri AddQuery(Uri realm, OciBearerChallenge challenge)
    {
        var query = new StringBuilder(realm.Query);
        AppendParameter(query, "service", challenge.Service);
        AppendParameter(query, "scope", challenge.Scope);
        return new UriBuilder(realm) { Query = query.ToString().TrimStart('?') }.Uri;
    }

    private static void AppendParameter(StringBuilder query, string name, string? value)
    {
        if (value is not null)
        {
            query.Append(query.Length == 0 ? '?' : '&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }
    }
}
