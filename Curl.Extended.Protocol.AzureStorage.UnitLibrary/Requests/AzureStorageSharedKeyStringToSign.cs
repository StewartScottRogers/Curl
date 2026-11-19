using System.Globalization;
using System.Text;

namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>
/// Builds the string a Shared Key signature covers ("Authorize with Shared Key", the Blob service's string-to-sign for
/// version 2009-09-19 and later): the verb, the eleven standard headers, the canonicalized <c>x-ms-*</c> headers and
/// the canonicalized resource.
/// </summary>
public static class AzureStorageSharedKeyStringToSign
{
    private static readonly string[] StandardHeaders =
    [
        "Content-Encoding", "Content-Language", "Content-Length", "Content-MD5", "Content-Type", "Date",
        "If-Modified-Since", "If-Match", "If-None-Match", "If-Unmodified-Since", "Range",
    ];

    /// <summary>Builds the string-to-sign.</summary>
    /// <param name="method">The request's verb.</param>
    /// <param name="account">The storage account name.</param>
    /// <param name="path">The request path as sent, percent-encoded.</param>
    /// <param name="query">The request query as sent, without its <c>?</c>, or empty.</param>
    /// <param name="headers">Every header field the request sends that can take part: the standard headers and the <c>x-ms-*</c> ones.</param>
    /// <param name="bodyLength">The request body's length; a zero length signs as an empty <c>Content-Length</c> (version 2015-02-21 and later).</param>
    /// <returns>The string to sign.</returns>
    public static string Build(string method, string account, string path, string query, IReadOnlyList<KeyValuePair<string, string>> headers, long bodyLength)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(headers);
        var text = new StringBuilder(method.ToUpperInvariant()).Append('\n');
        foreach (var name in StandardHeaders)
        {
            text.Append(StandardHeaderValue(name, headers, bodyLength)).Append('\n');
        }

        foreach (var header in headers.Where(IsMicrosoftHeader).OrderBy(header => header.Key.ToLowerInvariant(), StringComparer.Ordinal))
        {
            text.Append(header.Key.ToLowerInvariant()).Append(':').Append(header.Value.Trim()).Append('\n');
        }

        text.Append('/').Append(account).Append(path);
        foreach (var parameter in CanonicalQuery(query))
        {
            text.Append('\n').Append(parameter);
        }

        return text.ToString();
    }

    private static string StandardHeaderValue(string name, IReadOnlyList<KeyValuePair<string, string>> headers, long bodyLength) =>
        name != "Content-Length"
            ? headers.FirstOrDefault(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value
            : bodyLength > 0 ? bodyLength.ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static bool IsMicrosoftHeader(KeyValuePair<string, string> header) => header.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> CanonicalQuery(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.IndexOf('=', StringComparison.Ordinal) is var at && at < 0 ? (Name: pair, Value: string.Empty) : (Name: pair[..at], Value: pair[(at + 1)..]))
            .GroupBy(pair => Uri.UnescapeDataString(pair.Name).ToLowerInvariant(), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + ":" + string.Join(',', group.Select(pair => Uri.UnescapeDataString(pair.Value)).Order(StringComparer.Ordinal)));
}
