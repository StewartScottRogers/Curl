using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContentLength" /> against curl 8.21.0 (mingw, Schannel), measured
/// against a loopback server that sent <c>Content-Length: &lt;value&gt;</c> and the body
/// <c>hello</c> (BL-170 Notes).
/// </summary>
[TestClass]
public sealed class HttpContentLengthTests
{
    [TestMethod]
    [DataRow("5", 5L, DisplayName = "Plain number")]
    [DataRow("05", 5L, DisplayName = "Leading zero")]
    [DataRow("0", 0L, DisplayName = "Zero")]
    [DataRow("5, 5", 5L, DisplayName = "List of equal numbers")]
    [DataRow("5 , 5", 5L, DisplayName = "Blanks around list items")]
    [DataRow("\t5\t", 5L, DisplayName = "Tabs around the number")]
    public void Find_ValuesCurlAccepts_GiveTheLength(string value, long length)
    {
        Assert.AreEqual(length, HttpContentLength.Find([new HttpResponseHeader("Content-Length", value)]));
    }

    [TestMethod]
    [DataRow("abc", DisplayName = "Letters")]
    [DataRow("-1", DisplayName = "Negative")]
    [DataRow("+5", DisplayName = "Plus sign")]
    [DataRow("0x5", DisplayName = "Hexadecimal")]
    [DataRow("5 x", DisplayName = "Trailing word")]
    [DataRow("5,6", DisplayName = "Unequal list")]
    [DataRow("5,5,6", DisplayName = "Unequal last item")]
    [DataRow("5,", DisplayName = "Empty last item")]
    [DataRow(",5", DisplayName = "Empty first item")]
    [DataRow("5,,5", DisplayName = "Empty middle item")]
    [DataRow("", DisplayName = "Empty value")]
    public void Find_ValuesCurlRefuses_ThrowExit8(string value)
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpContentLength.Find([new HttpResponseHeader("Content-Length", value)]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", thrown.Message);
    }

    [TestMethod]
    public void Find_TwoHeadersThatDisagree_ThrowExit8()
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpContentLength.Find(
                [new HttpResponseHeader("Content-Length", "5"), new HttpResponseHeader("Content-Length", "3")]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    [TestMethod]
    public void Find_TwoHeadersThatAgree_GiveTheLength()
    {
        Assert.AreEqual(
            5L,
            HttpContentLength.Find(
                [new HttpResponseHeader("Content-Length", "5"), new HttpResponseHeader("content-LENGTH", "5")]));
    }

    [TestMethod]
    public void Find_NameInAnyCase_IsRead()
    {
        Assert.AreEqual(3L, HttpContentLength.Find([new HttpResponseHeader("content-LENGTH", "3")]));
    }

    [TestMethod]
    public void Find_NoContentLengthHeader_IsUnknown()
    {
        Assert.IsNull(HttpContentLength.Find([new HttpResponseHeader("Content-Type", "text/plain")]));
    }

    [TestMethod]
    public void Find_NumberTooLargeForALong_IsUnknown()
    {
        // Measured: Content-Length 99999999999999999999 wrote "hello" and exited 0.
        Assert.IsNull(HttpContentLength.Find([new HttpResponseHeader("Content-Length", "99999999999999999999")]));
    }

    [TestMethod]
    [DataRow("Content-Length", "99999999999999999999", true, DisplayName = "Too large")]
    [DataRow("content-length", " 5, 99999999999999999999\t", true, DisplayName = "Second item too large")]
    [DataRow("Content-Length", "9223372036854775807", false, DisplayName = "long.MaxValue")]
    [DataRow("Content-Length", "x99999999999999999999", false, DisplayName = "Not a number")]
    [DataRow("Content-Length", "", false, DisplayName = "Empty")]
    [DataRow("X-Length", "99999999999999999999", false, DisplayName = "Another header")]
    public void Overflows_Header_IsTrueOnlyForAContentLengthTooLargeToHold(string name, string value, bool overflows)
    {
        Assert.AreEqual(overflows, HttpContentLength.Overflows([new HttpResponseHeader(name, value)]));
    }

    [TestMethod]
    [DataRow("Content-Length: 99999999999999999999", true, DisplayName = "Too large")]
    [DataRow("Content-Length: 5", false, DisplayName = "Fits")]
    [DataRow("X-Length: 99999999999999999999", false, DisplayName = "Another header")]
    [DataRow("Content-Length 99999999999999999999", false, DisplayName = "No colon")]
    public void OverflowsLine_HeaderLine_IsTrueOnlyForAContentLengthTooLargeToHold(string line, bool overflows)
    {
        Assert.AreEqual(overflows, HttpContentLength.OverflowsLine(line));
    }
}
