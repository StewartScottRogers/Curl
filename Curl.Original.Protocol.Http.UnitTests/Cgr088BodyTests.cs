using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-088 (BL-3952) on <see cref="HttpProtocolHandler" />: curl 8.22.0's <c>failf</c> writes
/// the message of a bad gzip body, a bad chunk, a cut-off chunked body and a short body as a
/// <c>-v</c> line (measured with <c>curl -sv</c> against a loopback server).
/// </summary>
[TestClass]
public sealed class Cgr088BodyTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\nhello", CurlExitCode.PartialFile, new[] { "end of response with 95 bytes missing" }, DisplayName = "short body")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n", CurlExitCode.PartialFile, new[] { "transfer closed with outstanding read data remaining" }, DisplayName = "cut-off chunked body")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\nhello\r\n0\r\n\r\n", CurlExitCode.RecvError, new[] { "chunk hex-length char not a hex digit: 0x7a", "Illegal or missing hexadecimal sequence in chunked-encoding" }, DisplayName = "chunk size not hex")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n11111111111111111\r\nhello\r\n0\r\n\r\n", CurlExitCode.RecvError, new[] { "chunk hex-length longer than 16", "Too long hexadecimal number in chunked-encoding" }, DisplayName = "chunk size too long")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFFFFFFFFFF\r\nhello\r\n0\r\n\r\n", CurlExitCode.RecvError, new[] { "invalid chunk size: 'FFFFFFFFFFFFFFFF'", "Illegal or missing hexadecimal sequence in chunked-encoding" }, DisplayName = "chunk size overflows")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhelloXX\r\n0\r\n\r\n", CurlExitCode.RecvError, new[] { "Malformed encoding found in chunked-encoding" }, DisplayName = "bad chunk end")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 5\r\n\r\nhello", CurlExitCode.BadContentEncoding, new[] { "Error while processing content unencoding: incorrect header check" }, DisplayName = "bad gzip")]
    public async Task ExecuteAsync_BodyFails_EchoesTheFailureAsInfoLines(string response, CurlExitCode exitCode, string[] expectedLines)
    {
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:20700/a"),
                Output = new MemoryStream(),
                Events = events,
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0", Compressed = true },
            });

        Assert.AreEqual(exitCode, result.ExitCode);
        CollectionAssert.IsSubsetOf(expectedLines, events.Info.ToList());
        Assert.AreEqual(expectedLines.Length, events.Info.Count(line => expectedLines.Contains(line)));
    }
}
