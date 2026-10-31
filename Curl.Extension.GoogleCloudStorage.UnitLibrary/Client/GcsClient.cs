using System.Security.Cryptography;
using System.Text;
using Curl.Extension.GoogleCloudStorage.Http;
using Curl.Extension.GoogleCloudStorage.Signing;
using Curl.Extension.GoogleCloudStorage.Xml;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.GoogleCloudStorage.Client;

/// <summary>
/// Sends one XML API request per connection (ADR-1099): it adds <c>Host</c> and then either the OAuth 2.0
/// <c>Authorization: Bearer</c> header or, with an HMAC key, the V4 signing process's <c>GOOG4-HMAC-SHA256</c> headers over
/// the payload's SHA-256, and turns a response of 300 or above into a failure naming the <c>Error</c> code ("XML API error
/// responses").
/// </summary>
/// <param name="connector">Opens the connection to the endpoint.</param>
/// <param name="context">The transfer, for its clock, proxy, events and cancellation.</param>
/// <param name="endpoint">The server the request goes to.</param>
/// <param name="bearerToken">The OAuth 2.0 access token from <c>--oauth2-bearer</c>, or <see langword="null"/>.</param>
/// <param name="hmacKey">The HMAC key from <c>--user</c>, used when there is no bearer token, or <see langword="null"/>.</param>
internal sealed class GcsClient(IConnector connector, ITransferContext context, GcsEndpoint endpoint, string? bearerToken, GcsHmacKey? hmacKey)
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
        Func<GcsHttpResponse, Task<T>> readResponse)
    {
        GcsHttpRequest request = RequestFor(method, path, query, body);
        ConnectTarget target = new(endpoint.Host, endpoint.Port, endpoint.UseTls) { Proxy = context.Proxy, Events = context.Events, DiagnosticLog = context.DiagnosticLog };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken);
        if (connect.Connection is not { } connection)
        {
            throw new GcsTransferException(connect.ExitCode, connect.ErrorMessage!);
        }

        // ConfigureAwait gives a struct to dispose, so the compiler adds no null check.
        await using (connection.ConfigureAwait(false))
        {
            GcsHttpResponse response = await new GcsHttpClientConnection(connection).SendAsync(request, context.CancellationToken);
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
    internal async Task<string> ReadTextAsync(GcsHttpResponse response)
    {
        using StreamReader reader = new(response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(context.CancellationToken);
    }

    /// <summary>Gives the failure for a Cloud Storage error: exit 22 with its code and message, or only the status when the body is no <c>Error</c>.</summary>
    /// <param name="statusCode">The response's status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The failure.</returns>
    internal static GcsTransferException ErrorOf(int statusCode, string body) =>
        GcsErrorResponseParser.Parse(body).Value is { } error
            ? new GcsTransferException(CurlExitCode.HttpReturnedError, $"Cloud Storage returned error {statusCode}: {error.Code}: {error.Message}")
            : new GcsTransferException(CurlExitCode.HttpReturnedError, $"Cloud Storage returned error {statusCode}");

    private GcsHttpRequest RequestFor(string method, string path, IReadOnlyList<KeyValuePair<string, string>> query, byte[]? body)
    {
        List<KeyValuePair<string, string>> headers = [new("host", endpoint.HostHeader)];
        if (bearerToken is not null)
        {
            headers.Add(GcsBearerAuthorization.Header(bearerToken));
        }
        else if (hmacKey is not null)
        {
            string payloadHash = Convert.ToHexStringLower(SHA256.HashData(body ?? []));
            headers.AddRange(new GcsRequestSigner(context.TimeProvider).Sign(new GcsSigningRequest(method, path, query, [.. headers], payloadHash), hmacKey));
        }

        string target = TargetOf(path, query);
        return body is null ? new GcsHttpRequest(method, target, headers) : new GcsHttpRequest(method, target, headers, new MemoryStream(body), body.Length);
    }

    private static string TargetOf(string path, IReadOnlyList<KeyValuePair<string, string>> query)
    {
        string encodedPath = GcsUriEncoder.EncodePath(path);
        return query.Count == 0
            ? encodedPath
            : $"{encodedPath}?{string.Join('&', query.Select(parameter => $"{GcsUriEncoder.Encode(parameter.Key)}={GcsUriEncoder.Encode(parameter.Value)}"))}";
    }
}
