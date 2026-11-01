using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-271: a 70 KB custom request header fails the send over HTTP/2 (exit 55), as curl's
/// nghttp2 refuses a header whose name and value pass 65536 bytes.
/// </summary>
[TestClass]
public sealed class Cgr271Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_SeventyKilobyteHeader_FailsTheSendWithExit55()
    {
        string head = "GET /x HTTP/1.1\r\nHost: h\r\nX-Big: " + new string('b', 70000) + "\r\n\r\n";
        Diagnostics.Arrange("header value length", 70000);

        HttpTransferException exception = Assert.ThrowsExactly<HttpTransferException>(() => Http2RequestHeaders.Of(System.Text.Encoding.Latin1.GetBytes(head), "http"));

        Diagnostics.Assert("exit code", CurlExitCode.SendError, exception.ExitCode);
        Diagnostics.Assert("message", "Failed sending data to the peer", exception.Message);
    }

    [TestMethod]
    public void Of_TwentyKilobyteHeader_StillSends()
    {
        string head = "GET /x HTTP/1.1\r\nHost: h\r\nX-Big: " + new string('b', 20000) + "\r\n\r\n";
        Diagnostics.Arrange("header value length", 20000);

        Diagnostics.Assert("field count", 5, Http2RequestHeaders.Of(System.Text.Encoding.Latin1.GetBytes(head), "http").Count);
    }
}
