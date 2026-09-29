namespace Curl.Networking;

/// <summary>Pins <see cref="SystemDnsServers" />: it reads this machine's configuration and sends nothing.</summary>
[TestClass]
public sealed class SystemDnsServersTests
{
    [TestMethod]
    public void List_EveryServer_IsOnPort53AndListedOnce()
    {
        var servers = SystemDnsServers.List();

        Assert.IsTrue(servers.All(server => server.Port == DnsServerList.DefaultPort));
        Assert.HasCount(servers.Count, servers.Distinct());
    }
}
