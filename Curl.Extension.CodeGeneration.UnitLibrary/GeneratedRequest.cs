using System.Text;

namespace Curl.Extension.CodeGeneration;

/// <summary>
/// One HTTP request as the generated code sends it (ADR-1128): the method and URL curl sends, the request fields the
/// command line gave (with <c>-A</c>, <c>-e</c>, the body's <c>Content-Type</c> and the credentials' <c>Authorization</c>
/// among them), the body, and whether redirects are followed and the body decompressed.
/// </summary>
/// <param name="Method">The request method.</param>
/// <param name="Url">The URL as sent: scheme, host, port when not the default, path and query, never the fragment.</param>
/// <param name="Headers">The request fields, in command-line order.</param>
/// <param name="Body">The request body, or <see langword="null" /> for none.</param>
/// <param name="FollowRedirects">Whether <c>-L</c> was given.</param>
/// <param name="MaxRedirects">The <c>--max-redirs</c> limit.</param>
/// <param name="Compressed">Whether <c>--compressed</c> was given.</param>
internal sealed record GeneratedRequest(
    string Method,
    string Url,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    byte[]? Body,
    bool FollowRedirects,
    int MaxRedirects,
    bool Compressed)
{
    /// <summary>The fields HttpClient carries on <c>HttpContent.Headers</c> rather than on the request (HttpContentHeaders).</summary>
    internal static readonly IReadOnlySet<string> ContentFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Allow", "Content-Disposition", "Content-Encoding", "Content-Language", "Content-Length",
        "Content-Location", "Content-MD5", "Content-Range", "Content-Type", "Expires", "Last-Modified",
    };

    /// <summary>Reads the request a transfer sends.</summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The request.</returns>
    internal static GeneratedRequest From(ITransferContext context)
    {
        var http = context.Http ?? new HttpRequestOptions();
        var body = http.Body is BytesBody bytes ? bytes.Content.ToArray() : context.PostData?.ToArray();
        return new GeneratedRequest(
            http.CustomMethod ?? DefaultMethod(context, http),
            UrlOf(context.Url),
            HeadersOf(context, http),
            body,
            http.FollowRedirects,
            http.MaxRedirects,
            http.Compressed);
    }

    /// <summary>Gives the fields that go on the request itself.</summary>
    internal IEnumerable<KeyValuePair<string, string>> RequestHeaders => Headers.Where(header => !ContentFields.Contains(header.Key));

    /// <summary>Gives the fields that go on the content.</summary>
    internal IEnumerable<KeyValuePair<string, string>> ContentHeaders => Headers.Where(header => ContentFields.Contains(header.Key));

    /// <summary>Gives the body as text when it is UTF-8, else <see langword="null" />.</summary>
    internal string? BodyText
    {
        get
        {
            try
            {
                return Body is null ? null : new UTF8Encoding(false, true).GetString(Body);
            }
            catch (DecoderFallbackException)
            {
                return null;
            }
        }
    }

    private static string DefaultMethod(ITransferContext context, HttpRequestOptions http)
    {
        if (http.Body is not null || context.PostData is not null || context.PostWithoutData)
        {
            return "POST";
        }

        return context.Upload is not null ? "PUT" : context.NoBody ? "HEAD" : "GET";
    }

    private static string UrlOf(CurlUrl url)
    {
        var host = url.IdnHost.Contains(':', StringComparison.Ordinal) && !url.IdnHost.StartsWith('[') ? "[" + url.IdnHost + "]" : url.IdnHost;
        var port = url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var query = url.Query is null ? string.Empty : "?" + url.Query;
        return url.Scheme + "://" + host + port + url.RequestPath + query;
    }

    private static List<KeyValuePair<string, string>> HeadersOf(ITransferContext context, HttpRequestOptions http)
    {
        List<KeyValuePair<string, string>> headers = [];
        AddUnlessGiven(headers, http.Headers, "User-Agent", http.UserAgent);
        AddUnlessGiven(headers, http.Headers, "Referer", http.Referer);
        AddUnlessGiven(headers, http.Headers, "Authorization", AuthorizationOf(context, http));
        AddUnlessGiven(headers, http.Headers, "Content-Type", http.Body?.ContentType ?? ContentTypeOfPostData(context));
        foreach (var line in http.Headers)
        {
            AddGiven(headers, line);
        }

        return headers;
    }

    private static string? ContentTypeOfPostData(ITransferContext context) =>
        context.PostData is null ? null : "application/x-www-form-urlencoded";

    private static string? AuthorizationOf(ITransferContext context, HttpRequestOptions http)
    {
        if (http.BearerToken is not null)
        {
            return "Bearer " + http.BearerToken;
        }

        var credentials = context.Credentials;
        return credentials is null ? null : "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.UserName + ":" + credentials.Password));
    }

    private static void AddUnlessGiven(List<KeyValuePair<string, string>> headers, IReadOnlyList<string> given, string name, string? value)
    {
        if (value is not null && !given.Any(line => NameOf(line).Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(new(name, value));
        }
    }

    /// <summary>
    /// Adds one <c>-H</c> line as curl sends it: <c>Name: value</c> sends the field, <c>Name;</c> sends it empty, and
    /// <c>Name:</c> only removes a field curl would add, which the generated code never adds, so it sends nothing.
    /// </summary>
    private static void AddGiven(List<KeyValuePair<string, string>> headers, string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            var value = line[(colon + 1)..].Trim();
            if (value.Length > 0)
            {
                headers.Add(new(line[..colon].Trim(), value));
            }

            return;
        }

        if (line.EndsWith(';'))
        {
            headers.Add(new(line[..^1].Trim(), string.Empty));
        }
    }

    private static string NameOf(string line)
    {
        var end = line.IndexOfAny([':', ';']);
        return end < 0 ? line.Trim() : line[..end].Trim();
    }
}
