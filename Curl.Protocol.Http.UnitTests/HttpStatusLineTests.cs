using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpStatusLine" /> on its own: the status lines curl 8.21.0 accepts, and
/// how early it gives up on bytes that cannot be one. Each case was measured (BL-169 Notes).
/// </summary>
[TestClass]
public sealed class HttpStatusLineTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK", "1.1", 200, "OK", DisplayName = "HTTP/1.1")]
    [DataRow("HTTP/1.0 404 Not Found", "1.0", 404, "Not Found", DisplayName = "HTTP/1.0")]
    [DataRow("HTTP/1.1 999", "1.1", 999, "", DisplayName = "Code 999, no reason")]
    [DataRow("http/1.1 404 x", "1.0", 200, "", DisplayName = "Lower-case http/ taken as HTTP/1.0 200")]
    public void Parse_StatusLineCurlAccepts_ReturnsItsParts(string line, string version, int statusCode, string reasonPhrase)
    {
        Diagnostics.Arrange("line", line);

        HttpStatusLine statusLine = HttpStatusLine.Parse(line);

        Diagnostics.Act("parsed version", statusLine.Version);
        Diagnostics.Act("parsed status code", statusLine.StatusCode);
        Diagnostics.Act("parsed reason phrase", statusLine.ReasonPhrase);
        Diagnostics.Assert("version", Version.Parse(version), statusLine.Version);
        Diagnostics.Assert("status code", statusCode, statusLine.StatusCode);
        Diagnostics.Assert("reason phrase", reasonPhrase, statusLine.ReasonPhrase);
        Assert.AreEqual(Version.Parse(version), statusLine.Version);
        Assert.AreEqual(statusCode, statusLine.StatusCode);
        Assert.AreEqual(reasonPhrase, statusLine.ReasonPhrase);
    }

    [TestMethod]
    public void ParseHttp2OrHttp3_StatusLineTheHttp2StreamWrites_IsVersion2WithTheCodeAndNoReason()
    {
        Diagnostics.Arrange("line", "HTTP/2 404 ");

        HttpStatusLine statusLine = HttpStatusLine.ParseHttp2OrHttp3("HTTP/2 404 ");

        Diagnostics.Act("parsed version", statusLine.Version);
        Diagnostics.Act("parsed status code", statusLine.StatusCode);
        Diagnostics.Act("parsed reason phrase", statusLine.ReasonPhrase);
        Diagnostics.Assert("version", HttpVersion.Version20, statusLine.Version);
        Diagnostics.Assert("status code", 404, statusLine.StatusCode);
        Diagnostics.Assert("reason phrase", string.Empty, statusLine.ReasonPhrase);
        Assert.AreEqual(HttpVersion.Version20, statusLine.Version);
        Assert.AreEqual(404, statusLine.StatusCode);
        Assert.AreEqual(string.Empty, statusLine.ReasonPhrase);
    }

    [TestMethod]
    public void ParseHttp2OrHttp3_StatusLineTheHttp3StreamWrites_IsVersion3WithTheCodeAndNoReason()
    {
        Diagnostics.Arrange("line", "HTTP/3 204 ");

        HttpStatusLine statusLine = HttpStatusLine.ParseHttp2OrHttp3("HTTP/3 204 ");

        Diagnostics.Act("parsed version", statusLine.Version);
        Diagnostics.Act("parsed status code", statusLine.StatusCode);
        Diagnostics.Act("parsed reason phrase", statusLine.ReasonPhrase);
        Diagnostics.Assert("version", HttpVersion.Version30, statusLine.Version);
        Diagnostics.Assert("status code", 204, statusLine.StatusCode);
        Diagnostics.Assert("reason phrase", string.Empty, statusLine.ReasonPhrase);
        Assert.AreEqual(HttpVersion.Version30, statusLine.Version);
        Assert.AreEqual(204, statusLine.StatusCode);
        Assert.AreEqual(string.Empty, statusLine.ReasonPhrase);
    }

    [TestMethod]
    [DataRow(100, true, DisplayName = "100")]
    [DataRow(199, true, DisplayName = "199")]
    [DataRow(200, false, DisplayName = "200")]
    public void IsInformational_StatusCode_IsTrueOnlyFor1xx(int statusCode, bool isInformational)
    {
        Diagnostics.Arrange("status code", statusCode);
        HttpStatusLine statusLine = new(HttpVersion.Version11, statusCode, string.Empty);

        Diagnostics.Act("is informational", statusLine.IsInformational);
        Diagnostics.Assert("is informational", isInformational, statusLine.IsInformational);
        Assert.AreEqual(isInformational, statusLine.IsInformational);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Nothing yet")]
    [DataRow("h", DisplayName = "Lower-case h")]
    [DataRow("HTTP/", DisplayName = "The whole prefix")]
    [DataRow("HTTP/1.1 200 OK", DisplayName = "A whole status line")]
    public void RejectHttp09_BytesThatCanStillBeginHttp_AreAccepted(string received)
    {
        Diagnostics.Arrange("received", received);

        HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received));

        Diagnostics.Act("outcome", "returned without throwing");
        Diagnostics.Assert("outcome", "no exception", "no exception");
    }

    [TestMethod]
    [DataRow("I", DisplayName = "First byte wrong")]
    [DataRow("HTTX", DisplayName = "Fourth byte wrong")]
    [DataRow(" HTTP/", DisplayName = "Leading blank")]
    public void RejectHttp09_BytesThatCannotBeginHttp_ThrowUnsupportedProtocol(string received)
    {
        Diagnostics.Arrange("received", received);

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received)));

        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Act("message", thrown.Message);
        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Diagnostics.Assert("message", "Received HTTP/0.9 when not allowed", thrown.Message);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Assert.AreEqual("Received HTTP/0.9 when not allowed", thrown.Message);
    }
}
