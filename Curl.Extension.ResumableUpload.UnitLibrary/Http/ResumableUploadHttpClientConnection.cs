using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Http;

/// <summary>
/// Sends HTTP/1.1 requests to a resumable upload server over one <see cref="IConnection"/>, one at a time, and reads each response's head,
/// exposing its body as a stream (RFC 9112).
/// </summary>
/// <remarks>
/// A response's body must be read to its end before the next request is sent, and only when the response says
/// <see cref="ResumableUploadHttpResponse.ConnectionPersists"/>; the caller then marks the connection reusable. <c>Expect: 100-continue</c>
/// is not sent; interim 1xx responses other than 101 (such as <c>104 Upload Resumption Supported</c>) are read and kept in
/// <see cref="ResumableUploadHttpResponse.InterimResponses"/> until the final response arrives. Every failure of the exchange is a
/// <see cref="ResumableUploadHttpException"/>.
/// </remarks>
/// <param name="connection">The connection; the caller disposes it.</param>
public sealed class ResumableUploadHttpClientConnection(IConnection connection)
{
    private const int BodyBufferSize = 65536;

    private readonly ResumableUploadHttpConnectionReader reader = new(connection);

    /// <summary>Sends <paramref name="request"/> and reads the response's status line and header fields.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, its body not yet read.</returns>
    public async Task<ResumableUploadHttpResponse> SendAsync(ResumableUploadHttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await connection.WriteAsync(Encoding.Latin1.GetBytes(FormatHead(request)), cancellationToken);
        if (request.Body is not null)
        {
            if (request.BodyOffset != 0)
            {
                request.Body.Position = request.BodyOffset;
            }

            await WriteBodyAsync(request.Body, request.BodyLength, cancellationToken);
        }

        await connection.FlushAsync(cancellationToken);
        return await ReadResponseAsync(request.Method, cancellationToken);
    }

    private static string FormatHead(ResumableUploadHttpRequest request)
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

    private static ResumableUploadHttpException ClosedEarly() =>
        new(ResumableUploadHttpFailure.ClosedEarly, "The connection closed before the response head ended.");

    private async Task WriteBodyAsync(Stream body, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[(int)Math.Min(BodyBufferSize, Math.Max(length, 1))];
        for (var remaining = length; remaining > 0;)
        {
            var read = await body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                throw new ResumableUploadHttpException(ResumableUploadHttpFailure.RequestBodyTooShort, $"The request body ended {remaining} bytes before its length of {length}.");
            }

            await connection.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    private async Task<ResumableUploadHttpResponse> ReadResponseAsync(string method, CancellationToken cancellationToken)
    {
        var interimResponses = new List<ResumableUploadHttpInterimResponse>();
        var (statusCode, reasonPhrase, isHttp11, headers) = await ReadHeadAsync(cancellationToken);
        while (IsInterim(statusCode))
        {
            interimResponses.Add(new ResumableUploadHttpInterimResponse(statusCode, headers));
            (statusCode, reasonPhrase, isHttp11, headers) = await ReadHeadAsync(cancellationToken);
        }

        var bodyLength = ResumableUploadHttpResponseHead.FindBodyLength(headers, CannotHaveBody(method, statusCode));
        return new ResumableUploadHttpResponse(
            statusCode, reasonPhrase, headers, CreateBody(bodyLength), ResumableUploadHttpResponseHead.ConnectionPersists(headers, isHttp11, bodyLength), interimResponses);
    }

    private static bool IsInterim(int statusCode) => statusCode is >= 100 and < 200 and not 101;

    private async Task<(int StatusCode, string ReasonPhrase, bool IsHttp11, List<KeyValuePair<string, string>> Headers)> ReadHeadAsync(CancellationToken cancellationToken)
    {
        var (statusCode, reasonPhrase, isHttp11) = ResumableUploadHttpResponseHead.ParseStatusLine(await reader.ReadLineAsync(cancellationToken) ?? throw ClosedEarly());
        var headers = new List<KeyValuePair<string, string>>();
        for (var line = await ReadHeadLineAsync(cancellationToken); line.Length > 0; line = await ReadHeadLineAsync(cancellationToken))
        {
            headers.Add(ResumableUploadHttpResponseHead.ParseField(line));
        }

        return (statusCode, reasonPhrase, isHttp11, headers);
    }

    private static bool CannotHaveBody(string method, int statusCode) =>
        method == "HEAD" || statusCode < 200 || statusCode == 204 || statusCode == 304;

    private ResumableUploadHttpBodyStream CreateBody(long? bodyLength) => bodyLength switch
    {
        null => new ResumableUploadChunkedBodyStream(reader),
        -1 => new ResumableUploadCloseDelimitedBodyStream(reader),
        _ => new ResumableUploadContentLengthBodyStream(reader, bodyLength.Value),
    };

    private async ValueTask<string> ReadHeadLineAsync(CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken) ?? throw ClosedEarly();
        return line.Length > 0 && (line[0] == ' ' || line[0] == '\t')
            ? throw new ResumableUploadHttpException(ResumableUploadHttpFailure.MalformedHeaderField, "The response folds a header field line (obs-fold).")
            : line;
    }
}
