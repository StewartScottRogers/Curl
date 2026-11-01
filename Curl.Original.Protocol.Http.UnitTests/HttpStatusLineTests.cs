using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

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
    [DataRow("HTTP/2 101 OK", true, DisplayName = "HTTP/2 101")]
    [DataRow("HTTP/3 100 Continue", true, DisplayName = "HTTP/3 100")]
    [DataRow("HTTP/3 100", false, DisplayName = "Nothing after the code (measured: assumed 200, BL-3646)")]
    [DataRow("HTTP/2 200 OK", true, DisplayName = "HTTP/2 200")]
    [DataRow("HTTP/2 99 OK", false, DisplayName = "Two-digit code (measured: assumed 200)")]
    [DataRow("HTTP/2 abc", false, DisplayName = "Code not digits (measured: assumed 200)")]
    [DataRow("HTTP/2.0 200 OK", false, DisplayName = "HTTP/2.0 (measured: assumed 200)")]
    [DataRow("HTTP/1.1 101", false, DisplayName = "HTTP/1.1")]
    [DataRow("HTTP/4 101", false, DisplayName = "HTTP/4")]
    public void NamesHttp2OrHttp3_StatusLine_TellsWhetherItNamesHttp2OrHttp3(string line, bool expected)
    {
        Diagnostics.Arrange("line", line);

        bool actual = HttpStatusLine.NamesHttp2OrHttp3(line);

        Diagnostics.Act("names HTTP/2 or HTTP/3", actual);
        Diagnostics.Assert("names HTTP/2 or HTTP/3", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("HTTP/2 101 OK", CurlExitCode.WeirdServerReply, "Version mismatch (from HTTP/1 to HTTP/2)", DisplayName = "HTTP/2 (test1704)")]
    [DataRow("HTTP/3 200 OK", CurlExitCode.UnsupportedProtocol, "Unsupported HTTP version (3.0) in response", DisplayName = "HTTP/3")]
    public void RefusalOverHttp1_StatusLine_GivesCurlsOpenSslFailure(string line, CurlExitCode exitCode, string message)
    {
        Diagnostics.Arrange("line", line);

        HttpTransferException refusal = HttpStatusLine.RefusalOverHttp1(line, withoutHttp2: false);

        Diagnostics.Act("refusal", $"exit code {refusal.ExitCode}, message '{refusal.Message}'");
        Diagnostics.Assert("exit code", exitCode, refusal.ExitCode);
        Assert.AreEqual(exitCode, refusal.ExitCode);
        Assert.AreEqual(message, refusal.Message);
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

        HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received), []);

        Diagnostics.Act("outcome", "returned without throwing");
        Diagnostics.Assert("outcome", "no exception", "no exception");
    }

    [TestMethod]
    [DataRow("I", DisplayName = "First byte of the alias")]
    [DataRow("icy 2", DisplayName = "Lower-case start")]
    [DataRow("ICY 200 OK\r", DisplayName = "The whole alias and more")]
    public void RejectHttp09_BytesThatCanStillBeginAnHttp200Alias_AreAccepted(string received)
    {
        Diagnostics.Arrange("received, aliases", $"{received}, ICY 200 OK");

        HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received), ["ICY 200 OK"]);

        Diagnostics.Act("outcome", "returned without throwing");
        Diagnostics.Assert("outcome", "no exception", "no exception");
    }

    [TestMethod]
    public void RejectHttp09_BytesThatBeginNoAlias_ThrowUnsupportedProtocol()
    {
        Diagnostics.Arrange("received, aliases", "ICX, ICY 200 OK");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpStatusLine.RejectHttp09("ICX"u8, ["ICY 200 OK"]));

        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
    }

    [TestMethod]
    [DataRow("ICY 200 OK", "ICY 200 OK", true, DisplayName = "The alias itself")]
    [DataRow("icy 200 ok extra", "ICY 200 OK", true, DisplayName = "Another case, then more text")]
    [DataRow("ICY", "ICY 200 OK", true, DisplayName = "A line shorter than the alias, as checkprefixmax compares")]
    [DataRow("ICX 200 OK", "ICY 200 OK", false, DisplayName = "A different line")]
    public void MatchesAlias_Line_TellsWhetherItBeginsAnAlias(string line, string alias, bool expected)
    {
        Diagnostics.Arrange("line, alias", $"{line}, {alias}");

        bool matches = HttpStatusLine.MatchesAlias(line, ["OTHER", alias]);

        Diagnostics.Act("matches", matches);
        Diagnostics.Assert("matches", expected, matches);
        Assert.AreEqual(expected, matches);
    }

    [TestMethod]
    public void Http200Alias_IsHttp10Status200()
    {
        Diagnostics.Arrange("alias", "any");

        HttpStatusLine statusLine = HttpStatusLine.Http200Alias();

        Diagnostics.Act("version, code", $"{statusLine.Version} {statusLine.StatusCode}");
        Diagnostics.Assert("version, code", "1.0 200", $"{statusLine.Version} {statusLine.StatusCode}");
        Assert.AreEqual(new Version(1, 0), statusLine.Version);
        Assert.AreEqual(200, statusLine.StatusCode);
        Assert.IsEmpty(statusLine.ReasonPhrase);
    }

    [TestMethod]
    [DataRow("I", DisplayName = "First byte wrong")]
    [DataRow("HTTX", DisplayName = "Fourth byte wrong")]
    [DataRow(" HTTP/", DisplayName = "Leading blank")]
    public void RejectHttp09_BytesThatCannotBeginHttp_ThrowUnsupportedProtocol(string received)
    {
        Diagnostics.Arrange("received", received);

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpStatusLine.RejectHttp09(Encoding.ASCII.GetBytes(received), []));

        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Act("message", thrown.Message);
        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Diagnostics.Assert("message", "Received HTTP/0.9 when not allowed", thrown.Message);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Assert.AreEqual("Received HTTP/0.9 when not allowed", thrown.Message);
    }
}
