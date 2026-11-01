using System.Security.Cryptography;
using System.Text;
using Curl.Extended.Protocol.S3.Http;
using Curl.Extended.Protocol.S3.Signing;
using Curl.Extended.Protocol.S3.Xml;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>
/// Sends one S3 REST request per connection (ADR-1090): it adds <c>Host</c>, the session token and, with credentials, the
/// SigV4 headers over the whole payload's SHA-256, and turns a response of 300 or above into a failure naming S3's
/// <c>Error</c> code (Amazon S3 API reference, "Error responses").
/// </summary>
/// <param name="connector">Opens the connection to the endpoint.</param>
/// <param name="context">The transfer, for its clock, proxy, events and cancellation.</param>
/// <param name="configuration">The credentials and session token.</param>
/// <param name="endpoint">The server the request goes to.</param>
internal sealed class S3Client(IConnector connector, ITransferContext context, S3Configuration configuration, S3Endpoint endpoint)
{
    /// <summary>Sends a request and reads its successful response.</summary>
    /// <typeparam name="T">What the response gives.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="path">The absolute path, before percent-encoding.</param>
    /// <param name="query">The query parameters, before percent-encoding.</param>
    /// <param name="body">The body, or <see langword="null"/> for none.</param>
    /// <param name="readResponse">Reads the response, whose status is below 300, while the connection is open.</param>
    /// <returns>What <paramref name="readResponse"/> gives.</returns>
    internal async Task<T> ExchangeAsync<T>(
        string method,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> query,
        byte[]? body,
        Func<S3HttpResponse, Task<T>> readResponse)
    {
        S3HttpRequest request = RequestFor(method, path, query, body);
        ConnectTarget target = new(endpoint.Host, endpoint.Port, endpoint.UseTls) { Proxy = context.Proxy, Events = context.Events, DiagnosticLog = context.DiagnosticLog };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken);
        if (connect.Connection is not { } connection)
        {
            throw new S3TransferException(connect.ExitCode, connect.ErrorMessage!);
        }

        // ConfigureAwait gives a struct to dispose, so the compiler adds no null check.
        await using (connection.ConfigureAwait(false))
        {
            S3HttpResponse response = await new S3HttpClientConnection(connection).SendAsync(request, context.CancellationToken);
            if (response.StatusCode >= 300)
            {
                throw ErrorOf(response.StatusCode, await ReadTextAsync(response));
            }

            return await readResponse(response);
        }
    }

    /// <summary>Reads a response body as UTF-8 text.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The body's text.</returns>
    internal async Task<string> ReadTextAsync(S3HttpResponse response)
    {
        using StreamReader reader = new(response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(context.CancellationToken);
    }

    /// <summary>Gives the failure for an S3 error: exit 22 with S3's code and message, or only the status when the body is no <c>Error</c>.</summary>
    /// <param name="statusCode">The response's status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The failure.</returns>
    internal static S3TransferException ErrorOf(int statusCode, string body) =>
        S3ErrorResponseParser.Parse(body).Value is { } error
            ? new S3TransferException(CurlExitCode.HttpReturnedError, $"S3 returned error {statusCode}: {error.Code}: {error.Message}")
            : new S3TransferException(CurlExitCode.HttpReturnedError, $"S3 returned error {statusCode}");

    private S3HttpRequest RequestFor(string method, string path, IReadOnlyList<KeyValuePair<string, string>> query, byte[]? body)
    {
        List<KeyValuePair<string, string>> headers = [new("host", endpoint.HostHeader)];
        if (configuration.SessionToken is { } sessionToken)
        {
            headers.Add(new("x-amz-security-token", sessionToken));
        }

        if (configuration.Credentials is { } credentials)
        {
            string payloadHash = Convert.ToHexStringLower(SHA256.HashData(body ?? []));
            headers.AddRange(new S3RequestSigner(context.TimeProvider).Sign(new S3SigningRequest(method, path, query, [.. headers], payloadHash), credentials));
        }

        string target = TargetOf(path, query);
        return body is null ? new S3HttpRequest(method, target, headers) : new S3HttpRequest(method, target, headers, new MemoryStream(body), body.Length);
    }

    private static string TargetOf(string path, IReadOnlyList<KeyValuePair<string, string>> query)
    {
        string encodedPath = S3UriEncoder.EncodePath(path);
        return query.Count == 0
            ? encodedPath
            : $"{encodedPath}?{string.Join('&', query.Select(parameter => $"{S3UriEncoder.Encode(parameter.Key)}={S3UriEncoder.Encode(parameter.Value)}"))}";
    }
}
