using System.Security.Cryptography;
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

    [TestMethod]
    public void FromSeed_Rfc8032Test1_WritesTheSshEd25519Blob()
    {
        Ed25519SshPrivateKey key = Ed25519SshPrivateKey.FromSeed(Seed);

        Assert.AreEqual("ssh-ed25519", key.KeyType);
        CollectionAssert.AreEqual(Join(Name("ssh-ed25519"), String(PublicKey)), key.PublicKeyBlob);
    }

    [TestMethod]
    public void Sign_Rfc8032Test1EmptyMessage_WritesTheRfcSignatureInABlob()
    {
        Ed25519SshPrivateKey key = Ed25519SshPrivateKey.FromSeed(Seed);

        byte[] blob = key.Sign("ssh-ed25519", []);

        CollectionAssert.AreEqual(Join(Name("ssh-ed25519"), String(EmptyMessageSignature)), blob);
    }

    [TestMethod]
    public void FromOpenSshFields_MatchingFields_ReadsTheKey()
    {
        Ed25519SshPrivateKey key = Ed25519SshPrivateKey.FromOpenSshFields(PublicKey, [.. Seed, .. PublicKey]);

        CollectionAssert.AreEqual(Join(Name("ssh-ed25519"), String(PublicKey)), key.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow(31, DisplayName = "short")]
    [DataRow(33, DisplayName = "long")]
    public void FromSeed_NotThirtyTwoBytes_Throws(int length)
    {
        Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromSeed(new byte[length]));
    }

    [TestMethod]
    public void FromOpenSshFields_PrivateKeyNotSixtyFourBytes_Throws()
    {
        Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromOpenSshFields(PublicKey, Seed));
    }

    [TestMethod]
    public void FromOpenSshFields_PublicKeyNotTheSeeds_Throws()
    {
        byte[] other = new byte[32];

        Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromOpenSshFields(other, [.. Seed, .. other]));
    }

    [TestMethod]
    public void FromOpenSshFields_PrivateKeysCopyOfThePublicKeyDiffers_Throws()
    {
        Assert.ThrowsExactly<CryptographicException>(() => Ed25519SshPrivateKey.FromOpenSshFields(PublicKey, [.. Seed, .. new byte[32]]));
    }
}
