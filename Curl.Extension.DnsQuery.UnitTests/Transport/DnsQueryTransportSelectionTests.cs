using Curl.Extension.DnsQuery.Client;
using Curl.Extension.DnsQuery.Transport;
using Curl.Extension.DnsQuery.Transport.Tcp;
using Curl.Extension.DnsQuery.Transport.Udp;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Transport;

/// <summary>Checks the transport each <c>--dns-transport</c> name gives.</summary>
[TestClass]
public sealed class DnsQueryTransportSelectionTests
{
    private static readonly ScriptedDnsServer Server = new((_, _) => [], []);
    private static readonly ProtocolHandlerServices Services = new(Server, Server, null!, null!);

    [TestMethod]
    public void Names_AreUdpThenTcp() =>
        CollectionAssert.AreEqual(new[] { "udp", "tcp" }, DnsQueryTransportSelection.Names.ToArray());

    [TestMethod]
    [DataRow("udp")]
    [DataRow("UDP")]
    public void TransportOf_Udp_GivesTheUdpTransport(string name) =>
        Assert.IsInstanceOfType<DnsUdpTransport>(DnsQueryTransportSelection.TransportOf(name, Services));

    [TestMethod]
    [DataRow("tcp")]
    [DataRow("Tcp")]
    public void TransportOf_Tcp_GivesTheTcpTransport(string name) =>
        Assert.IsInstanceOfType<DnsTcpTransport>(DnsQueryTransportSelection.TransportOf(name, Services));

    [TestMethod]
    public void TransportOf_UnknownName_FailsWithExit2NamingTheTransports()
    {
        DnsQueryException failure = Assert.ThrowsExactly<DnsQueryException>(() => DnsQueryTransportSelection.TransportOf("sctp", Services));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("--dns-transport sctp names no transport; give one of udp, tcp", failure.Message);
    }
}
