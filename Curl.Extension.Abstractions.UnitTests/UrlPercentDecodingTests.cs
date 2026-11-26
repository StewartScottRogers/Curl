namespace Curl.Extension.Abstractions;

/// <summary>Checks percent-decoding a URL part to bytes and to text (ADR-1429).</summary>
[TestClass]
public sealed class UrlPercentDecodingTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("%FF%00", "FF00")]
    [DataRow("k%E9", "6BE9")]
    [DataRow("a+b%20%2F", "612B62202F")]
    [DataRow("%zz%4", "257A7A2534")]
    [DataRow("é", "C3A9")]
    public void BytesOf_GivesEveryEscapesByte(string escaped, string hex) =>
        Assert.AreEqual(hex, Convert.ToHexString(UrlPercentDecoding.BytesOf(escaped)));

    [TestMethod]
    [DataRow("", "")]
    [DataRow("caf%C3%A9", "café")]
    [DataRow("a+%00", "a+\0")]
    [DataRow("100%", "100%")]
    public void Utf8TextOf_ValidUtf8_GivesTheText(string escaped, string text) =>
        Assert.AreEqual(text, UrlPercentDecoding.Utf8TextOf(escaped));

    [TestMethod]
    [DataRow("caf%E9")]
    [DataRow("b%FF")]
    [DataRow("%C0%AF")]
    [DataRow("%ED%A0%80")]
    public void Utf8TextOf_NotUtf8_GivesNull(string escaped) =>
        Assert.IsNull(UrlPercentDecoding.Utf8TextOf(escaped));

    [TestMethod]
    public void BytesOf_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlPercentDecoding.BytesOf(null!));
}
