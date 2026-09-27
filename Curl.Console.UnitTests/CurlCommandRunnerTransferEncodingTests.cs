using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>--compressed</c>, <c>-0</c> / <c>--http1.0</c>, <c>--http1.1</c>, <c>--raw</c>,
/// <c>--tr-encoding</c> and <c>--ignore-content-length</c> end to end through the runner and
/// <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />, with the request
/// bytes and output curl 8.21.0 (mingw, Schannel) sent and wrote against
/// <c>Record-CurlExchange.ps1</c> on 2026-09-26 and 2026-09-27 (BL-177, BL-180 and BL-315 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTransferEncodingTests
{
    private const string Url = "http://127.0.0.1:18180/a";

    private const string Http11RequestLine = "GET /a HTTP/1.1\r\n";

    private const string DefaultHeaders = "Host: 127.0.0.1:18180\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    private const string DefaultRequest = Http11RequestLine + DefaultHeaders + "\r\n";

    /// <summary>The 25-byte gzip of <c>hello</c> BL-177 measured with.</summary>
    private static readonly byte[] GzippedHello = Convert.FromHexString("1F8B080000000000000ACB48CDC9C9070086A6103605000000");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string RequestText => Encoding.Latin1.GetString(server.Written);

    [TestMethod]
    public async Task RunAsync_Compressed_SendsAcceptEncodingAndDecodesTheGzipBody()
    {
        byte[] response = [.. Latin1("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 25\r\n\r\n"), .. GzippedHello];

        int exitCode = await RunAsync(response, "-sS", "--compressed", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Http11RequestLine + DefaultHeaders + "Accept-Encoding: deflate, gzip, br\r\n\r\n", RequestText);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithoutCompressed_WritesTheGzipBodyUntouched()
    {
        byte[] response = [.. Latin1("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 25\r\n\r\n"), .. GzippedHello];

        int exitCode = await RunAsync(response, "-sS", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(DefaultRequest, RequestText);
        CollectionAssert.AreEqual(GzippedHello, standardOutput.ToArray());
    }

    [TestMethod]
    [DataRow("-0")]
    [DataRow("--http1.0")]
    public async Task RunAsync_Http10_EndsTheRequestLineInHttp10(string option)
    {
        int exitCode = await RunAsync(Latin1("HTTP/1.0 200 OK\r\nContent-Length: 5\r\n\r\nhello"), "-sS", option, Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("GET /a HTTP/1.0\r\n" + DefaultHeaders + "\r\n", RequestText);
        Assert.AreEqual("hello", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_Http11AfterHttp10_EndsTheRequestLineInHttp11()
    {
        int exitCode = await RunAsync(Latin1("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"), "-s", "-0", "--http1.1", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(DefaultRequest, RequestText);
        Assert.AreEqual("hello", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_Raw_WritesTheChunkedBodyAsSent()
    {
        const string chunked = "5\r\nhello\r\n0\r\n\r\n";

        int exitCode = await RunAsync(Latin1("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n" + chunked), "-sS", "--raw", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(DefaultRequest, RequestText);
        Assert.AreEqual(chunked, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_TransferEncoding_SendsTeAndConnectionAndDecodesTheGzipTransferCoding()
    {
        byte[] response = [.. Latin1("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\nContent-Length: 25\r\n\r\n"), .. GzippedHello];

        int exitCode = await RunAsync(response, "-sS", "--tr-encoding", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Http11RequestLine + DefaultHeaders + "TE: gzip\r\nConnection: TE\r\n\r\n", RequestText);
        Assert.AreEqual("hello", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IgnoreContentLength_ReadsTheBodyToClose()
    {
        int exitCode = await RunAsync(
            Latin1("HTTP/1.1 200 OK\r\nContent-Length: 3\r\nConnection: close\r\n\r\nhello"),
            "-sS",
            "--ignore-content-length",
            Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(DefaultRequest, RequestText);
        Assert.AreEqual("hello", StandardOutputText);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> answering <paramref name="response" /> and then closing.
    /// </summary>
    private Task<int> RunAsync(byte[] response, params string[] arguments)
    {
        server = new ScriptedConnector([response]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: false)
            .RunAsync(arguments);
    }
}
