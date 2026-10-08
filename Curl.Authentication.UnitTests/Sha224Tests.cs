using System.Text;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="Sha224" /> to the NIST example values for SHA-224 and to OpenSSL's hash
/// of 55 zero bytes, the longest message one block holds.
/// </summary>
[TestClass]
public sealed class Sha224Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("", "d14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f", DisplayName = "Empty")]
    [DataRow("abc", "23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7", DisplayName = "One block")]
    [DataRow(
        "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq",
        "75388b16512776cc5dba5da1fd890150b0c6455cb4f58b1952522525",
        DisplayName = "56 bytes: padding needs a second block")]
    public void HashData_NistExample_GivesTheExampleHash(string message, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] input = Encoding.ASCII.GetBytes(message);
        diagnostics.Arrange("message length", input.Length);
        diagnostics.Bytes("message", input);

        byte[] hash = Sha224.HashData(input);

        string actual = Convert.ToHexStringLower(hash);
        diagnostics.Act("hash", actual);
        diagnostics.Diff("hash", expected, actual);
        diagnostics.Assert("hash", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void HashData_55ZeroBytes_FitsOneBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] input = new byte[55];
        diagnostics.Arrange("message length", input.Length);
        diagnostics.Bytes("message", input);

        byte[] hash = Sha224.HashData(input);

        string actual = Convert.ToHexStringLower(hash);
        const string expected = "7142c3964c75895cc3d1bbdfc851e167a7fdbf2e0c0f2e7212bfd9f5";
        diagnostics.Act("hash", actual);
        diagnostics.Diff("hash", expected, actual);
        diagnostics.Assert("hash", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
