using System.Net;
using System.Net.Security;
using System.Security.Authentication;

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

    [TestMethod]
    public void EveryMember_Called_DoesNotThrow()
    {
        ITransferEvents events = NoTransferEvents.Instance;
        byte[] bytes = [0x48, 0x49];

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
    }

    [TestMethod]
    public void ConnectionOpenedEvent_Built_RoundTripsEveryValue()
    {
        Assert.AreEqual("example.com", Opened.HostName);
        Assert.AreEqual(new IPEndPoint(IPAddress.Parse("93.184.215.14"), 443), Opened.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 51270), Opened.LocalEndPoint);
        Assert.AreEqual(0L, Opened.ConnectionNumber);
    }

    [TestMethod]
    public void ConnectionReusedEvent_Built_RoundTripsEveryValue()
    {
        Assert.AreEqual("example.com", Reused.HostName);
        Assert.AreEqual(443, Reused.Port);
        Assert.AreEqual(1L, Reused.ConnectionNumber);
    }

    [TestMethod]
    public void TlsHandshakeEvent_Built_RoundTripsEveryValue()
    {
        Assert.AreEqual(SslProtocols.Tls13, Handshake.ProtocolVersion);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_256_GCM_SHA384, Handshake.CipherSuite);
        Assert.AreEqual("http/1.1", Handshake.NegotiatedApplicationProtocol);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, Handshake.OfferedApplicationProtocols.ToArray());
        Assert.IsNull(Handshake.ServerCertificate);
        Assert.IsTrue(Handshake.CertificateVerified);
    }
}
