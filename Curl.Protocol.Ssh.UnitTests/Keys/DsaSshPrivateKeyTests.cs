using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Transport;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="DsaSshPrivateKey" /> with RFC 6979's key, whose signatures are
/// deterministic: the <c>ssh-dss</c> blob is r || s over SHA-1.
/// </summary>
[TestClass]
public sealed class DsaSshPrivateKeyTests
{
    // RFC 6979 section 3.2's deterministic k over SHA-1, checked below by DsaSignature.VerifyHash.
    private const string SshDssBlob = "000000077373682D64737300000028907E94DCA475E01D88AA7C8B0A6651CD25AF7F9475F0A974BFAFD6F706EEB2C45608FAD549AB3227";

    private static readonly byte[] Data = Encoding.ASCII.GetBytes("session identifier and request");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Sign_SshDss_PinsTheSignatureBlob()
    {
        DsaSshPrivateKey key = DsaSshPrivateKey.Create(TestDsaKey.Prime, TestDsaKey.Subprime, TestDsaKey.Generator, TestDsaKey.PublicKey, TestDsaKey.PrivateKey);
        Diagnostics.Arrange("key", "RFC 6979 section 3.2 DSA key, y given");
        Diagnostics.Bytes("data", Data);

        byte[] blob = key.Sign("ssh-dss", Data);

        Diagnostics.ActBytes("signature blob", blob);
        Diagnostics.AssertBytes("signature blob", Convert.FromHexString(SshDssBlob), blob);
        Assert.AreEqual(SshDssBlob, Convert.ToHexString(blob));
        SshWireReader reader = new(blob);
        Assert.AreEqual("ssh-dss", reader.ReadName());
        byte[] signature = reader.ReadString().ToArray();
        Diagnostics.Assert("r || s length", 40, signature.Length);
        Assert.HasCount(40, signature);
        Assert.IsTrue(DsaSignature.VerifyHash(TestDsaKey.Prime, TestDsaKey.Subprime, TestDsaKey.Generator, TestDsaKey.PublicKey, SHA1.HashData(Data), signature));
    }

    [TestMethod]
    public void Create_WithoutY_ComputesItAsGToTheXModP()
    {
        Diagnostics.Arrange("key", "RFC 6979 section 3.2 DSA key, y left out");

        DsaSshPrivateKey key = DsaSshPrivateKey.Create(TestDsaKey.Prime, TestDsaKey.Subprime, TestDsaKey.Generator, null, TestDsaKey.PrivateKey);

        Diagnostics.ActKey(key);
        Diagnostics.Assert("key type", "ssh-dss", key.KeyType);
        Diagnostics.AssertBytes("public key blob", TestDsaKey.PublicKeyBlob, key.PublicKeyBlob);
        Assert.AreEqual("ssh-dss", key.KeyType);
        CollectionAssert.AreEqual(TestDsaKey.PublicKeyBlob, key.PublicKeyBlob);
    }

    [TestMethod]
    public void Create_XOutsideTheSubgroup_Throws()
    {
        Diagnostics.Arrange("x", "q, outside the subgroup");

        var failure = Assert.ThrowsExactly<ArgumentException>(() => DsaSshPrivateKey.Create(TestDsaKey.Prime, TestDsaKey.Subprime, TestDsaKey.Generator, null, TestDsaKey.Subprime));

        Diagnostics.ActAndAssertThrown(nameof(ArgumentException), failure);
    }
}
