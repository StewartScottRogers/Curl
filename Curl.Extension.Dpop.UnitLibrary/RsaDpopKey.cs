using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Curl.Extension.Dpop;

/// <summary>An RSA DPoP key: <c>RS256</c>, RSASSA-PKCS1-v1_5 with SHA-256 (RFC 7518 §3.3), its JWK an <c>RSA</c> key (§6.3.1).</summary>
internal sealed class RsaDpopKey : DpopKey
{
    private readonly RSA _rsa;
    private readonly RSAParameters _publicKey;

    /// <summary>Takes an RSA key that holds its private part.</summary>
    /// <param name="rsa">The key, owned from here on.</param>
    /// <exception cref="CryptographicException">The key is public only.</exception>
    public RsaDpopKey(RSA rsa)
    {
        _ = rsa.ExportParameters(includePrivateParameters: true);
        _publicKey = rsa.ExportParameters(includePrivateParameters: false);
        _rsa = rsa;
    }

    public override string Algorithm => "RS256";

    public override void WriteJwk(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("e", Base64Url.EncodeToString(_publicKey.Exponent));
        writer.WriteString("kty", "RSA");
        writer.WriteString("n", Base64Url.EncodeToString(_publicKey.Modulus));
        writer.WriteEndObject();
    }

    public override byte[] Sign(byte[] signingInput) => _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
