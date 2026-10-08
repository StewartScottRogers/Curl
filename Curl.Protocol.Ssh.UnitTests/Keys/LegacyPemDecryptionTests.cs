using System.Security.Cryptography;
using System.Text;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decrypt_NoDekInfo_ReturnsTheBodyAsItIs()
    {
        PemBlock block = new("RSA PRIVATE KEY", new Dictionary<string, string> { ["Proc-Type"] = "4,ENCRYPTED" }, [1, 2, 3]);
        Diagnostics.Arrange("headers", "Proc-Type: 4,ENCRYPTED, no DEK-Info");
        Diagnostics.Bytes("body", block.Body);

        byte[] decrypted = LegacyPemDecryption.Decrypt(block, Secret);

        Diagnostics.ActBytes("decrypted", decrypted);
        Diagnostics.AssertBytes("decrypted", [1, 2, 3], decrypted);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, decrypted);
    }

    [TestMethod]
    [DataRow("AES-128-CBC", DisplayName = "no IV")]
    [DataRow("AES-128-CBC,0011", DisplayName = "IV shorter than the salt")]
    [DataRow("CAMELLIA-128-CBC,00112233445566778899AABBCCDDEEFF", DisplayName = "a cipher OpenSSL does not write for keys")]
    public void Decrypt_UnusableDekInfo_Throws(string dekInfo)
    {
        PemBlock block = new("RSA PRIVATE KEY", new Dictionary<string, string> { ["DEK-Info"] = dekInfo }, new byte[16]);
        Diagnostics.Arrange("DEK-Info", dekInfo);

        var failure = Assert.ThrowsExactly<CryptographicException>(() => LegacyPemDecryption.Decrypt(block, Secret));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void Decrypt_IvNotHexadecimal_Throws()
    {
        PemBlock block = new("RSA PRIVATE KEY", new Dictionary<string, string> { ["DEK-Info"] = "AES-128-CBC,XYZ" }, new byte[16]);
        Diagnostics.Arrange("DEK-Info", block.Headers["DEK-Info"]);

        var failure = Assert.ThrowsExactly<FormatException>(() => LegacyPemDecryption.Decrypt(block, Secret));

        Diagnostics.ActAndAssertThrown(nameof(FormatException), failure);
    }
}
