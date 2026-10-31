using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-070 (BL-3599) on <see cref="HttpProtocolHandler" />: a <c>101 Switching Protocols</c>
/// that no h2c upgrade asked for is the final head, and the bytes after it are its body, read to
/// the close whatever its Content-Length says, as curl 8.22.0 does (measured: <c>HTTP/1.1 101
/// Switching\r\n\r\nhello</c> writes <c>hello</c> and exits 0, also with <c>Content-Length: 2</c>
/// and for <c>HTTP/1.0 101</c>; <c>-i</c> writes the 101 head and then the body; <c>-w
/// %{http_code}</c> is <c>101</c>).
/// </summary>
[TestClass]
public sealed class Cgr070Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching\r\n\r\nhello", DisplayName = "x101b")]
    [DataRow("HTTP/1.1 101 Switching\r\nContent-Length: 2\r\n\r\nhello", DisplayName = "x101c, Content-Length ignored")]
    [DataRow("HTTP/1.0 101 Switching\r\n\r\nhello", DisplayName = "x101_10")]
    [DataRow("HTTP/1.1 101 Switching\r\nUpgrade: h2c\r\nConnection: Upgrade\r\n\r\nhello", DisplayName = "Upgrade: h2c not asked for")]
    public async Task ExecuteAsync_UnsolicitedSwitchingProtocols_WritesTheBytesAfterItsHeadAsTheBodyAndSucceeds(string response)
    {
        MemoryStream output = new();
        Diagnostics.Arrange("response", response);

        TransferResult result = await Execute(response, output, headerOutput: null);

        Diagnostics.Act("exit code, response code, body", string.Join(", ", result.ExitCode, result.Report?.ResponseCode, Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(101, result.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnsolicitedSwitchingProtocolsWithIncludedHeaders_WritesTheHeadThenTheBody()
    {
        const string Response = "HTTP/1.1 101 Switching\r\nUpgrade: foo\r\n\r\nhello";
        MemoryStream output = new();
        Diagnostics.Arrange("response, -i", Response);

        TransferResult result = await Execute(Response, output, output);

        Diagnostics.Act("exit code, output", string.Join(", ", result.ExitCode, Encoding.Latin1.GetString(output.ToArray())));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(Response, Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 101 Switching\r\nA: 1\r\n\r\n", DisplayName = "No body")]
    [DataRow("HTTP/1.1 101 Switching\r\nContent-Length: 0\r\n\r\n", DisplayName = "Content-Length: 0")]
    public async Task ExecuteAsync_UnsolicitedSwitchingProtocolsWithNoBody_WritesTheHeadThenFailsWithEmptyReply(string response)
    {
        MemoryStream output = new();
        Diagnostics.Arrange("response, -i", response);

        TransferResult result = await Execute(response, output, output);

        Diagnostics.Act("exit code, message, output", string.Join(", ", result.ExitCode, result.ErrorMessage, Encoding.Latin1.GetString(output.ToArray())));
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.AreEqual(response, Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_UnsolicitedSwitchingProtocolsVerbose_WritesNoAssumeCloseLine()
    {
        RecordingTransferEvents events = new();

        TransferResult result = await Execute("HTTP/1.1 101 Switching\r\n\r\nhello", new MemoryStream(), headerOutput: null, events);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.DoesNotContain(events.Info, "no chunk, no close, no size. Assume close to signal end");
    }

    private static ValueTask<TransferResult> Execute(string response, Stream output, Stream? headerOutput, RecordingTransferEvents? events = null) =>
        new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:20400/x101b"),
                Output = output,
                HeaderOutput = headerOutput,
                Events = events ?? new RecordingTransferEvents(),
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0" },
            });
}
