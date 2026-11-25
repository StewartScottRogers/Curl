using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Dpop;

/// <summary>The <c>--dpop</c> key as another hook gets it: its thumbprint and its proofs for token and refresh requests (RFC 9449 §5).</summary>
/// <param name="key">The private key.</param>
internal sealed class DpopPostProofKey(DpopKey key) : IDpopKey
{
    public string Thumbprint { get; } = key.Thumbprint();

    public string ProofForPost(Uri target, DateTimeOffset issuedAt, string? nonce)
    {
        var targetUri = target.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        return DpopProof.Create(key, new DpopRequest(DpopProof.NewId(), "POST", targetUri, issuedAt, null, nonce));
    }
}
