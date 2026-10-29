using System.Collections.Concurrent;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins BL-3986: a warning reported through <see cref="ITransferEvents.ReportWarning" /> reaches
/// standard error as curl's <c>Warning: &lt;text&gt;</c> line with or without <c>-v</c>, nothing
/// under <c>-s</c>, and every console wrapper of the run's events passes it on.
/// </summary>
[TestClass]
public sealed class TransferEventWarningTests
{
    private const string Warning = "VLAN priority 7 failed with errno 1: Operation not permitted";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenAsync_NoVerbose_WritesWarningLineOnStandardError()
    {
        using var standardError = new MemoryStream();
        await using TransferEventOutput output = await OpenAsync(standardError, runsOnWindows: false);
        Diagnostics.Arrange("options", "no -v");

        output.Events.ReportWarning(Warning);

        string written = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Assert("standard error", $"Warning: {Warning}\n", written);
        Assert.AreEqual($"Warning: {Warning}\n", written);
    }

    [TestMethod]
    public async Task OpenAsync_Windows_WritesWarningLineEndingInCrLf()
    {
        using var standardError = new MemoryStream();
        await using TransferEventOutput output = await OpenAsync(standardError, runsOnWindows: true, "-v");
        Diagnostics.Arrange("options", "-v, Windows text mode");

        output.Events.ReportWarning(Warning);

        string written = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Assert("standard error", $"Warning: {Warning}\r\n", written);
        Assert.AreEqual($"Warning: {Warning}\r\n", written);
    }

    [TestMethod]
    public async Task OpenAsync_Silent_WritesNothing()
    {
        using var standardError = new MemoryStream();
        await using TransferEventOutput output = await OpenAsync(standardError, runsOnWindows: false, "-s");
        Diagnostics.Arrange("options", "-s");

        output.Events.ReportWarning(Warning);

        Diagnostics.Assert("standard error length", 0L, standardError.Length);
        Assert.AreEqual(0L, standardError.Length);
        Assert.AreSame(TransferEventOutput.None, output);
    }

    [TestMethod]
    public void ReportWarning_LongerThanTheTerminal_WrapsAsWarnf()
    {
        using var standardError = new MemoryStream();
        var events = new WarningWritingTransferEvents(NoTransferEvents.Instance, standardError);
        string text = string.Join(' ', Enumerable.Repeat("word", 20));
        Diagnostics.Arrange("warning", text);

        events.ReportWarning(text);

        string[] lines = Encoding.UTF8.GetString(standardError.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Diagnostics.Assert("line count", 2, lines.Length);
        Assert.HasCount(2, lines);
        Assert.IsTrue(lines.All(line => line.StartsWith("Warning: ", StringComparison.Ordinal) && line.Length <= TerminalColumns.Default));
    }

    [TestMethod]
    public void WarningWritingTransferEvents_EveryOtherEvent_IsPassedOn()
    {
        var inner = new WarningRecordingEvents();
        ITransferEvents events = new WarningWritingTransferEvents(inner, Stream.Null);
        Diagnostics.Arrange("inner", "recording");

        ReportEveryEvent(events);

        Diagnostics.Assert("calls", 13, inner.Calls.Count);
        CollectionAssert.AreEqual(
            new[] { "info", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify", "early-data", "request-header", "response-header", "data-sent", "data-received" },
            inner.Calls);
        Assert.IsEmpty(inner.Warnings);
    }

    [TestMethod]
    public void ConsoleWrappers_ReportWarning_PassItOn()
    {
        var inner = new WarningRecordingEvents();
        RunningTransferState state = NewState();
        ITransferEvents[] wrappers =
        [
            new ConnectionIdRecordingTransferEvents(inner, state, new ConcurrentDictionary<long, long>(), () => 1),
            new MultiStateTraceEvents(inner, TimeProvider.System, () => 1),
            new OutputFileOpenFailureTransferEvents(inner, state, null),
            new ResponseCodeRecordingTransferEvents(inner, state),
            new TlsResultRecordingTransferEvents(inner, state),
        ];
        Diagnostics.Arrange("wrappers", wrappers.Length);

        foreach (ITransferEvents wrapper in wrappers)
        {
            wrapper.ReportWarning(Warning);
        }

        Diagnostics.Assert("warnings", wrappers.Length, inner.Warnings.Count);
        Assert.HasCount(wrappers.Length, inner.Warnings);
        Assert.IsTrue(inner.Warnings.All(warning => warning == Warning));
    }

    private static async Task<TransferEventOutput> OpenAsync(Stream standardError, bool runsOnWindows, params string[] arguments)
    {
        CommandLineOptions options = CommandLineParser.Parse([.. arguments, "http://127.0.0.1/"], _ => true).Options!;
        return await TransferEventOutput.OpenAsync(
            options, new InMemoryFileSystem(), Stream.Null, standardError, runsOnWindows, standardOutputIsTerminal: false, TimeProvider.System, () => false);
    }

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream output = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, output, output);
    }

    private static void ReportEveryEvent(ITransferEvents events)
    {
        events.ReportInfo("i");
        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(0, isProxy: false);
        events.ReportTlsEarlyData(0);
        events.ReportRequestHeader([1]);
        events.ReportResponseHeader([1]);
        events.ReportDataSent([1]);
        events.ReportDataReceived([1]);
    }

    private sealed class WarningRecordingEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public List<string> Warnings { get; } = [];

        public void ReportWarning(string text) => Warnings.Add(text);

        public void ReportInfo(string text) => Calls.Add("info");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add("tls-data");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("tls-message");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("trust");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add("verify");

        public void ReportTlsEarlyData(long bytes) => Calls.Add("early-data");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add("request-header");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add("response-header");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add("data-sent");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add("data-received");
    }
}
