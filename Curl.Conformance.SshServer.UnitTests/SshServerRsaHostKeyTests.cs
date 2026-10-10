using System.Security.Cryptography;
using Curl.Protocol.Ssh.HostKeys;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerRsaHostKeyTests
{
    [TestMethod]
    public void ComputeMd5Fingerprint_FixedHostKey_IsThePinnedValue()
    {
        string fingerprint = SshServerRsaHostKey.ComputeMd5Fingerprint();

        Assert.AreEqual(SshServerRsaHostKey.Md5Fingerprint, fingerprint);
    }

    [TestMethod]
    public void ComputeSha256Fingerprint_FixedHostKey_IsThePinnedValue()
    {
        string fingerprint = SshServerRsaHostKey.ComputeSha256Fingerprint();

        Assert.AreEqual(SshServerRsaHostKey.Sha256Fingerprint, fingerprint);
    }

    [TestMethod]
    [DataRow("rsa-sha2-512", "SHA512")]
    [DataRow("rsa-sha2-256", "SHA256")]
    [DataRow("ssh-rsa", "SHA1")]
    public void Sign_ExchangeHash_VerifiesWithTheBlobsPublicKey(string algorithm, string hashName)
    {
        byte[] exchangeHash = [1, 2, 3, 4];

        byte[] signature = SshServerRsaHostKey.Sign(algorithm, exchangeHash);

        Assert.IsTrue(new RsaSshSignatureVerifier(algorithm, new HashAlgorithmName(hashName)).Verify(SshServerRsaHostKey.Blob, signature, exchangeHash));
    }
}
