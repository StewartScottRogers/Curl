using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.Dpop.UnitTests;

[TestClass]
public sealed class DpopProofTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_AnyRequest_GivesAJwsWhoseHeaderIsTypedDpopJwtWithTheAlgAndPublicJwk_Rfc9449Section4Point2()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var parts = DpopProof.Create(DpopKey.FromPem(ecdsa.ExportECPrivateKeyPem()), Request()).Split('.');
        var header = Decode(parts[0]);

        Assert.HasCount(3, parts);
        CollectionAssert.AreEqual(new[] { "typ", "alg", "jwk" }, header.EnumerateObject().Select(member => member.Name).ToArray());
        Assert.AreEqual("dpop+jwt", header.GetProperty("typ").GetString());
        Assert.AreEqual("ES256", header.GetProperty("alg").GetString());
        Assert.AreEqual("EC", header.GetProperty("jwk").GetProperty("kty").GetString());
        Assert.IsFalse(header.GetProperty("jwk").TryGetProperty("d", out _));
        Assert.DoesNotContain("=", parts[0] + parts[1] + parts[2]);
    }

    [TestMethod]
    public void Create_AnyRequest_SignsTheHeaderAndPayloadWithTheJwksKey_Rfc9449Section4Point3()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var parts = DpopProof.Create(DpopKey.FromPem(ecdsa.ExportECPrivateKeyPem()), Request()).Split('.');

        var jwk = Decode(parts[0]).GetProperty("jwk");
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64Url.DecodeFromChars(jwk.GetProperty("x").GetString()), Y = Base64Url.DecodeFromChars(jwk.GetProperty("y").GetString()) },
        });
        Assert.IsTrue(verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256));
    }

    [TestMethod]
    public void Create_ARequestWithoutTokenOrNonce_ClaimsJtiHtmHtuAndIatOnly_Rfc9449Section4Point2()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var payload = Decode(DpopProof.Create(DpopKey.FromPem(ecdsa.ExportECPrivateKeyPem()), Request()).Split('.')[1]);

        Assert.AreEqual("{\"jti\":\"id-1\",\"htm\":\"POST\",\"htu\":\"https://server.example.com/token\",\"iat\":1793275200}", payload.GetRawText());
    }

    [TestMethod]
    public void Create_ARequestWithTokenAndNonce_ClaimsAthAndNonce_Rfc9449Sections4Point2And8()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var payload = Decode(DpopProof.Create(DpopKey.FromPem(ecdsa.ExportECPrivateKeyPem()), Request() with { AccessToken = "Kz~8mXK1EalYznwH-LC-1fBAo.4Ljp~zsPE_NeO.gxU", Nonce = "eyJ7S_zG.eyJH0-Z.HX4w-7v" }).Split('.')[1]);

        Assert.AreEqual("fUHyO2r2Z3DZ53EsNrWBb0xWXoaNy59IiKCAqksmQEo", payload.GetProperty("ath").GetString());
        Assert.AreEqual("eyJ7S_zG.eyJH0-Z.HX4w-7v", payload.GetProperty("nonce").GetString());
    }

    [TestMethod]
    public void AccessTokenHashOf_TheRfcsExampleToken_GivesTheRfcsAth_Rfc9449Section7Point1()
    {
        Assert.AreEqual("fUHyO2r2Z3DZ53EsNrWBb0xWXoaNy59IiKCAqksmQEo", DpopProof.AccessTokenHashOf("Kz~8mXK1EalYznwH-LC-1fBAo.4Ljp~zsPE_NeO.gxU"));
    }

    internal static JsonElement Decode(string part) => JsonDocument.Parse(Base64Url.DecodeFromChars(part)).RootElement.Clone();

    private static DpopRequest Request() => new("id-1", "POST", "https://server.example.com/token", IssuedAt, null, null);
}
