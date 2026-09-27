using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins <c>-w</c> / <c>--write-out</c> end to end through the runner: rendered after every
/// transfer, successful or not, after the failure line, to standard output or standard error
/// as the template says, and on Windows with the line feeds curl's text-mode streams give.
/// Every expectation was measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel), through
/// <c>Record-CurlExchange.ps1</c> on 127.0.0.1 or against <c>file://</c> URLs (BL-235 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerWriteOutTests
{
    private const string Url = "http://127.0.0.1:18235/a";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string NotFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\n\r\ngone";

    private const string HttpCode = "%{http_code}\\n";

    private const string ExitCode = "%{exitcode}\\n";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_SuccessToStandardOutputOnWindows_WritesTheCodeAfterTheBodyWithALineFeed()
    {
        int exitCode = await RunHttpAsync([Ok], runsOnWindows: true, "-w", HttpCode, Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello200\n", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferUnderSilent_StillWritesTheCode()
    {
        int exitCode = await RunHttpAsync([NotFound], runsOnWindows: true, "-s", "-f", "-w", HttpCode, Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("404\n", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferToStandardError_WritesTheCodeAfterTheFailureLineWithCrLf()
    {
        int exitCode = await RunHttpAsync(
            [NotFound], runsOnWindows: true, "-sS", "-f", "-w", "%{stderr}" + HttpCode, Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual("curl: (22) The requested URL returned error: 404" + NewLine + "404\r\n", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessToAnOutputFileOnWindows_WritesTheCodeWithCrLf()
    {
        int exitCode = await RunHttpAsync([Ok], runsOnWindows: true, "-s", "-o", "out.txt", "-w", HttpCode, Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("200\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferToAnOutputFileOnWindows_WritesTheCodeWithCrLf()
    {
        int exitCode = await RunHttpAsync([NotFound], runsOnWindows: true, "-s", "-f", "-o", "out.txt", "-w", HttpCode, Url);

        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("404\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessToAnOutputFileOffWindows_WritesTheCodeWithALineFeed()
    {
        int exitCode = await RunHttpAsync([Ok], runsOnWindows: false, "-s", "-o", "out.txt", "-w", HttpCode, Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("200\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ALaterUrlToStandardOutput_KeepsTheEarlierLineFeeds()
    {
        int exitCode = await RunOkAndFailingAsync(
            runsOnWindows: true, "-s", "-o", "a", "-o", "b", "-w", ExitCode, "ok://h/x", "ok://h/y", "ok://h/z");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0\n0\n/z0\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_AFailedTransferThatDoesNotEndTheRun_KeepsItsLineFeedForALaterStandardOutputUrl()
    {
        int exitCode = await RunOkAndFailingAsync(
            runsOnWindows: true, "-s", "-o", "a", "-w", ExitCode, "fail://h/x", "ok://h/y");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("7\n/y0\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyEndsTheRunBeforeAStandardOutputUrl_WritesCrLf()
    {
        int exitCode = await RunOkAndFailingAsync(
            runsOnWindows: true, "-s", "--fail-early", "-o", "a", "-o", "b", "-w", ExitCode, "fail://h/x", "ok://h/y", "ok://h/z");

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("7\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_HeaderFileCannotBeOpened_WritesTheExitCodeWithCrLf()
    {
        outputFiles.UnwritablePaths.Add("adir");

        int exitCode = await RunOkAndFailingAsync(runsOnWindows: true, "-s", "-D", "adir", "-w", ExitCode, "ok://h/x");

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("23\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UnsupportedOrMalformedUrl_PrintsNoScheme()
    {
        await RunOkAndFailingAsync(
            runsOnWindows: false,
            "-s",
            "-w",
            "[%{scheme}][%{url}][%{urlnum}]\\n",
            "xyz://a/b",
            "dict://exa mple.com/d:x",
            "OK://h/x");

        Assert.AreEqual("[][xyz://a/b][0]\n[][dict://exa mple.com/d:x][1]\n/x[ok][OK://h/x][2]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_GetQuery_IsInTheEffectiveUrl()
    {
        await RunOkAndFailingAsync(runsOnWindows: false, "-s", "-G", "-d", "a=b", "-o", "a", "-w", "%{url_effective}", "ok://h/x");

        Assert.AreEqual("ok://h/x?a=b", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UnknownVariableOnWindows_WarnsWithCrLf()
    {
        await RunOkAndFailingAsync(runsOnWindows: true, "-s", "-o", "a", "-w", "x%{nosuch}y", "ok://h/x");

        Assert.AreEqual("xy", StandardOutputText);
        Assert.AreEqual("curl: unknown --write-out variable: 'nosuch'\r\n", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirectiveWithoutAnOpener_StaysOnStandardOutput()
    {
        await RunOkAndFailingAsync(runsOnWindows: false, "-s", "-o", "a", "-w", "A%output{o.txt}B", "ok://h/x");

        Assert.AreEqual("AB", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirectiveWithAnOpener_WritesToTheOpenedFile()
    {
        MemoryOpener opener = new();

        await RunAsync(
            new ProtocolDispatcher([RecordingProtocolHandler.WritingPath("ok")]),
            runsOnWindows: false,
            opener,
            "-s", "-o", "a", "-w", "A%output{o.txt}B", "ok://h/x");

        Assert.AreEqual("A", StandardOutputText);
        Assert.AreEqual("B", Encoding.UTF8.GetString(opener.Opened.ToArray()));
    }

    [TestMethod]
    [DataRow(18)]
    [DataRow(10000)]
    public async Task RunAsync_ClosedStandardOutput_DropsTheStandardOutputPartAndRendersTheRest(int bodyLength)
    {
        using ClosedStandardOutputStream closed = new();
        RecordingProtocolHandler writing = new("ok", async context =>
        {
            try
            {
                await context.Output.WriteAsync(new byte[bodyLength], context.CancellationToken);
                return TransferResult.Success(bodyLength);
            }
            catch (OutputWriteFailedException)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination");
            }
        });

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([writing])),
                outputFiles,
                outputFiles,
                closed,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(["-s", "-w", "A%{exitcode}%{stderr}B%{exitcode}\\n", "ok://h/x"]);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("B23\n", StandardErrorText);
    }

    private Task<int> RunHttpAsync(string[] responses, bool runsOnWindows, params string[] arguments)
    {
        ScriptedConnector server = new(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return RunAsync(new ProtocolDispatcher([http]), runsOnWindows, null, arguments);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> with an <c>ok</c> scheme that writes the URL's path and
    /// succeeds, and a <c>fail</c> scheme that fails with exit 7.
    /// </summary>
    private Task<int> RunOkAndFailingAsync(bool runsOnWindows, params string[] arguments) =>
        RunAsync(
            new ProtocolDispatcher(
            [
                RecordingProtocolHandler.WritingPath("ok"),
                RecordingProtocolHandler.Failing("fail", CurlExitCode.CouldntConnect, "Failed to connect"),
            ]),
            runsOnWindows,
            null,
            arguments);

    private Task<int> RunAsync(
        ProtocolDispatcher dispatcher,
        bool runsOnWindows,
        IWriteOutFileOpener? opener,
        params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(dispatcher),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                writeOutFileOpener: opener,
                timeProvider: TimeProvider.System)
            .RunAsync(arguments);

    /// <summary>Opens every <c>%output{…}</c> file as one memory stream, kept for reading.</summary>
    private sealed class MemoryOpener : IWriteOutFileOpener
    {
        public MemoryStream Opened { get; } = new();

        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = new NonDisposingStream(Opened);
            return true;
        }
    }

    /// <summary>Passes writes through and leaves the inner stream open when disposed.</summary>
    private sealed class NonDisposingStream(Stream inner) : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(buffer, cancellationToken);
    }
}
