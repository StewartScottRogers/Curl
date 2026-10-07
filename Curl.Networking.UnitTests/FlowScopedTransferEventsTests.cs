using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// <see cref="FlowScopedTransferEvents" /> passes each event to the events set for the reporting
/// asynchronous flow, and drops it when none is set (BL-1102).
/// </summary>
[TestClass]
public sealed class FlowScopedTransferEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void EveryReport_WithCurrentSet_IsPassedOnToIt()
    {
        var inner = new CountingTransferEvents();
        var scoped = new FlowScopedTransferEvents { Current = inner };
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        Diagnostics.Arrange("current", "a counting sink");
        Diagnostics.Arrange("reports", "one of every kind");

        ReportEverything(scoped, endPoint);

        var expectedCalls = new[] { "info", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" };
        Diagnostics.Act("calls", string.Join(", ", inner.Calls));
        Diagnostics.Diff("calls", string.Join(", ", expectedCalls), string.Join(", ", inner.Calls));
        Diagnostics.Assert("current is the sink set", true, ReferenceEquals(inner, scoped.Current));
        CollectionAssert.AreEqual(
            expectedCalls,
            inner.Calls);
        Assert.AreSame(inner, scoped.Current);
    }

    [TestMethod]
    public void EveryReport_WithNoCurrent_IsDropped()
    {
        var scoped = new FlowScopedTransferEvents();
        Diagnostics.Arrange("current", "(none)");
        Diagnostics.Arrange("reports", "one of every kind");

        ReportEverything(scoped, new IPEndPoint(IPAddress.Loopback, 80));

        Diagnostics.Act("current", scoped.Current is null ? "(none)" : "a sink");
        Diagnostics.Assert("current", "(none)", scoped.Current is null ? "(none)" : "a sink");
        Assert.IsNull(scoped.Current);
    }

    [TestMethod]
    public async Task Current_SetInsideAnAsyncMethod_ReachesWhatItAwaitsAndIsGoneAfter()
    {
        var scoped = new FlowScopedTransferEvents();
        var inner = new CountingTransferEvents();

        Diagnostics.Arrange("reports", "\"inside\" from the async method that sets current, \"after\" once it returned");

        await SetAndReportAsync(scoped, inner);
        scoped.ReportInfo("after");

        Diagnostics.Act("calls", string.Join(", ", inner.Calls));
        Diagnostics.Act("current after", scoped.Current is null ? "(none)" : "a sink");
        Diagnostics.Assert("calls", "inside", string.Join(", ", inner.Calls));
        CollectionAssert.AreEqual(new[] { "inside" }, inner.Calls);
        Assert.IsNull(scoped.Current);
    }

    [TestMethod]
    public async Task Current_OfTwoFlowsAtOnce_IsEachFlowsOwn()
    {
        var scoped = new FlowScopedTransferEvents();
        var first = new CountingTransferEvents();
        var second = new CountingTransferEvents();
        var bothSet = new TaskCompletionSource();
        var firstSet = new TaskCompletionSource();

        async Task RunAsync(ITransferEvents events, string text, TaskCompletionSource mine, TaskCompletionSource other)
        {
            await Task.Yield();
            scoped.Current = events;
            mine.SetResult();
            await other.Task;
            scoped.ReportInfo(text);
        }

        Diagnostics.Arrange("flows", "first sets its sink and reports \"one\", second sets its sink and reports \"two\", each after both are set");

        await Task.WhenAll(RunAsync(first, "one", firstSet, bothSet), RunAsync(second, "two", bothSet, firstSet));

        Diagnostics.Act("first's calls", string.Join(", ", first.Calls));
        Diagnostics.Act("second's calls", string.Join(", ", second.Calls));
        Diagnostics.Assert("first's calls", "one", string.Join(", ", first.Calls));
        Diagnostics.Assert("second's calls", "two", string.Join(", ", second.Calls));
        CollectionAssert.AreEqual(new[] { "one" }, first.Calls);
        CollectionAssert.AreEqual(new[] { "two" }, second.Calls);
    }

    private static async Task SetAndReportAsync(FlowScopedTransferEvents scoped, ITransferEvents events)
    {
        scoped.Current = events;
        await Task.Yield();
        scoped.ReportInfo("inside");
    }

    private static void ReportEverything(ITransferEvents events, IPEndPoint endPoint)
    {
        events.ReportInfo("info");
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = endPoint, LocalEndPoint = endPoint, ConnectionNumber = 0 });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 0 });
        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0303, ContentType = default, Bytes = new byte[] { 2 }, Sent = false });
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
        events.ReportCertificateVerifyResult(18, isProxy: true);
        events.ReportTlsEarlyData(-36);
        events.ReportRequestHeader([3]);
        events.ReportResponseHeader([4]);
        events.ReportDataSent([5]);
        events.ReportDataReceived([6]);
    }
}
