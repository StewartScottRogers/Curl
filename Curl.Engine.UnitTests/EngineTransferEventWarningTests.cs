using System.Collections.Concurrent;
using System.Text;

using Curl.Api.Callbacks;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins that every <see cref="ITransferEvents" /> wrapper in the engine that forwards every member forwards
/// <see cref="ITransferEvents.ReportWarning" /> to its inner events rather than dropping it on the interface's
/// default no-op (BL-3996), and that the debug-callback events hand a warning to the callback as an info line.
/// </summary>
[TestClass]
public sealed class EngineTransferEventWarningTests
{
    private const string Warning = "Could not set TCP_KEEPALIVE";

    [TestMethod]
    public void ReportWarning_AbortedHeadRecordingTransferEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new AbortedHeadRecordingTransferEvents(inner));

    [TestMethod]
    public void ReportWarning_ClientReaderResetTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new ClientReaderResetTraceEvents(inner));

    [TestMethod]
    public void ReportWarning_ClientWriterTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new ClientWriterTraceEvents(inner));

    [TestMethod]
    public void ReportWarning_ConnectReplyHeadWritingEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new ConnectReplyHeadWritingEvents(inner, Stream.Null));

    [TestMethod]
    public void ReportWarning_EngineConnectionIdTransferEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new EngineConnectionIdTransferEvents(
            inner, new EngineTransferState(), new ConcurrentDictionary<long, long>(), () => 0, _ => { }));

    [TestMethod]
    public void ReportWarning_NegotiateFailureRecordingTransferEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new NegotiateFailureRecordingTransferEvents(inner));

    [TestMethod]
    public void ReportWarning_ResponseWaitTimerTraceEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new ResponseWaitTimerTraceEvents(inner, []));

    [TestMethod]
    public void ReportWarning_TlsVerifyResultRecordingTransferEvents_GoesToTheInnerEvents() =>
        AssertForwards(inner => new TlsVerifyResultRecordingTransferEvents(inner));

    [TestMethod]
    public void ReportWarning_DebugCallbackTransferEvents_GoesToTheCallbackAsAnInfoLine()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        ITransferEvents events = new DebugCallbackTransferEvents(
            (type, data) =>
            {
                calls.Add((type, Encoding.Latin1.GetString(data)));
                return 0;
            },
            TlsBackend.Schannel);

        events.ReportWarning(Warning);

        CollectionAssert.AreEqual(new (CurlInfoType, string)[] { (CurlInfoType.Text, Warning + "\n") }, calls);
    }

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
