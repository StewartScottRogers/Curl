using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>
/// Builds the header fields curl's options add to a handshake request, as curl sends them on HTTP: <c>Authorization</c>
/// from <c>--oauth2-bearer</c> (Bearer) or else <c>-u</c> (Basic), <c>User-Agent</c> from <c>-A</c> or curl's own,
/// <c>Cookie</c> from <c>-b</c>, then each <c>-H</c> value (ADR-1243 sets the same order for MCP).
/// </summary>
internal static class HandshakeHeaderFields
{
    /// <summary>The <c>User-Agent</c> curl sends when <c>-A</c> is not given.</summary>
    internal const string DefaultUserAgent = "curl/8.22.0";

    /// <summary>Builds the fields of <paramref name="context"/>'s options.</summary>
    /// <param name="context">The transfer whose <c>-H</c>, <c>-A</c>, <c>-b</c>, <c>-u</c> and <c>--oauth2-bearer</c> apply.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Of(ITransferContext context) =>
        Build(context.Http?.BearerToken, context.Credentials, context.Http?.UserAgent, context.Http?.CookieStrings, context.Http?.Headers ?? []);

    /// <summary>Builds the fields from the option values.</summary>
    /// <param name="bearerToken">The <c>--oauth2-bearer</c> token, or <see langword="null"/>.</param>
    /// <param name="credentials">The <c>-u</c> user and password, or <see langword="null"/>.</param>
    /// <param name="userAgent">The <c>-A</c> value: <see langword="null"/> sends curl's own, empty sends none.</param>
    /// <param name="cookies">The <c>-b</c> cookie text, or <see langword="null"/>.</param>
    /// <param name="customHeaders">Each <c>-H</c> value: <c>Name: value</c> replaces a field of that name,
    /// <c>Name:</c> removes it, <c>Name;</c> sends it empty.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Build(
        string? bearerToken, NetworkCredential? credentials, string? userAgent, string? cookies, IReadOnlyList<string> customHeaders)
    {
        List<KeyValuePair<string, string>> fields = [];
        if (AuthorizationOf(bearerToken, credentials) is { } authorization)
        {
            fields.Add(new("Authorization", authorization));
        }

        string agent = userAgent ?? DefaultUserAgent;
        if (agent.Length > 0)
        {
            fields.Add(new("User-Agent", agent));
        }

        if (!string.IsNullOrEmpty(cookies))
        {
            fields.Add(new("Cookie", cookies));
        }

        foreach (string header in customHeaders)
        {
            ApplyCustomHeader(fields, FirstLine(header));
        }

        return fields;
    }

    private static string? AuthorizationOf(string? bearerToken, NetworkCredential? credentials)
    {
        if (!string.IsNullOrEmpty(bearerToken))
        {
            return $"Bearer {bearerToken}";
        }

        return credentials is null
            ? null
            : "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}"));
    }

    private static string FirstLine(string header)
    {
        int end = header.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? header : header[..end];
    }

    private static void ApplyCustomHeader(List<KeyValuePair<string, string>> fields, string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            string name = header[..colon];
            string value = header[(colon + 1)..].Trim(' ', '\t');
            fields.RemoveAll(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (value.Length > 0)
            {
                fields.Add(new(name, value));
            }

            return;
        }

        if (header.EndsWith(';') && header.Length > 1)
        {
            fields.Add(new(header[..^1], string.Empty));
        }
    }
}
