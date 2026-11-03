using System.Security.Cryptography;
using Curl.Extended.Protocol.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

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
    public async Task<byte[]> PullAsync(Uri url, string accept, CancellationToken cancellationToken) =>
        BodyOf(await ReceiveFinalAsync(url, accept, cancellationToken));

    /// <summary>
    /// Pulls <paramref name="url"/>, and <paramref name="fallbackUrl"/> when the registry answers the first with 404
    /// (ADR-1207): an <c>@digest</c> URL names a blob or a manifest, and only the registry knows which. When both are
    /// unknown the failure is the first one's.
    /// </summary>
    /// <param name="url">The registry API URL pulled first.</param>
    /// <param name="accept">The <c>Accept</c> value of the first pull.</param>
    /// <param name="fallbackUrl">The registry API URL pulled when the first is not found.</param>
    /// <param name="fallbackAccept">The <c>Accept</c> value of the fallback pull.</param>
    /// <param name="cancellationToken">Cancels the pull.</param>
    /// <returns>The body of the final 2xx response.</returns>
    /// <exception cref="OciTransferException">No 2xx response was reached.</exception>
    public async Task<byte[]> PullWithFallbackAsync(Uri url, string accept, Uri fallbackUrl, string fallbackAccept, CancellationToken cancellationToken)
    {
        var response = await ReceiveFinalAsync(url, accept, cancellationToken);
        if (response.StatusCode != 404)
        {
            return BodyOf(response);
        }

        var fallback = await ReceiveFinalAsync(fallbackUrl, fallbackAccept, cancellationToken);
        return BodyOf(fallback.StatusCode == 404 ? response : fallback);
    }

    private static byte[] BodyOf(OciHttpResponse response) =>
        response.StatusCode is >= 200 and < 300 ? response.Body : throw OciRegistryError.From(response);

    private async Task<OciHttpResponse> ReceiveFinalAsync(Uri url, string accept, CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, accept, null, cancellationToken);
        if (response.StatusCode == 401)
        {
            response = await SendAsync(url, accept, await session.RequestAuthorizationAsync(response, null, cancellationToken), cancellationToken);
        }

        return await FollowRedirectsAsync(response, url, accept, cancellationToken);
    }

    /// <summary>
    /// Checks that a blob pulled by <paramref name="digest"/> hashes to it (OCI Image Specification, descriptor,
    /// "Digests"), after any redirect, so tampered content never reaches the output (ADR-1206). The registered
    /// algorithms <c>sha256</c> and <c>sha512</c> are checked; a digest of another algorithm passes unchecked, as the
    /// specification allows for an unrecognised algorithm that meets the grammar.
    /// </summary>
    /// <param name="digest">The URL's digest, <c>algorithm:encoded</c>.</param>
    /// <param name="body">The pulled content.</param>
    /// <returns><paramref name="body"/>, when it matches the digest.</returns>
    /// <exception cref="OciTransferException">The content does not hash to the digest (exit 8).</exception>
    public static byte[] VerifyDigest(string digest, byte[] body)
    {
        var colon = digest.IndexOf(':', StringComparison.Ordinal);
        var algorithm = digest[..colon];
        byte[]? hash = algorithm switch
        {
            "sha256" => SHA256.HashData(body),
            "sha512" => SHA512.HashData(body),
            _ => null,
        };
        return hash is null || Convert.ToHexStringLower(hash) == digest[(colon + 1)..]
            ? body
            : throw new OciTransferException(CurlExitCode.WeirdServerReply, $"The pulled content does not match its digest {digest}.");
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
