using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that every member of <see cref="NoTransferEvents" /> does nothing, and that the event records carry what they are given (ADR-0046).
/// </summary>
[TestClass]
public sealed class NoTransferEventsTests
{
    private static readonly ConnectionOpenedEvent Opened = new()
    {
        HostName = "example.com",
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("93.184.215.14"), 443),
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 51270),
        ConnectionNumber = 0,
    };

    private static readonly ConnectionReusedEvent Reused = new()
    {
        Scheme = "https",
        IsProxy = true,
        HostName = "example.com",
        Port = 443,
        ConnectionNumber = 1,
    };

    private static readonly TlsHandshakeEvent Handshake = new()
    {
        ProtocolVersion = SslProtocols.Tls13,
        CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
        NegotiatedApplicationProtocol = "http/1.1",
        OfferedApplicationProtocols = ["http/1.1"],
        ServerCertificate = null,
        CertificateVerified = true,
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EveryMember_Called_DoesNotThrow()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ITransferEvents events = NoTransferEvents.Instance;
        byte[] bytes = [0x48, 0x49];
        diagnostics.Arrange("info text", "Trying 127.0.0.1:80...");
        diagnostics.Bytes("payload", bytes);

        events.ReportInfo("Trying 127.0.0.1:80...");
        events.ReportConnectionOpened(Opened);
        events.ReportConnectionReused(Reused);
        events.ReportTlsHandshake(Handshake);
        events.ReportTlsData(bytes, sent: true);
        events.ReportTlsData(bytes, sent: false);
        events.ReportRequestHeader(bytes);
        events.ReportResponseHeader(bytes);
        events.ReportDataSent(bytes);
        events.ReportDataReceived(bytes);

        diagnostics.Act("calls made", 10);
        diagnostics.Assert("calls made", 10, 10);
    }

    [TestMethod]
    public void ConnectionOpenedEvent_Built_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "Opened, example.com 93.184.215.14:443");
        diagnostics.Act("HostName", Opened.HostName);
        diagnostics.Act("RemoteEndPoint", Opened.RemoteEndPoint);
        diagnostics.Act("ConnectionNumber", Opened.ConnectionNumber);
        diagnostics.Assert("HostName", "example.com", Opened.HostName);
        Assert.AreEqual("example.com", Opened.HostName);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("93.184.215.14"), 443), Opened.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 51270), Opened.LocalEndPoint);
        Assert.AreEqual(0L, Opened.ConnectionNumber);
    }

    [TestMethod]
    public void ConnectionReusedEvent_Built_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "Reused, https example.com:443 proxy");
        diagnostics.Act("Scheme", Reused.Scheme);
        diagnostics.Act("Port", Reused.Port);
        diagnostics.Act("ConnectionNumber", Reused.ConnectionNumber);
        diagnostics.Assert("Scheme", "https", Reused.Scheme);
        Assert.AreEqual("https", Reused.Scheme);
        Assert.IsTrue(Reused.IsProxy);
        Assert.AreEqual("example.com", Reused.HostName);
        Assert.AreEqual(443, Reused.Port);
        Assert.AreEqual(1L, Reused.ConnectionNumber);
    }

    [TestMethod]
    public void TlsHandshakeEvent_Built_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "Handshake, Tls13 TLS_AES_256_GCM_SHA384 http/1.1");
        diagnostics.Act("ProtocolVersion", Handshake.ProtocolVersion);
        diagnostics.Act("CipherSuite", Handshake.CipherSuite);
        diagnostics.Act("NegotiatedApplicationProtocol", Handshake.NegotiatedApplicationProtocol);
        diagnostics.Assert("ProtocolVersion", SslProtocols.Tls13, Handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls13, Handshake.ProtocolVersion);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_256_GCM_SHA384, Handshake.CipherSuite);
        Assert.AreEqual("http/1.1", Handshake.NegotiatedApplicationProtocol);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, Handshake.OfferedApplicationProtocols.ToArray());
        Assert.IsNull(Handshake.ServerCertificate);
        Assert.IsTrue(Handshake.CertificateVerified);
    }

    [TestMethod]
    public void TlsHandshakeEvent_OpenSslFactsLeftOut_AreNullAndEmpty()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "Handshake without OpenSSL facts");
        diagnostics.Act("NegotiatedGroupName", Handshake.NegotiatedGroupName);
        diagnostics.Act("CertificateVerifyResult", Handshake.CertificateVerifyResult);
        diagnostics.Assert("NegotiatedGroupName", null, Handshake.NegotiatedGroupName);
        Assert.IsNull(Handshake.NegotiatedGroupName);
        Assert.IsNull(Handshake.PeerSignatureTypeName);
        Assert.IsNull(Handshake.CertificateVerifyResult);
        Assert.IsEmpty(Handshake.PeerCertificateChain);
        Assert.IsNull(Handshake.EchResult);
        Assert.IsEmpty(Handshake.EchRetryConfigLines);
    }

    [TestMethod]
    public void TlsHandshakeEvent_OpenSslFactsGiven_RoundTrip()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("OpenSSL facts", "X25519MLKEM768, RSASSA-PSS, verify result 18, ECH lines");
        TlsHandshakeEvent handshake = Handshake with
        {
            NegotiatedGroupName = "X25519MLKEM768",
            PeerSignatureTypeName = "RSASSA-PSS",
            CertificateVerifyResult = 18,
            EchResult = "status is sent GREASE, inner is NULL, outer is NULL",
            EchRetryConfigLines = ["ECH: retry_configs for NULL from NULL, 0 3"],
            PeerCertificateChain = [],
        };

        diagnostics.Act("NegotiatedGroupName", handshake.NegotiatedGroupName);
        diagnostics.Act("CertificateVerifyResult", handshake.CertificateVerifyResult);
        diagnostics.Act("EchResult", handshake.EchResult);
        diagnostics.Assert("NegotiatedGroupName", "X25519MLKEM768", handshake.NegotiatedGroupName);
        Assert.AreEqual("X25519MLKEM768", handshake.NegotiatedGroupName);
        Assert.AreEqual("RSASSA-PSS", handshake.PeerSignatureTypeName);
        Assert.AreEqual(18L, handshake.CertificateVerifyResult);
        Assert.AreEqual("status is sent GREASE, inner is NULL, outer is NULL", handshake.EchResult);
        Assert.AreEqual("ECH: retry_configs for NULL from NULL, 0 3", handshake.EchRetryConfigLines.Single());
        Assert.IsEmpty(handshake.PeerCertificateChain);
    }
}
