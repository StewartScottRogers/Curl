using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// Gets an access token the cheapest way (ADR-1034, ADR-1045): an unexpired cached one, else a refreshed one, else by
/// running the flow and caching what it gets.
/// </summary>
/// <param name="cache">The token cache, which also refreshes an expired token (RFC 6749 §6).</param>
public sealed class OAuth2CachedTokenSource(OAuth2TokenCache cache)
{
    /// <summary>Finds a usable cached token for <paramref name="request"/>, or runs <paramref name="flow"/> and caches its token.</summary>
    /// <param name="request">The token endpoint, client and scope, which key the cache.</param>
    /// <param name="flow">The grant to run when the cache has no usable token.</param>
    /// <param name="cancellationToken">Cancels the lookup, refresh and flow.</param>
    /// <returns>
    /// The token, or the flow's failure. A token from the cache carries no <c>expires_in</c>: its lifetime is in the cache.
    /// A token the cache file could not be written with is still given back.
    /// </returns>
    public async Task<OAuth2FlowResult> GetTokenAsync(OAuth2FlowRequest request, IOAuth2TokenFlow flow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(flow);
        if (await cache.FindAsync(request.CacheKey, request.Client, cancellationToken) is { } cached)
        {
            return OAuth2FlowResult.Issued(new OAuth2TokenResponse(cached.AccessToken, cached.TokenType, null, cached.RefreshToken, cached.Scope));
        }

        var result = await flow.RunAsync(cancellationToken);
        if (result.Token is { } token)
        {
            await cache.StoreAsync(request.CacheKey, token, cancellationToken);
        }

        return result;
    }
}
