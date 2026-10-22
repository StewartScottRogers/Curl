using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class DebugCallbackTransferEventsTests
{
    private static readonly TlsHandshakeEvent Handshake = new()
    {
        ProtocolVersion = SslProtocols.Tls13,
        CipherSuite = null,
        NegotiatedApplicationProtocol = "h2",
        OfferedApplicationProtocols = ["h2", "http/1.1"],
        ServerCertificate = null,
        CertificateVerified = false,
    };

    private static readonly TlsMessageEvent ClientHello = new()
    {
        ProtocolVersion = 0x0304,
        ContentType = TlsContentType.Handshake,
        Sent = true,
        Bytes = new byte[] { 1, 0, 0, 0 },
    };

    [TestMethod]
    public void Of_VerboseWithADebugCallback_GivesTheEvents()
    {
        Assert.IsNotNull(DebugCallbackTransferEvents.Of(verbose: true, (_, _) => 0));
    }

    [TestMethod]
    public void Of_VerboseOffOrNoDebugCallback_GivesNone()
    {
        Assert.IsNull(DebugCallbackTransferEvents.Of(verbose: false, (_, _) => 0));
        Assert.IsNull(DebugCallbackTransferEvents.Of(verbose: true, debug: null));
    }

    [TestMethod]
    public void ByteEvents_EachGoesToTheCallbackWithItsInfoType()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        ITransferEvents events = new DebugCallbackTransferEvents(Recording(calls), TlsBackend.Schannel);

        events.ReportInfo("Trying 127.0.0.1:80...");
        events.ReportRequestHeader("GET / HTTP/1.1\r\n\r\n"u8);
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportDataSent("up"u8);
        events.ReportDataReceived("down"u8);

        CollectionAssert.AreEqual(
            new (CurlInfoType, string)[]
            {
                (CurlInfoType.Text, "Trying 127.0.0.1:80...\n"),
                (CurlInfoType.HeaderOut, "GET / HTTP/1.1\r\n\r\n"),
                (CurlInfoType.HeaderIn, "HTTP/1.1 200 OK\r\n"),
                (CurlInfoType.DataOut, "up"),
                (CurlInfoType.DataIn, "down"),
            },
            calls);
    }

    [TestMethod]
    [DataRow(TlsBackend.Schannel)]
    [DataRow(TlsBackend.OpenSsl)]
    public void StructuredEvents_GiveTheTextLinesVerboseWritesForThem(TlsBackend tlsBackend)
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        MemoryStream verbose = new();
        ITransferEvents events = new DebugCallbackTransferEvents(Recording(calls), tlsBackend);
        ITransferEvents writer = new VerboseTransferEventWriter(verbose, writesDataLines: false, tlsBackend);

        foreach (ITransferEvents sink in new[] { events, writer })
        {
            sink.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = "h",
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
                ConnectionNumber = 0,
            });
            sink.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "HTTP", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 0 });
            sink.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
            sink.ReportTlsHandshake(Handshake);
        }

        Assert.IsTrue(calls.All(call => call.Type == CurlInfoType.Text));
        Assert.AreEqual(Encoding.UTF8.GetString(verbose.ToArray()), string.Concat(calls.Select(call => "* " + call.Data)));
    }

    [TestMethod]
    public void TlsMessage_OpenSsl_GivesItsTextLineThenItsBytesAsSslDataOut()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        ITransferEvents events = new DebugCallbackTransferEvents(Recording(calls), TlsBackend.OpenSsl);

        events.ReportTlsMessage(ClientHello);
        events.ReportTlsData("in"u8, sent: false);

        Assert.AreEqual(3, calls.Count);
        Assert.AreEqual(CurlInfoType.Text, calls[0].Type);
        StringAssert.StartsWith(calls[0].Data, "TLSv1.3 (OUT), TLS handshake");
        Assert.AreEqual((CurlInfoType.SslDataOut, "\u0001\0\0\0"), calls[1]);
        Assert.AreEqual((CurlInfoType.SslDataIn, "in"), calls[2]);
    }

    [TestMethod]
    public void ReportInfo_LineOfMoreThan2045Bytes_IsCutTo2043BytesEndingInDots()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        ITransferEvents events = new DebugCallbackTransferEvents(Recording(calls), TlsBackend.Schannel);

        events.ReportInfo(new string('A', 2045));
        events.ReportInfo(new string('B', 2046));

        Assert.AreEqual(new string('A', 2045) + "\n", calls[0].Data);
        Assert.AreEqual(new string('B', 2043) + "...\n", calls[1].Data);
    }

    [TestMethod]
    public void TlsData_Schannel_GivesNothing()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        ITransferEvents events = new DebugCallbackTransferEvents(Recording(calls), TlsBackend.Schannel);

        events.ReportTlsMessage(ClientHello);
        events.ReportTlsData("in"u8, sent: false);

        Assert.IsEmpty(calls);
    }

    private static DebugCallback Recording(List<(CurlInfoType Type, string Data)> calls) =>
        (type, data) =>
        {
            calls.Add((type, Encoding.Latin1.GetString(data)));
            return 0;
        };
}
