using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="VerifyResultRecordingTransferEvents" />: each certificate verify code lands on
/// the running transfer, the origin's and the proxy's apart, and every event goes on to the
/// transfer's own events unchanged (BL-661).
/// </summary>
[TestClass]
public sealed class VerifyResultRecordingTransferEventsTests
{
    [TestMethod]
    public void ReportCertificateVerifyResult_OriginsAndProxys_RecordsEachOnTheRunningTransfer()
    {
        RunningTransferState state = NewState();
        ITransferEvents events = new VerifyResultRecordingTransferEvents(NoTransferEvents.Instance, state);

        events.ReportCertificateVerifyResult(20, isProxy: true);
        events.ReportCertificateVerifyResult(18, isProxy: false);

        Assert.AreEqual(18L, state.SslVerifyResult);
        Assert.AreEqual(20L, state.ProxySslVerifyResult);
    }

    [TestMethod]
    public void NoCodeReported_LeavesBothZero()
    {
        RunningTransferState state = NewState();
        _ = new VerifyResultRecordingTransferEvents(NoTransferEvents.Instance, state);

        Assert.AreEqual(0L, state.SslVerifyResult);
        Assert.AreEqual(0L, state.ProxySslVerifyResult);
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CallRecordingEvents inner = new();
        ITransferEvents events = new VerifyResultRecordingTransferEvents(inner, NewState());

        events.ReportInfo("text");
        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(18, isProxy: false);
        events.ReportRequestHeader([2]);
        events.ReportResponseHeader([3]);
        events.ReportDataSent([4]);
        events.ReportDataReceived([5]);

        CollectionAssert.AreEqual(
            new[]
            {
                "Info text", "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
                "VerifyResult 18 False", "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
            },
            inner.Calls);
    }

    private static RunningTransferState NewState()
    {
        StandardOutputFailureDeferringStream standardOutput = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, standardOutput, standardOutput);
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

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"RequestHeader {bytes[0]}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ResponseHeader {bytes[0]}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"DataSent {bytes[0]}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"DataReceived {bytes[0]}");
    }
}
