using System.Net;
using Curl.Extension.OciRegistry.Auth;
using Curl.Extension.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>
/// Pulls one manifest or blob (OCI Distribution Specification 1.1, "Pulling manifests", "Pulling blobs"): a <c>GET</c>
/// that a 401 answers with a <c>Bearer</c> challenge is sent again with the token the challenge's realm gives
/// (Docker/Distribution token authentication specification), and a redirect, which a registry may answer a blob with,
/// is followed without the token, since it leads to storage that did not issue it.
/// </summary>
/// <param name="client">Sends each request.</param>
/// <param name="credentials"><c>-u</c>'s user and password, sent to the token endpoint, or <see langword="null"/> for an anonymous token.</param>
internal sealed class OciPullFlow(OciHttpClient client, NetworkCredential? credentials)
{
    /// <summary>The most redirects followed, curl's own default for <c>--max-redirs</c>.</summary>
    internal const int MaxRedirects = 50;

    /// <summary>Pulls <paramref name="url"/>.</summary>
    /// <param name="url">The registry API URL.</param>
    /// <param name="accept">The <c>Accept</c> value: the manifest media types, or <c>*/*</c> for a blob.</param>
    /// <param name="cancellationToken">Cancels the pull.</param>
    /// <returns>The body of the final 2xx response.</returns>
    /// <exception cref="OciPullException">No 2xx response was reached.</exception>
    public async Task<byte[]> PullAsync(Uri url, string accept, CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, accept, null, cancellationToken);
        if (response.StatusCode == 401)
        {
            response = await SendAsync(url, accept, await RequestTokenAsync(response, cancellationToken), cancellationToken);
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
                throw new OciPullException(CurlExitCode.TooManyRedirects, $"Maximum ({MaxRedirects}) redirects followed");
            }

            url = RedirectTarget(url, response);
            response = await SendAsync(url, accept, null, cancellationToken);
        }

        return response;
    }

    private static Uri RedirectTarget(Uri url, OciHttpResponse response) =>
        response.FindHeader("Location") is { } location && Uri.TryCreate(url, location, out var target)
            ? target
            : throw new OciPullException(CurlExitCode.WeirdServerReply, $"The registry answered {response.StatusCode} without a usable Location.");

    // A 401 without a Bearer challenge that has a realm cannot be answered, so it fails as the registry's own error.
    private async Task<string> RequestTokenAsync(OciHttpResponse challengeResponse, CancellationToken cancellationToken)
    {
        var challenge = OciBearerChallengeParser.Parse(challengeResponse.FindHeader("WWW-Authenticate"));
        var request = (challenge is null ? null : OciTokenRequestBuilder.Build(challenge, UserAndPassword()))
            ?? throw OciRegistryError.From(challengeResponse);
        var response = await ReceiveAsync(request, cancellationToken);
        var token = response.StatusCode is >= 200 and < 300 ? OciTokenResponseParser.ParseToken(response.Body) : null;
        return token is null
            ? throw new OciPullException(CurlExitCode.LoginDenied, $"The token endpoint answered {response.StatusCode} without a token.")
            : "Bearer " + token;
    }

    private string? UserAndPassword() => credentials is null ? null : credentials.UserName + ":" + credentials.Password;

    private Task<OciHttpResponse> SendAsync(Uri url, string accept, string? authorization, CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> headers = [new("Accept", accept)];
        if (authorization is not null)
        {
            headers.Add(new("Authorization", authorization));
        }

        return ReceiveAsync(new OciHttpRequest("GET", url, headers, null), cancellationToken);
    }

    private async Task<OciHttpResponse> ReceiveAsync(OciHttpRequest request, CancellationToken cancellationToken)
    {
        var result = await client.SendAsync(request, cancellationToken);
        return result.Response ?? throw new OciPullException(result.ExitCode, result.Message!);
    }
}
