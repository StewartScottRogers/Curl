using System.Text;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Http;

/// <summary>Writes the HTTP/1.1 bytes of a form POST to a token or device authorization endpoint (RFC 9112 §3, RFC 6749 §4.4.2).</summary>
internal static class OAuth2HttpRequestWriter
{
    /// <summary>Writes the request line, the fields and the body.</summary>
    /// <param name="endpoint">The endpoint, an absolute <c>http</c> or <c>https</c> URL.</param>
    /// <param name="request">The form body and the <c>Authorization</c> value.</param>
    /// <returns>
    /// The bytes: <c>POST</c> to the endpoint's path and query, <c>Host</c>, <c>Accept: application/json</c>, the form
    /// <c>Content-Type</c>, any <c>Authorization</c>, <c>Content-Length</c> and <c>Connection: close</c>, then the body.
    /// </returns>
    public static byte[] Write(Uri endpoint, OAuth2FormRequest request)
    {
        var body = Encoding.UTF8.GetBytes(request.Body);
        var head = new StringBuilder()
            .Append("POST ").Append(endpoint.PathAndQuery).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(HostField(endpoint)).Append("\r\n")
            .Append("Accept: application/json\r\n")
            .Append("Content-Type: ").Append(OAuth2FormRequest.ContentType).Append("\r\n");
        if (request.Authorization is not null)
        {
            head.Append("Authorization: ").Append(RequireNoLineBreak(request.Authorization)).Append("\r\n");
        }

        head.Append("Content-Length: ").Append(body.Length).Append("\r\n")
            .Append("Connection: close\r\n\r\n");
        return [.. Encoding.Latin1.GetBytes(head.ToString()), .. body];
    }

    private static string HostField(Uri endpoint)
    {
        var host = endpoint.HostNameType == UriHostNameType.IPv6 ? $"[{endpoint.IdnHost}]" : endpoint.IdnHost;
        return endpoint.IsDefaultPort ? host : $"{host}:{endpoint.Port}";
    }

    private static string RequireNoLineBreak(string value) =>
        value.AsSpan().IndexOfAny('\r', '\n') < 0
            ? value
            : throw new OAuth2HttpException(
                OAuth2HttpFailure.InvalidRequestField, CurlExitCode.BadFunctionArgument, "The Authorization value holds a CR or LF.");
}
