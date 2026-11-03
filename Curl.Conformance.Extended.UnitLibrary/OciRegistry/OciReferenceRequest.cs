using System.Globalization;

namespace Curl.Conformance.Extended.OciRegistry;

/// <summary>One HTTP/1.1 request curl sent the OCI reference registry.</summary>
/// <param name="Method">The request method, such as <c>GET</c> or <c>PUT</c>.</param>
/// <param name="Target">The request target exactly as sent, still percent-encoded.</param>
/// <param name="Headers">The request headers, names compared ignoring case.</param>
/// <param name="Body">The body, framed by <c>Content-Length</c>; empty without one.</param>
public sealed record OciReferenceRequest(string Method, string Target, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    /// <summary>Gets the target's path, without its query.</summary>
    public string Path => Target.Split('?', 2)[0];

    /// <summary>Gets the <c>Content-Length</c> header's value, or 0 when the request has none.</summary>
    public int ContentLength =>
        Headers.TryGetValue("Content-Length", out string? length) ? int.Parse(length, NumberStyles.None, CultureInfo.InvariantCulture) : 0;

    /// <summary>Gives a query parameter's percent-decoded value.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <returns>The value, or <see langword="null" /> when the target has no such parameter.</returns>
    public string? QueryValue(string name) =>
        Target.Split('?', 2).Skip(1).SelectMany(query => query.Split('&')).Select(parameter => parameter.Split('=', 2))
            .Where(nameAndValue => nameAndValue[0] == name).Select(nameAndValue => Uri.UnescapeDataString(nameAndValue.ElementAtOrDefault(1) ?? string.Empty))
            .LastOrDefault();

    /// <summary>Reads a request head: the request line and the header lines, without the blank line that ends it.</summary>
    /// <param name="head">The head's text.</param>
    /// <returns>The request, its body empty.</returns>
    public static OciReferenceRequest ParseHead(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        string[] lines = head.Split("\r\n");
        string[] requestLine = lines[0].Split(' ');
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string[] nameAndValue in lines.Skip(1).Select(line => line.Split(':', 2)))
        {
            headers[nameAndValue[0].Trim()] = nameAndValue[1].Trim();
        }

        return new OciReferenceRequest(requestLine[0], requestLine[1], headers, []);
    }
}
