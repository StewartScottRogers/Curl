using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>%{size_download}</c> and <c>%{size_delivered}</c> end to end through the runner and
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />, with the output
/// curl 8.21.0 (mingw, Schannel) wrote for the same canned responses against
/// <c>Record-CurlExchange.ps1</c> on 2026-09-28 (BL-516 Notes): the download counts the body
/// after chunked framing is removed, and the delivery counts it after content decoding.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSizeDeliveredTests
{
    private const string Url = "http://127.0.0.1:18516/x";

    private const string Sizes = "%{size_download} %{size_delivered}";

    /// <summary>
    /// The 51-byte gzip of <c>Hello, compressed world! </c> twenty times and a line feed, 501
    /// bytes, that BL-516 measured with.
    /// </summary>
    private static readonly byte[] GzippedGreeting = Convert.FromHexString(
        "1F8B0800000000000400F348CDC9C9D75148CECF2D284A2D2E4E4D5128CF2FCA495154F0189518AE125C00220656B1F5010000");

    private readonly MemoryStream standardOutput = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_CompressedGzipBody_DownloadsTheEncodedBytesAndDeliversTheDecodedOnes()
    {
        byte[] response = [.. Latin1("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: 51\r\n\r\n"), .. GzippedGreeting];

        int exitCode = await RunAsync(response, "-s", "--compressed", "-o", "out", "-w", Sizes, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "51 501", StandardOutputText);
        Assert.AreEqual("51 501", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_GzipBodyWithoutCompressed_DeliversWhatItDownloads()
    {
        byte[] response = [.. Latin1("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: 51\r\n\r\n"), .. GzippedGreeting];

        int exitCode = await RunAsync(response, "-s", "-o", "out", "-w", Sizes, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "51 51", StandardOutputText);
        Assert.AreEqual("51 51", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ChunkedBody_CountsNeitherSizesFraming()
    {
        byte[] response = Latin1("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n7\r\n world\n\r\n0\r\n\r\n");

        int exitCode = await RunAsync(response, "-s", "-o", "out", "-w", Sizes, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "12 12", StandardOutputText);
        Assert.AreEqual("12 12", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_CompressedChunkedGzipBody_DownloadsTheUnframedEncodedBytes()
    {
        byte[] response =
        [
            .. Latin1("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nTransfer-Encoding: chunked\r\n\r\n1a\r\n"),
            .. GzippedGreeting[..26],
            .. Latin1("\r\n19\r\n"),
            .. GzippedGreeting[26..],
            .. Latin1("\r\n0\r\n\r\n"),
        ];

        int exitCode = await RunAsync(response, "-s", "--compressed", "-o", "out", "-w", Sizes, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "51 501", StandardOutputText);
        Assert.AreEqual("51 501", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_PlainBodyToStandardOutput_DeliversWhatItDownloads()
    {
        byte[] response = Latin1("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello world\n");

        int exitCode = await RunAsync(response, "-s", "-w", "[" + Sizes + "]", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "hello world\n[12 12]", StandardOutputText);
        Assert.AreEqual("hello world\n[12 12]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_CompressedGzipBodyAsJson_PrintsBothSizes()
    {
        byte[] response = [.. Latin1("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: 51\r\n\r\n"), .. GzippedGreeting];

        int exitCode = await RunAsync(response, "-s", "--compressed", "-o", "out", "-w", "%{json}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stdout contains the sizes", true, StandardOutputText.Contains("\"size_delivered\":501,\"size_download\":51,\"size_header\":89,", StringComparison.Ordinal));
        StringAssert.Contains(StandardOutputText, "\"size_delivered\":501,\"size_download\":51,\"size_header\":89,");
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> answering <paramref name="response" /> and then closing.
    /// </summary>
    private async Task<int> RunAsync(byte[] response, params string[] arguments)
    {
        ScriptedConnector server = new([response]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("connector script", "one canned response, then close");
        Diagnostics.Bytes("scripted response", response);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    new MemoryStream(),
                    new MemoryStream(),
                    runsOnWindows: false,
                    writesProgressMeter: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", StandardOutputText.Replace("\r\n", "\n", StringComparison.Ordinal));
        return exitCode;
    }
}
