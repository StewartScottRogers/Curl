using System.Net;
using System.Text;

namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>
/// Builds the header fields every MCP Streamable HTTP request carries from curl's options, as curl sends them on HTTP
/// (ADR-1243): <c>Authorization</c> from <c>--oauth2-bearer</c> (Bearer, Model Context Protocol 2025-06-18 "Authorization")
/// or else <c>-u</c> (Basic), <c>User-Agent</c> from <c>-A</c> or curl's own, then each <c>-H</c> value.
/// </summary>
internal static class McpRequestHeaderFields
{
    /// <summary>The <c>User-Agent</c> curl sends when <c>-A</c> is not given.</summary>
    internal const string DefaultUserAgent = "curl/8.22.0";

    /// <summary>Builds the fields, each as <c>Name: value</c> without its line end.</summary>
    /// <param name="bearerToken">The <c>--oauth2-bearer</c> token, or <see langword="null"/>.</param>
    /// <param name="credentials">The <c>-u</c> user and password, or <see langword="null"/>.</param>
    /// <param name="userAgent">The <c>-A</c> value: <see langword="null"/> sends curl's own, empty sends none.</param>
    /// <param name="customHeaders">Each <c>-H</c> value verbatim: <c>Name: value</c> replaces a field of that name,
    /// <c>Name:</c> removes it, <c>Name;</c> sends it empty.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<string> Build(string? bearerToken, NetworkCredential? credentials, string? userAgent, IReadOnlyList<string> customHeaders)
    {
        List<KeyValuePair<string, string>> fields = OwnFields(bearerToken, credentials, userAgent);
        foreach (string header in customHeaders)
        {
            ApplyCustomHeader(fields, FirstLine(header));
        }

        return fields.ConvertAll(field => $"{field.Key}: {field.Value}".TrimEnd(' '));
    }

    private static List<KeyValuePair<string, string>> OwnFields(string? bearerToken, NetworkCredential? credentials, string? userAgent)
    {
        var fields = new List<KeyValuePair<string, string>>();
        if (AuthorizationOf(bearerToken, credentials) is { } authorization)
        {
            fields.Add(new("Authorization", authorization));
        }

        string agent = userAgent ?? DefaultUserAgent;
        if (agent.Length > 0)
        {
            fields.Add(new("User-Agent", agent));
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
