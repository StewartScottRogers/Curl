using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Adversarial black-box tests (BL-1495, by <c>Documentation/Wiki/Adversarial-Testing.md</c>) that attack
/// <see cref="CurlCommandRunner"/> through the command line only: numeric options at and past their limits,
/// malformed and blank arguments, URLs in invalid partitions, and the same refused command line run from many
/// runners at once. Every expected exit code and stderr line was measured from curl 8.21.0 (mingw, Schannel)
/// on 2026-10-07 with <c>curl &lt;args&gt; http://127.0.0.1:1/ -o /dev/null</c>. No test opens a socket: the
/// only handler is a recording fake.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerAdversarialCommandLineTests
{
    private const string Url = "http://127.0.0.1:1/";

    private static readonly string TryHelpLine =
        "curl: try 'curl --help' or 'curl --manual' for more information" + Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--max-time", "abc")]
    [DataRow("--max-time", "-1")]
    [DataRow("--connect-timeout", "x")]
    [DataRow("--expect100-timeout", "abc")]
    [DataRow("--max-filesize", "-1")]
    [DataRow("--max-redirs", "-2")]
    [DataRow("--retry", "1.5")]
    [DataRow("--retry", "2147483648")]
    public async Task RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit(
        string option, string value)
    {
        RunResult result = await RunAsync([option, value, Url]);

        AssertRefusedBeforeAnyTransfer(
            result,
            $"curl: option {option}: expected a proper numerical parameter" + Environment.NewLine + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_RetryJustBelowZero_PrintsPositiveNumericalParameterAndExitsFailedInit()
    {
        RunResult result = await RunAsync(["--retry", "-1", Url]);

        AssertRefusedBeforeAnyTransfer(
            result,
            "curl: option --retry: expected a positive numerical parameter" + Environment.NewLine + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_RetryAtIntMaxValue_RunsTheTransfer()
    {
        RunResult result = await RunAsync(["--retry", "2147483647", "--retry-max-time", "0", Url], handlerExitCode: CurlExitCode.Ok);

        Diagnostics.Assert("exit code", (int)CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((int)CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("transfers run", 1, result.TransfersRun);
        Assert.AreEqual(1, result.TransfersRun);
    }

    [TestMethod]
    public async Task RunAsync_LongOptionWithEqualsAndNoValue_NamesTheWholeArgumentAndExitsFailedInit()
    {
        RunResult result = await RunAsync(["--max-time=", Url]);

        AssertRefusedBeforeAnyTransfer(
            result,
            "curl: option --max-time=: expected a proper numerical parameter" + Environment.NewLine + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_UnknownLongOption_PrintsIsUnknownAndExitsFailedInit()
    {
        RunResult result = await RunAsync(["--bogus-option", Url]);

        AssertRefusedBeforeAnyTransfer(
            result,
            "curl: option --bogus-option: is unknown" + Environment.NewLine + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_ProtoWithOnlyAnUnknownProtocol_WarnsThenPrintsBadlyUsedAndExitsFailedInit()
    {
        RunResult result = await RunAsync(["--proto", "=bogus", Url]);

        AssertRefusedBeforeAnyTransfer(
            result,
            "Warning: unrecognized protocol 'bogus'" + Environment.NewLine
            + "curl: option --proto: is badly used here" + Environment.NewLine
            + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_BlankUrlArgument_PrintsBlankArgumentAndExitsFailedInit()
    {
        RunResult result = await RunAsync([string.Empty]);

        AssertRefusedBeforeAnyTransfer(
            result,
            "curl: option : blank argument where content is expected" + Environment.NewLine + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_WriteOutConsumingTheOnlyUrl_PrintsNoUrlSpecifiedAndExitsFailedInit()
    {
        RunResult result = await RunAsync(["-w", Url]);

        AssertRefusedBeforeAnyTransfer(
            result,
            "curl: (2) no URL specified" + Environment.NewLine + TryHelpLine);
    }

    [TestMethod]
    public async Task RunAsync_PortOnePastTheLargest_PrintsUrlRejectedAndExitsUrlMalformat()
    {
        RunResult result = await RunAsync(["-s", "-S", "http://127.0.0.1:65536/"]);

        AssertTransferRefused(
            result,
            CurlExitCode.UrlMalformat,
            "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535" + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_UserInfoWithEmptyHostAndSecondAt_PrintsUrlRejectedAndExitsUrlMalformat()
    {
        RunResult result = await RunAsync(["-s", "-S", "http://user@:80@host/"]);

        AssertTransferRefused(
            result,
            CurlExitCode.UrlMalformat,
            "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535" + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_UnknownScheme_PrintsProtocolNotSupportedAndExitsUnsupportedProtocol()
    {
        RunResult result = await RunAsync(["-s", "-S", "bogus://x/"]);

        AssertTransferRefused(
            result,
            CurlExitCode.UnsupportedProtocol,
            "curl: (1) Protocol \"bogus\" not supported" + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_SameRefusedCommandLineFromManyRunnersAtOnce_EveryRunnerAnswersAsOneRunAlone()
    {
        const int Runners = 16;
        string expected = "curl: option --retry: expected a positive numerical parameter" + Environment.NewLine + TryHelpLine;
        Diagnostics.Arrange("runners", Runners);

        RunResult[] results = await Task.WhenAll(
            Enumerable.Range(0, Runners).Select(_ => Task.Run(() => RunAsync(["--retry", "-1", Url]))));

        foreach (RunResult result in results)
        {
            AssertRefusedBeforeAnyTransfer(result, expected);
        }
    }

    private void AssertRefusedBeforeAnyTransfer(RunResult result, string expectedStandardError)
    {
        AssertTransferRefused(result, CurlExitCode.FailedInit, expectedStandardError);
        Diagnostics.Assert("dispatches created", 0, result.DispatchesCreated);
        Assert.AreEqual(0, result.DispatchesCreated);
    }

    private void AssertTransferRefused(RunResult result, CurlExitCode expectedExitCode, string expectedStandardError)
    {
        Diagnostics.Assert("exit code", (int)expectedExitCode, result.ExitCode);
        Assert.AreEqual((int)expectedExitCode, result.ExitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(result.StandardError));
        Assert.AreEqual(expectedStandardError, result.StandardError);
        Diagnostics.Assert("stdout length", 0, result.StandardOutputLength);
        Assert.AreEqual(0, result.StandardOutputLength);
        Diagnostics.Assert("transfers run", 0, result.TransfersRun);
        Assert.AreEqual(0, result.TransfersRun);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<RunResult> RunAsync(IReadOnlyList<string> arguments, CurlExitCode handlerExitCode = CurlExitCode.CouldntConnect)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        InMemoryFileSystem fileSystem = new();
        RecordingProtocolHandler http = handlerExitCode == CurlExitCode.Ok
            ? RecordingProtocolHandler.WritingPath("http")
            : RecordingProtocolHandler.Failing("http", handlerExitCode, "Failed to connect to 127.0.0.1 port 1");
        int dispatchesCreated = 0;
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
                _ =>
                {
                    Interlocked.Increment(ref dispatchesCreated);
                    return new TransferDispatch(new ProtocolDispatcher([http]), []);
                },
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                TerminalColumns.Default)
            .RunAsync(arguments);

        RunResult result = new(
            exitCode,
            Encoding.UTF8.GetString(standardError.ToArray()),
            (int)standardOutput.Length,
            dispatchesCreated,
            http.Contexts.Count);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(result.StandardError));
        return result;
    }

    private sealed record RunResult(int ExitCode, string StandardError, int StandardOutputLength, int DispatchesCreated, int TransfersRun);
}
