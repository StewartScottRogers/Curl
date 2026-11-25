using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Http;

/// <summary>
/// Signs one transfer's token and refresh requests with a DPoP proof (RFC 9449 §5), keeping the token endpoint's last
/// <c>DPoP-Nonce</c> for the next proof and sending a request once more when the endpoint answers
/// <c>use_dpop_nonce</c> (§8).
/// </summary>
/// <param name="key">The key the proofs are signed with.</param>
/// <param name="tokenEndpoint">The endpoint whose requests are signed.</param>
/// <param name="timeProvider">Gives each proof's <c>iat</c>.</param>
internal sealed class OAuth2DpopTokenProver(IDpopKey key, Uri tokenEndpoint, TimeProvider timeProvider)
{
    private string? nonce;

    /// <summary>Gets the endpoint whose requests are signed.</summary>
    public Uri TokenEndpoint => tokenEndpoint;

    /// <summary>Posts <paramref name="request"/> with a proof.</summary>
    /// <param name="request">The token or refresh request.</param>
    /// <param name="post">Sends the signed request and reads the answer.</param>
    /// <returns>The answer; the second try's when the first asked for a nonce.</returns>
    public async Task<OAuth2HttpResult> PostAsync(OAuth2FormRequest request, Func<OAuth2FormRequest, Task<OAuth2HttpResult>> post)
    {
        var result = await PostSignedAsync(request, post);
        return AsksForNonce(result.Response) ? await PostSignedAsync(request, post) : result;
    }

    private async Task<OAuth2HttpResult> PostSignedAsync(OAuth2FormRequest request, Func<OAuth2FormRequest, Task<OAuth2HttpResult>> post)
    {
        var result = await post(request with { DpopProof = key.ProofForPost(tokenEndpoint, timeProvider.GetUtcNow(), nonce) });
        nonce = result.Response?.FindHeader("DPoP-Nonce") ?? nonce;
        return result;
    }

    private static bool AsksForNonce(OAuth2HttpResponse? response) =>
        response is { StatusCode: 400 } && OAuth2ErrorResponseParser.Parse(response.Body).Value?.Code == OAuth2ErrorCode.UseDpopNonce;
}
