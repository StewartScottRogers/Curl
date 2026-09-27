using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpStatusLine" /> on its own: the status lines curl 8.21.0 accepts, and
/// how early it gives up on bytes that cannot be one. Each case was measured (BL-169 Notes).
/// </summary>
[TestClass]
public sealed class HttpStatusLineTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK", "1.1", 200, "OK", DisplayName = "HTTP/1.1")]
    [DataRow("HTTP/1.0 404 Not Found", "1.0", 404, "Not Found", DisplayName = "HTTP/1.0")]
    [DataRow("HTTP/1.1 999", "1.1", 999, "", DisplayName = "Code 999, no reason")]
    [DataRow("http/1.1 404 x", "1.0", 200, "", DisplayName = "Lower-case http/ taken as HTTP/1.0 200")]
    public void Parse_StatusLineCurlAccepts_ReturnsItsParts(string line, string version, int statusCode, string reasonPhrase)
    {
        HttpStatusLine statusLine = HttpStatusLine.Parse(line);

        Assert.AreEqual(Version.Parse(version), statusLine.Version);
        Assert.AreEqual(statusCode, statusLine.StatusCode);
        Assert.AreEqual(reasonPhrase, statusLine.ReasonPhrase);
    }

    [TestMethod]
    [DataRow(100, true, DisplayName = "100")]
    [DataRow(199, true, DisplayName = "199")]
    [DataRow(200, false, DisplayName = "200")]
    public void IsInformational_StatusCode_IsTrueOnlyFor1xx(int statusCode, bool isInformational)
    {
        HttpStatusLine statusLine = new(HttpVersion.Version11, statusCode, string.Empty);

        Assert.AreEqual(isInformational, statusLine.IsInformational);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Nothing yet")]
    [DataRow("h", DisplayName = "Lower-case h")]
    [DataRow("HTTP/", DisplayName = "The whole prefix")]
    [DataRow("HTTP/1.1 200 OK", DisplayName = "A whole status line")]
    public void RejectHttp09_BytesThatCanStillBeginHttp_AreAccepted(string received)
    {
        HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received));
    }

    [TestMethod]
    [DataRow("I", DisplayName = "First byte wrong")]
    [DataRow("HTTX", DisplayName = "Fourth byte wrong")]
    [DataRow(" HTTP/", DisplayName = "Leading blank")]
    public void RejectHttp09_BytesThatCannotBeginHttp_ThrowUnsupportedProtocol(string received)
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received)));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Assert.AreEqual("Received HTTP/0.9 when not allowed", thrown.Message);
    }
}
