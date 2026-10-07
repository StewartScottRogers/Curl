using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="SspiFailureRecordingTransferEvents" /> passes every event on unchanged and keeps
/// the first <c>InitializeSecurityContext failed: ...</c> line, curl's SSPI <c>failf</c> (BL-1033).
/// </summary>
[TestClass]
public sealed class SspiFailureRecordingTransferEventsTests
{
    private const string NoCredentialsLine =
        "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void EveryReport_IsPassedOnToTheInnerEvents()
    {
        var inner = new HandshakeCapturingTransferEventsTests.CountingTransferEvents();
        var recording = new SspiFailureRecordingTransferEvents(inner);
        var handshake = new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        };
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);

        Diagnostics.Arrange("reports", "one of each of the 13 events, info line \"info\"");

        recording.ReportInfo("info");
        recording.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = endPoint, LocalEndPoint = endPoint, ConnectionNumber = 0 });
        recording.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 0 });
        recording.ReportTlsHandshake(handshake);
        recording.ReportTlsData([1], sent: true);
        recording.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0303, ContentType = default, Bytes = new byte[] { 2 }, Sent = false });
        recording.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
        recording.ReportCertificateVerifyResult(18, isProxy: true);
        recording.ReportTlsEarlyData(-36);
        recording.ReportRequestHeader([3]);
        recording.ReportResponseHeader([4]);
        recording.ReportDataSent([5]);
        recording.ReportDataReceived([6]);

        Diagnostics.Act("inner calls", string.Join(", ", inner.Calls));
        Diagnostics.Act("first SSPI failure", recording.FirstSspiFailure);
        Diagnostics.Assert("inner call count", 13, inner.Calls.Count);
        Diagnostics.Assert("first SSPI failure", null, recording.FirstSspiFailure);

        CollectionAssert.AreEqual(
            new[] { "info", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
        Assert.IsNull(recording.FirstSspiFailure);
    }

    [TestMethod]
    public void FirstSspiFailure_AfterTwoFailureLines_IsTheFirst()
    {
        var inner = new HandshakeCapturingTransferEventsTests.CountingTransferEvents();
        var recording = new SspiFailureRecordingTransferEvents(inner);
        const string LaterLine = "InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid";

        Diagnostics.Arrange("first line", NoCredentialsLine);
        Diagnostics.Arrange("later line", LaterLine);

        recording.ReportInfo(NoCredentialsLine);
        recording.ReportInfo(LaterLine);

        Diagnostics.Act("first SSPI failure", recording.FirstSspiFailure);
        Diagnostics.Act("inner call count", inner.Calls.Count);
        Diagnostics.Assert("first SSPI failure", NoCredentialsLine, recording.FirstSspiFailure);
        Diagnostics.Assert("inner call count", 2, inner.Calls.Count);

        Assert.AreEqual(NoCredentialsLine, recording.FirstSspiFailure);
        CollectionAssert.AreEqual(new[] { NoCredentialsLine, LaterLine }, inner.Calls);
    }

    [TestMethod]
    public void FirstSspiFailure_AfterAGssApiFailureLine_IsNull()
    {
        // curl's GSS-API build writes its failure with infof, which leaves the error buffer alone.
        var recording = new SspiFailureRecordingTransferEvents(new HandshakeCapturingTransferEventsTests.CountingTransferEvents());
        const string GssApiLine = "gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ";

        Diagnostics.Arrange("info line", GssApiLine);

        recording.ReportInfo(GssApiLine);

        Diagnostics.Act("first SSPI failure", recording.FirstSspiFailure);
        Diagnostics.Assert("first SSPI failure", null, recording.FirstSspiFailure);

        Assert.IsNull(recording.FirstSspiFailure);
    }
}
