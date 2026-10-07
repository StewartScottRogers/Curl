using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Transport;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="EcdsaSshPrivateKey" />: the RFC 5656 blob, a signature of two
/// <c>mpint</c>s that verifies (ECDSA signatures are randomized, so they cannot be pinned
/// byte for byte), and the curves and points it refuses.
/// </summary>
[TestClass]
public sealed class EcdsaSshPrivateKeyTests
{
    private static readonly byte[] Data = Encoding.ASCII.GetBytes("session identifier and request");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Sign_P256_WritesTwoMpintsThatVerify()
    {
        SshPrivateKey key = SshPrivateKeyReader.Read(TestUserKeys.EcdsaP256Sec1, [])!;
        Diagnostics.Arrange("key", "TestUserKeys.EcdsaP256Sec1");
        Diagnostics.Bytes("data", Data);

        byte[] blob = key.Sign("ecdsa-sha2-nistp256", Data);

        Diagnostics.ActBytes("signature blob", blob);
        SshWireReader reader = new(blob);
        Assert.AreEqual("ecdsa-sha2-nistp256", reader.ReadName());
        SshWireReader values = new(reader.ReadString());
        byte[] r = values.ReadMpint().ToArray();
        byte[] s = values.ReadMpint().ToArray();
        Diagnostics.Act("r", Convert.ToHexString(r));
        Diagnostics.Act("s", Convert.ToHexString(s));
        Diagnostics.Assert("bytes after s", 0, values.RemainingLength);
        Assert.AreEqual(0, values.RemainingLength);
        byte[] ieee = new byte[64];
        r.CopyTo(ieee, 32 - r.Length);
        s.CopyTo(ieee, 64 - s.Length);
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(TestUserKeys.EcdsaP256Sec1);
        Assert.IsTrue(ecdsa.VerifyData(Data, ieee, HashAlgorithmName.SHA256));
    }

    [TestMethod]
    public void FromCurveOid_Secp256k1_ReturnsNull()
    {
        Diagnostics.Arrange("curve OID", "1.3.132.0.10 (secp256k1)");

        EcdsaSshPrivateKey? key = EcdsaSshPrivateKey.FromCurveOid("1.3.132.0.10", [1], null);

        Diagnostics.ActKey(key);
        Diagnostics.Assert("key", "(none)", key?.KeyType ?? "(none)");
        Assert.IsNull(key);
    }

    [TestMethod]
    public void FromCurveIdentifier_UnknownCurve_ReturnsNull()
    {
        Diagnostics.Arrange("curve identifier", "nistp192");

        EcdsaSshPrivateKey? key = EcdsaSshPrivateKey.FromCurveIdentifier("nistp192", [1], [4]);

        Diagnostics.ActKey(key);
        Diagnostics.Assert("key", "(none)", key?.KeyType ?? "(none)");
        Assert.IsNull(key);
    }

    [TestMethod]
    [DataRow("02", DisplayName = "compressed point")]
    [DataRow("04", DisplayName = "too short")]
    public void FromCurveIdentifier_PointNotUncompressedOnTheCurve_Throws(string prefixHex)
    {
        byte[] point = [.. Convert.FromHexString(prefixHex), .. new byte[prefixHex == "02" ? 64 : 10]];
        Diagnostics.Arrange("curve identifier", "nistp256");
        Diagnostics.Bytes("point", point);

        var failure = Assert.ThrowsExactly<CryptographicException>(() => EcdsaSshPrivateKey.FromCurveIdentifier("nistp256", [1], point));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void FromCurveIdentifier_PrivateKeyLongerThanTheCurve_Throws()
    {
        Diagnostics.Arrange("private key length", 33);

        var failure = Assert.ThrowsExactly<CryptographicException>(() => EcdsaSshPrivateKey.FromCurveIdentifier("nistp256", new byte[33], new byte[65]));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }
}
