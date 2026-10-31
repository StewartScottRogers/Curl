using System.Text;
using Curl.Extended.Protocol.AzureStorage.Bodies;
using Curl.Extended.Protocol.AzureStorage.Http;
using Curl.Extended.Protocol.AzureStorage.Requests;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>
/// Sends one Azure Storage REST request per connection, over HTTPS on port 443 (ADR-1105): each request is addressed,
/// versioned and authorized by <see cref="AzureStorageRequestAuthorizer"/>, and a response of 300 or above becomes a failure
/// naming the <c>Error</c> body's code ("Status and error codes").
/// </summary>
/// <param name="connector">Opens the connection to the endpoint.</param>
/// <param name="context">The transfer, for its proxy, events and cancellation.</param>
/// <param name="authorizer">Addresses and authorizes each request.</param>
/// <param name="credential">What authorizes each request.</param>
internal sealed class AzureStorageClient(IConnector connector, ITransferContext context, AzureStorageRequestAuthorizer authorizer, AzureStorageCredential credential)
{
    /// <summary>The port every request goes to: Azure Storage is reached over HTTPS.</summary>
    internal const int HttpsPort = 443;

    /// <summary>Gives the endpoint of an operation on <paramref name="path"/>: its query first, then the URL's own query.</summary>
    /// <param name="endpoint">The URL's endpoint.</param>
    /// <param name="path">The resource path, percent-encoded.</param>
    /// <param name="operationQuery">The operation's query parameters, percent-encoded, without a <c>?</c>; empty for none.</param>
    /// <returns>The endpoint of the operation.</returns>
    internal static AzureStorageEndpoint Operation(AzureStorageEndpoint endpoint, string path, string operationQuery) =>
        endpoint with { Path = path, Query = string.Join('&', new[] { operationQuery, endpoint.Query }.Where(part => part.Length > 0)) };

    /// <summary>Sends a request and reads its successful response.</summary>
    /// <typeparam name="T">What the response gives.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="endpoint">Where the request goes.</param>
    /// <param name="headers">The header fields to send after the authorizer's.</param>
    /// <param name="body">The body, or <see langword="null"/> for none.</param>
    /// <param name="readResponse">Reads the response, whose status is below 300, while the connection is open.</param>
    /// <returns>What <paramref name="readResponse"/> gives.</returns>
    internal async Task<T> ExchangeAsync<T>(
        string method,
        AzureStorageEndpoint endpoint,
        IReadOnlyList<KeyValuePair<string, string>> headers,
        byte[]? body,
        Func<AzureStorageHttpResponse, Task<T>> readResponse)
    {
        AzureStorageAuthorizedRequest authorized = authorizer.Authorize(endpoint, credential);
        List<KeyValuePair<string, string>> fields = [.. authorized.Headers, .. headers];
        AzureStorageHttpRequest request = body is null
            ? new(method, authorized.Target, fields)
            : new(method, authorized.Target, fields, new MemoryStream(body), body.Length);
        ConnectTarget target = new(endpoint.Host, HttpsPort, true) { Proxy = context.Proxy, Events = context.Events, DiagnosticLog = context.DiagnosticLog };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken);
        if (connect.Connection is not { } connection)
        {
            throw new AzureStorageTransferException(connect.ExitCode, connect.ErrorMessage!);
        }

        // ConfigureAwait gives a struct to dispose, so the compiler adds no null check.
        await using (connection.ConfigureAwait(false))
        {
            AzureStorageHttpResponse response = await new AzureStorageHttpClientConnection(connection).SendAsync(request, context.CancellationToken);
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
    internal async Task<string> ReadTextAsync(AzureStorageHttpResponse response)
    {
        using StreamReader reader = new(response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(context.CancellationToken);
    }

    /// <summary>Copies a response body to <paramref name="output"/>.</summary>
    /// <param name="source">The response body.</param>
    /// <param name="output">Where its bytes go.</param>
    /// <returns>The number of bytes copied.</returns>
    internal async Task<long> CopyAsync(Stream source, Stream output)
    {
        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken);
            copied += read;
        }

        return copied;
    }

    /// <summary>Reads up to <paramref name="size"/> bytes of an upload, fewer only at its end.</summary>
    /// <param name="upload">The upload.</param>
    /// <param name="size">The most bytes to read.</param>
    /// <returns>The bytes read.</returns>
    internal async Task<byte[]> ReadBlockAsync(Stream upload, int size)
    {
        byte[] buffer = new byte[size];
        int read = await upload.ReadAtLeastAsync(buffer, size, throwOnEndOfStream: false, context.CancellationToken);
        return buffer[..read];
    }

    /// <summary>Writes names to <paramref name="output"/>, one per line.</summary>
    /// <param name="names">The names.</param>
    /// <param name="output">Where the lines go.</param>
    /// <returns>The number of bytes written.</returns>
    internal async Task<long> WriteLinesAsync(IEnumerable<string> names, Stream output)
    {
        byte[] lines = Encoding.UTF8.GetBytes(string.Concat(names.Select(name => name + "\n")));
        await output.WriteAsync(lines, context.CancellationToken);
        return lines.Length;
    }

    /// <summary>Gives the value of a body that was read, or throws exit 8 saying why it could not be.</summary>
    /// <typeparam name="T">The value the body holds.</typeparam>
    /// <param name="result">The parse result.</param>
    /// <returns>The value.</returns>
    internal static T ValueOf<T>(AzureStorageBodyParseResult<T> result)
        where T : class =>
        result.Value ?? throw new AzureStorageTransferException(CurlExitCode.WeirdServerReply, $"Azure Storage sent a body curl cannot read: {result.Failure}");

    /// <summary>Splits a percent-encoded <c>/first/rest</c> path into its decoded first segment and the decoded rest.</summary>
    /// <param name="path">The path, starting with <c>/</c>.</param>
    /// <returns>The first segment (the container or file system) and the remainder (the blob or path name), either possibly empty.</returns>
    internal static (string First, string Remainder) Split(string path)
    {
        string decoded = Uri.UnescapeDataString(path)[1..];
        int slash = decoded.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? (decoded, string.Empty) : (decoded[..slash], decoded[(slash + 1)..]);
    }

    /// <summary>Gives the failure for an Azure Storage error: exit 22 with its code and message, or only the status when the body is no <c>Error</c>.</summary>
    /// <param name="statusCode">The response's status code.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The failure.</returns>
    internal static AzureStorageTransferException ErrorOf(int statusCode, string body) =>
        AzureStorageErrorParser.Parse(body).Value is { } error
            ? new AzureStorageTransferException(CurlExitCode.HttpReturnedError, $"Azure Storage returned error {statusCode}: {error.Code}: {error.Message}")
            : new AzureStorageTransferException(CurlExitCode.HttpReturnedError, $"Azure Storage returned error {statusCode}");
}
