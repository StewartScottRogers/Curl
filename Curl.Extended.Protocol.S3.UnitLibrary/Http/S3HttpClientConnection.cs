using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Http;

/// <summary>
/// Sends HTTP/1.1 requests to an S3 endpoint over one <see cref="IConnection"/>, one at a time, and reads each response's head,
/// exposing its body as a stream (RFC 9112).
/// </summary>
/// <remarks>
/// A response's body must be read to its end before the next request is sent, and only when the response says
/// <see cref="S3HttpResponse.ConnectionPersists"/>; the caller then marks the connection reusable. <c>Expect: 100-continue</c>
/// is not sent, so no interim 1xx response is awaited. Every failure of the exchange is an <see cref="S3HttpException"/>.
/// </remarks>
/// <param name="connection">The connection; the caller disposes it.</param>
public sealed class S3HttpClientConnection(IConnection connection)
{
    private const int BodyBufferSize = 65536;

    private readonly S3HttpConnectionReader reader = new(connection);

    /// <summary>Sends <paramref name="request"/> and reads the response's status line and header fields.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, its body not yet read.</returns>
    public async Task<S3HttpResponse> SendAsync(S3HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await connection.WriteAsync(Encoding.Latin1.GetBytes(FormatHead(request)), cancellationToken);
        if (request.Body is not null)
        {
            await WriteBodyAsync(request.Body, request.BodyLength, cancellationToken);
        }

        await connection.FlushAsync(cancellationToken);
        return await ReadResponseAsync(request.Method, cancellationToken);
    }

    private static string FormatHead(S3HttpRequest request)
    {
        var head = new StringBuilder().Append(request.Method).Append(' ').Append(request.Target).Append(" HTTP/1.1\r\n");
        foreach (var (name, value) in request.Headers)
        {
            if (name.AsSpan().IndexOfAny('\r', '\n') >= 0 || value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                throw new ArgumentException($"The header field \"{name}\" holds a CR or LF.", nameof(request));
            }

            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        if (request.Body is not null)
        {
            head.Append("Content-Length: ").Append(request.BodyLength).Append("\r\n");
        }

        return head.Append("\r\n").ToString();
    }

    private static S3HttpException ClosedEarly() =>
        new(S3HttpFailure.ClosedEarly, "The connection closed before the response head ended.");

    private async Task WriteBodyAsync(Stream body, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[(int)Math.Min(BodyBufferSize, Math.Max(length, 1))];
        for (var remaining = length; remaining > 0;)
        {
            var read = await body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                throw new S3HttpException(S3HttpFailure.RequestBodyTooShort, $"The request body ended {remaining} bytes before its length of {length}.");
            }

            await connection.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    private async Task<S3HttpResponse> ReadResponseAsync(string method, CancellationToken cancellationToken)
    {
        var (statusCode, reasonPhrase, isHttp11) = S3HttpResponseHead.ParseStatusLine(await reader.ReadLineAsync(cancellationToken) ?? throw ClosedEarly());
        var headers = new List<KeyValuePair<string, string>>();
        for (var line = await ReadHeadLineAsync(cancellationToken); line.Length > 0; line = await ReadHeadLineAsync(cancellationToken))
        {
            headers.Add(S3HttpResponseHead.ParseField(line));
        }

        var bodyLength = S3HttpResponseHead.FindBodyLength(headers, CannotHaveBody(method, statusCode));
        return new S3HttpResponse(statusCode, reasonPhrase, headers, CreateBody(bodyLength), S3HttpResponseHead.ConnectionPersists(headers, isHttp11, bodyLength));
    }

    private static bool CannotHaveBody(string method, int statusCode) =>
        method == "HEAD" || statusCode < 200 || statusCode == 204 || statusCode == 304;

    private S3HttpBodyStream CreateBody(long? bodyLength) => bodyLength switch
    {
        null => new S3ChunkedBodyStream(reader),
        -1 => new S3CloseDelimitedBodyStream(reader),
        _ => new S3ContentLengthBodyStream(reader, bodyLength.Value),
    };

    private async ValueTask<string> ReadHeadLineAsync(CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken) ?? throw ClosedEarly();
        return line.Length > 0 && (line[0] == ' ' || line[0] == '\t')
            ? throw new S3HttpException(S3HttpFailure.MalformedHeaderField, "The response folds a header field line (obs-fold).")
            : line;
    }
}
