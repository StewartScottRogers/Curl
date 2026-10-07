using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_SuccessToStandardOutputOnWindows_WritesTheCodeAfterTheBodyWithALineFeed()
    {
        int exitCode = await RunHttpAsync([Ok], runsOnWindows: true, "-w", HttpCode, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "hello200\n", StandardOutputText);
        Assert.AreEqual("hello200\n", StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, Normalized(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferUnderSilent_StillWritesTheCode()
    {
        int exitCode = await RunHttpAsync([NotFound], runsOnWindows: true, "-s", "-f", "-w", HttpCode, Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Diff("stdout", "404\n", StandardOutputText);
        Assert.AreEqual("404\n", StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, Normalized(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferToStandardError_WritesTheCodeAfterTheFailureLineWithCrLf()
    {
        int exitCode = await RunHttpAsync(
            [NotFound], runsOnWindows: true, "-sS", "-f", "-w", "%{stderr}" + HttpCode, Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
        Diagnostics.Diff(
            "stderr (CRLF shown as LF)",
            "curl: (22) The requested URL returned error: 404\n404\n",
            Normalized(StandardErrorText));
        Assert.AreEqual("curl: (22) The requested URL returned error: 404" + NewLine + "404\r\n", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessToAnOutputFileOnWindows_WritesTheCodeWithCrLf()
    {
        int exitCode = await RunHttpAsync([Ok], runsOnWindows: true, "-s", "-o", "out.txt", "-w", HttpCode, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "200\r\n", StandardOutputText);
        Assert.AreEqual("200\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferToAnOutputFileOnWindows_WritesTheCodeWithCrLf()
    {
        int exitCode = await RunHttpAsync([NotFound], runsOnWindows: true, "-s", "-f", "-o", "out.txt", "-w", HttpCode, Url);

        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(22, exitCode);
        Diagnostics.Diff("stdout", "404\r\n", StandardOutputText);
        Assert.AreEqual("404\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessToAnOutputFileOffWindows_WritesTheCodeWithALineFeed()
    {
        int exitCode = await RunHttpAsync([Ok], runsOnWindows: false, "-s", "-o", "out.txt", "-w", HttpCode, Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "200\n", StandardOutputText);
        Assert.AreEqual("200\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ALaterUrlToStandardOutput_KeepsTheEarlierLineFeeds()
    {
        int exitCode = await RunOkAndFailingAsync(
            runsOnWindows: true, "-s", "-o", "a", "-o", "b", "-w", ExitCode, "ok://h/x", "ok://h/y", "ok://h/z");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "0\n0\n/z0\n", StandardOutputText);
        Assert.AreEqual("0\n0\n/z0\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_AFailedTransferThatDoesNotEndTheRun_KeepsItsLineFeedForALaterStandardOutputUrl()
    {
        int exitCode = await RunOkAndFailingAsync(
            runsOnWindows: true, "-s", "-o", "a", "-w", ExitCode, "fail://h/x", "ok://h/y");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "7\n/y0\n", StandardOutputText);
        Assert.AreEqual("7\n/y0\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyEndsTheRunBeforeAStandardOutputUrl_WritesCrLf()
    {
        int exitCode = await RunOkAndFailingAsync(
            runsOnWindows: true, "-s", "--fail-early", "-o", "a", "-o", "b", "-w", ExitCode, "fail://h/x", "ok://h/y", "ok://h/z");

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        Diagnostics.Diff("stdout", "7\r\n", StandardOutputText);
        Assert.AreEqual("7\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_HeaderFileCannotBeOpened_WritesTheExitCodeWithCrLf()
    {
        outputFiles.UnwritablePaths.Add("adir");

        int exitCode = await RunOkAndFailingAsync(runsOnWindows: true, "-s", "-D", "adir", "-w", ExitCode, "ok://h/x");

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stdout", "23\r\n", StandardOutputText);
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

        Diagnostics.Diff("stdout", "[][xyz://a/b][0]\n[][dict://exa mple.com/d:x][1]\n/x[ok][OK://h/x][2]\n", StandardOutputText);
        Assert.AreEqual("[][xyz://a/b][0]\n[][dict://exa mple.com/d:x][1]\n/x[ok][OK://h/x][2]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_TwoUrls_PrintTheRefererTheirFilesAndCountConnectionsAndTransfers()
    {
        // curl -e http://ref.example/x, two URLs to a server that closes each connection:
        // conn_id and xfer_id 0 then 1; -o names the file (BL-284's Notes).
        const string Template = "[%{referer}][%{filename_effective}][%{conn_id}][%{xfer_id}]\\n";

        await RunOkAndFailingAsync(
            runsOnWindows: false, "-s", "-e", "http://ref.example/x", "-o", "out.bin", "-o", "b.bin", "-w", Template, "ok://h/x", "fail://h/y");

        Diagnostics.Diff("stdout", "[http://ref.example/x][out.bin][0][0]\n[http://ref.example/x][b.bin][1][1]\n", StandardOutputText);
        Assert.AreEqual("[http://ref.example/x][out.bin][0][0]\n[http://ref.example/x][b.bin][1][1]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_RejectedUrlThenStandardOutput_HasNoConnectionAndNoFileName()
    {
        // A URL curl rejected printed conn_id -1; standard output printed no filename_effective (BL-284's Notes).
        const string Template = "[%{referer}][%{filename_effective}][%{conn_id}][%{xfer_id}]\\n";

        await RunOkAndFailingAsync(runsOnWindows: false, "-s", "-w", Template, "xyz://a/b", "ok://h/x");

        Diagnostics.Diff("stdout", "[][][-1][0]\n/x[][][0][1]\n", StandardOutputText);
        Assert.AreEqual("[][][-1][0]\n/x[][][0][1]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_GetQuery_IsInTheEffectiveUrl()
    {
        await RunOkAndFailingAsync(runsOnWindows: false, "-s", "-G", "-d", "a=b", "-o", "a", "-w", "%{url_effective}", "ok://h/x");

        Diagnostics.Diff("stdout", "ok://h/x?a=b", StandardOutputText);
        Assert.AreEqual("ok://h/x?a=b", StandardOutputText);
    }

    /// <summary>Measured (BL-444 Notes): <c>HTTP://LocalHost:1/a/../b</c> prints <c>http://LocalHost:1/b</c>.</summary>
    [TestMethod]
    public async Task RunAsync_UpperCaseSchemeAndDotSegments_EffectiveUrlIsNormalised()
    {
        await RunOkAndFailingAsync(runsOnWindows: false, "-s", "-o", "a", "-w", "%{url_effective}", "OK://H/a/../b");

        Diagnostics.Diff("stdout", "ok://H/b", StandardOutputText);
        Assert.AreEqual("ok://H/b", StandardOutputText);
    }

    /// <summary>
    /// Measured (BL-524 Notes): <c>curl --proto-default dict -w '%{url_effective}' ftp.localhost:1/</c>
    /// prints <c>dict://ftp.localhost:1/</c>; the default scheme replaces the host-name guess.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_ProtoDefault_SchemelessUrlUsesItInsteadOfTheGuess()
    {
        await RunAsync(
            new ProtocolDispatcher([RecordingProtocolHandler.WritingPath("dict"), RecordingProtocolHandler.WritingPath("ftp")]),
            runsOnWindows: false,
            null,
            "-s", "-o", "a", "--proto-default", "dict", "-w", "%{url_effective}", "ftp.localhost:1/");

        Diagnostics.Diff("stdout", "dict://ftp.localhost:1/", StandardOutputText);
        Assert.AreEqual("dict://ftp.localhost:1/", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UnknownVariableOnWindows_WarnsWithCrLf()
    {
        await RunOkAndFailingAsync(runsOnWindows: true, "-s", "-o", "a", "-w", "x%{nosuch}y", "ok://h/x");

        Diagnostics.Diff("stdout", "xy", StandardOutputText);
        Assert.AreEqual("xy", StandardOutputText);
        Diagnostics.Diff("stderr", "curl: unknown --write-out variable: 'nosuch'\r\n", StandardErrorText);
        Assert.AreEqual("curl: unknown --write-out variable: 'nosuch'\r\n", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirectiveWithoutAnOpener_StaysOnStandardOutput()
    {
        await RunOkAndFailingAsync(runsOnWindows: false, "-s", "-o", "a", "-w", "A%output{o.txt}B", "ok://h/x");

        Diagnostics.Diff("stdout", "AB", StandardOutputText);
        Assert.AreEqual("AB", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputDirectiveWithAnOpener_WritesToTheOpenedFile()
    {
        MemoryOpener opener = new();
        Diagnostics.Arrange("%output opener", "every file opens onto one memory stream");

        await RunAsync(
            new ProtocolDispatcher([RecordingProtocolHandler.WritingPath("ok")]),
            runsOnWindows: false,
            opener,
            "-s", "-o", "a", "-w", "A%output{o.txt}B", "ok://h/x");

        Diagnostics.Diff("stdout", "A", StandardOutputText);
        Assert.AreEqual("A", StandardOutputText);
        Diagnostics.Bytes("%output{o.txt} file", opener.Opened.ToArray());
        Diagnostics.Diff("%output{o.txt} file", "B", Encoding.UTF8.GetString(opener.Opened.ToArray()));
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
        Diagnostics.Arrange("body length", bodyLength);
        Diagnostics.Arrange("handler behaviour", "ok writes the body to standard output, which is closed; a write failure ends with exit 23");

        int exitCode = await RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([writing])),
                outputFiles,
                outputFiles,
                closed,
                standardError,
                new MemoryStream(),
                runsOnWindows: false),
            ["-s", "-w", "A%{exitcode}%{stderr}B%{exitcode}\\n", "ok://h/x"],
            runsOnWindows: false);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "B23\n", StandardErrorText);
        Assert.AreEqual("B23\n", StandardErrorText);
    }

    [TestMethod]
    [DataRow(";auto", "-L", 1, "http://127.0.0.1:18361/a", "|http://127.0.0.1:18361/a")]
    [DataRow(";auto", "-L", 2, "http://127.0.0.1:18361/b", "|http://127.0.0.1:18361/a|http://127.0.0.1:18361/b")]
    [DataRow("http://r/;auto", "-L", 1, "http://127.0.0.1:18361/a", "http://r/|http://127.0.0.1:18361/a")]
    [DataRow("http://r/;auto", "-L", 2, "http://127.0.0.1:18361/b", "http://r/|http://127.0.0.1:18361/a|http://127.0.0.1:18361/b")]
    [DataRow("http://r/;auto", "-s", 1, "http://r/", "http://r/")]
    [DataRow(";auto", "-s", 1, "", "")]
    public async Task RunAsync_AutoReferer_PrintsTheRefererTheLastRequestWasSentWith(
        string referer, string location, int redirects, string expectedReferer, string expectedSentReferers)
    {
        // curl -s -e <referer> [-L] -w "%{referer}", /a -> /b -> /c (measured, BL-361 Notes).
        string[] responses = [.. RedirectResponses(redirects), "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"];
        Diagnostics.Arrange("redirects before the 200", redirects);

        (int exitCode, ScriptedConnector server) = await RunHttpWithServerAsync(
            responses, "-s", "-e", referer, location, "-w", "%{referer}", "http://127.0.0.1:18361/a");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", expectedReferer, StandardOutputText);
        Assert.AreEqual(expectedReferer, StandardOutputText);
        Diagnostics.Diff("Referer header of each request, joined by |", expectedSentReferers, string.Join('|', SentReferers(server)));
        Assert.AreEqual(expectedSentReferers, string.Join('|', SentReferers(server)));
    }

    [TestMethod]
    public async Task RunAsync_AutoRefererFromAUrlWithUserAndFragment_PrintsItWithoutThemButWithTheQuery()
    {
        // curl -s -e ";auto" -L -w "%{referer}" http://u:p@127.0.0.1:18361/a?q=1#f (measured, BL-361 Notes).
        string[] responses = [.. RedirectResponses(1), "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"];

        (int exitCode, ScriptedConnector server) = await RunHttpWithServerAsync(
            responses, "-s", "-e", ";auto", "-L", "-w", "%{referer}", "http://u:p@127.0.0.1:18361/a?q=1#f");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "http://127.0.0.1:18361/a?q=1", StandardOutputText);
        Assert.AreEqual("http://127.0.0.1:18361/a?q=1", StandardOutputText);
        Diagnostics.Diff("Referer header of each request, joined by |", "|http://127.0.0.1:18361/a?q=1", string.Join('|', SentReferers(server)));
        Assert.AreEqual("|http://127.0.0.1:18361/a?q=1", string.Join('|', SentReferers(server)));
    }

    [TestMethod]
    [DataRow(WriteOutTimeDialect.Glibc, "[2026-09-27]")]
    [DataRow(WriteOutTimeDialect.WindowsCRuntime, "[]")]
    public async Task RunAsync_TimeTemplateWithIsoDate_PrintsItInTheDialectTheRunnerWasGiven(WriteOutTimeDialect dialect, string expected)
    {
        // glibc's strftime knows %F; the Windows C runtime rejects it, so the whole %time{%F} prints nothing (ADR-0078).
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([RecordingProtocolHandler.WritingPath("ok")])),
            outputFiles,
            outputFiles,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            timeProvider: new FixedUtcClock(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero)),
            writeOutTimeDialect: dialect);
        Diagnostics.Arrange("write-out time dialect", dialect);
        Diagnostics.Arrange("clock", "fixed at 2026-09-27T12:00:00Z, local time zone UTC");

        int exitCode = await RunWithDiagnosticsAsync(
            runner, ["-s", "-o", "out.txt", "-w", "[%time{%F}]", "ok://h/x"], runsOnWindows: false);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", expected, StandardOutputText);
        Assert.AreEqual(expected, StandardOutputText);
    }

    private static IEnumerable<string> RedirectResponses(int count) =>
        Enumerable.Range(0, count).Select(index => $"HTTP/1.1 302 Found\r\nLocation: /{(char)('b' + index)}\r\nContent-Length: 0\r\n\r\n");

    /// <summary>Each request's <c>Referer</c> value, empty for a request that sent none.</summary>
    private static IEnumerable<string> SentReferers(ScriptedConnector server) =>
        Encoding.Latin1.GetString(server.Written)
            .Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(head => head.Split("\r\n").FirstOrDefault(line => line.StartsWith("Referer: ", StringComparison.Ordinal))?["Referer: ".Length..] ?? string.Empty);

    private async Task<(int ExitCode, ScriptedConnector Server)> RunHttpWithServerAsync(string[] responses, params string[] arguments)
    {
        ScriptedConnector server = new(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("scripted HTTP responses (CRLF shown as LF)", Normalized(string.Join(" | ", responses)));

        int exitCode = await RunAsync(new ProtocolDispatcher([http]), runsOnWindows: false, null, arguments);
        return (exitCode, server);
    }

    private Task<int> RunHttpAsync(string[] responses, bool runsOnWindows, params string[] arguments)
    {
        ScriptedConnector server = new(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("scripted HTTP responses (CRLF shown as LF)", Normalized(string.Join(" | ", responses)));

        return RunAsync(new ProtocolDispatcher([http]), runsOnWindows, null, arguments);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> with an <c>ok</c> scheme that writes the URL's path and
    /// succeeds, and a <c>fail</c> scheme that fails with exit 7.
    /// </summary>
    private Task<int> RunOkAndFailingAsync(bool runsOnWindows, params string[] arguments)
    {
        Diagnostics.Arrange("handler behaviour", "ok writes the URL's path and succeeds; fail fails with exit 7, Failed to connect");
        Diagnostics.Arrange("unwritable paths", string.Join(", ", outputFiles.UnwritablePaths));

        return RunAsync(
            new ProtocolDispatcher(
            [
                RecordingProtocolHandler.WritingPath("ok"),
                RecordingProtocolHandler.Failing("fail", CurlExitCode.CouldntConnect, "Failed to connect"),
            ]),
            runsOnWindows,
            null,
            arguments);
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private Task<int> RunAsync(
        ProtocolDispatcher dispatcher,
        bool runsOnWindows,
        IWriteOutFileOpener? opener,
        params string[] arguments) =>
        RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(dispatcher),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                writeOutFileOpener: opener,
                timeProvider: TimeProvider.System),
            arguments,
            runsOnWindows);

    private async Task<int> RunWithDiagnosticsAsync(CurlCommandRunner runner, IReadOnlyList<string> arguments, bool runsOnWindows)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr (CRLF shown as LF)", Normalized(StandardErrorText));
        return exitCode;
    }

    /// <summary>A clock stopped at one instant, in a UTC local time zone.</summary>
    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;
    }

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
