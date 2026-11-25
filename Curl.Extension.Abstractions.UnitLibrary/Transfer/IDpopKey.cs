namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// The key an <see cref="IDpopKeySource" />'s DPoP proofs are signed with (RFC 9449), as another hook needs it to
/// get an access token bound to that key from a token endpoint.
/// </summary>
public interface IDpopKey
{
    /// <summary>Gets the public key's JWK SHA-256 thumbprint (RFC 7638), base64url without padding.</summary>
    string Thumbprint { get; }

    /// <summary>Signs a proof for one <c>POST</c> that sends no access token, as a token or refresh request is (RFC 9449 §5).</summary>
    /// <param name="target">The endpoint posted to.</param>
    /// <param name="issuedAt">When the proof is made.</param>
    /// <param name="nonce">The endpoint's last <c>DPoP-Nonce</c>, or <see langword="null" /> when it has given none.</param>
    /// <returns>The value of the request's <c>DPoP</c> header.</returns>
    string ProofForPost(Uri target, DateTimeOffset issuedAt, string? nonce);
}
