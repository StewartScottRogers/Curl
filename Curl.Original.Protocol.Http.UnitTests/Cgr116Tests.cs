namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-116 (BL-3645): a NUL byte in the status line was accepted
/// and the head written; curl 8.22.0 (mingw, Schannel) fails with exit 8, <c>Nul byte in
/// header</c>, and writes nothing, with or without <c>-i</c> and <c>-f</c>, measured on
/// 2026-10-26 with <c>Record-CurlExchange.ps1</c> (BL-3645 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\0\r\nContent-Length: 6\r\n\r\n-foo-\n", "-i", DisplayName = "NUL after the reason phrase, with -i")]
    [DataRow("HTTP/1.1 200 OK\0\nDate: Thu, 09 Nov 2010\0 14:49:00 GMT\nContent-Length: 6\n\n-foo-\n", "-i", DisplayName = "test262 shape: LF line ends, NUL in the status line and in Date, with -i")]
    [DataRow("HTTP/1.1 200 O\0K\r\nContent-Length: 2\r\n\r\nok", "", DisplayName = "NUL inside the reason phrase")]
    [DataRow("HTTP/1.1 200\0\r\nContent-Length: 2\r\n\r\nok", "-i", DisplayName = "NUL straight after the status code, with -i")]
    [DataRow("HTTP/1.1 404 Not\0\r\nContent-Length: 2\r\n\r\nok", "-f", DisplayName = "NUL ending a 404's reason phrase, with -f")]
    public async Task ExecuteAsync_NulByteInTheStatusLine_FailsWithWeirdServerReplyBeforeReportingIt(string response, string options)
    {
        await AssertWeirdHeaderLineAsync(response, options, "Nul byte in header", []);
    }
}
