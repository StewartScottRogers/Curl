using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Http;

/// <summary>Sends one request over an <see cref="IConnection"/> and reads its final response whole (RFC 9112).</summary>
internal static class OAuth2HttpExchange
{
    /// <summary>Writes <paramref name="request"/>, skips any interim 1xx responses (RFC 9110 §15.2), and reads the final one.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request's bytes, from <see cref="OAuth2HttpRequestWriter.Write"/>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response, whatever its status.</returns>
    public static async Task<OAuth2HttpResponse> ExchangeAsync(IConnection connection, byte[] request, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(request, cancellationToken);
        await connection.FlushAsync(cancellationToken);
        var reader = new OAuth2HttpConnectionReader(connection);
        var statusLine = await reader.ReadLineAsync(cancellationToken)
            ?? throw new OAuth2HttpException(OAuth2HttpFailure.NoResponse, CurlExitCode.GotNothing, "Empty reply from server");
        var (statusCode, reasonPhrase, headers) = await ReadHeadAsync(reader, statusLine, cancellationToken);
        while (statusCode < 200)
        {
            (statusCode, reasonPhrase, headers) = await ReadHeadAsync(reader, await ReadRequiredLineAsync(reader, cancellationToken), cancellationToken);
        }

        var body = await OAuth2HttpBodyReader.ReadAsync(reader, OAuth2HttpResponseHead.FindBodyLength(headers, statusCode), cancellationToken);
        return new OAuth2HttpResponse(statusCode, reasonPhrase, headers, body);
    }

    private static async ValueTask<(int StatusCode, string ReasonPhrase, List<KeyValuePair<string, string>> Headers)> ReadHeadAsync(
        OAuth2HttpConnectionReader reader, string statusLine, CancellationToken cancellationToken)
    {
        var (statusCode, reasonPhrase) = OAuth2HttpResponseHead.ParseStatusLine(statusLine);
        var headers = new List<KeyValuePair<string, string>>();
        for (var line = await ReadRequiredLineAsync(reader, cancellationToken); line.Length > 0; line = await ReadRequiredLineAsync(reader, cancellationToken))
        {
            headers.Add(line[0] is ' ' or '\t'
                ? throw OAuth2HttpException.Malformed(OAuth2HttpFailure.MalformedHeaderField, "The response folds a header field line (obs-fold).")
                : OAuth2HttpResponseHead.ParseField(line));
        }

        return (statusCode, reasonPhrase, headers);
    }

    private static async ValueTask<string> ReadRequiredLineAsync(OAuth2HttpConnectionReader reader, CancellationToken cancellationToken) =>
        await reader.ReadLineAsync(cancellationToken) ?? throw OAuth2HttpException.ClosedEarly();
}
