using Curl.Testing;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins the lenient decoding libssh2 1.11.1 applies to a hashed known-hosts name.
/// </summary>
[TestClass]
public sealed class Libssh2Base64Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("TWFu", "4D616E")]
    [DataRow("TWE=", "4D61")]
    [DataRow("TWE", "4D61")]
    [DataRow("TQ==", "4D")]
    [DataRow("T W\tF*u", "4D616E")]
    [DataRow("", "")]
    public void TryDecode_SkipsWhatIsNotInTheAlphabet(string text, string expectedHex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", text.Replace("\t", "\\t", StringComparison.Ordinal));

        bool decoded = Libssh2Base64.TryDecode(text, out byte[]? bytes);

        diagnostics.Act("decoded", decoded);
        diagnostics.Bytes("bytes", bytes ?? []);
        diagnostics.Assert("decoded", true, decoded);
        diagnostics.Diff("hex", expectedHex, bytes is null ? "(null)" : Convert.ToHexString(bytes));
        Assert.IsTrue(decoded);
        Assert.AreEqual(expectedHex, Convert.ToHexString(bytes!));
    }

    [TestMethod]
    [DataRow("T")]
    [DataRow("TWFuT")]
    [DataRow("T===")]
    public void TryDecode_ALoneLeftoverCharacter_Fails(string text)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", text);

        bool decoded = Libssh2Base64.TryDecode(text, out byte[]? bytes);

        diagnostics.Act("decoded", decoded);
        diagnostics.Act("bytes", bytes is null ? "(null)" : Convert.ToHexString(bytes));
        diagnostics.Assert("decoded", false, decoded);
        diagnostics.Assert("bytes", "(null)", bytes is null ? "(null)" : Convert.ToHexString(bytes));
        Assert.IsFalse(decoded);
        Assert.IsNull(bytes);
    }

    [TestMethod]
    public void TryDecode_ALongText_DecodesEveryByte()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] original = [.. Enumerable.Range(0, 300).Select(value => (byte)value)];
        diagnostics.Arrange("original", "the 300 bytes 00, 01, .. ff, 00, .. 2b");
        diagnostics.Bytes("original", original);

        bool decoded = Libssh2Base64.TryDecode(Convert.ToBase64String(original), out byte[]? bytes);

        diagnostics.Act("decoded", decoded);
        diagnostics.Assert("decoded", true, decoded);
        diagnostics.Diff("bytes", original, bytes ?? []);
        Assert.IsTrue(decoded);
        CollectionAssert.AreEqual(original, bytes);
    }
}
