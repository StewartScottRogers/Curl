using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>-o -</c> end to end through the runner: the body goes to standard output, in binary
/// mode on Windows, no file named <c>-</c> is opened, and <c>--output-dir</c> neither turns it
/// into a path nor creates its directory. Every expectation was measured on 2026-09-27 with
/// curl 8.21.0 (mingw, Schannel) through <c>Record-CurlExchange.ps1</c> (BL-349 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerStandardOutputDashTests
{
    private const string Url = "http://127.0.0.1:18349/x";

    private const string Body = "a\nb\r\nc\n";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\n" + Body;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_OutputDash_WritesTheBodyToStandardOutputAndOpensNoFile()
    {
        int exitCode = await RunHttpAsync("-s", "-o", "-", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Body, StandardOutputText);
        Assert.AreEqual(0, standardError.Length);
        Assert.AreEqual(0, outputFiles.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirWithOutputDash_WritesTheBodyToStandardOutputAndCreatesNothingUnderTheDirectory()
    {
        int exitCode = await RunHttpAsync("-s", "--create-dirs", "--output-dir", "d", "-o", "-", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Body, StandardOutputText);
        Assert.AreEqual(0, outputFiles.Written.Count);
        Assert.AreEqual(0, outputFiles.CreatedDirectories.Count);
    }

    [TestMethod]
    public async Task RunAsync_OutputDashWithWriteOutOnWindows_KeepsTheBodyAndWriteOutLineEndings()
    {
        int exitCode = await RunHttpAsync("-s", "-o", "-", "-w", "%{http_code}\\n", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Body + "200\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirWithOutputDash_LeavesTheEffectiveFileNameEmpty()
    {
        int exitCode = await RunHttpAsync("-s", "--output-dir", "d", "-o", "-", "-w", "[%{filename_effective}]", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Body + "[]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ALaterOutputDashUrlOnWindows_KeepsTheEarlierWriteOutLineFeed()
    {
        int exitCode = await RunAsync(
            new ProtocolDispatcher([RecordingProtocolHandler.WritingPath("ok")]),
            "-s", "-o", "a", "-o", "-", "-w", "%{exitcode}\\n", "ok://h/x", "ok://h/y");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0\n/y0\n", StandardOutputText);
        CollectionAssert.AreEquivalent(new[] { "a" }, outputFiles.Written.Keys);
    }

    private Task<int> RunHttpAsync(params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return RunAsync(new ProtocolDispatcher([http]), arguments);
    }

    private Task<int> RunAsync(ProtocolDispatcher dispatcher, params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(dispatcher),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true,
                timeProvider: TimeProvider.System,
                outputPaths: outputFiles)
            .RunAsync(arguments);
}
