using Curl.Testing;

namespace Curl.Networking;

/// <summary>Pins <see cref="SystemDnsServers" />: it reads this machine's configuration and sends nothing.</summary>
[TestClass]
public sealed class SystemDnsServersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void List_EveryServer_IsOnPort53AndListedOnce()
    {
        Diagnostics.Arrange("source", "this machine's DNS configuration");

        var servers = SystemDnsServers.List();

        var everyServerOnPort53 = servers.All(server => server.Port == DnsServerList.DefaultPort);
        var serversDistinct = servers.Distinct().Count() == servers.Count;
        Diagnostics.Act("every server on port 53", everyServerOnPort53);
        Diagnostics.Act("servers distinct", serversDistinct);
        Diagnostics.Assert("every server on port 53", true, everyServerOnPort53);
        Diagnostics.Assert("servers distinct", true, serversDistinct);

        Assert.IsTrue(servers.All(server => server.Port == DnsServerList.DefaultPort));
        Assert.HasCount(servers.Count, servers.Distinct());
    }
}
