using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="DohSubTransferEvents" /> against curl 8.21.0's DoH sub-transfer lines (BL-1157
/// Notes, BL-1180): every <c>* </c> line a <see cref="VerboseTransferEventWriter" /> would write for
/// an event gets <c>[DNS] </c> after the <c>* </c>, the head, data and TLS byte lines stay as they are,
/// and the DNS filter's creation line keeps its one prefix.
/// </summary>
[TestClass]
public sealed class DohSubTransferEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(TlsBackend.Schannel)]
    [DataRow(TlsBackend.OpenSsl)]
    public void EveryEvent_IsWrittenAsTheWriterWritesIt_WithEachInfoLinePrefixed(TlsBackend tlsBackend)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tlsBackend", tlsBackend);
        MemoryStream direct = new();
        MemoryStream prefixed = new();

        ReportEveryEvent(new VerboseTransferEventWriter(direct, writesDataLines: true, tlsBackend));
        ReportEveryEvent(new DohSubTransferEvents(new VerboseTransferEventWriter(prefixed, writesDataLines: true, tlsBackend), tlsBackend));

        string[] expected = [.. Lines(direct).Select(line => line.StartsWith("* ", StringComparison.Ordinal) ? "* [DNS] " + line[2..] : line)];
        string[] actual = Lines(prefixed);
        diagnostics.Act("prefixed lines", string.Join(" | ", actual));
        diagnostics.Diff("prefixed lines", string.Join("\n", expected), string.Join("\n", actual));
        diagnostics.Assert("line count", expected.Length, actual.Length);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReportTlsTrust_UnderSchannelToAnIpAddress_WritesCurlsPrefixedSniLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tlsBackend", TlsBackend.Schannel);
        diagnostics.Arrange("trust event", "VerifiesPeer=true, TargetsIpAddress=true");
        MemoryStream output = new();
        DohSubTransferEvents events = new(new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel), TlsBackend.Schannel);

        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true, TargetsIpAddress = true });

        string[] lines = Lines(output);
        diagnostics.Act("lines", string.Join(" | ", lines));
        diagnostics.Assert("contains SNI line", true, lines.Contains("* [DNS] schannel: using IP address, SNI is not supported by OS."));
        Assert.Contains("* [DNS] schannel: using IP address, SNI is not supported by OS.", lines);
    }

    [TestMethod]
    public void ReportInfo_TheFilterCreatedLine_KeepsItsOnePrefix()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("info lines", "[DNS] created DNS filter for 127.0.0.1:47112, transport=3, queries=3; [DNS] added");
        MemoryStream output = new();
        DohSubTransferEvents events = new(new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel), TlsBackend.Schannel);

        events.ReportInfo("[DNS] created DNS filter for 127.0.0.1:47112, transport=3, queries=3");
        events.ReportInfo("[DNS] added");

        string[] expected = ["* [DNS] created DNS filter for 127.0.0.1:47112, transport=3, queries=3", "* [DNS] [DNS] added"];
        string[] actual = Lines(output);
        diagnostics.Act("lines", string.Join(" | ", actual));
        diagnostics.Diff("lines", string.Join("\n", expected), string.Join("\n", actual));
        diagnostics.Assert("line count", expected.Length, actual.Length);
        CollectionAssert.AreEqual(expected, actual);
    }

    private static void ReportEveryEvent(ITransferEvents events)
    {
        events.ReportInfo("  Trying 127.0.0.1:47112...");
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true, TargetsIpAddress = true });
        events.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0303, ContentType = TlsContentType.Handshake, Sent = true, Bytes = new byte[] { 1, 0, 0, 1, 0 } });
        events.ReportTlsData(new byte[] { 1, 2 }, sent: false);
        events.ReportCertificateVerifyResult(0, isProxy: false);
        events.ReportTlsEarlyData(0);
        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = ["http/1.1"],
            ServerCertificate = null,
            CertificateVerified = true,
        });
        events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = "127.0.0.1",
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 47112),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
            ConnectionNumber = 1,
        });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "127.0.0.1", Port = 47112, ConnectionNumber = 1 });
        events.ReportRequestHeader("POST /dns-query HTTP/1.1\r\n\r\n"u8);
        events.ReportDataSent(new byte[30]);
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportDataReceived(new byte[3]);
    }

    private static string[] Lines(MemoryStream output) =>
        Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
