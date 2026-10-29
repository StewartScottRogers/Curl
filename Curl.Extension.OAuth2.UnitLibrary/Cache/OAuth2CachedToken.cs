namespace Curl.Extension.OAuth2.Cache;

/// <summary>A token kept in the cache (RFC 6749 §5.1, ADR-1034).</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="TokenType">The token type, such as <c>Bearer</c>.</param>
/// <param name="ExpiresAt">When the token expires, from <c>expires_in</c>, or <see langword="null"/> when the server did not say.</param>
/// <param name="RefreshToken">The refresh token, or <see langword="null"/> when the server sent none.</param>
/// <param name="Scope">The scope granted, or <see langword="null"/> when the server did not say.</param>
public sealed record OAuth2CachedToken(
    string AccessToken,
    string TokenType,
    DateTimeOffset? ExpiresAt,
    string? RefreshToken,
    string? Scope);
