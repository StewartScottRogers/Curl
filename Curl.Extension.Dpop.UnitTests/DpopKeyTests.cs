using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.Dpop.UnitTests;

[TestClass]
public sealed class DpopKeyTests
{
    [TestMethod]
    [DataRow("P-256", "ES256", 32)]
    [DataRow("P-384", "ES384", 48)]
    [DataRow("P-521", "ES512", 66)]
    public void FromPem_AnEcPrivateKey_SignsWithTheCurvesAlgorithmAndGivesItsPublicJwk_Rfc9449Section4Point2(string crv, string algorithm, int coordinateSize)
    {
        using var ecdsa = ECDsa.Create(CurveNamed(crv));

        var key = DpopKey.FromPem(ecdsa.ExportECPrivateKeyPem());
        var jwk = Jwk(key);

        Assert.AreEqual(algorithm, key.Algorithm);
        Assert.AreEqual("{\"crv\":\"" + crv + "\",\"kty\":\"EC\",\"x\":", jwk.GetRawText()[..(25 + crv.Length)]);
        Assert.IsFalse(jwk.TryGetProperty("d", out _));
        var x = Base64Url.DecodeFromChars(jwk.GetProperty("x").GetString());
        var y = Base64Url.DecodeFromChars(jwk.GetProperty("y").GetString());
        Assert.HasCount(coordinateSize, x);
        using var verifier = ECDsa.Create(new ECParameters { Curve = CurveNamed(crv), Q = new ECPoint { X = x, Y = y } });
        var signature = key.Sign("a.b"u8.ToArray());
        Assert.HasCount(2 * coordinateSize, signature);
        Assert.IsTrue(verifier.VerifyData("a.b"u8, signature, HashOf(algorithm), DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [TestMethod]
    public void FromPem_AnEcKeyInPkcs8_IsRead_Rfc9449Section4Point2()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.AreEqual("ES256", DpopKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem()).Algorithm);
    }

    [TestMethod]
    public void FromPem_AnRsaPrivateKey_SignsWithRs256AndGivesItsPublicJwk_Rfc9449Section4Point2()
    {
        using var rsa = RSA.Create(2048);

        foreach (var pem in new[] { rsa.ExportRSAPrivateKeyPem(), rsa.ExportPkcs8PrivateKeyPem() })
        {
            var key = DpopKey.FromPem(pem);
            var jwk = Jwk(key);

            Assert.AreEqual("RS256", key.Algorithm);
            CollectionAssert.AreEqual(new[] { "e", "kty", "n" }, jwk.EnumerateObject().Select(member => member.Name).ToArray());
            Assert.AreEqual("RSA", jwk.GetProperty("kty").GetString());
            CollectionAssert.AreEqual(rsa.ExportParameters(false).Modulus, Base64Url.DecodeFromChars(jwk.GetProperty("n").GetString()));
            Assert.IsTrue(rsa.VerifyData("a.b"u8, key.Sign("a.b"u8.ToArray()), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
    }

    [TestMethod]
    public void FromPem_APublicKeyOnly_IsRefused_Rfc9449Section4Point2()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var rsa = RSA.Create(2048);

        Assert.Throws<CryptographicException>(() => DpopKey.FromPem(ecdsa.ExportSubjectPublicKeyInfoPem()));
        Assert.Throws<CryptographicException>(() => DpopKey.FromPem(rsa.ExportRSAPublicKeyPem()));
    }

    [TestMethod]
    public void FromPem_TextWithNoKey_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => DpopKey.FromPem("not a key"));
    }

    [TestMethod]
    public void CurveOf_ACurveJwsDoesNotName_IsRefused_Rfc7518Section3Point4()
    {
        Assert.ThrowsExactly<CryptographicException>(() => EcDpopKey.CurveOf("1.3.132.0.10"));
        Assert.ThrowsExactly<CryptographicException>(() => EcDpopKey.CurveOf(null));
    }

    internal static ECCurve CurveNamed(string crv) => crv switch
    {
        "P-256" => ECCurve.NamedCurves.nistP256,
        "P-384" => ECCurve.NamedCurves.nistP384,
        _ => ECCurve.NamedCurves.nistP521,
    };

    private static HashAlgorithmName HashOf(string algorithm) => algorithm switch
    {
        "ES256" => HashAlgorithmName.SHA256,
        "ES384" => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA512,
    };

    private static JsonElement Jwk(DpopKey key)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            key.WriteJwk(writer);
        }

        return JsonDocument.Parse(Encoding.UTF8.GetString(buffer.ToArray())).RootElement.Clone();
    }
}
