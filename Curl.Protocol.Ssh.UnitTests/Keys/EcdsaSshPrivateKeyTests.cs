using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Transport;

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

    [TestMethod]
    public void Sign_P256_WritesTwoMpintsThatVerify()
    {
        SshPrivateKey key = SshPrivateKeyReader.Read(TestUserKeys.EcdsaP256Sec1, [])!;

        byte[] blob = key.Sign("ecdsa-sha2-nistp256", Data);

        SshWireReader reader = new(blob);
        Assert.AreEqual("ecdsa-sha2-nistp256", reader.ReadName());
        SshWireReader values = new(reader.ReadString());
        byte[] r = values.ReadMpint().ToArray();
        byte[] s = values.ReadMpint().ToArray();
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
        Assert.IsNull(EcdsaSshPrivateKey.FromCurveOid("1.3.132.0.10", [1], null));
    }

    [TestMethod]
    public void FromCurveIdentifier_UnknownCurve_ReturnsNull()
    {
        Assert.IsNull(EcdsaSshPrivateKey.FromCurveIdentifier("nistp192", [1], [4]));
    }

    [TestMethod]
    [DataRow("02", DisplayName = "compressed point")]
    [DataRow("04", DisplayName = "too short")]
    public void FromCurveIdentifier_PointNotUncompressedOnTheCurve_Throws(string prefixHex)
    {
        byte[] point = [.. Convert.FromHexString(prefixHex), .. new byte[prefixHex == "02" ? 64 : 10]];

        Assert.ThrowsExactly<CryptographicException>(() => EcdsaSshPrivateKey.FromCurveIdentifier("nistp256", [1], point));
    }

    [TestMethod]
    public void FromCurveIdentifier_PrivateKeyLongerThanTheCurve_Throws()
    {
        Assert.ThrowsExactly<CryptographicException>(() => EcdsaSshPrivateKey.FromCurveIdentifier("nistp256", new byte[33], new byte[65]));
    }
}
