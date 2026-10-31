using Curl.Extension.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>
/// Pulls one manifest or blob (OCI Distribution Specification 1.1, "Pulling manifests", "Pulling blobs"): a <c>GET</c>
/// that a 401 answers with a <c>Bearer</c> challenge is sent again with the token the challenge's realm gives
/// (Docker/Distribution token authentication specification), and a redirect, which a registry may answer a blob with,
/// is followed without the token, since it leads to storage that did not issue it.
/// </summary>
/// <param name="session">Sends each request and fetches the token for the challenge's own scope.</param>
internal sealed class OciPullFlow(OciRegistrySession session)
{
    /// <summary>The most redirects followed, curl's own default for <c>--max-redirs</c>.</summary>
    internal const int MaxRedirects = 50;

    /// <summary>Pulls <paramref name="url"/>.</summary>
    /// <param name="url">The registry API URL.</param>
    /// <param name="accept">The <c>Accept</c> value: the manifest media types, or <c>*/*</c> for a blob.</param>
    /// <param name="cancellationToken">Cancels the pull.</param>
    /// <returns>The body of the final 2xx response.</returns>
    /// <exception cref="OciTransferException">No 2xx response was reached.</exception>
    public async Task<byte[]> PullAsync(Uri url, string accept, CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, accept, null, cancellationToken);
        if (response.StatusCode == 401)
        {
            response = await SendAsync(url, accept, await session.RequestAuthorizationAsync(response, null, cancellationToken), cancellationToken);
        }

        response = await FollowRedirectsAsync(response, url, accept, cancellationToken);
        return response.StatusCode is >= 200 and < 300 ? response.Body : throw OciRegistryError.From(response);
    }

    private async Task<OciHttpResponse> FollowRedirectsAsync(OciHttpResponse response, Uri url, string accept, CancellationToken cancellationToken)
    {
        for (var redirects = 0; response.StatusCode is 301 or 302 or 303 or 307 or 308; redirects++)
        {
            if (redirects == MaxRedirects)
            {
                throw new OciTransferException(CurlExitCode.TooManyRedirects, $"Maximum ({MaxRedirects}) redirects followed");
            }

            url = RedirectTarget(url, response);
            response = await SendAsync(url, accept, null, cancellationToken);
        }

        return response;
    }

    private static Uri RedirectTarget(Uri url, OciHttpResponse response) =>
        response.FindHeader("Location") is { } location && Uri.TryCreate(url, location, out var target)
            ? target
            : throw new OciTransferException(CurlExitCode.WeirdServerReply, $"The registry answered {response.StatusCode} without a usable Location.");

    private Task<OciHttpResponse> SendAsync(Uri url, string accept, string? authorization, CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> headers = [new("Accept", accept)];
        if (authorization is not null)
        {
            headers.Add(new("Authorization", authorization));
        }

        return session.ReceiveAsync(new OciHttpRequest("GET", url, headers, null), cancellationToken);
    }
}
