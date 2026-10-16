using System.Text;

namespace Curl.Mcp;

[TestClass]
public sealed class McpReturnedBytesTests
{
    [TestMethod]
    public void Encode_ValidUtf8_IsReturnedAsText()
    {
        McpReturnedBytes returned = McpReturnedBytes.Encode("héllo €"u8, new McpResponseCap(100));

        Assert.AreEqual("héllo €", returned.Content);
        Assert.IsFalse(returned.IsBase64);
        Assert.IsFalse(returned.IsTruncated);
        Assert.IsNull(returned.TruncationNote);
        Assert.AreEqual(10, returned.TotalBytes);
        Assert.AreEqual(10, returned.ReturnedBytes);
    }

    [TestMethod]
    public void Encode_InvalidUtf8_IsReturnedAsBase64()
    {
        byte[] output = [0x89, 0x50, 0x4E, 0x47, 0xFF];

        McpReturnedBytes returned = McpReturnedBytes.Encode(output, new McpResponseCap(100));

        Assert.IsTrue(returned.IsBase64);
        CollectionAssert.AreEqual(output, Convert.FromBase64String(returned.Content));
        Assert.IsFalse(returned.IsTruncated);
    }

    [TestMethod]
    public void Encode_ExactlyAtCap_IsNotTruncated()
    {
        McpReturnedBytes returned = McpReturnedBytes.Encode("abcd"u8, new McpResponseCap(4));

        Assert.AreEqual("abcd", returned.Content);
        Assert.IsFalse(returned.IsTruncated);
        Assert.IsNull(returned.TruncationNote);
    }

    [TestMethod]
    public void Encode_OneByteOverCap_IsTruncatedWithANoteStatingBothSizes()
    {
        McpReturnedBytes returned = McpReturnedBytes.Encode("abcde"u8, new McpResponseCap(4));

        Assert.AreEqual("abcd", returned.Content);
        Assert.IsTrue(returned.IsTruncated);
        Assert.AreEqual(5, returned.TotalBytes);
        Assert.AreEqual(4, returned.ReturnedBytes);
        Assert.AreEqual(
            "Output truncated: returned 4 of 5 bytes (CURL_MCP_MAX_RESPONSE_BYTES caps it).",
            returned.TruncationNote);
    }

    [TestMethod]
    [DataRow("ab€cd", 3, "ab")]
    [DataRow("ab€cd", 4, "ab")]
    [DataRow("ab€cd", 5, "ab€")]
    [DataRow("a😀z", 2, "a")]
    [DataRow("a😀z", 4, "a")]
    [DataRow("a😀z", 5, "a😀")]
    [DataRow("é", 1, "")]
    public void Encode_CapInsideAMultiByteCharacter_CutsBeforeIt(string text, int maxBytes, string expected)
    {
        byte[] output = Encoding.UTF8.GetBytes(text);

        McpReturnedBytes returned = McpReturnedBytes.Encode(output, new McpResponseCap(maxBytes));

        Assert.IsFalse(returned.IsBase64);
        Assert.AreEqual(expected, returned.Content);
        Assert.AreEqual(Encoding.UTF8.GetByteCount(expected), returned.ReturnedBytes);
        Assert.IsTrue(returned.IsTruncated);
    }

    [TestMethod]
    public void Encode_InvalidUtf8OverCap_IsCutAtTheCapExactlyAsBase64()
    {
        byte[] output = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80];

        McpReturnedBytes returned = McpReturnedBytes.Encode(output, new McpResponseCap(4));

        Assert.IsTrue(returned.IsBase64);
        CollectionAssert.AreEqual(output[..4], Convert.FromBase64String(returned.Content));
        Assert.AreEqual(4, returned.ReturnedBytes);
        Assert.AreEqual(6, returned.TotalBytes);
    }
}
