using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

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

    private const string HeaderDumpedTwice = "HTTP/1.1 200 OK\r\nHTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\n\r\nhello";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string NotFoundLine = "curl: (22) The requested URL returned error: 404" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string RequestsText => Encoding.Latin1.GetString(server.Written);

    [TestMethod]
    public async Task RunAsync_IncludeWithDumpHeaderFile_WritesTheHeadToBothAndTheBodyToStandardOutput()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-i", "-D", "hd.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stdout", Lf(Ok), Lf(StandardOutputText));
        Assert.AreEqual(Ok, StandardOutputText);
        Diagnostics.Assert("hd.txt", Lf(OkHead), Lf(WrittenText("hd.txt")));
        Assert.AreEqual(OkHead, WrittenText("hd.txt"));
        Diagnostics.Assert("stderr", Lf(string.Empty), Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeWithDumpHeaderToStandardOutput_WritesEachHeaderLineTwice()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-i", "-D", "-", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert(
            "stdout",
            Lf(HeaderDumpedTwice),
            Lf(StandardOutputText));
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\nHTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\n\r\nhello",
            StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeWithOutputFile_WritesTheHeadAndBodyToTheFile()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-i", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("out.txt", Lf(Ok), Lf(WrittenText("out.txt")));
        Assert.AreEqual(Ok, WrittenText("out.txt"));
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_HeadWithOutputFile_SendsHeadAndWritesOnlyTheHeadToTheFile()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-I", "-o", "out.txt", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("requests", Lf("HEAD /a HTTP/1.1\r\n" + Request), Lf(RequestsText));
        Assert.AreEqual("HEAD /a HTTP/1.1\r\n" + Request, RequestsText);
        Diagnostics.Assert("out.txt", Lf(OkHead), Lf(WrittenText("out.txt")));
        Assert.AreEqual(OkHead, WrittenText("out.txt"));
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
        Diagnostics.Assert("stderr", Lf(string.Empty), Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_Head_WritesOnlyTheHeadToStandardOutput()
    {
        int exitCode = await RunAsync([Ok], "-sS", "-I", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stdout", Lf(OkHead), Lf(StandardOutputText));
        Assert.AreEqual(OkHead, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailOnNotFoundUnderShowError_Exits22WithTheLineAndNoBody()
    {
        int exitCode = await RunAsync([NotFound], "-sS", "-f", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("stderr", Lf(NotFoundLine), Lf(StandardErrorText));
        Assert.AreEqual(NotFoundLine, StandardErrorText);
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailOnNotFoundUnderSilent_Exits22AndPrintsNothing()
    {
        int exitCode = await RunAsync([NotFound], "-s", "-f", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("stderr", Lf(string.Empty), Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailOnNotFoundByDefault_PrintsTheMeterOpeningThenTheLine()
    {
        int exitCode = await RunAsync([NotFound], writesProgressMeter: true, "-f", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert(
            "stderr",
            Lf(ProgressMeterLines.FirstHeaderLine + NewLine + ProgressMeterLines.SecondHeaderLine + NewLine + ProgressMeterLines.ZeroStatusLine + NewLine + NotFoundLine),
            Lf(StandardErrorText));
        Assert.AreEqual(
            ProgressMeterLines.FirstHeaderLine + NewLine
            + ProgressMeterLines.SecondHeaderLine + NewLine
            + ProgressMeterLines.ZeroStatusLine + NewLine
            + NotFoundLine,
            StandardErrorText);
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_IncludeAndFailOnNotFound_WritesTheHeadButNoBody()
    {
        int exitCode = await RunAsync([NotFound], "-sS", "-i", "-f", Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("stdout", Lf(NotFoundHead), Lf(StandardOutputText));
        Assert.AreEqual(NotFoundHead, StandardOutputText);
        Diagnostics.Assert("stderr", Lf(NotFoundLine), Lf(StandardErrorText));
        Assert.AreEqual(NotFoundLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailEarly_StopsAfterTheFirstFailure()
    {
        int exitCode = await RunAsync([NotFound, NotFound, NotFound], "-sS", "-f", "--fail-early", Url, SecondUrl, ThirdUrl);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("requests", Lf("GET /a HTTP/1.1\r\n" + Request), Lf(RequestsText));
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request, RequestsText);
        Diagnostics.Assert("stderr", Lf(NotFoundLine), Lf(StandardErrorText));
        Assert.AreEqual(NotFoundLine, StandardErrorText);
        Diagnostics.Assert("stdout length", 0, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailWithoutFailEarly_TransfersEveryUrl()
    {
        int exitCode = await RunAsync([NotFound, NotFound], "-sS", "-f", Url, SecondUrl);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("requests", Lf("GET /a HTTP/1.1\r\n" + Request + "GET /b HTTP/1.1\r\n" + Request), Lf(RequestsText));
        Assert.AreEqual("GET /a HTTP/1.1\r\n" + Request + "GET /b HTTP/1.1\r\n" + Request, RequestsText);
        Diagnostics.Assert("stderr", Lf(NotFoundLine + NotFoundLine), Lf(StandardErrorText));
        Assert.AreEqual(NotFoundLine + NotFoundLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyWithoutFail_TreatsNotFoundAsSuccessAndTransfersEveryUrl()
    {
        int exitCode = await RunAsync([NotFound, NotFound], "-sS", "--fail-early", Url, SecondUrl);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stdout", Lf("nope!nope!"), Lf(StandardOutputText));
        Assert.AreEqual("nope!nope!", StandardOutputText);
        Diagnostics.Assert("stderr", Lf(string.Empty), Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailWithBodyAndFailEarly_WritesTheFirstBodyAndStops()
    {
        int exitCode = await RunAsync([NotFound, NotFound], "-sS", "--fail-with-body", "--fail-early", Url, SecondUrl);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("stdout", Lf("nope!"), Lf(StandardOutputText));
        Assert.AreEqual("nope!", StandardOutputText);
        Diagnostics.Assert("stderr", Lf(NotFoundLine), Lf(StandardErrorText));
        Assert.AreEqual(NotFoundLine, StandardErrorText);
        Diagnostics.Assert("requests", Lf("GET /a HTTP/1.1\r\n" + Request), Lf(RequestsText));
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
    private async Task<int> RunAsync(string[] responses, bool writesProgressMeter, params string[] arguments)
    {
        Diagnostics.Arrange("command line", "curl " + string.Join(" ", arguments));
        Diagnostics.Arrange("scripted responses", string.Join(" | ", responses.Select(Lf)));
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
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

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        Diagnostics.Bytes("requests", server.Written);
        return exitCode;
    }

    private static string Lf(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace(NewLine, "\n", StringComparison.Ordinal);
}
