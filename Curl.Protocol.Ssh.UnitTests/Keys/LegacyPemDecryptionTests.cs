using System.Security.Cryptography;
using System.Text;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="LegacyPemDecryption" />'s handling of the <c>DEK-Info</c> header; the
/// ciphers themselves are pinned by <see cref="SshPrivateKeyReaderTests" /> against files
/// OpenSSL encrypted.
/// </summary>
[TestClass]
public sealed class LegacyPemDecryptionTests
{
    private static readonly byte[] Secret = Encoding.UTF8.GetBytes("secret");

    [TestMethod]
    public void Decrypt_NoDekInfo_ReturnsTheBodyAsItIs()
    {
        PemBlock block = new("RSA PRIVATE KEY", new Dictionary<string, string> { ["Proc-Type"] = "4,ENCRYPTED" }, [1, 2, 3]);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, LegacyPemDecryption.Decrypt(block, Secret));
    }

    [TestMethod]
    [DataRow("AES-128-CBC", DisplayName = "no IV")]
    [DataRow("AES-128-CBC,0011", DisplayName = "IV shorter than the salt")]
    [DataRow("CAMELLIA-128-CBC,00112233445566778899AABBCCDDEEFF", DisplayName = "a cipher OpenSSL does not write for keys")]
    public void Decrypt_UnusableDekInfo_Throws(string dekInfo)
    {
        PemBlock block = new("RSA PRIVATE KEY", new Dictionary<string, string> { ["DEK-Info"] = dekInfo }, new byte[16]);

        Assert.ThrowsExactly<CryptographicException>(() => LegacyPemDecryption.Decrypt(block, Secret));
    }

    [TestMethod]
    public void Decrypt_IvNotHexadecimal_Throws()
    {
        PemBlock block = new("RSA PRIVATE KEY", new Dictionary<string, string> { ["DEK-Info"] = "AES-128-CBC,XYZ" }, new byte[16]);

        Assert.ThrowsExactly<FormatException>(() => LegacyPemDecryption.Decrypt(block, Secret));
    }
}
