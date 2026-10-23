using System.Net;
using System.Security.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="NegotiateFailureRecordingTransferEvents" />: it keeps the first SSPI failure line (BL-3322),
/// <see cref="NegotiateFailureRecordingTransferEvents.ReportOn" /> puts it on a successful result that has no message
/// and on no other, and every event goes on unchanged.
/// </summary>
[TestClass]
public sealed class NegotiateFailureRecordingTransferEventsTests
{
    private const string Line = "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";

    [TestMethod]
    public void ReportInfo_SeveralSspiFailures_KeepsTheFirst()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);

        events.ReportInfo("Connected");
        events.ReportInfo(Line);
        events.ReportInfo("InitializeSecurityContext failed: SEC_E_LOGON_DENIED (0x8009030c) - The logon attempt failed");

        Assert.AreEqual(Line, events.FirstSspiFailure);
    }

    [TestMethod]
    public void ReportInfo_GssApiFailureLine_KeepsNothing()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);

        events.ReportInfo("gss_init_sec_context() failed: No credentials were supplied");

        Assert.IsNull(events.FirstSspiFailure);
    }

    [TestMethod]
    public void ReportOn_SuccessfulResultAfterAFailure_CarriesTheLineAsItsMessage()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportInfo(Line);

        TransferResult? result = events.ReportOn(new TransferResult(CurlExitCode.Ok, 0));

        Assert.AreEqual(Line, result!.ErrorMessage);
    }

    [TestMethod]
    public void ReportOn_NoFailureReported_ReturnsTheResultUnchanged()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);
        TransferResult result = new(CurlExitCode.Ok, 0);

        Assert.AreSame(result, events.ReportOn(result));
    }

    [TestMethod]
    public void ReportOn_FailedResultOrOneWithAMessage_KeepsItsMessage()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportInfo(Line);

        Assert.AreEqual("peer", events.ReportOn(new TransferResult(CurlExitCode.PeerFailedVerification, 0, "peer"))!.ErrorMessage);
        Assert.AreEqual("kept", events.ReportOn(new TransferResult(CurlExitCode.Ok, 0, "kept"))!.ErrorMessage);
    }

    [TestMethod]
    public void ReportOn_AfterAFailureWithNoSchemePicked_PicksNegotiate()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportInfo(Line);

        Assert.AreEqual(HttpAuthSchemes.Negotiate, events.ReportOn(new TransferResult(CurlExitCode.Ok, 0))!.Report!.AuthSchemePicked);
        Assert.AreEqual(HttpAuthSchemes.Negotiate, events.ReportOn(new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport() })!.Report!.AuthSchemePicked);
    }

    [TestMethod]
    public void ReportOn_AfterAFailureWithASchemePicked_KeepsIt()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportInfo(Line);
        TransferReport report = new() { AuthSchemePicked = HttpAuthSchemes.Digest };

        Assert.AreEqual(HttpAuthSchemes.Digest, events.ReportOn(new TransferResult(CurlExitCode.Ok, 0) { Report = report })!.Report!.AuthSchemePicked);
    }

    [TestMethod]
    public void ReportOn_AbortedTransfer_AnswersNull()
    {
        NegotiateFailureRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportInfo(Line);

        Assert.IsNull(events.ReportOn(null));
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CountingEvents inner = new();
        NegotiateFailureRecordingTransferEvents events = new(inner);

        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000), ConnectionNumber = 1 });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 1 });
        events.ReportCertificateVerifyResult(0, isProxy: false);
        events.ReportTlsEarlyData(0);
        events.ReportInfo("i");
        events.ReportTlsHandshake(new TlsHandshakeEvent { ProtocolVersion = SslProtocols.Tls13, CipherSuite = null, NegotiatedApplicationProtocol = null, OfferedApplicationProtocols = [], ServerCertificate = null, CertificateVerified = false });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportRequestHeader([1]);
        events.ReportResponseHeader([1]);
        events.ReportDataSent([1]);
        events.ReportDataReceived([1]);

        Assert.AreEqual(13, inner.Count);
    }

    /// <summary>Counts every event it is given.</summary>
    private sealed class CountingEvents : ITransferEvents
    {
        public int Count { get; private set; }

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Count++;

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Count++;

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Count++;

        public void ReportTlsEarlyData(long bytes) => Count++;

        public void ReportInfo(string text) => Count++;

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Count++;

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Count++;

        public void ReportTlsMessage(TlsMessageEvent message) => Count++;

        public void ReportTlsTrust(TlsTrustEvent trust) => Count++;

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Count++;
    }
}
