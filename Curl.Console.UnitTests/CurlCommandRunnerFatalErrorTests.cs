using System.Text;

using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a transfer failing with one of the codes curl's <c>is_fatal_error()</c> names (2, 27, 43, 48)
/// ends the whole run, <c>--next</c> groups and further URLs included, and that under <c>-Z</c> the
/// abandoned transfers are reported as aborted by a critical error in another transfer (BL-5313).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerFatalErrorTests
{
    private const string Fail = "http://h.test/fail";

    private const string Next = "http://h.test/next";

    private const string Other = "http://h.test/other";

    private const string Aborted = "curl: (42) Transfer aborted due to critical error in another transfer";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    [DataRow(CurlExitCode.FailedInit)]
    [DataRow(CurlExitCode.OutOfMemory)]
    [DataRow(CurlExitCode.BadFunctionArgument)]
    [DataRow(CurlExitCode.UnknownOption)]
    public async Task RunAsync_FatalFailureThenNextGroup_NeverTransfersTheNextGroup(CurlExitCode code)
    {
        RecordingProtocolHandler http = Handler(code);

        int exitCode = await RunAsync(http, ["-sS", Fail, "--next", Next]);

        Assert.AreEqual((int)code, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    [DataRow(CurlExitCode.FailedInit)]
    [DataRow(CurlExitCode.OutOfMemory)]
    [DataRow(CurlExitCode.BadFunctionArgument)]
    [DataRow(CurlExitCode.UnknownOption)]
    public async Task RunAsync_FatalFailureThenNextUrlOfTheGroup_NeverTransfersTheNextUrl(CurlExitCode code)
    {
        RecordingProtocolHandler http = Handler(code);

        int exitCode = await RunAsync(http, ["-sS", "-w", "%{response_code}\n", Fail, Next]);

        Assert.AreEqual((int)code, exitCode);
        Assert.HasCount(1, http.Contexts);
        Assert.AreEqual("000\n", StandardOutputText);
        Assert.AreEqual(1, StandardErrorText.Split("curl: (", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntConnect)]
    [DataRow(CurlExitCode.OperationTimedOut)]
    public async Task RunAsync_NonFatalFailure_StillRunsTheNextGroupAndUrl(CurlExitCode code)
    {
        RecordingProtocolHandler http = Handler(code);

        int exitCode = await RunAsync(http, ["-sS", Fail, "--next", Next, Other]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(3, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FatalFailureUnderParallel_ExitsWithItAndReportsTheAbandonedTransferAborted()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunParallelAsync(http, ["-Z", "-sS", "http://h.test/a", "--next", "http://o.test/b"]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b")).WaitAsync(TimeSpan.FromSeconds(10));
        http.Fail("/a", CurlExitCode.BadFunctionArgument, "Session ID cannot be set as a custom header.");

        int exitCode = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual($"curl: (43) Session ID cannot be set as a custom header.\n{Aborted}\n", StandardErrorText);
    }

    [TestMethod]
    [DataRow("rtsp://h.test/m", "Session: 1", 43, "(43) Session ID cannot be set as a custom header.")]
    [DataRow("telnet://h.test", "x", 48, "(48) An unknown option was passed in to libcurl")]
    public async Task RunAsync_FatalSetupFailureUnderParallel_StartsNothingAndAbortsTheOthers(string url, string marker, int code, string line)
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.Ok);
        RecordingProtocolHandler other = new(new Uri(url).Scheme, _ => ValueTask.FromResult(TransferResult.Success(0)));
        string[] first = marker == "x" ? [url, "-t", "BOGUS=1"] : url.StartsWith("telnet", StringComparison.Ordinal) ? [url, "-t", marker] : [url, "-H", marker];

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http, other])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-sS", "-Z", "-w", "[%{exitcode}]\n", .. first, "--next", "-w", "[%{exitcode}]\n", "http://h.test/next"]);

        Assert.AreEqual(code, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.IsEmpty(other.Contexts);
        Assert.AreEqual($"curl: {line}\n{Aborted}\n", StandardErrorText);
        Assert.AreEqual($"[{code}]\n[42]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FatalSetupFailureFirstUnderParallelMaxOne_ReportsTheNextTransferWithTheFailureToo()
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.Ok);
        RecordingProtocolHandler telnet = new("telnet", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await RunWithAsync(
            [http, telnet],
            ["-sS", "-Z", "--parallel-max", "1", "-w", "[%{exitcode}]\n", "telnet://h.test", "-t", "NOPE=1", "--next", "-w", "[%{exitcode}]\n", "http://h.test/hello"]);

        Assert.AreEqual(48, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual("[48]\n[48]\n", StandardOutputText);
        Assert.DoesNotContain("Index", StandardErrorText, StringComparison.Ordinal);
        Assert.AreEqual(
            "curl: (48) Unknown telnet option NOPE=1\ncurl: (48) Unknown telnet option NOPE=1\n",
            StandardErrorText);
    }

    [TestMethod]
    [DataRow("NEW_ENV=nocomma", "")]
    [DataRow("TTYPE=0123456789012345678901234567890123456789", "")]
    [DataRow("XDISPLOC=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", "")]
    [DataRow("NEW_ENV=nocomma", "1")]
    [DataRow("TTYPE=0123456789012345678901234567890123456789", "1")]
    public async Task RunAsync_TelnetOptionValueCurlJudgesOnlyOnSubnegotiationUnderParallel_StartsEveryTransfer(string option, string parallelMax)
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.Ok);
        RecordingProtocolHandler telnet = new("telnet", _ => ValueTask.FromResult(TransferResult.Success(0)));
        string[] maximum = parallelMax.Length == 0 ? [] : ["--parallel-max", parallelMax];

        int exitCode = await RunWithAsync(
            [http, telnet],
            ["-sS", "-Z", .. maximum, "-w", "[%{exitcode}]\n", "telnet://h.test", "-t", option, "--next", "-w", "[%{exitcode}]\n", "http://h.test/hello"]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, telnet.Contexts);
        Assert.HasCount(1, http.Contexts);
        Assert.AreEqual("[0]\nhi[0]\n", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FatalSetupFailureBeyondParallelMax_LetsTheEarlierTransferRun()
    {
        RecordingProtocolHandler http = Handler(CurlExitCode.Ok);
        RecordingProtocolHandler telnet = new("telnet", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await RunWithAsync(
            [http, telnet],
            ["-sS", "-Z", "--parallel-max", "1", "-w", "[%{exitcode}]\n", "http://h.test/hello", "--next", "-w", "[%{exitcode}]\n", "telnet://h.test", "-t", "NOPE=1", "--next", "-w", "[%{exitcode}]\n", "http://h.test/hello"]);

        Assert.AreEqual(48, exitCode);
        Assert.HasCount(1, http.Contexts);
        Assert.IsEmpty(telnet.Contexts);
        Assert.AreEqual("hi[0]\n[48]\n", StandardOutputText);
        Assert.DoesNotContain("Index", StandardErrorText, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_SetupLooksFineUnderParallel_StartsEveryTransfer()
    {
        RecordingProtocolHandler rtsp = new("rtsp", _ => ValueTask.FromResult(TransferResult.Success(0)));
        RecordingProtocolHandler telnet = new("telnet", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([rtsp, telnet])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-sS", "-Z", "rtsp://h.test/m", "-H", "Foo: 1", "-H", "Session", "--next", "telnet://h.test", "-t", "ttype=x", "-t", "noequals", "-t", "WS=é", "-t", "BINARY=1", "-t", "TTYPE=vt100", "-t", "XDISPLOC=h:0", "-t", "NEW_ENV=A,b"]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, rtsp.Contexts);
        Assert.HasCount(1, telnet.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_TwoFatalSetupFailuresUnderParallel_ReportsTheFirstWithItsOwnMessage()
    {
        RecordingProtocolHandler rtsp = new("rtsp", _ => ValueTask.FromResult(TransferResult.Success(0)));
        RecordingProtocolHandler telnet = new("telnet", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([rtsp, telnet])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-sS", "-Z", "rtsp://h.test/m", "-H", "Session: 1", "--next", "telnet://h.test", "-t", "NOPE=1"]);

        Assert.AreEqual(43, exitCode);
        Assert.IsEmpty(rtsp.Contexts);
        Assert.IsEmpty(telnet.Contexts);
        Assert.AreEqual($"curl: (43) Session ID cannot be set as a custom header.\n{Aborted}\n", StandardErrorText);
    }

    private static RecordingProtocolHandler Handler(CurlExitCode code) =>
        new("http", async context =>
        {
            if (context.Url.AbsolutePath == "/fail")
            {
                return TransferResult.Failure(code, "failed on purpose");
            }

            await context.Output.WriteAsync("hi"u8.ToArray(), context.CancellationToken);
            return TransferResult.Success(2);
        });

    private Task<int> RunWithAsync(IProtocolHandler[] handlers, string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(handlers)),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(arguments);

    private Task<int> RunAsync(IProtocolHandler handler, string[] arguments) => RunParallelAsync(handler, arguments);

    private Task<int> RunParallelAsync(IProtocolHandler handler, string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(arguments);
}
