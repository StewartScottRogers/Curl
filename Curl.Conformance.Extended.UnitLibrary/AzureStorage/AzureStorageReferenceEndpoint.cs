using System.Collections.Concurrent;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>
/// An Azure Storage reference endpoint: it reads each HTTP/1.1 request curl sends on a connection, records it,
/// and writes the response <see cref="Answer" /> gives, framed as <paramref name="framing" /> says; under
/// <see cref="AzureStorageResponseFraming.Close" /> it closes the connection after one response. The Blob
/// endpoint derives from it, and a DFS endpoint can derive from it in a file of its own.
/// </summary>
/// <param name="framing">How every response body is framed.</param>
public abstract class AzureStorageReferenceEndpoint(AzureStorageResponseFraming framing) : IReferenceServer
{
    private readonly ConcurrentQueue<AzureStorageHttpRequest> requests = new();

    /// <summary>Gets every request curl sent, in the order the endpoint read them, with its header fields and query.</summary>
    public IReadOnlyList<AzureStorageHttpRequest> Requests => [.. requests];

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        while (await AzureStorageHttpRequestReader.ReadAsync(connection, cancellationToken).ConfigureAwait(false) is { } request)
        {
            requests.Enqueue(request);
            await AzureStorageHttpResponseWriter.WriteAsync(connection, Answer(request), framing, cancellationToken).ConfigureAwait(false);
            if (framing == AzureStorageResponseFraming.Close)
            {
                return;
            }
        }
    }

    /// <summary>Says whether <paramref name="request" /> carries a credential: an <c>Authorization</c> field or a SAS <c>sig</c> parameter.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true" /> when it is no anonymous request.</returns>
    protected static bool IsAuthorized(AzureStorageHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Header("Authorization") is not null || request.QueryValue("sig") is not null;
    }

    /// <summary>Gives the response to <paramref name="request" />.</summary>
    /// <param name="request">The request curl sent.</param>
    /// <returns>The response, without its framing.</returns>
    protected abstract AzureStorageHttpResponse Answer(AzureStorageHttpRequest request);
}
