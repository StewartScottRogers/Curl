namespace Curl.Extension.OAuth2.Wire;

/// <summary>An error response (RFC 6749 §5.2).</summary>
/// <param name="Error">The <c>error</c> as the server sent it.</param>
/// <param name="Code">The <c>error</c> as an <see cref="OAuth2ErrorCode"/>.</param>
/// <param name="Description">The <c>error_description</c>, or <see langword="null"/> when the server sent none.</param>
/// <param name="Uri">The <c>error_uri</c>, or <see langword="null"/> when the server sent none.</param>
public sealed record OAuth2ErrorResponse(string Error, OAuth2ErrorCode Code, string? Description, string? Uri);
