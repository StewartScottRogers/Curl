using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="RedialedConnectTransferEvents" />, which holds back what a connect run again
/// for the hand-built client repeats (ADR-1460, BL-6073).
/// </summary>
[TestClass]
public sealed class RedialedConnectTransferEventsTests
{
    [TestMethod]
    public void Report_BeforeTheRedialedHandshakesTrust_PassesNothingOn()
    {
        var inner = new RecordingTransferEvents();
        var events = new RedialedConnectTransferEvents(inner, earlierTrusts: 1);

        ReportEverything(events);
        events.ReportInfo("Trying");
        events.ReportWarning("a warning");
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });

        Assert.IsEmpty(inner.Transcript);
        Assert.IsEmpty(inner.TlsEvents);
        Assert.IsEmpty(inner.TlsMessages);
        Assert.IsEmpty(inner.VerifyResults);
        Assert.IsEmpty(inner.EarlyDataSent);
        Assert.IsEmpty(inner.Opened);
        Assert.IsEmpty(inner.Reused);
    }

    [TestMethod]
    public void Report_AfterTheRedialedHandshakesTrust_PassesEverythingOnButThatTrustAndTheLinesHeld()
    {
        var inner = new RecordingTransferEvents();
        var events = new RedialedConnectTransferEvents(inner, earlierTrusts: 1);
        events.ReportInfo("held");
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });

        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });
        ReportEverything(events);
        events.ReportInfo("after");
        events.ReportWarning("a warning");
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });
        events.ReportHeldLines();

        CollectionAssert.AreEqual(new[] { "after" }, inner.Info);
        CollectionAssert.AreEqual(new[] { "> GET / HTTP/1.1", "< HTTP/1.1 200 OK", "* after" }.Order().ToArray(), inner.Transcript.Order().ToArray());
        Assert.HasCount(2, inner.TlsEvents);
        Assert.HasCount(1, inner.TlsMessages);
        Assert.HasCount(1, inner.VerifyResults);
        Assert.HasCount(1, inner.EarlyDataSent);
        Assert.HasCount(1, inner.Opened);
        Assert.HasCount(1, inner.Reused);
    }

    [TestMethod]
    public void ReportHeldLines_BeforeTheRedialedHandshakesTrust_WritesTheLinesHeldOnceInOrder()
    {
        var inner = new WarningRecordingEvents();
        var events = new RedialedConnectTransferEvents(inner, earlierTrusts: 0);
        events.ReportInfo("Trying");
        events.ReportWarning("a warning");
        events.ReportInfo("Failed to connect");

        events.ReportHeldLines();
        events.ReportHeldLines();

        CollectionAssert.AreEqual(new[] { "* Trying", "! a warning", "* Failed to connect" }, inner.Lines);
    }

    [TestMethod]
    public void ReportWarning_AfterTheRedialedHandshakesTrust_PassesItOn()
    {
        var inner = new WarningRecordingEvents();
        var events = new RedialedConnectTransferEvents(inner, earlierTrusts: 0);
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });

        events.ReportWarning("a warning");

        CollectionAssert.AreEqual(new[] { "! a warning" }, inner.Lines);
    }

    [TestMethod]
    public async Task WriteConnectReplyHeadAsync_BeforeAndAfterTheRedialedHandshakesTrust_WritesOnlyTheLaterHead()
    {
        var inner = new WarningRecordingEvents();
        var events = new RedialedConnectTransferEvents(inner, earlierTrusts: 0);

        await events.WriteConnectReplyHeadAsync("first"u8.ToArray(), CancellationToken.None);
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });
        await events.WriteConnectReplyHeadAsync("second"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "= second" }, inner.Lines);
    }

    [TestMethod]
    public async Task WriteConnectReplyHeadAsync_OverEventsWithoutAHeaderOutput_WritesNothing()
    {
        var inner = new RecordingTransferEvents();
        var events = new RedialedConnectTransferEvents(inner, earlierTrusts: 0);
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });

        await events.WriteConnectReplyHeadAsync("head"u8.ToArray(), CancellationToken.None);

        Assert.IsEmpty(inner.Transcript);
    }

    // Every report but the lines and the trust, which each test makes itself.
    private static void ReportEverything(ITransferEvents events)
    {
        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = System.Security.Authentication.SslProtocols.Tls13,
            CipherSuite = System.Net.Security.TlsCipherSuite.TLS_AES_128_GCM_SHA256,
            OfferedApplicationProtocols = [],
            NegotiatedApplicationProtocol = null,
            ServerCertificate = null,
            CertificateVerified = false,
        });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0304, ContentType = TlsContentType.Handshake, Sent = true, Bytes = new byte[] { 1 } });
        events.ReportCertificateVerifyResult(1, isProxy: false);
        events.ReportTlsEarlyData(1);
        var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 443);
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "host", RemoteEndPoint = endPoint, LocalEndPoint = endPoint, ConnectionNumber = 0 });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "https", IsProxy = false, HostName = "host", Port = 443, ConnectionNumber = 0 });
        events.ReportRequestHeader("GET / HTTP/1.1\r\n"u8);
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportDataSent([1]);
        events.ReportDataReceived([1]);
    }

    // Takes warnings and CONNECT reply heads, which RecordingTransferEvents does not.
    private sealed class WarningRecordingEvents : IConnectReplyHeadWritingEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string text) => Lines.Add("* " + text);

        public void ReportWarning(string text) => Lines.Add("! " + text);

        public ValueTask WriteConnectReplyHeadAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken)
        {
            Lines.Add("= " + System.Text.Encoding.Latin1.GetString(head.Span));
            return ValueTask.CompletedTask;
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
