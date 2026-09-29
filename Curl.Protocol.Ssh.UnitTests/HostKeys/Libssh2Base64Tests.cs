namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins the lenient decoding libssh2 1.11.1 applies to a hashed known-hosts name.
/// </summary>
[TestClass]
public sealed class Libssh2Base64Tests
{
    [TestMethod]
    [DataRow("TWFu", "4D616E")]
    [DataRow("TWE=", "4D61")]
    [DataRow("TWE", "4D61")]
    [DataRow("TQ==", "4D")]
    [DataRow("T W\tF*u", "4D616E")]
    [DataRow("", "")]
    public void TryDecode_SkipsWhatIsNotInTheAlphabet(string text, string expectedHex)
    {
        Assert.IsTrue(Libssh2Base64.TryDecode(text, out byte[]? bytes));
        Assert.AreEqual(expectedHex, Convert.ToHexString(bytes!));
    }

    [TestMethod]
    [DataRow("T")]
    [DataRow("TWFuT")]
    [DataRow("T===")]
    public void TryDecode_ALoneLeftoverCharacter_Fails(string text)
    {
        Assert.IsFalse(Libssh2Base64.TryDecode(text, out byte[]? bytes));
        Assert.IsNull(bytes);
    }

    [TestMethod]
    public void TryDecode_ALongText_DecodesEveryByte()
    {
        byte[] original = [.. Enumerable.Range(0, 300).Select(value => (byte)value)];

        Assert.IsTrue(Libssh2Base64.TryDecode(Convert.ToBase64String(original), out byte[]? bytes));
        CollectionAssert.AreEqual(original, bytes);
    }
}
