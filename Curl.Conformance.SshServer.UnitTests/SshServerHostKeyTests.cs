using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerHostKeyTests
{
    [TestMethod]
    public void ComputeMd5Fingerprint_FixedHostKey_IsThePinnedValue()
    {
        string fingerprint = SshServerHostKey.ComputeMd5Fingerprint();

        Assert.AreEqual(SshServerHostKey.Md5Fingerprint, fingerprint);
    }

    [TestMethod]
    public void ComputeSha256Fingerprint_FixedHostKey_IsThePinnedValue()
    {
        string fingerprint = SshServerHostKey.ComputeSha256Fingerprint();

        Assert.AreEqual(SshServerHostKey.Sha256Fingerprint, fingerprint);
    }

    [TestMethod]
    public void Sign_ExchangeHash_VerifiesWithTheBlobsPublicKey()
    {
        byte[] exchangeHash = [1, 2, 3, 4];

        SshWireReader signature = new(SshServerHostKey.Sign(exchangeHash));
        SshWireReader blob = new(SshServerHostKey.Blob);

        Assert.AreEqual(SshServerHostKey.Algorithm, signature.ReadName());
        Assert.AreEqual(SshServerHostKey.Algorithm, blob.ReadName());
        Assert.IsTrue(Ed25519.Verify(blob.ReadString().Span, exchangeHash, signature.ReadString().Span));
    }
}
