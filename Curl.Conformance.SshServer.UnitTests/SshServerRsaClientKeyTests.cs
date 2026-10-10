using System.Security.Cryptography;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerRsaClientKeyTests
{
    [TestMethod]
    public void PrivateKeyPem_FixedClientKey_IsAPkcs1RsaPrivateKeyFile()
    {
        string pem = SshServerRsaClientKey.PrivateKeyPem;

        Assert.StartsWith("-----BEGIN RSA PRIVATE KEY-----\n", pem);
        Assert.EndsWith("-----END RSA PRIVATE KEY-----\n", pem);
    }

    [TestMethod]
    public void PublicKeyLine_FixedClientKey_HoldsTheBlobOfThePrivateKeysPublicHalf()
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(SshServerRsaClientKey.PrivateKeyPem);
        string[] fields = SshServerRsaClientKey.PublicKeyLine.Split(' ');

        Assert.AreEqual(SshServerRsaClientKey.KeyType, fields[0]);
        CollectionAssert.AreEqual(SshServerRsaClientKey.PublicKeyBlob, Convert.FromBase64String(fields[1]));
        CollectionAssert.AreEqual(rsa.ExportParameters(false).Modulus, SshServerRsaClientKey.PublicKeyBlob[^256..]);
    }
}
