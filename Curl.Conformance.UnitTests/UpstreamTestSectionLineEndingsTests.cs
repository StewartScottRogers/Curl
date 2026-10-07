using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTestSectionLineEndings"/> against what <c>runtests.pl</c> and
/// <c>testutil.pm</c> at <c>curl-8_21_0</c> do for <c>nonewline</c>, <c>crlf</c> and
/// <c>mode="text"</c>.
/// </summary>
[TestClass]
public sealed class UpstreamTestSectionLineEndingsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("a\nb\n", "a\nb")]
    [DataRow("a\r\n", "a\r")]
    [DataRow("a", "a")]
    [DataRow("", "")]
    public void CutFinalNewline_Body_DropsOneFinalLineFeedLikeChomp(string body, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex(body));
        byte[] result = UpstreamTestSectionLineEndings.CutFinalNewline(Latin1(body));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        AssertBytes(expected, result);
    }

    [TestMethod]
    [DataRow("a\nb\n", "a\r\nb\r\n")]
    [DataRow("a\r\r\nb\r\n", "a\r\nb\r\n")]
    [DataRow("\nlast", "\r\nlast")]
    [DataRow("", "")]
    public void ForceCrlf_Body_EndsEveryLineWithOneCrlf(string body, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex(body));
        byte[] result = UpstreamTestSectionLineEndings.ForceCrlf(Latin1(body));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        AssertBytes(expected, result);
    }

    [TestMethod]
    public void ForceCrlf_BodyWithHighBytes_KeepsEveryByte()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] input = [0xE9, 0xFF, 0x0A];
        byte[] expected = [0xE9, 0xFF, 0x0D, 0x0A];
        diagnostics.Arrange("input bytes (hex)", Convert.ToHexString(input));
        byte[] result = UpstreamTestSectionLineEndings.ForceCrlf(input);
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));
        diagnostics.Diff("result", expected, result);
        CollectionAssert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ForceHeaderCrlf_HttpResponse_EndsHeadersAndTheBlankLineAfterThemWithCrlf()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex("HTTP/1.1 200 OK\nContent-Length: 4\n\nbody\n\n"));
        byte[] result = UpstreamTestSectionLineEndings.ForceHeaderCrlf(Latin1("HTTP/1.1 200 OK\nContent-Length: 4\n\nbody\n\n"));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        AssertBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nbody\n\n", result);
    }

    [TestMethod]
    public void ForceHeaderCrlf_FinalHeaderWithoutLineFeed_IsUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex("A: b"));
        byte[] result = UpstreamTestSectionLineEndings.ForceHeaderCrlf(Latin1("A: b"));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        AssertBytes("A: b", result);
    }

    [TestMethod]
    public void ForceHeaderCrlf_BlankCrlfLineAfterHeader_IsUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex("A: b\n\r\n"));
        byte[] result = UpstreamTestSectionLineEndings.ForceHeaderCrlf(Latin1("A: b\n\r\n"));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        AssertBytes("A: b\r\n\r\n", result);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\n", true)]
    [DataRow("HTTP/1.0 404 Not Found\n", true)]
    [DataRow("HTTP/1x1 200 OK\n", true)]
    [DataRow("HTTP/2 500\n", true)]
    [DataRow("HTTP/3 100\n", true)]
    [DataRow("HTTP/1.1 999\n", true)]
    [DataRow("HTTP/1.1 600\n", false)]
    [DataRow("HTTP/1.1 099\n", false)]
    [DataRow("HTTP/1.2 200\n", false)]
    [DataRow("HTTP/4 200\n", false)]
    [DataRow("HTTP/1\n", false)]
    [DataRow("HTTP/2\n", false)]
    [DataRow("HTTP/2x200\n", false)]
    [DataRow("HTTP/1.1 200 OK\r\n", false)]
    [DataRow("GET /path HTTP/1.1\n", true)]
    [DataRow("CONNECT host:443 HTTP/1.1\r\n", true)]
    [DataRow("GET  HTTP/1.1\n", false)]
    [DataRow("GET /path\tHTTP/1.1\n", false)]
    [DataRow("GET /path FTP/1\n", false)]
    [DataRow("GET /path HTTP/\n", false)]
    [DataRow("GET /path HTTP/", false)]
    [DataRow("OPTIONS rtsp://host/ RTSP/1.0\n", true)]
    [DataRow("GET_PARAMETER rtsp://host/ RTSP/1.0\n", true)]
    [DataRow("DESCRIBE rtsp://host/ HTTP/1.0\n", false)]
    [DataRow("Content-Type: text/html\n", true)]
    [DataRow("x_a-B9: 1\n", true)]
    [DataRow(": value\n", false)]
    [DataRow("no separator\n", false)]
    [DataRow("Bad Name: value\n", false)]
    [DataRow("curl: (7) Failed to connect\n", false)]
    [DataRow("curl: (x) not an error\n", true)]
    [DataRow("curl: (7)x\n", true)]
    [DataRow("curl: hello\n", true)]
    [DataRow("plain text\n", false)]
    public void ForceHeaderCrlf_OneLine_EndsItWithCrlfOnlyWhenUpstreamGuessesAHeader(string line, bool isHeader)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex(line));
        byte[] result = UpstreamTestSectionLineEndings.ForceHeaderCrlf(Latin1(line));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        string expected = isHeader ? line.TrimEnd('\n').TrimEnd('\r') + "\r\n" : line;
        AssertBytes(expected, result);
    }

    [TestMethod]
    public void NormalizeText_MixedLineEndings_MakesEveryLineFeedCrlf()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input bytes (hex)", Hex("a\r\nb\nc"));
        byte[] result = UpstreamTestSectionLineEndings.NormalizeText(Latin1("a\r\nb\nc"));
        diagnostics.Act("result bytes (hex)", Convert.ToHexString(result));

        AssertBytes("a\r\nb\r\nc", result);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static string Hex(string text) => Convert.ToHexString(Latin1(text));

    private void AssertBytes(string expected, byte[] actual)
    {
        byte[] expectedBytes = Latin1(expected);
        TestDiagnostics.For(TestContext).Diff("result", expectedBytes, actual);
        CollectionAssert.AreEqual(expectedBytes, actual);
    }
}
