using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// Keeps tokens in the <see cref="OAuth2TokenCacheFile"/> by <see cref="OAuth2TokenCacheKey"/> (ADR-1034): gives back an
/// unexpired one, and refreshes an expired one with its refresh token (RFC 6749 §6).
/// </summary>
/// <remarks>
/// A token expires <see cref="ExpirySafetyMargin"/> before <c>expires_in</c> (§5.1) runs out by the
/// <see cref="TimeProvider"/>, so it is not sent as it lapses; one whose lifetime the server did not give lasts until
/// <see cref="RemoveAsync"/>. A refresh that fails with <c>invalid_grant</c> (§5.2), or an expired token with no refresh
/// token, drops the entry so the flow runs again; any other failed refresh keeps it. A new refresh token in the response
/// replaces the old (§6).
/// </remarks>
/// <param name="file">The cache file.</param>
/// <param name="httpClient">Sends refresh requests to the token endpoint.</param>
/// <param name="timeProvider">Gives the current time.</param>
public sealed class OAuth2TokenCache(OAuth2TokenCacheFile file, OAuth2HttpClient httpClient, TimeProvider timeProvider)
{
    /// <summary>How long before <c>expires_in</c> runs out a token counts as expired.</summary>
    public static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(30);

    // Caps expires_in at 100 years so an absurd value cannot overflow DateTimeOffset.
    private const long LongestLifetimeSeconds = 100L * 366 * 24 * 60 * 60;

    /// <summary>Finds a usable token for <paramref name="key"/>, refreshing an expired one.</summary>
    /// <param name="key">The token endpoint, client and scope.</param>
    /// <param name="client">The client a refresh request is made for.</param>
    /// <param name="cancellationToken">Cancels the lookup and any refresh.</param>
    /// <returns>The token, or <see langword="null"/> when the cache holds none usable and the flow must run.</returns>
    public async Task<OAuth2CachedToken?> FindAsync(OAuth2TokenCacheKey key, OAuth2Client client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(client);
        var entries = await file.ReadAsync(cancellationToken);
        if (!entries.TryGetValue(key, out var token))
        {
            return null;
        }

        return IsExpired(token) ? await RefreshAsync(entries, key, token, client, cancellationToken) : token;
    }

    /// <summary>Keeps the token in <paramref name="response"/> under <paramref name="key"/>, replacing any there.</summary>
    /// <param name="key">The token endpoint, client and scope.</param>
    /// <param name="response">The access token response (§5.1).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the cache file was written.</returns>
    public async Task<bool> StoreAsync(OAuth2TokenCacheKey key, OAuth2TokenResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(response);
        var entries = await file.ReadAsync(cancellationToken);
        entries[key] = ToCachedToken(response, null);
        return await file.WriteAsync(entries, cancellationToken);
    }

    /// <summary>Drops the token kept under <paramref name="key"/>, such as one the resource server refused.</summary>
    /// <param name="key">The token endpoint, client and scope.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether a token was dropped and the cache file written.</returns>
    public async Task<bool> RemoveAsync(OAuth2TokenCacheKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var entries = await file.ReadAsync(cancellationToken);
        return entries.Remove(key) && await file.WriteAsync(entries, cancellationToken);
    }

    private bool IsExpired(OAuth2CachedToken token) =>
        token.ExpiresAt is { } expiresAt && timeProvider.GetUtcNow() + ExpirySafetyMargin >= expiresAt;

    private async Task<OAuth2CachedToken?> RefreshAsync(
        Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> entries,
        OAuth2TokenCacheKey key,
        OAuth2CachedToken token,
        OAuth2Client client,
        CancellationToken cancellationToken)
    {
        if (token.RefreshToken is null)
        {
            await DropAsync(entries, key, cancellationToken);
            return null;
        }

        var request = OAuth2TokenRequestWriter.WriteRefreshToken(client, token.RefreshToken, key.Scope);
        var result = await httpClient.PostFormAsync(key.TokenEndpoint, request, cancellationToken);
        if (result.Response is not { } response)
        {
            return null;
        }

        if (ReadRefreshedToken(response) is { } refreshed)
        {
            var cached = ToCachedToken(refreshed, token.RefreshToken);
            entries[key] = cached;
            await file.WriteAsync(entries, cancellationToken);
            return cached;
        }

        if (IsInvalidGrant(response))
        {
            await DropAsync(entries, key, cancellationToken);
        }

        return null;
    }

    private static OAuth2TokenResponse? ReadRefreshedToken(OAuth2HttpResponse response) =>
        response.StatusCode == 200 ? OAuth2TokenResponseParser.Parse(response.Body).Value : null;

    private static bool IsInvalidGrant(OAuth2HttpResponse response) =>
        OAuth2ErrorResponseParser.Parse(response.Body).Value?.Code == OAuth2ErrorCode.InvalidGrant;

    private async Task DropAsync(Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> entries, OAuth2TokenCacheKey key, CancellationToken cancellationToken)
    {
        entries.Remove(key);
        await file.WriteAsync(entries, cancellationToken);
    }

    private OAuth2CachedToken ToCachedToken(OAuth2TokenResponse response, string? previousRefreshToken) => new(
        response.AccessToken,
        response.TokenType,
        ExpiresAt(response.ExpiresIn),
        response.RefreshToken ?? previousRefreshToken,
        response.Scope);

    private DateTimeOffset? ExpiresAt(long? expiresIn) =>
        expiresIn is { } seconds ? timeProvider.GetUtcNow().AddSeconds(Math.Clamp(seconds, 0, LongestLifetimeSeconds)) : null;
}
