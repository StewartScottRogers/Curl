using System.Text;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Sha224" /> to FIPS 180-4's examples (NIST's "SHA224.pdf" of the
/// Cryptographic Standards and Guidelines examples: one block and two blocks) and to
/// HMAC-SHA-224 in RFC 4231 section 4.2, test case 1, through <see cref="FixedBlockHmac" />.
/// </summary>
[TestClass]
public sealed class Sha224Tests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("abc", "23097D223405D8228642A477BDA255B32AADBCE4BDA0B3F7E36C9DA7")]
    [DataRow("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "75388B16512776CC5DBA5DA1FD890150B0C6455CB4F58B1952522525")]
    public void Hash_FipsExample_GivesPublishedDigest(string message, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] data = Encoding.ASCII.GetBytes(message);
        byte[] digest = new byte[Sha224.HashSize];
        diagnostics.Arrange("source", "FIPS 180-4 SHA-224 example (NIST SHA224.pdf)");
        diagnostics.Bytes("message", data);

        FixedBlockMerkleDamgard<Sha224>.Hash([], [], data, data.Length, data.Length, digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", expected, Convert.ToHexString(digest));
        Assert.AreEqual(expected, Convert.ToHexString(digest));
    }

    [TestMethod]
    public void Hmac_Rfc4231TestCase1_GivesPublishedMac()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] key = new byte[20];
        Array.Fill(key, (byte)0x0b);
        byte[] data = Encoding.ASCII.GetBytes("Hi There");
        byte[] mac = new byte[Sha224.HashSize];
        diagnostics.Arrange("source", "RFC 4231 section 4.2, test case 1 (HMAC-SHA-224)");
        diagnostics.Bytes("key", key);
        diagnostics.Bytes("data", data);

        FixedBlockHmac.Compute<Sha224>(key, [], data, data.Length, data.Length, mac);
        diagnostics.Act("mac", Convert.ToHexString(mac));

        diagnostics.Diff("mac", "896FB1128ABBDF196832107CD49DF33F47B4B1169912BA4F53684B22", Convert.ToHexString(mac));
        Assert.AreEqual("896FB1128ABBDF196832107CD49DF33F47B4B1169912BA4F53684B22", Convert.ToHexString(mac));
    }
}
