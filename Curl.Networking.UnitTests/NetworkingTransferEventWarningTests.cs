using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that every <see cref="ITransferEvents" /> wrapper in Curl.Networking that forwards every member forwards
/// <see cref="ITransferEvents.ReportWarning" /> to its inner events rather than dropping it on the interface's
/// default no-op (BL-4031).
/// </summary>
[TestClass]
public sealed class NetworkingTransferEventWarningTests
{
    private const string Warning = "Could not set TCP_KEEPALIVE";

    [TestMethod]
    public void ReportWarning_AsyncResolveTeardownTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new AsyncResolveTeardownTraceEvents(inner));

    [TestMethod]
    public void ReportWarning_ConnectAttemptTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new ConnectAttemptTraceEvents(inner, "example.com", tracesHappyEyeballs: false, tracesTcp: false));

    [TestMethod]
    public void ReportWarning_DnsFilterTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new DnsFilterTraceEvents(inner));

    [TestMethod]
    public void ReportWarning_FlowScopedTransferEvents_GoesToTheCurrentEvents() =>
        AssertForwards(inner => new FlowScopedTransferEvents { Current = inner });

    [TestMethod]
    public void ReportWarning_HandshakeCapturingTransferEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new HandshakeCapturingTransferEvents(inner));

    [TestMethod]
    public void ReportWarning_HttpsConnectFilterTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new HttpsConnectFilterTraceEvents(inner, "h2"));

    [TestMethod]
    public void ReportWarning_SetupFilterTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new SetupFilterTraceEvents(inner, "example.com", 443));

    [TestMethod]
    public void ReportWarning_SspiFailureRecordingTransferEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new SspiFailureRecordingTransferEvents(inner));

    private static void AssertForwards(Func<ITransferEvents, ITransferEvents> wrap)
    {
        WarningRecordingEvents inner = new();

        wrap(inner).ReportWarning(Warning);

        CollectionAssert.AreEqual(new[] { Warning }, inner.Warnings);
    }

    private sealed class WarningRecordingEvents : ITransferEvents
    {
        public List<string> Warnings { get; } = [];

        public void ReportWarning(string text) => Warnings.Add(text);

        public void ReportInfo(string text)
        {
        }

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
