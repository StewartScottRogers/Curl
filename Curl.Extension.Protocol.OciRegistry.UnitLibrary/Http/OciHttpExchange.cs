using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.OciRegistry.Http;

/// <summary>Sends one request over an <see cref="IConnection"/> and reads its final response whole (RFC 9112).</summary>
internal static class OciHttpExchange
{
    /// <summary>Writes <paramref name="request"/>, skips any interim 1xx responses (RFC 9110 §15.2), and reads the final one.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, whatever its status; a response to <c>HEAD</c> has no body (RFC 9110 §9.3.2).</returns>
    public static async Task<OciHttpResponse> ExchangeAsync(IConnection connection, OciHttpRequest request, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(OciHttpRequestWriter.Write(request), cancellationToken);
        await connection.FlushAsync(cancellationToken);
        var reader = new OciHttpConnectionReader(connection);
        var statusLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new OciHttpException(OciHttpFailure.NoResponse, CurlExitCode.GotNothing, "Empty reply from server");
        var (statusCode, reasonPhrase, headers) = await ReadHeadAsync(reader, statusLine, cancellationToken);
        while (statusCode < 200)
        {
            (statusCode, reasonPhrase, headers) = await ReadHeadAsync(reader, await ReadRequiredLineAsync(reader, cancellationToken), cancellationToken);
        }

        var bodyLength = request.Method == "HEAD" ? 0 : OciHttpResponseHead.FindBodyLength(headers, statusCode);
        var body = await OciHttpBodyReader.ReadAsync(reader, bodyLength, cancellationToken);
        return new OciHttpResponse(statusCode, reasonPhrase, headers, body);
    }

    private static async ValueTask<(int StatusCode, string ReasonPhrase, List<KeyValuePair<string, string>> Headers)> ReadHeadAsync(
        OciHttpConnectionReader reader, string statusLine, CancellationToken cancellationToken)
    {
        var (statusCode, reasonPhrase) = OciHttpResponseHead.ParseStatusLine(statusLine);
        var headers = new List<KeyValuePair<string, string>>();
        for (var line = await ReadRequiredLineAsync(reader, cancellationToken); line.Length > 0; line = await ReadRequiredLineAsync(reader, cancellationToken))
        {
            headers.Add(line[0] is ' ' or '\t'
                ? throw OciHttpException.Malformed(OciHttpFailure.MalformedHeaderField, "The response folds a header field line (obs-fold).")
                : OciHttpResponseHead.ParseField(line));
        }

        return (statusCode, reasonPhrase, headers);
    }

    private static async ValueTask<string> ReadRequiredLineAsync(OciHttpConnectionReader reader, CancellationToken cancellationToken) =>
        await reader.ReadLineAsync(cancellationToken) ?? throw OciHttpException.ClosedEarly();
}
