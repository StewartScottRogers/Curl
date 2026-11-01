using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>Writes the HTTP/1.1 bytes of an <see cref="OciHttpRequest"/> (RFC 9112 §3).</summary>
internal static class OciHttpRequestWriter
{
    /// <summary>Writes the request line, the fields and the body.</summary>
    /// <param name="request">The request.</param>
    /// <returns>
    /// The bytes: the method and the URL's path and query, <c>Host</c>, the request's own fields, <c>Content-Length</c> when it
    /// has a body, and <c>Connection: close</c>, then the body.
    /// </returns>
    public static byte[] Write(OciHttpRequest request)
    {
        var head = new StringBuilder()
            .Append(RequireToken(request.Method)).Append(' ').Append(request.Url.PathAndQuery).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(HostField(request.Url)).Append("\r\n");
        foreach (var (name, value) in request.Headers)
        {
            head.Append(RequireToken(name)).Append(": ").Append(RequireNoLineBreak(name, value)).Append("\r\n");
        }

        if (request.Body is not null)
        {
            head.Append("Content-Length: ").Append(request.Body.Length).Append("\r\n");
        }

        head.Append("Connection: close\r\n\r\n");
        return [.. Encoding.Latin1.GetBytes(head.ToString()), .. request.Body ?? []];
    }

    private static string HostField(Uri url)
    {
        var host = url.HostNameType == UriHostNameType.IPv6 ? $"[{url.IdnHost}]" : url.IdnHost;
        return url.IsDefaultPort ? host : $"{host}:{url.Port}";
    }

    // RFC 9110 §5.6.2: a method and a field name are tokens, so neither may be empty or hold a space, a colon or a line break.
    private static string RequireToken(string value) =>
        value.Length > 0 && value.AsSpan().IndexOfAny(" \t\r\n:") < 0
            ? value
            : throw InvalidField($"\"{value}\" is not an HTTP token.");

    private static string RequireNoLineBreak(string name, string value) =>
        value.AsSpan().IndexOfAny('\r', '\n') < 0 ? value : throw InvalidField($"The {name} value holds a CR or LF.");

    private static OciHttpException InvalidField(string message) =>
        new(OciHttpFailure.InvalidRequestField, CurlExitCode.BadFunctionArgument, message);
}
