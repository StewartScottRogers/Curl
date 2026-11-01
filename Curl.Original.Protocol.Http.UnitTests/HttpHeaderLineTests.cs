namespace Curl.Original.Protocol.Http.UnitTests;

[TestClass]
public sealed class HttpHeaderLineTests
{
    [TestMethod]
    [DataRow("Transfer-Encoding: gzip, chunked", "Transfer-Encoding:", "chunked", true)]
    [DataRow("transfer-ENCODING: gzip,CHUNKED", "Transfer-Encoding:", "chunked", true)]
    [DataRow("Connection: keep-alive close", "Connection:", "close", true)]
    [DataRow("Connection: ab,,close\r\n", "Connection:", "close", true)]
    [DataRow("Transfer-Encoding: gzip2, chunked", "Transfer-Encoding:", "gzip", false)]
    [DataRow("Content-Encoding: chunked", "Transfer-Encoding:", "chunked", false)]
    [DataRow("Transfer-Encoding:", "Transfer-Encoding:", "chunked", false)]
    [DataRow("Encoding: gzipped,  \r\nEncoding: gzip, chunked", "Encoding:", "chunked", false)]
    public void ListsToken_HeaderLine_FindsOnlyAWholeTokenOfTheNamedHeaderOnItsFirstLine(string line, string name, string token, bool expected) =>
        Assert.AreEqual(expected, HttpHeaderLine.ListsToken(line, name, token));

    [TestMethod]
    public void ListsToken_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpHeaderLine.ListsToken(null!, "a:", "b"));
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpHeaderLine.ListsToken("a: b", null!, "b"));
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpHeaderLine.ListsToken("a: b", "a:", null!));
    }

    [TestMethod]
    [DataRow("Content-Type: text/html", "text/html")]
    [DataRow(" Location :\t /a:b  \r\n", "/a:b")]
    [DataRow("X-Two: one\ttwo\nX-Next: no", "one\ttwo")]
    [DataRow("Empty:\t\t", "")]
    [DataRow("NoColon", "")]
    public void CopyValue_HeaderLine_ReturnsTheTrimmedValueOfItsFirstLine(string line, string expected) =>
        Assert.AreEqual(expected, HttpHeaderLine.CopyValue(line));

    [TestMethod]
    public void CopyValue_NullLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpHeaderLine.CopyValue(null!));
}
