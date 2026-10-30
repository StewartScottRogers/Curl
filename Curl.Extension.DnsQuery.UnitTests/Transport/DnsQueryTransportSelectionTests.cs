using Curl.Extension.DnsQuery.Client;
using Curl.Extension.DnsQuery.Transport;
using Curl.Extension.DnsQuery.Transport.Quic;
using Curl.Extension.DnsQuery.Transport.Tcp;
using Curl.Extension.DnsQuery.Transport.Tls;
using Curl.Extension.DnsQuery.Transport.Udp;
using Curl.Extension.DnsQuery.UnitTests.Transport.Tls;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Transport;

/// <summary>Checks the transport each <c>--dns-transport</c> name gives.</summary>
[TestClass]
public sealed class DnsQueryTransportSelectionTests
{
    private static readonly ScriptedDnsServer Server = new((_, _) => [], []);
    private static readonly ProtocolHandlerServices Services = new(Server, Server, new HandshakingTlsProvider(), null!);

    [TestMethod]
    public void Names_AreUdpTcpTlsThenQuic() =>
        CollectionAssert.AreEqual(new[] { "udp", "tcp", "tls", "quic" }, DnsQueryTransportSelection.Names.ToArray());

    [TestMethod]
    [DataRow("udp")]
    [DataRow("UDP")]
    public void TransportOf_Udp_GivesTheUdpTransportOnPort53(string name)
    {
        IDnsQueryTransport transport = DnsQueryTransportSelection.TransportOf(name, Services);

        Assert.IsInstanceOfType<DnsUdpTransport>(transport);
        Assert.AreEqual(53, transport.DefaultPort);
    }

    [TestMethod]
    [DataRow("tcp")]
    [DataRow("Tcp")]
    public void TransportOf_Tcp_GivesTheTcpTransportOnPort53(string name)
    {
        IDnsQueryTransport transport = DnsQueryTransportSelection.TransportOf(name, Services);

        Assert.IsInstanceOfType<DnsTcpTransport>(transport);
        Assert.AreEqual(53, transport.DefaultPort);
    }

    [TestMethod]
    [DataRow("tls")]
    [DataRow("TLS")]
    public void TransportOf_Tls_GivesTheDnsOverTlsTransport(string name) =>
        Assert.IsInstanceOfType<DnsTlsTransport>(DnsQueryTransportSelection.TransportOf(name, Services));

    [TestMethod]
    [DataRow("quic")]
    [DataRow("QUIC")]
    public void TransportOf_Quic_GivesTheDnsOverQuicTransport(string name) =>
        Assert.IsInstanceOfType<DnsQuicTransport>(DnsQueryTransportSelection.TransportOf(name, Services));

    [TestMethod]
    public void TransportOf_UnknownName_FailsWithExit2NamingTheTransports()
    {
        DnsQueryException failure = Assert.ThrowsExactly<DnsQueryException>(() => DnsQueryTransportSelection.TransportOf("sctp", Services));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("--dns-transport sctp names no transport; give one of udp, tcp, tls, quic", failure.Message);
    }
}
