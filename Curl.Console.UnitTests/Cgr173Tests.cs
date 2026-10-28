using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-173 (BL-3702): under <c>-Z --fail-early</c> a transfer aborted after its response arrived
/// still prints that response's <c>%{http_code}</c> and its <c>%{filename_effective}</c>, and
/// <c>--remove-on-error</c> deletes the file it had opened, as curl 8.22.0 does:
/// <c>1 22 500 b.txt</c>, <c>0 42 200 a.txt</c>, no files left, exit 22.
/// </summary>
[TestClass]
public sealed class Cgr173Tests
{
    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_FailEarlyAbortsATransferThatHadItsResponse_PrintsItsCodeAndFileAndRemovesTheFile()
    {
        BodyThenFailureHandler http = new();
        string[] arguments =
        [
            "-Z", "--parallel-immediate", "-s", "--remove-on-error", "--fail-early", "-f",
            "-w", "%{urlnum} %{exitcode} %{http_code} %{filename_effective}\\n",
            "-o", "a.txt", "http://127.0.0.1:22000/a", "-o", "b.txt", "http://127.0.0.1:22000/b",
        ];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);
        string stdout = Encoding.UTF8.GetString(standardOutput.ToArray());
        string deleted = string.Join(", ", files.DeleteAttempts);
        Diagnostics.Act("exit code, stdout, deleted", $"{exitCode} [{stdout}] [{deleted}]");

        Diagnostics.Assert("exit code", 22, exitCode);
        Diagnostics.Diff("stdout", "1 22 500 b.txt\n0 42 200 a.txt\n", stdout);
        Diagnostics.Assert("deleted", "a.txt", deleted);
        Assert.AreEqual(22, exitCode);
        Assert.AreEqual("1 22 500 b.txt\n0 42 200 a.txt\n", stdout);
        CollectionAssert.AreEqual(new[] { "a.txt" }, files.DeleteAttempts);
        Assert.IsFalse(files.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n", 200)]
    [DataRow("HTTP/2 404 \r\n", 404)]
    [DataRow("HTTP/1.1 100 Continue\r\n", 100)]
    public void ReportResponseHeader_StatusLine_RecordsItsCode(string line, int code)
    {
        RunningTransferState state = NewState();
        ITransferEvents events = new ResponseCodeRecordingTransferEvents(NoTransferEvents.Instance, state);

        events.ReportResponseHeader(Encoding.ASCII.GetBytes(line));
        Diagnostics.Act("recorded", state.ReceivedResponseCode);

        Diagnostics.Assert("recorded", code, state.ReceivedResponseCode);
        Assert.AreEqual(code, state.ReceivedResponseCode);
    }

    [TestMethod]
    [DataRow("Content-Length: 200\r\n")]
    [DataRow("HTTP/1.1\r\n")]
    [DataRow("HTTP/1.1 20")]
    [DataRow("HTTP/1.1 2x0 OK\r\n")]
    [DataRow("HTTP/1.1 +20 OK\r\n")]
    public void ReportResponseHeader_LineThatIsNoStatusLine_RecordsNothing(string line)
    {
        RunningTransferState state = NewState();
        ITransferEvents events = new ResponseCodeRecordingTransferEvents(NoTransferEvents.Instance, state);

        events.ReportResponseHeader(Encoding.ASCII.GetBytes(line));
        Diagnostics.Act("recorded", state.ReceivedResponseCode);

        Diagnostics.Assert("recorded", null, state.ReceivedResponseCode);
        Assert.IsNull(state.ReceivedResponseCode);
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new ResponseCodeRecordingTransferEvents(inner, NewState());
        Diagnostics.Arrange("events reported", "all thirteen, in interface order");

        events.ReportInfo("text");
        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(18, isProxy: false);
        events.ReportTlsEarlyData(-7);
        events.ReportRequestHeader([2]);
        events.ReportResponseHeader([3]);
        events.ReportDataSent([4]);
        events.ReportDataReceived([5]);
        Diagnostics.Act("inner calls", string.Join(", ", inner.Calls));

        CollectionAssert.AreEqual(
            new[]
            {
                "Info text", "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
                "VerifyResult 18 False", "EarlyData -7", "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
            },
            inner.Calls);
    }

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream output = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, output, output);
    }

    /// <summary>
    /// The repro's server: <c>/a</c> answers <c>200</c> and sends part of its body, then waits until it is
    /// aborted; <c>/b</c>, once <c>/a</c>'s first part is written, fails <c>-f</c>'s way with <c>500</c>.
    /// </summary>
    private sealed class BodyThenFailureHandler : IProtocolHandler
    {
        private readonly TaskCompletionSource firstPartWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            if (context.Url.AbsolutePath == "/a")
            {
                context.Events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
                context.Events.ReportResponseHeader("Content-Length: 50000\r\n"u8);
                await context.Output.WriteAsync(new byte[10000], context.CancellationToken);
                firstPartWritten.SetResult();
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }

            await firstPartWritten.Task;
            context.Events.ReportResponseHeader("HTTP/1.1 500 Internal Server Error\r\n"u8);
            return TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: 500")
                with
            { Report = new TransferReport { ResponseCode = 500 } };
        }
    }

    /// <summary>Records each event it is given as one line naming it and its payload.</summary>
    private sealed class CallRecordingEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Calls.Add($"Info {text}");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("Opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("Reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("Handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add($"TlsData {bytes[0]} {sent}");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("TlsMessage");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("TlsTrust");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add($"VerifyResult {verifyResult} {isProxy}");

        public void ReportTlsEarlyData(long bytes) => Calls.Add($"EarlyData {bytes}");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"RequestHeader {bytes[0]}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ResponseHeader {bytes[0]}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"DataSent {bytes[0]}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"DataReceived {bytes[0]}");
    }
}
