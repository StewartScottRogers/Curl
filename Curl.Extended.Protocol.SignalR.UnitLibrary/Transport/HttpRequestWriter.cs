using System.Text;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>Writes the bytes of an HTTP/1.1 request with no body (RFC 9112 section 3).</summary>
internal static class HttpRequestWriter
{
    /// <summary>
    /// The request line in origin form, <c>Host</c> from the URL's authority, then <paramref name="headers"/> in order and
    /// the empty line.
    /// </summary>
    public static byte[] Write(string method, Uri url, IEnumerable<KeyValuePair<string, string>> headers)
    {
        StringBuilder request = new();
        request.Append(method).Append(' ').Append(url.PathAndQuery).Append(" HTTP/1.1\r\n");
        request.Append("Host: ").Append(url.Authority).Append("\r\n");
        foreach (KeyValuePair<string, string> header in headers)
        {
            request.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
        }

        return Encoding.Latin1.GetBytes(request.Append("\r\n").ToString());
    }

    /// <summary>
    /// <paramref name="ownFields"/>, then <paramref name="requestFields"/> (curl's <c>-H</c>, <c>-A</c>, <c>-b</c>, <c>-u</c>
    /// and <c>--oauth2-bearer</c>) and the bearer field of <paramref name="accessToken"/>; an access token replaces any
    /// <c>Authorization</c> in <paramref name="requestFields"/>.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> Combine(
        IEnumerable<KeyValuePair<string, string>> ownFields,
        IReadOnlyList<KeyValuePair<string, string>>? requestFields,
        string? accessToken) =>
    [
        .. ownFields,
        .. (requestFields ?? []).Where(field => accessToken is null || !field.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)),
        .. BearerAuthorization(accessToken),
    ];

    /// <summary>The <c>Authorization: Bearer</c> field for an access token, or none without one.</summary>
    public static IEnumerable<KeyValuePair<string, string>> BearerAuthorization(string? accessToken) =>
        accessToken is null ? [] : [new("Authorization", "Bearer " + accessToken)];
}
