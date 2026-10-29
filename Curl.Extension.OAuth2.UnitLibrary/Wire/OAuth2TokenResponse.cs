namespace Curl.Extension.OAuth2.Wire;

/// <summary>A successful access token response (RFC 6749 §5.1).</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="TokenType">The token type, such as <c>Bearer</c>.</param>
/// <param name="ExpiresIn">How many seconds the token lasts, or <see langword="null"/> when the server did not say.</param>
/// <param name="RefreshToken">The refresh token, or <see langword="null"/> when the server sent none.</param>
/// <param name="Scope">The scope granted, or <see langword="null"/> when it is the one asked for.</param>
public sealed record OAuth2TokenResponse(
    string AccessToken,
    string TokenType,
    long? ExpiresIn,
    string? RefreshToken,
    string? Scope);
