using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>Sends one request over an <see cref="IConnection"/> and reads its final response whole (RFC 9112).</summary>
internal static class OciHttpExchange
{
    /// <summary>Writes <paramref name="request"/>, skips any interim 1xx responses (RFC 9110 §15.2), and reads the final one.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request.</param>
    /// <param name="headReporter">Shows the request and response heads for <c>-v</c>, <c>-i</c> and <c>-D</c>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, whatever its status; a response to <c>HEAD</c> has no body (RFC 9110 §9.3.2).</returns>
    public static async Task<OciHttpResponse> ExchangeAsync(
        IConnection connection, OciHttpRequest request, OciHttpHeadReporter headReporter, CancellationToken cancellationToken)
    {
        var head = OciHttpRequestWriter.Write(request);
        await connection.WriteAsync(head, cancellationToken);
        headReporter.ReportRequestHead(head);
        await connection.FlushAsync(cancellationToken);
        var reader = new OciHttpConnectionReader(connection);
        var statusLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new OciHttpException(OciHttpFailure.NoResponse, CurlExitCode.GotNothing, "Empty reply from server");
        await headReporter.ReportResponseLineAsync(statusLine, cancellationToken);
        var (statusCode, reasonPhrase, headers) = await ReadHeadAsync(reader, headReporter, statusLine, cancellationToken);
        while (statusCode < 200)
        {
            (statusCode, reasonPhrase, headers) = await ReadHeadAsync(reader, headReporter, await ReadReportedLineAsync(reader, headReporter, cancellationToken), cancellationToken);
        }

        var bodyLength = request.Method == "HEAD" ? 0 : OciHttpResponseHead.FindBodyLength(headers, statusCode);
        var body = await OciHttpBodyReader.ReadAsync(reader, bodyLength, cancellationToken);
        return new OciHttpResponse(statusCode, reasonPhrase, headers, body);
    }

    private static async ValueTask<(int StatusCode, string ReasonPhrase, List<KeyValuePair<string, string>> Headers)> ReadHeadAsync(
        OciHttpConnectionReader reader, OciHttpHeadReporter headReporter, string statusLine, CancellationToken cancellationToken)
    {
        var (statusCode, reasonPhrase) = OciHttpResponseHead.ParseStatusLine(statusLine);
        var headers = new List<KeyValuePair<string, string>>();
        for (var line = await ReadReportedLineAsync(reader, headReporter, cancellationToken); line.Length > 0; line = await ReadReportedLineAsync(reader, headReporter, cancellationToken))
        {
            headers.Add(line[0] is ' ' or '\t'
                ? throw OciHttpException.Malformed(OciHttpFailure.MalformedHeaderField, "The response folds a header field line (obs-fold).")
                : OciHttpResponseHead.ParseField(line));
        }

        return (statusCode, reasonPhrase, headers);
    }

    private static async ValueTask<string> ReadReportedLineAsync(
        OciHttpConnectionReader reader, OciHttpHeadReporter headReporter, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken) ?? throw OciHttpException.ClosedEarly();
        await headReporter.ReportResponseLineAsync(line, cancellationToken);
        return line;
    }
}
