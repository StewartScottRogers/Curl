using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Curl.Extension.Dpop;

/// <summary>
/// An ECDSA DPoP key (RFC 7518 §3.4): <c>ES256</c> on P-256, <c>ES384</c> on P-384, <c>ES512</c> on P-521, its JWK an
/// <c>EC</c> key (RFC 7518 §6.2.1) and its signature R || S, each the curve's size.
/// </summary>
internal sealed class EcDpopKey : DpopKey
{
    private readonly ECDsa _ecdsa;
    private readonly ECParameters _publicKey;
    private readonly EcCurve _curve;

    /// <summary>Takes an ECDSA key that holds its private part.</summary>
    /// <param name="ecdsa">The key, owned from here on.</param>
    /// <exception cref="CryptographicException">The key is public only or not on a named JWS curve.</exception>
    public EcDpopKey(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        _curve = CurveOf(parameters.Curve.Oid.Value);
        _publicKey = parameters with { D = null };
        _ecdsa = ecdsa;
    }

    public override string Algorithm => _curve.Algorithm;

    public override void WriteJwk(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("crv", _curve.Name);
        writer.WriteString("kty", "EC");
        writer.WriteString("x", Base64Url.EncodeToString(_publicKey.Q.X));
        writer.WriteString("y", Base64Url.EncodeToString(_publicKey.Q.Y));
        writer.WriteEndObject();
    }

    public override byte[] Sign(byte[] signingInput) => _ecdsa.SignData(signingInput, _curve.Hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    /// <summary>Names the JWK curve and JWS algorithm of a curve OID (RFC 7518 §3.4, §6.2.1.1).</summary>
    /// <param name="oid">The curve's OID.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="CryptographicException">The curve has no JWS algorithm.</exception>
    internal static EcCurve CurveOf(string? oid) => oid switch
    {
        "1.2.840.10045.3.1.7" => new("P-256", "ES256", HashAlgorithmName.SHA256),
        "1.3.132.0.34" => new("P-384", "ES384", HashAlgorithmName.SHA384),
        "1.3.132.0.35" => new("P-521", "ES512", HashAlgorithmName.SHA512),
        _ => throw new CryptographicException($"The curve {oid} has no JWS algorithm."),
    };

    /// <summary>A curve JWS names.</summary>
    /// <param name="Name">The JWK <c>crv</c>.</param>
    /// <param name="Algorithm">The JWS <c>alg</c>.</param>
    /// <param name="Hash">The hash <paramref name="Algorithm" /> signs with.</param>
    internal sealed record EcCurve(string Name, string Algorithm, HashAlgorithmName Hash);
}
