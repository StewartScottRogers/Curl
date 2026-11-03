using System.Globalization;

namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>One HTTP/1.1 request curl sent the Cloud Storage XML API reference endpoint, its target split into path and query.</summary>
/// <param name="Method">The request method, such as <c>GET</c> or <c>PUT</c>.</param>
/// <param name="Target">The request target exactly as sent, still percent-encoded.</param>
/// <param name="Path">The target's path, percent-decoded: <c>/bucket/object</c>.</param>
/// <param name="Query">The target's query parameters, percent-decoded; a name sent twice keeps its last value.</param>
/// <param name="Headers">The request headers, names compared ignoring case.</param>
/// <param name="Body">The body, framed by <c>Content-Length</c>; empty without one.</param>
public sealed record GcsReferenceRequest(
    string Method,
    string Target,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body)
{
    /// <summary>Gets the <c>Content-Length</c> header's value, or <see langword="null" /> when the request has none.</summary>
    public int? ContentLength =>
        Headers.TryGetValue("Content-Length", out string? length) ? int.Parse(length, NumberStyles.None, CultureInfo.InvariantCulture) : null;

    /// <summary>Reads a request head: the request line and the header lines, without the blank line that ends it.</summary>
    /// <param name="head">The head's text.</param>
    /// <returns>The request, its body empty.</returns>
    public static GcsReferenceRequest ParseHead(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        string[] lines = head.Split("\r\n");
        string[] requestLine = lines[0].Split(' ');
        string target = requestLine[1];
        string[] pathAndQuery = target.Split('?', 2);
        return new GcsReferenceRequest(requestLine[0], target, Uri.UnescapeDataString(pathAndQuery[0]), QueryOf(pathAndQuery.Skip(1)), HeadersOf(lines.Skip(1)), []);
    }

    private static Dictionary<string, string> QueryOf(IEnumerable<string> queryText)
    {
        Dictionary<string, string> query = new(StringComparer.Ordinal);
        foreach (string[] nameAndValue in queryText.SelectMany(text => text.Split('&')).Select(parameter => parameter.Split('=', 2)))
        {
            query[Uri.UnescapeDataString(nameAndValue[0])] = Uri.UnescapeDataString(nameAndValue.ElementAtOrDefault(1) ?? string.Empty);
        }

        return query;
    }

    private static Dictionary<string, string> HeadersOf(IEnumerable<string> headerLines)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string[] nameAndValue in headerLines.Select(line => line.Split(':', 2)))
        {
            headers[nameAndValue[0].Trim()] = nameAndValue[1].Trim();
        }

        return headers;
    }
}
