using Curl.Api.Client;
using Curl.Api.EngineContracts;
using Curl.Api.Options.Network;

namespace Curl.Api.UnitTests.Options.Transport;

[TestClass]
public sealed class CurlNetworkOptionsTests
{
    private static async Task<EasyNetworkOptions> ReceivedByEngine(CurlClientOptions options)
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine, options, ownsEngine: true);
        client.CreateEasy();
        return engine.CreatedEasyHandles.Single().Options.Network;
    }

    [TestMethod]
    public async Task EveryMember_ReachesTheEngineUnderItsEasyName()
    {
        CurlNetworkOptions network = new()
        {
            PathAsIs = true,
            Protocols = "http,https",
            RedirectProtocols = "https",
            DefaultProtocol = "ftp",
            Port = 8080,
            Interface = "eth0",
            LocalPort = 4000,
            LocalPortRange = 10,
            ConnectTo = ["a:80:b:81"],
            Resolve = ["a:80:127.0.0.1"],
            DnsCacheTimeout = TimeSpan.FromSeconds(5),
            DnsServers = "1.1.1.1",
            DnsInterface = "eth1",
            DnsLocalIPv4 = "10.0.0.1",
            DnsLocalIPv6 = "::1",
            DnsShuffleAddresses = true,
            DohUrl = "https://doh.example/dns-query",
            IpResolve = IpResolve.V6,
            TcpNoDelay = false,
            TcpFastOpen = true,
            TcpKeepAlive = true,
            TcpKeepIdle = TimeSpan.FromSeconds(7),
            TcpKeepInterval = TimeSpan.FromSeconds(8),
            TcpKeepCount = 3,
            IpTypeOfService = 16,
            VlanPriority = 5,
            MultipathTcp = true,
            AddressScope = 2,
            UnixSocketPath = "/tmp/s",
            AbstractUnixSocket = "abs",
            ConnectTimeout = TimeSpan.FromSeconds(9),
            HappyEyeballsTimeout = TimeSpan.FromMilliseconds(50),
            FreshConnect = true,
            ForbidReuse = true,
            MaxConnects = 2,
            MaxIdleConnectionAge = TimeSpan.FromSeconds(11),
            MaxConnectionLifetime = TimeSpan.FromSeconds(12),
            AcceptTimeout = TimeSpan.FromSeconds(13),
            UpkeepInterval = TimeSpan.FromSeconds(14),
            ServerResponseTimeout = TimeSpan.FromSeconds(15),
            UseSsl = UseSsl.All,
        };

        EasyNetworkOptions easy = await ReceivedByEngine(new CurlClientOptions { Network = network });

        Assert.IsTrue(easy.PathAsIs);
        Assert.AreEqual("http,https", easy.Protocols);
        Assert.AreEqual("https", easy.RedirectProtocols);
        Assert.AreEqual("ftp", easy.DefaultProtocol);
        Assert.AreEqual(8080, easy.Port);
        Assert.AreEqual("eth0", easy.Interface);
        Assert.AreEqual(4000, easy.LocalPort);
        Assert.AreEqual(10, easy.LocalPortRange);
        CollectionAssert.AreEqual(new[] { "a:80:b:81" }, easy.ConnectTo.ToArray());
        CollectionAssert.AreEqual(new[] { "a:80:127.0.0.1" }, easy.Resolve.ToArray());
        Assert.AreEqual(TimeSpan.FromSeconds(5), easy.DnsCacheTimeout);
        Assert.AreEqual("1.1.1.1", easy.DnsServers);
        Assert.AreEqual("eth1", easy.DnsInterface);
        Assert.AreEqual("10.0.0.1", easy.DnsLocalIPv4);
        Assert.AreEqual("::1", easy.DnsLocalIPv6);
        Assert.IsTrue(easy.DnsShuffleAddresses);
        Assert.AreEqual("https://doh.example/dns-query", easy.DohUrl);
        Assert.AreEqual(IpResolve.V6, easy.IpResolve);
        Assert.IsFalse(easy.TcpNoDelay);
        Assert.IsTrue(easy.TcpFastOpen);
        Assert.IsTrue(easy.TcpKeepAlive);
        Assert.AreEqual(TimeSpan.FromSeconds(7), easy.TcpKeepIdle);
        Assert.AreEqual(TimeSpan.FromSeconds(8), easy.TcpKeepInterval);
        Assert.AreEqual(3, easy.TcpKeepCount);
        Assert.AreEqual(16, easy.IpTypeOfService);
        Assert.AreEqual(5, easy.VlanPriority);
        Assert.IsTrue(easy.MultipathTcp);
        Assert.AreEqual(2, easy.AddressScope);
        Assert.AreEqual("/tmp/s", easy.UnixSocketPath);
        Assert.AreEqual("abs", easy.AbstractUnixSocket);
        Assert.AreEqual(TimeSpan.FromSeconds(9), easy.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(50), easy.HappyEyeballsTimeout);
        Assert.IsTrue(easy.FreshConnect);
        Assert.IsTrue(easy.ForbidReuse);
        Assert.AreEqual(2, easy.MaxConnects);
        Assert.AreEqual(TimeSpan.FromSeconds(11), easy.MaxIdleConnectionAge);
        Assert.AreEqual(TimeSpan.FromSeconds(12), easy.MaxConnectionLifetime);
        Assert.AreEqual(TimeSpan.FromSeconds(13), easy.AcceptTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(14), easy.UpkeepInterval);
        Assert.AreEqual(TimeSpan.FromSeconds(15), easy.ServerResponseTimeout);
        Assert.AreEqual(UseSsl.All, easy.UseSsl);
    }

    [TestMethod]
    public async Task DefaultRecord_LeavesTheEngineAtLibcurlDefaults()
    {
        EasyNetworkOptions easy = await ReceivedByEngine(new CurlClientOptions { Network = new CurlNetworkOptions() });

        Assert.AreEqual(new EasyNetworkOptions(), easy);
    }

    [TestMethod]
    public async Task UnsetConnectTimeout_KeepsTheClientConnectTimeout()
    {
        CurlClientOptions options = new() { ConnectTimeout = TimeSpan.FromSeconds(4), Network = new CurlNetworkOptions { Port = 1 } };

        EasyNetworkOptions easy = await ReceivedByEngine(options);

        Assert.AreEqual(TimeSpan.FromSeconds(4), easy.ConnectTimeout);
        Assert.AreEqual(1, easy.Port);
    }

    [TestMethod]
    public void ApplyTo_KeepsTheUrlAndConnectOnlyMode()
    {
        EasyNetworkOptions before = new() { Url = "http://example/", ConnectOnly = ConnectOnly.WebSocket };

        EasyNetworkOptions after = new CurlNetworkOptions { Port = 81 }.ApplyTo(before);

        Assert.AreEqual("http://example/", after.Url);
        Assert.AreEqual(ConnectOnly.WebSocket, after.ConnectOnly);
    }

    [TestMethod]
    public void InfiniteDnsCacheTimeout_IsAccepted()
    {
        Assert.AreEqual(Timeout.InfiniteTimeSpan, new CurlNetworkOptions { DnsCacheTimeout = Timeout.InfiniteTimeSpan }.DnsCacheTimeout);
    }

    [TestMethod]
    public void InvalidValues_ThrowAsTheyAreSet()
    {
        TimeSpan negative = TimeSpan.FromMilliseconds(-1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { Port = 65536 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { LocalPort = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { LocalPortRange = 0 });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlNetworkOptions { ConnectTo = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlNetworkOptions { Resolve = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { DnsCacheTimeout = TimeSpan.FromSeconds(-2) });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { IpResolve = (IpResolve)3 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { TcpKeepIdle = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { TcpKeepInterval = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { TcpKeepCount = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { IpTypeOfService = 256 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { VlanPriority = 8 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { AddressScope = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { ConnectTimeout = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { HappyEyeballsTimeout = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { MaxConnects = -1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { MaxIdleConnectionAge = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { MaxConnectionLifetime = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { AcceptTimeout = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { UpkeepInterval = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { ServerResponseTimeout = negative });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlNetworkOptions { UseSsl = (UseSsl)4 });
    }

    [TestMethod]
    public void NullConnectTimeout_IsAccepted()
    {
        Assert.IsNull(new CurlNetworkOptions { ConnectTimeout = null }.ConnectTimeout);
    }
}
