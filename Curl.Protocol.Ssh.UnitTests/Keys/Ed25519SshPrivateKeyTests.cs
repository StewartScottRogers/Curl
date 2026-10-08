using System.Security.Cryptography;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="Ed25519SshPrivateKey" /> on RFC 8032 section 7.1's first test: the
/// RFC 8709 blob, the signature blob byte for byte (Ed25519 signatures are deterministic),
/// and the fields it refuses.
/// </summary>
[TestClass]
public sealed class Ed25519SshPrivateKeyTests
{
    private static readonly byte[] Seed = Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");

    private static readonly byte[] PublicKey = Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");

    private static readonly byte[] EmptyMessageSignature = Convert.FromHexString(
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromSeed_Rfc8032Test1_WritesTheSshEd25519Blob()
    {
        Diagnostics.Arrange("seed", Convert.ToHexString(Seed));

        Ed25519SshPrivateKey key = Ed25519SshPrivateKey.FromSeed(Seed);

        Diagnostics.ActKey(key);
        Diagnostics.Assert("key type", "ssh-ed25519", key.KeyType);
        Diagnostics.AssertBytes("public key blob", Join(Name("ssh-ed25519"), String(PublicKey)), key.PublicKeyBlob);
        Assert.AreEqual("ssh-ed25519", key.KeyType);
        CollectionAssert.AreEqual(Join(Name("ssh-ed25519"), String(PublicKey)), key.PublicKeyBlob);
    }

    [TestMethod]
    public void Sign_Rfc8032Test1EmptyMessage_WritesTheRfcSignatureInABlob()
    {
        Ed25519SshPrivateKey key = Ed25519SshPrivateKey.FromSeed(Seed);
        Diagnostics.Arrange("seed", Convert.ToHexString(Seed));
        Diagnostics.Arrange("message", "(empty)");

        byte[] blob = key.Sign("ssh-ed25519", []);

        Diagnostics.ActBytes("signature blob", blob);
        Diagnostics.AssertBytes("signature blob", Join(Name("ssh-ed25519"), String(EmptyMessageSignature)), blob);
        CollectionAssert.AreEqual(Join(Name("ssh-ed25519"), String(EmptyMessageSignature)), blob);
    }

    [TestMethod]
    public void FromOpenSshFields_MatchingFields_ReadsTheKey()
    {
        Diagnostics.Arrange("public key", Convert.ToHexString(PublicKey));
        Diagnostics.Arrange("private key", "seed || public key");

        Ed25519SshPrivateKey key = Ed25519SshPrivateKey.FromOpenSshFields(PublicKey, [.. Seed, .. PublicKey]);

        Diagnostics.ActKey(key);
        Diagnostics.AssertBytes("public key blob", Join(Name("ssh-ed25519"), String(PublicKey)), key.PublicKeyBlob);
        CollectionAssert.AreEqual(Join(Name("ssh-ed25519"), String(PublicKey)), key.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow(31, DisplayName = "short")]
    [DataRow(33, DisplayName = "long")]
    public void FromSeed_NotThirtyTwoBytes_Throws(int length)
    {
        Diagnostics.Arrange("seed length", length);

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromSeed(new byte[length]));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void FromOpenSshFields_PrivateKeyNotSixtyFourBytes_Throws()
    {
        Diagnostics.Arrange("private key length", Seed.Length);

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromOpenSshFields(PublicKey, Seed));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void FromOpenSshFields_PublicKeyNotTheSeeds_Throws()
    {
        byte[] other = new byte[32];
        Diagnostics.Arrange("public key", Convert.ToHexString(other));

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromOpenSshFields(other, [.. Seed, .. other]));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void FromOpenSshFields_PrivateKeysCopyOfThePublicKeyDiffers_Throws()
    {
        Diagnostics.Arrange("private key", "seed || 32 zero bytes");

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromOpenSshFields(PublicKey, [.. Seed, .. new byte[32]]));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }
}
