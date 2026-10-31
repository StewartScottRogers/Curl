using System.Text;

namespace Curl.Extension.Protocol.SignalR.Transport;

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

    /// <summary>The <c>Authorization: Bearer</c> field for an access token, or none without one.</summary>
    public static IEnumerable<KeyValuePair<string, string>> BearerAuthorization(string? accessToken) =>
        accessToken is null ? [] : [new("Authorization", "Bearer " + accessToken)];
}
