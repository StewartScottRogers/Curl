using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>--http0.9</c> over HTTP (AF-0019). Measured on curl 8.21.0 against a loopback server with
/// <c>Record-CurlExchange.ps1</c> that sent each response and closed (BL-1275 Notes):
/// <c>curl --http0.9 -sS -v -i -w "[%{http_code} %{http_version} %{size_header}]"</c> against
/// <c>just text</c> wrote <c>just text[000 0 0]</c>, exit 0, and no header or <c>&lt;</c> line.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("just text", DisplayName = "No line end")]
    [DataRow("line one\nline two\r\nHTTP/1.1 200 OK\r\n\r\n", DisplayName = "Lines, then a status line")]
    public async Task ExecuteAsync_Http09ReplyWithHttp09Allowed_WritesEveryByteAsTheBodyWithCode000(string response)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size, allow http0.9", $"{LoopbackUrl}, {chunkSize}, True");
            Diagnostics.Arrange("response", OneLine(response));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize, LoopbackGet)))
                .ExecuteAsync(new TransferContext
                {
                    Url = CurlUrl.Parse(LoopbackUrl),
                    Output = output,
                    HeaderOutput = headerOutput,
                    Events = events,
                    Http = new HttpRequestOptions { AllowHttp09Reply = true },
                });

            WriteResult(result);
            Diagnostics.Act("header output length", headerOutput.Length);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("body", OneLine(response), OneLine(Latin1(output.ToArray())));
            Assert.AreEqual(response, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("header output length", 0, headerOutput.Length);
            Assert.AreEqual(0, headerOutput.Length, $"Chunk size {chunkSize}");
            Diagnostics.Assert("response code", 0, result.Report!.ResponseCode);
            Assert.AreEqual(0, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("http version", new Version(0, 9), result.Report!.HttpVersion);
            Assert.AreEqual(new Version(0, 9), result.Report!.HttpVersion, $"Chunk size {chunkSize}");
            Diagnostics.Assert("any < line among events", false, events.Events.Any(line => line.StartsWith("< ", StringComparison.Ordinal)));
            Assert.IsFalse(events.Events.Any(line => line.StartsWith("< ", StringComparison.Ordinal)), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Http09ReplyWithoutHttp09Allowed_FailsWithExit1()
    {
        Diagnostics.Arrange("response, allow http0.9", "just text, False");

        TransferResult result = await Handler(QueueConnector.For(Connection("just text", 65536, LoopbackGet)))
            .ExecuteAsync(EncodingContext(new MemoryStream(), new HttpRequestOptions()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Diagnostics.Assert("error message", "Received HTTP/0.9 when not allowed", result.ErrorMessage);
        Assert.AreEqual("Received HTTP/0.9 when not allowed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http11ReplyWithHttp09Allowed_IsReadAsHttp11()
    {
        MemoryStream output = new();
        Diagnostics.Arrange("response, allow http0.9", "HTTP/1.1 200 OK with Content-Length 2 and body ok, True");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536, LoopbackGet)))
            .ExecuteAsync(EncodingContext(output, new HttpRequestOptions { AllowHttp09Reply = true }));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("body", "ok", Latin1(output.ToArray()));
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Diagnostics.Assert("response code", 200, result.Report!.ResponseCode);
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }
}
