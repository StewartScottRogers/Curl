using System.Text;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

[TestClass]
public sealed class OutputFileOpenFailureTransferEventsTests
{
    private const string HandlersLine = "Failure writing output to destination, passed 12 returned 0";

    private readonly CallRecordingEvents inner = new();
    private readonly MemoryStream warnings = new();

    [TestMethod]
    public async Task ReportInfo_FileCouldNotBeOpened_WritesTheWarningThenCurlsLine()
    {
        RunningTransferState state = await StateWithFailedOpenAsync();

        new OutputFileOpenFailureTransferEvents(inner, state, warnings).ReportInfo(HandlersLine);

        CollectionAssert.AreEqual(new[] { "Info client returned ERROR on write of 12 bytes" }, inner.Calls);
        Assert.AreEqual("Warning: Failed to open the file nodir/x: No such file or directory" + Environment.NewLine, Encoding.UTF8.GetString(warnings.ToArray()));
    }

    [TestMethod]
    public async Task ReportInfo_WarningAlreadyWritten_WritesItOnlyOnce()
    {
        RunningTransferState state = await StateWithFailedOpenAsync();
        var events = new OutputFileOpenFailureTransferEvents(inner, state, warnings);

        events.ReportInfo(HandlersLine);
        events.ReportInfo(HandlersLine);

        Assert.AreEqual(2, inner.Calls.Count);
        Assert.AreEqual(1, Encoding.UTF8.GetString(warnings.ToArray()).Split("Warning:").Length - 1);
    }

    [TestMethod]
    public async Task ReportInfo_SilentWarningOutput_KeepsTheWarningUnwrittenButChangesTheLine()
    {
        RunningTransferState state = await StateWithFailedOpenAsync();

        new OutputFileOpenFailureTransferEvents(inner, state, null).ReportInfo(HandlersLine);

        CollectionAssert.AreEqual(new[] { "Info client returned ERROR on write of 12 bytes" }, inner.Calls);
        Assert.AreEqual(0, warnings.Length);
    }

    [TestMethod]
    public void ReportInfo_NoOutputFile_PassesTheLineOn()
    {
        new OutputFileOpenFailureTransferEvents(inner, NewState(), warnings).ReportInfo(HandlersLine);

        CollectionAssert.AreEqual(new[] { "Info " + HandlersLine }, inner.Calls);
    }

    [TestMethod]
    public async Task ReportInfo_OtherLine_PassesItOn()
    {
        RunningTransferState state = await StateWithFailedOpenAsync();

        new OutputFileOpenFailureTransferEvents(inner, state, warnings).ReportInfo("closing connection #0");

        CollectionAssert.AreEqual(new[] { "Info closing connection #0" }, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherEvent_IsPassedOn()
    {
        var events = new OutputFileOpenFailureTransferEvents(inner, NewState(), warnings);
        byte[] bytes = [7];

        events.ReportConnectionOpened(default!);
        events.ReportConnectionReused(default!);
        events.ReportTlsHandshake(default!);
        events.ReportTlsData(bytes, true);
        events.ReportTlsMessage(default!);
        events.ReportTlsTrust(default!);
        events.ReportCertificateVerifyResult(3, true);
        events.ReportTlsEarlyData(5);
        events.ReportRequestHeader(bytes);
        events.ReportResponseHeader(bytes);
        events.ReportDataSent(bytes);
        events.ReportDataReceived(bytes);

        Assert.AreEqual(12, inner.Calls.Count);
        Assert.AreEqual("VerifyResult 3 True", inner.Calls[6]);
        Assert.AreEqual("EarlyData 5", inner.Calls[7]);
    }

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream output = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, output, output);
    }

    private static async Task<RunningTransferState> StateWithFailedOpenAsync()
    {
        InMemoryFileSystem files = new();
        files.UnwritablePaths.Add("nodir/x");
        DeferredOutputFileStream output = new(files, "nodir/x", FileWriteMode.Truncate);
        await Assert.ThrowsExactlyAsync<IOException>(() => output.WriteAsync(new byte[12].AsMemory()).AsTask());
        RunningTransferState state = NewState();
        state.OutputFile = output;

        return state;
    }

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
