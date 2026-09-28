using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>--out-null</c> end to end through the runner and <see cref="HttpProtocolHandler" />
/// over a <see cref="ScriptedConnector" />, with any output file in an
/// <see cref="InMemoryFileSystem" />. Every expectation was measured on 2026-09-28 with curl 8.21.0
/// (mingw, Schannel) against a loopback server answering <c>200</c> with an <c>X-A: b</c> header
/// and a 5-byte <c>hello</c> body (BL-495 Notes): the body, and any <c>-i</c> header lines, go
/// nowhere, no file is created, <c>-D -</c> still prints the head, the progress meter is drawn as
/// for a file, and the <c>-w</c> output reaches standard output in binary mode.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerOutNullTests
{
    private const string Url = "http://127.0.0.1:48495/a";

    private const string OtherUrl = "http://127.0.0.1:48495/b";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: b\r\n\r\nhello";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    [DataRow("--out-null")]
    [DataRow("--no-out-null")]
    public async Task RunAsync_OutNullWithWriteOut_WritesOnlyTheWriteOut(string spelledOption)
    {
        int exitCode = await RunAsync([Ok], "-s", spelledOption, Url, "-w", "%{http_code}");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("200", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.IsEmpty(outputFiles.Written);
    }

    [TestMethod]
    public async Task RunAsync_OutNullAfterItsUrl_StillDiscardsTheBody()
    {
        int exitCode = await RunAsync([Ok], "-s", Url, "--out-null");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.IsEmpty(outputFiles.Written);
    }

    [TestMethod]
    public async Task RunAsync_OutNullThenOutput_DiscardsTheFirstBodyAndSavesTheSecond()
    {
        int exitCode = await RunAsync([Ok, Ok], "-s", "--out-null", "-o", "b.txt", Url, OtherUrl);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        CollectionAssert.AreEquivalent(new[] { "b.txt" }, outputFiles.Written.Keys);
        Assert.AreEqual("hello", WrittenText("b.txt"));
    }

    [TestMethod]
    public async Task RunAsync_OutputThenOutNull_SavesTheFirstBodyAndDiscardsTheSecond()
    {
        int exitCode = await RunAsync([Ok, Ok], "-s", "-o", "c.txt", "--out-null", Url, OtherUrl);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        CollectionAssert.AreEquivalent(new[] { "c.txt" }, outputFiles.Written.Keys);
        Assert.AreEqual("hello", WrittenText("c.txt"));
    }

    [TestMethod]
    public async Task RunAsync_OutNullForTheFirstOfTwoUrls_WritesOnlyTheSecondBody()
    {
        int exitCode = await RunAsync([Ok, Ok], "-s", "--out-null", Url, OtherUrl, "-w", "%{urlnum}\n");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0\nhello1\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutNullWithInclude_DiscardsTheHeaderLinesToo()
    {
        int exitCode = await RunAsync([Ok], "-s", "-i", "--out-null", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutNullWithDumpHeaderToStandardOutput_PrintsOnlyTheHead()
    {
        int exitCode = await RunAsync([Ok], "-s", "-D", "-", "--out-null", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: b\r\n\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutNullWithFileNameAndSize_LeavesTheFileNameEmpty()
    {
        int exitCode = await RunAsync([Ok], "-s", "--out-null", Url, "-w", "%{filename_effective}|%{size_download}");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("|5", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutNullUnderRemoteNameAll_SavesNoFile()
    {
        int exitCode = await RunAsync([Ok], "-s", "--remote-name-all", "--out-null", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.IsEmpty(outputFiles.Written);
    }

    [TestMethod]
    public async Task RunAsync_OutNullOnWindows_WritesTheWriteOutLineFeedUnchanged()
    {
        int exitCode = await RunAsync([Ok], runsOnWindows: true, "-s", "--out-null", Url, "-w", "%{http_code}\n");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("200\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutNullWithStandardOutputATerminal_StillDrawsTheProgressMeter()
    {
        int exitCode = await RunAsync(
            [Ok], runsOnWindows: false, writesProgressMeter: true, standardOutputIsTerminal: true, "--out-null", Url);

        Assert.AreEqual(0, exitCode);
        StringAssert.StartsWith(StandardErrorText, "  % Total    % Received % Xferd  Average Speed");
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private Task<int> RunAsync(string[] responses, params string[] arguments) =>
        RunAsync(responses, runsOnWindows: false, arguments);

    private Task<int> RunAsync(string[] responses, bool runsOnWindows, params string[] arguments) =>
        RunAsync(responses, runsOnWindows, writesProgressMeter: false, standardOutputIsTerminal: false, arguments);

    private Task<int> RunAsync(
        string[] responses,
        bool runsOnWindows,
        bool writesProgressMeter,
        bool standardOutputIsTerminal,
        params string[] arguments)
    {
        ScriptedConnector server = new(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: runsOnWindows,
                writesProgressMeter: writesProgressMeter,
                standardOutputIsTerminal: standardOutputIsTerminal,
                outputPaths: outputFiles)
            .RunAsync(arguments);
    }
}
