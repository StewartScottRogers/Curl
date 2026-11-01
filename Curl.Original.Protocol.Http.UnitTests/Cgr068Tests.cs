using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-068 (BL-3597): <c>-H "Transfer-Encoding: chunked"</c> on a
/// request with no body (GET, or HEAD for <c>-I</c>) still sends the body's last chunk,
/// <c>0\r\n\r\n</c>, after the head, as curl 8.22.0 does, measured on 2026-10-26 with
/// <c>Record-CurlExchange.ps1</c> (BL-3597 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string Cgr068Response = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n";

    [TestMethod]
    public async Task ExecuteAsync_GetWithChunkedTransferEncodingHeader_SendsTheLastChunkAfterTheHead()
    {
        string written = await WrittenForChunkedWithoutBodyAsync(noBody: false);

        StringAssert.StartsWith(written, "GET /");
        StringAssert.EndsWith(written, "Transfer-Encoding: chunked\r\n\r\n0\r\n\r\n");
        Assert.IsFalse(written.Contains("Content-", StringComparison.Ordinal), "no Content-Length or Content-Type");
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithChunkedTransferEncodingHeader_SendsTheLastChunkAfterTheHead()
    {
        string written = await WrittenForChunkedWithoutBodyAsync(noBody: true);

        StringAssert.StartsWith(written, "HEAD /");
        StringAssert.EndsWith(written, "Transfer-Encoding: chunked\r\n\r\n0\r\n\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_GetWithOtherTransferEncodingHeader_SendsTheHeadAlone()
    {
        HttpRequestOptions options = new() { Headers = ["Transfer-Encoding: gzip"] };
        ScriptedConnection connection = Connection(Cgr068Response, 65536);

        await Handler(QueueConnector.For(connection)).ExecuteAsync(EncodingContext(new MemoryStream(), options));

        StringAssert.EndsWith(Encoding.Latin1.GetString(connection.Written), "Transfer-Encoding: gzip\r\n\r\n");
    }

    private async Task<string> WrittenForChunkedWithoutBodyAsync(bool noBody)
    {
        HttpRequestOptions options = new() { Headers = ["Transfer-Encoding: chunked"] };
        Diagnostics.Arrange("request", $"no body, -I {noBody}, -H Transfer-Encoding: chunked");
        ScriptedConnection connection = Connection(Cgr068Response, 65536);
        TransferContext context = new() { Url = CurlUrl.Parse(LoopbackUrl), Output = new MemoryStream(), NoBody = noBody, Http = options };

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        WriteResult(result);
        string written = Encoding.Latin1.GetString(connection.Written);
        Diagnostics.Assert("request ends", "Transfer-Encoding: chunked\r\n\r\n0\r\n\r\n", written[Math.Max(0, written.Length - 35)..]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return written;
    }
}
