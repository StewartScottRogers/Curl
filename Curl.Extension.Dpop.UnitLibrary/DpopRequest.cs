namespace Curl.Extension.Dpop;

/// <summary>What a DPoP proof's payload claims about one request (RFC 9449 §4.2).</summary>
/// <param name="Id">The <c>jti</c>, unique to the proof.</param>
/// <param name="Method">The <c>htm</c>, the request method.</param>
/// <param name="TargetUri">The <c>htu</c>, the target URI without its query and fragment.</param>
/// <param name="IssuedAt">The <c>iat</c>, when the proof was made.</param>
/// <param name="AccessToken">The access token sent with the request, hashed into <c>ath</c>, or <see langword="null" />.</param>
/// <param name="Nonce">The server's <c>nonce</c>, or <see langword="null" />.</param>
internal sealed record DpopRequest(string Id, string Method, string TargetUri, DateTimeOffset IssuedAt, string? AccessToken, string? Nonce);
