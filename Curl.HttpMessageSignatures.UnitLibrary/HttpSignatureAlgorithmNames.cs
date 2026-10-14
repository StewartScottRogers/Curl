namespace Curl.HttpMessageSignatures;

/// <summary>
/// The RFC 9421 algorithm names curl writes in the <c>alg</c> parameter of
/// <c>Signature-Input</c>.
/// </summary>
public static class HttpSignatureAlgorithmNames
{
    /// <summary>The name of <see cref="HttpSignatureAlgorithm.Ed25519" />.</summary>
    public const string Ed25519 = "ed25519";

    /// <summary>The name of <see cref="HttpSignatureAlgorithm.HmacSha256" />.</summary>
    public const string HmacSha256 = "hmac-sha256";

    /// <summary>Returns the RFC 9421 name of <paramref name="algorithm" />.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns><c>ed25519</c> or <c>hmac-sha256</c>.</returns>
    public static string ToName(HttpSignatureAlgorithm algorithm) =>
        algorithm == HttpSignatureAlgorithm.Ed25519 ? Ed25519 : HmacSha256;
}
