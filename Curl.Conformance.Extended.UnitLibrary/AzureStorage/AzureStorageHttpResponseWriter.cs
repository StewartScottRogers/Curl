using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>Writes an Azure Storage reference endpoint's response (RFC 9112), framed by length, by chunks or by the close.</summary>
public static class AzureStorageHttpResponseWriter
{
    /// <summary>Sends <paramref name="response" /> on <paramref name="connection" /> in one write.</summary>
    /// <param name="connection">The reference server's end of the connection.</param>
    /// <param name="response">The response.</param>
    /// <param name="framing">How the body is framed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends once the response is written.</returns>
    public static ValueTask WriteAsync(ReferenceConnection connection, AzureStorageHttpResponse response, AzureStorageResponseFraming framing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(response);
        StringBuilder head = new StringBuilder("HTTP/1.1 ").Append(response.StatusCode.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(response.ReasonPhrase).Append("\r\n");
        foreach ((string name, string value) in response.Headers)
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        head.Append(framing switch
        {
            AzureStorageResponseFraming.ContentLength => "Content-Length: " + response.Body.Length.ToString(CultureInfo.InvariantCulture),
            AzureStorageResponseFraming.Chunked => "Transfer-Encoding: chunked",
            _ => "Connection: close",
        }).Append("\r\n\r\n");
        byte[] body = framing == AzureStorageResponseFraming.Chunked ? Chunked(response.Body) : response.Body;
        byte[] bytes = [.. Encoding.Latin1.GetBytes(head.ToString()), .. body];
        return connection.WriteAsync(bytes, cancellationToken);
    }

    // The body as one chunk, when it has any bytes, then the last chunk.
    private static byte[] Chunked(byte[] body) =>
        body.Length == 0
            ? "0\r\n\r\n"u8.ToArray()
            : [.. Encoding.Latin1.GetBytes(body.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), .. body, .. "\r\n0\r\n\r\n"u8];
}
