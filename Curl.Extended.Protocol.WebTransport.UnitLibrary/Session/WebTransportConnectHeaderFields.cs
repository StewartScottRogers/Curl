using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport.Session;

/// <summary>
/// Builds the header fields curl's options add to the extended CONNECT, as curl sends them on HTTP:
/// <c>authorization</c> from <c>--oauth2-bearer</c> (Bearer) or else <c>-u</c> (Basic), <c>user-agent</c> from
/// <c>-A</c> or curl's own, <c>cookie</c> from <c>-b</c>, then each <c>-H</c> value (ADR-1243 sets the same order
/// for the other upgrade-based schemes). Names are lower case as HTTP/3 requires (RFC 9114 section 4.2); a pseudo-header,
/// <c>host</c> and the connection-specific fields are never added.
/// </summary>
internal static class WebTransportConnectHeaderFields
{
    /// <summary>The <c>user-agent</c> curl sends when <c>-A</c> is not given.</summary>
    internal const string DefaultUserAgent = "curl/8.22.0";

    private static readonly HashSet<string> NeverSent = new(StringComparer.Ordinal)
    {
        "host", "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade", "te",
    };

    /// <summary>Builds the fields of <paramref name="context"/>'s options.</summary>
    /// <param name="context">The transfer whose <c>-H</c>, <c>-A</c>, <c>-b</c>, <c>-u</c> and <c>--oauth2-bearer</c> apply.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<HeaderField> Of(ITransferContext context) =>
        Build(context.Http?.BearerToken, context.Credentials, context.Http?.UserAgent, context.Http?.CookieStrings, context.Http?.Headers ?? []);

    /// <summary>Builds the fields from the option values.</summary>
    /// <param name="bearerToken">The <c>--oauth2-bearer</c> token, or <see langword="null"/>.</param>
    /// <param name="credentials">The <c>-u</c> user and password, or <see langword="null"/>.</param>
    /// <param name="userAgent">The <c>-A</c> value: <see langword="null"/> sends curl's own, empty sends none.</param>
    /// <param name="cookies">The <c>-b</c> cookie text, or <see langword="null"/>.</param>
    /// <param name="customHeaders">Each <c>-H</c> value: <c>Name: value</c> replaces the field curl adds of that name,
    /// <c>Name:</c> removes it, <c>Name;</c> replaces it with an empty one; a repeated name is sent as often as given.</param>
    /// <returns>The fields in the order they are sent.</returns>
    public static IReadOnlyList<HeaderField> Build(
        string? bearerToken, NetworkCredential? credentials, string? userAgent, string? cookies, IReadOnlyList<string> customHeaders)
    {
        List<HeaderField> fields = CurlAddedFields(bearerToken, credentials, userAgent, cookies);
        HashSet<string> ownFields = [.. fields.Select(field => field.Name)];
        foreach (string header in customHeaders)
        {
            ApplyCustomHeader(fields, ownFields, FirstLine(header));
        }

        return fields;
    }

    private static List<HeaderField> CurlAddedFields(string? bearerToken, NetworkCredential? credentials, string? userAgent, string? cookies)
    {
        List<HeaderField> fields = [];
        if (AuthorizationOf(bearerToken, credentials) is { } authorization)
        {
            fields.Add(new("authorization", authorization));
        }

        string agent = userAgent ?? DefaultUserAgent;
        if (agent.Length > 0)
        {
            fields.Add(new("user-agent", agent));
        }

        if (!string.IsNullOrEmpty(cookies))
        {
            fields.Add(new("cookie", cookies));
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

    private static void ApplyCustomHeader(List<HeaderField> fields, HashSet<string> ownFields, string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            ReplaceOrRemove(fields, ownFields, header[..colon].ToLowerInvariant(), header[(colon + 1)..].Trim(' ', '\t'));
        }
        else if (header.EndsWith(';') && header.Length > 1)
        {
            string name = header[..^1].ToLowerInvariant();
            DropOwnField(fields, ownFields, name);
            AddIfSendable(fields, new(name, string.Empty));
        }
    }

    private static void ReplaceOrRemove(List<HeaderField> fields, HashSet<string> ownFields, string name, string value)
    {
        DropOwnField(fields, ownFields, name);
        if (value.Length > 0)
        {
            AddIfSendable(fields, new(name, value));
        }
    }

    private static void DropOwnField(List<HeaderField> fields, HashSet<string> ownFields, string name)
    {
        if (ownFields.Remove(name))
        {
            fields.RemoveAll(field => field.Name == name);
        }
    }

    private static void AddIfSendable(List<HeaderField> fields, HeaderField field)
    {
        if (!field.Name.StartsWith(':') && !NeverSent.Contains(field.Name))
        {
            fields.Add(field);
        }
    }
}
