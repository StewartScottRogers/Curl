using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>-i</c>, <c>-I</c>, <c>-f</c>, <c>--fail-with-body</c> and <c>--fail-early</c> end
/// to end through the runner and <see cref="HttpProtocolHandler" /> over a
/// <see cref="ScriptedConnector" />. Every expectation was measured on 2026-09-26 with
/// curl 8.21.0 (mingw, Schannel) through <c>Record-CurlExchange.ps1</c> on 127.0.0.1:18332,
/// answering <c>200 OK</c> with <c>hello</c> or <c>404 Not Found</c> with <c>nope!</c>
/// (BL-232 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHeaderAndFailTests
{
    private const string Url = "http://127.0.0.1:18332/a";

    private const string SecondUrl = "http://127.0.0.1:18332/b";

    private const string ThirdUrl = "http://127.0.0.1:18332/c";

    private const string OkHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";

    private const string NotFoundHead = "HTTP/1.1 404 Not Found\r\nContent-Length: 5\r\n\r\n";

    private const string Ok = OkHead + "hello";

    private const string NotFound = NotFoundHead + "nope!";

    private const string Request = "Host: 127.0.0.1:18332\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string NotFoundLine = "curl: (22) The requested URL returned error: 404" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string RequestsText => Encoding.Latin1.GetString(server.Written);

    [TestMethod]
    public async Task RunAsync_IncludeWithDumpHeaderFile_WritesTheHeadToBothAndTheBodyToStandardOutput()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-i", "-D", "hd.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Ok, StandardOutputText);
        Assert.AreEqual(OkHead, WrittenText("hd.txt"));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeWithDumpHeaderToStandardOutput_WritesEachHeaderLineTwice()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-i", "-D", "-", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\nHTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\n\r\nhello",
            StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeWithOutputFile_WritesTheHeadAndBodyToTheFile()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-i", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Ok, WrittenText("out.txt"));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_HeadWithOutputFile_SendsHeadAndWritesOnlyTheHeadToTheFile()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-I", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("HEAD /a HTTP/1.1\r\n" + Request, RequestsText);
        Assert.AreEqual(OkHead, WrittenText("out.txt"));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_Head_WritesOnlyTheHeadToStandardOutput()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-I", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(OkHead, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailOnNotFoundUnderShowError_Exits22WithTheLineAndNoBody()
    {
        int exitCode = await RunAsync([NotFound], "-sS", "-f", Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(NotFoundLine, StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailOnNotFoundUnderSilent_Exits22AndPrintsNothing()
    {
        int exitCode = await RunAsync([NotFound], "-s", "-f", Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailOnNotFoundByDefault_PrintsTheMeterOpeningThenTheLine()
    {
        int exitCode = await RunAsync([NotFound], writesProgressMeter: true, "-f", Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(
            ProgressMeterLines.FirstHeaderLine + NewLine
            + ProgressMeterLines.SecondHeaderLine + NewLine
            + ProgressMeterLines.ZeroStatusLine + NewLine
            + NotFoundLine,
            StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_IncludeAndFailOnNotFound_WritesTheHeadButNoBody()
    {
        int exitCode = await RunAsync([NotFound], "-sS", "-i", "-f", Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(NotFoundHead, StandardOutputText);
        Assert.AreEqual(NotFoundLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailEarly_StopsAfterTheFirstFailure()
    {
        int exitCode = await RunAsync([NotFound, NotFound, NotFound], "-sS", "-f", "--fail-early", Url, SecondUrl, ThirdUrl);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request, RequestsText);
        Assert.AreEqual(NotFoundLine, StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailWithoutFailEarly_TransfersEveryUrl()
    {
        int exitCode = await RunAsync([NotFound, NotFound], "-sS", "-f", Url, SecondUrl);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request + "GET /b HTTP/1.1\r\n" + Request, RequestsText);
        Assert.AreEqual(NotFoundLine + NotFoundLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyWithoutFail_TreatsNotFoundAsSuccessAndTransfersEveryUrl()
    {
        int exitCode = await RunAsync([NotFound, NotFound], "-sS", "--fail-early", Url, SecondUrl);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("nope!nope!", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailWithBodyAndFailEarly_WritesTheFirstBodyAndStops()
    {
        int exitCode = await RunAsync([NotFound, NotFound], "-sS", "--fail-with-body", "--fail-early", Url, SecondUrl);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("nope!", StandardOutputText);
        Assert.AreEqual(NotFoundLine, StandardErrorText);
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request, RequestsText);
    }

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private Task<int> RunAsync(string[] responses, params string[] arguments) =>
        RunAsync(responses, writesProgressMeter: false, arguments);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> serving <paramref name="responses" />, one per
    /// connection, and <see cref="outputFiles" /> as the <c>-o</c> and <c>-D</c> file system.
    /// </summary>
    private Task<int> RunAsync(string[] responses, bool writesProgressMeter, params string[] arguments)
    {
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: writesProgressMeter)
            .RunAsync(arguments);
    }
}
