namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// What a cached token is kept under: the token endpoint that issued it, the client, the scope asked for and, for a token
/// bound to a DPoP key, that key (ADR-1034).
/// </summary>
/// <param name="TokenEndpoint">The token endpoint.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="Scope">The scope asked for, or <see langword="null"/> when none was.</param>
public sealed record OAuth2TokenCacheKey(Uri TokenEndpoint, string ClientId, string? Scope)
{
    /// <summary>
    /// Gets the JWK thumbprint (RFC 7638) of the DPoP key the token is bound to, or <see langword="null"/> for a token
    /// bound to none.
    /// </summary>
    public string? DpopKeyThumbprint { get; init; }
}
