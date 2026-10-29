namespace Curl.Extension.OAuth2.Cache;

/// <summary>What a cached token is kept under: the token endpoint that issued it, the client and the scope asked for (ADR-1034).</summary>
/// <param name="TokenEndpoint">The token endpoint.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="Scope">The scope asked for, or <see langword="null"/> when none was.</param>
public sealed record OAuth2TokenCacheKey(Uri TokenEndpoint, string ClientId, string? Scope);
