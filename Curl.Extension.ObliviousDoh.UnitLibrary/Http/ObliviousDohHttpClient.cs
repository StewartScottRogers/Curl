using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ObliviousDoh.Http;

/// <summary>
/// Sends one HTTP/1.1 request on its own connection, with <c>Connection: close</c>, and reads the response to the
/// close: a POST of an <c>ObliviousDoHMessage</c> to the proxy (RFC 9230 section 4.1) or a GET of the target's configs.
/// </summary>
/// <param name="connector">Opens the connection, with TLS for an <c>https</c> URL.</param>
public sealed class ObliviousDohHttpClient(IConnector connector)
{
    /// <summary>The media type of an <c>ObliviousDoHMessage</c> (RFC 9230 section 4.1).</summary>
    public const string MessageMediaType = "application/oblivious-dns-message";

    /// <summary>The most response bytes read before the response is given up as too large.</summary>
    internal const int MaximumResponseLength = 1 << 20;

    /// <summary>Sends a request and reads its response.</summary>
    /// <param name="uri">The request's URL.</param>
    /// <param name="body">The POST body, an <c>ObliviousDoHMessage</c>; <see langword="null" /> for a GET.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, or <see langword="null" /> when the connection fails or the response is malformed or too large.</returns>
    public async ValueTask<ObliviousDohHttpResponse?> SendAsync(Uri uri, byte[]? body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ConnectResult connected = await connector.ConnectAsync(new ConnectTarget(uri.IdnHost, uri.Port, uri.Scheme == Uri.UriSchemeHttps), cancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return null;
        }

        await using (connection.ConfigureAwait(false))
        {
            try
            {
                await connection.WriteAsync((byte[])[.. RequestHead(uri, body), .. body ?? []], cancellationToken).ConfigureAwait(false);
                await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
                byte[]? response = await ReadToCloseAsync(connection, cancellationToken).ConfigureAwait(false);
                return response is null ? null : ObliviousDohHttpResponseParser.Parse(response);
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>Writes the request line and header fields.</summary>
    /// <param name="uri">The request's URL.</param>
    /// <param name="body">The POST body; <see langword="null" /> for a GET.</param>
    /// <returns>The head's bytes, up to and including the empty line.</returns>
    internal static byte[] RequestHead(Uri uri, byte[]? body)
    {
        var head = new StringBuilder();
        head.Append(CultureInfo.InvariantCulture, $"{(body is null ? "GET" : "POST")} {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Authority}\r\n");
        head.Append(body is null
            ? "Accept: */*\r\n"
            : string.Create(CultureInfo.InvariantCulture, $"Accept: {MessageMediaType}\r\nContent-Type: {MessageMediaType}\r\nContent-Length: {body.Length}\r\n"));
        head.Append("Connection: close\r\n\r\n");
        return Encoding.ASCII.GetBytes(head.ToString());
    }

    private static async ValueTask<byte[]?> ReadToCloseAsync(IConnection connection, CancellationToken cancellationToken)
    {
        using var response = new MemoryStream();
        byte[] buffer = new byte[16384];
        int read;
        while ((read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            response.Write(buffer, 0, read);
            if (response.Length > MaximumResponseLength)
            {
                return null;
            }
        }

        return response.ToArray();
    }
}
