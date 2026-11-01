using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-143 (BL-3672) on <see cref="HttpProtocolHandler" />: an HTTP/1.1 request that asks to
/// upgrade to h2c (<c>--http2</c> over cleartext) carries no <c>Expect: 100-continue</c> of
/// curl's own and sends its body straight after the head, as curl 8.18.0 does for a 2 MiB
/// <c>--data-binary</c> body and for a chunked <c>-T -</c> upload (measured, BL-3672 Notes).
/// </summary>
[TestClass]
public sealed class Cgr143Tests
{
    private const string Url = "http://127.0.0.1:21999/echo";

    private const string UpgradeLines = "Upgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\n";

    private const string NoContent = "HTTP/1.1 204 No Content\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_H2cUpgradeWithBodyOverOneMebibyte_SendsNoExpectAndTheBodyAtOnce()
    {
        // curl 8.18.0 --http2 --data-binary @2m.bin (2 MiB): the head below, then the body.
        const int Length = 2097152;
        const string Head = "POST /echo HTTP/1.1\r\nHost: 127.0.0.1:21999\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n"
            + UpgradeLines + "Content-Length: 2097152\r\nContent-Type: application/x-www-form-urlencoded\r\n"
            + "Connection: Upgrade, HTTP2-Settings\r\n\r\n";
        GatedConnection connection = new(Encoding.Latin1.GetBytes(NoContent), 65536, Head.Length + Length);
        HttpRequestOptions options = Http2(new BytesBody(new byte[Length], "application/x-www-form-urlencoded"));
        Diagnostics.Arrange("body length, response", $"{Length}, {NoContent}");

        TransferResult result = await Execute(connection, options, upload: null);

        string written = Encoding.Latin1.GetString(connection.Written);
        Diagnostics.Act("exit code, bytes written", $"{result.ExitCode}, {written.Length}");
        Diagnostics.Assert("head", Head, written[..Math.Min(Head.Length, written.Length)]);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(Head, written[..Head.Length]);
        Assert.HasCount(Head.Length + Length, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_H2cUpgradeWithChunkedUploadFromStdin_SendsNoExpectAndTheLastChunkAtOnce()
    {
        // curl 8.18.0 --http2 -T - < /dev/null: the head below, then 0\r\n\r\n.
        const string Request = "PUT /echo HTTP/1.1\r\nHost: 127.0.0.1:21999\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\n" + UpgradeLines + "Connection: Upgrade, HTTP2-Settings\r\n\r\n0\r\n\r\n";
        GatedConnection connection = new(Encoding.Latin1.GetBytes(NoContent), 65536, Request.Length);
        Diagnostics.Arrange("upload, response", $"an empty stream of unknown length, {NoContent}");

        TransferResult result = await Execute(connection, Http2(body: null), new UnseekableStream([]));

        string written = Encoding.Latin1.GetString(connection.Written);
        Diagnostics.Act("exit code, written", $"{result.ExitCode}, {written}");
        Diagnostics.Assert("written", Request, written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(Request, written);
    }

    private static HttpRequestOptions Http2(HttpRequestBody? body) =>
        new() { Version = HttpVersionPreference.Http2, UserAgent = "curl/8.22.0", Body = body };

    // The system clock, so that a wait for 100 Continue, were there one, would end and the
    // test would fail on the Expect line rather than hang.
    private static ValueTask<TransferResult> Execute(GatedConnection connection, HttpRequestOptions options, Stream? upload) =>
        new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(Url),
                Output = new MemoryStream(),
                Http = options,
                Upload = upload,
                TimeProvider = TimeProvider.System,
            });
}
