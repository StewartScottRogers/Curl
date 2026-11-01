using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-117 (BL-3646): measured on curl 8.22.0 (Windows) on 2026-10-26, only the first three
/// digits of an HTTP/1.x status code are the code, <c>HTTP/2 200</c> with nothing after the code is
/// an assumed <c>HTTP/1.0 200</c>, a code below 100 fails only after its whole head is written, and
/// an empty first line is written before it is refused as HTTP/0.9.
/// </summary>
[TestClass]
public sealed class Cgr117Tests
{
    private const string Body = "Content-Length: 6\r\n\r\n-foo-\n";

    private static readonly int[] ChunkSizes = [1, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/1.1 2000 OK", 200)]
    [DataRow("HTTP/1.1 2001", 200)]
    [DataRow("HTTP/1.0 2000 OK", 200)]
    [DataRow("HTTP/1.1 20000 OK", 200)]
    [DataRow("HTTP/1.1 4040 Not Found", 404)]
    [DataRow("HTTP/1.1 99999 Huh", 999)]
    [DataRow("HTTP/2 200", 200)]
    [DataRow("HTTP/3 200", 200)]
    public async Task ExecuteAsync_StatusLineCurlAccepts_WritesHeadAndBodyWithItsCode(string statusLine, int code)
    {
        string response = statusLine + "\r\n" + Body;
        foreach (int chunkSize in ChunkSizes)
        {
            (TransferResult result, string output) = await RunAsync(response, chunkSize);

            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("response code", code, result.Report!.ResponseCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(response, output, $"Chunk size {chunkSize}");
            Assert.AreEqual(code, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_FourDigitCodeStartingWith100_IsAnInterimHeadThenAnInvalidStatusLine()
    {
        const string head = "HTTP/1.1 1000 Big\r\nContent-Length: 2\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            (TransferResult result, string output) = await RunAsync(head + "ok", chunkSize);

            Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Invalid status line", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, output, $"Chunk size {chunkSize}");
            Assert.AreEqual(100, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 099 Zero\r\nContent-Length: 2\r\n\r\n", 99)]
    [DataRow("HTTP/1.1 000 Zero\r\nContent-Length: 2\r\n\r\n", 0)]
    public async Task ExecuteAsync_CodeBelow100_WritesTheWholeHeadThenFailsWithItsCode(string head, int code)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            (TransferResult result, string output) = await RunAsync(head + "ok", chunkSize);

            Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
            Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Unsupported response code in HTTP response", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, output, $"Chunk size {chunkSize}");
            Assert.AreEqual(code, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A whole empty first line is written before it is refused; read a byte at a time, the
    /// <c>\r</c> alone is refused before the line is whole, as curl refuses it.
    /// </summary>
    [TestMethod]
    [DataRow("\r\n\r\n", "\r\n")]
    [DataRow("\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", "\n")]
    public async Task ExecuteAsync_EmptyFirstLine_WritesItThenFailsAsHttp09(string response, string written)
    {
        (TransferResult result, string output) = await RunAsync(response, 65536);

        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Received HTTP/0.9 when not allowed", result.ErrorMessage);
        Assert.AreEqual(written, output);
    }

    [TestMethod]
    [DataRow("HTTP/2 200", false)]
    [DataRow("HTTP/2 2000", false)]
    [DataRow("HTTP/2 200 OK", true)]
    [DataRow("HTTP/3 200\tOK", true)]
    public void NamesHttp2OrHttp3_NeedsABlankAfterTheCode(string line, bool expected)
    {
        Diagnostics.Arrange("line", line);

        bool names = HttpStatusLine.NamesHttp2OrHttp3(line);

        Diagnostics.Assert("names HTTP/2 or HTTP/3", expected, names);
        Assert.AreEqual(expected, names);
    }

    private async Task<(TransferResult Result, string Output)> RunAsync(string response, int chunkSize)
    {
        MemoryStream output = new();
        Diagnostics.Arrange("chunk size", chunkSize);
        Diagnostics.Arrange("scripted response", response.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal));
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize, null);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = output, HeaderOutput = output });

        Diagnostics.Act("exit code", $"{result.ExitCode}, error: {result.ErrorMessage ?? "(none)"}");
        return (result, Encoding.Latin1.GetString(output.ToArray()));
    }
}
