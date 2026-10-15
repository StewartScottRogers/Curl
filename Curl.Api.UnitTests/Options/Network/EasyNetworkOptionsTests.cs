using Curl.Api.Options.Network;

namespace Curl.Api.UnitTests.Options.Network;

[TestClass]
public sealed class EasyNetworkOptionsTests
{
    private static readonly EasyNetworkOptions Defaults = new();

    [TestMethod]
    public void New_StringOptions_DefaultToNull()
    {
        Assert.IsNull(Defaults.Url);
        Assert.IsNull(Defaults.UrlFromUrlHandle);
        Assert.IsNull(Defaults.Protocols);
        Assert.IsNull(Defaults.RedirectProtocols);
        Assert.IsNull(Defaults.DefaultProtocol);
        Assert.IsNull(Defaults.Interface);
        Assert.IsNull(Defaults.DnsServers);
        Assert.IsNull(Defaults.DnsInterface);
        Assert.IsNull(Defaults.DnsLocalIPv4);
        Assert.IsNull(Defaults.DnsLocalIPv6);
        Assert.IsNull(Defaults.DohUrl);
        Assert.IsNull(Defaults.UnixSocketPath);
        Assert.IsNull(Defaults.AbstractUnixSocket);
    }

    [TestMethod]
    public void New_EntryLists_DefaultToEmpty()
    {
        Assert.IsEmpty(Defaults.ConnectTo);
        Assert.IsEmpty(Defaults.Resolve);
    }

    [TestMethod]
    public void New_SwitchOptions_DefaultToLibcurlDefaults()
    {
        Assert.IsFalse(Defaults.PathAsIs);
        Assert.IsFalse(Defaults.DnsShuffleAddresses);
        Assert.IsTrue(Defaults.TcpNoDelay);
        Assert.IsFalse(Defaults.TcpFastOpen);
        Assert.IsFalse(Defaults.TcpKeepAlive);
        Assert.IsFalse(Defaults.FreshConnect);
        Assert.IsFalse(Defaults.ForbidReuse);
    }

    [TestMethod]
    public void New_NumberOptions_DefaultToLibcurlDefaults()
    {
        Assert.AreEqual(0, Defaults.Port);
        Assert.AreEqual(0, Defaults.LocalPort);
        Assert.AreEqual(1, Defaults.LocalPortRange);
        Assert.AreEqual(9, Defaults.TcpKeepCount);
        Assert.AreEqual(0L, Defaults.AddressScope);
        Assert.AreEqual(5, Defaults.MaxConnects);
    }

    [TestMethod]
    public void New_DurationOptions_DefaultToLibcurlDefaults()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(60), Defaults.DnsCacheTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(60), Defaults.TcpKeepIdle);
        Assert.AreEqual(TimeSpan.FromSeconds(60), Defaults.TcpKeepInterval);
        Assert.AreEqual(TimeSpan.FromSeconds(300), Defaults.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), Defaults.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(118), Defaults.MaxIdleConnectionAge);
        Assert.AreEqual(TimeSpan.FromHours(24), Defaults.MaxConnectionLifetime);
        Assert.AreEqual(TimeSpan.FromSeconds(60), Defaults.AcceptTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(60), Defaults.UpkeepInterval);
        Assert.AreEqual(TimeSpan.FromSeconds(60), Defaults.ServerResponseTimeout);
    }

    [TestMethod]
    public void New_EnumeratedOptions_DefaultToLibcurlDefaults()
    {
        Assert.AreEqual(IpResolve.Whatever, Defaults.IpResolve);
        Assert.AreEqual(ConnectOnly.Disabled, Defaults.ConnectOnly);
        Assert.AreEqual(UseSsl.None, Defaults.UseSsl);
    }

    [TestMethod]
    public void Enums_CarryLibcurlNumbers()
    {
        Assert.AreEqual(0, (int)IpResolve.Whatever);
        Assert.AreEqual(1, (int)IpResolve.V4);
        Assert.AreEqual(2, (int)IpResolve.V6);
        Assert.AreEqual(0, (int)ConnectOnly.Disabled);
        Assert.AreEqual(1, (int)ConnectOnly.Connect);
        Assert.AreEqual(2, (int)ConnectOnly.WebSocket);
        Assert.AreEqual(0, (int)UseSsl.None);
        Assert.AreEqual(1, (int)UseSsl.Try);
        Assert.AreEqual(2, (int)UseSsl.Control);
        Assert.AreEqual(3, (int)UseSsl.All);
    }

    public static IEnumerable<object[]> SingleOptionChanges =>
    [
        Change("Url", o => o with { Url = "http://example.com/" }, o => o with { Url = Defaults.Url }),
        Change("UrlFromUrlHandle", o => o with { UrlFromUrlHandle = "http://example.com/" }, o => o with { UrlFromUrlHandle = Defaults.UrlFromUrlHandle }),
        Change("PathAsIs", o => o with { PathAsIs = true }, o => o with { PathAsIs = Defaults.PathAsIs }),
        Change("Protocols", o => o with { Protocols = "http,https" }, o => o with { Protocols = Defaults.Protocols }),
        Change("RedirectProtocols", o => o with { RedirectProtocols = "https" }, o => o with { RedirectProtocols = Defaults.RedirectProtocols }),
        Change("DefaultProtocol", o => o with { DefaultProtocol = "ftp" }, o => o with { DefaultProtocol = Defaults.DefaultProtocol }),
        Change("Port", o => o with { Port = 8080 }, o => o with { Port = Defaults.Port }),
        Change("Interface", o => o with { Interface = "if!eth0" }, o => o with { Interface = Defaults.Interface }),
        Change("LocalPort", o => o with { LocalPort = 40000 }, o => o with { LocalPort = Defaults.LocalPort }),
        Change("LocalPortRange", o => o with { LocalPortRange = 10 }, o => o with { LocalPortRange = Defaults.LocalPortRange }),
        Change("ConnectTo", o => o with { ConnectTo = ["a:80:b:8080"] }, o => o with { ConnectTo = Defaults.ConnectTo }),
        Change("Resolve", o => o with { Resolve = ["a:80:127.0.0.1"] }, o => o with { Resolve = Defaults.Resolve }),
        Change("DnsCacheTimeout", o => o with { DnsCacheTimeout = Timeout.InfiniteTimeSpan }, o => o with { DnsCacheTimeout = Defaults.DnsCacheTimeout }),
        Change("DnsServers", o => o with { DnsServers = "8.8.8.8" }, o => o with { DnsServers = Defaults.DnsServers }),
        Change("DnsInterface", o => o with { DnsInterface = "eth0" }, o => o with { DnsInterface = Defaults.DnsInterface }),
        Change("DnsLocalIPv4", o => o with { DnsLocalIPv4 = "192.0.2.1" }, o => o with { DnsLocalIPv4 = Defaults.DnsLocalIPv4 }),
        Change("DnsLocalIPv6", o => o with { DnsLocalIPv6 = "2001:db8::1" }, o => o with { DnsLocalIPv6 = Defaults.DnsLocalIPv6 }),
        Change("DnsShuffleAddresses", o => o with { DnsShuffleAddresses = true }, o => o with { DnsShuffleAddresses = Defaults.DnsShuffleAddresses }),
        Change("DohUrl", o => o with { DohUrl = "https://doh.example/dns-query" }, o => o with { DohUrl = Defaults.DohUrl }),
        Change("IpResolve", o => o with { IpResolve = IpResolve.V6 }, o => o with { IpResolve = Defaults.IpResolve }),
        Change("TcpNoDelay", o => o with { TcpNoDelay = false }, o => o with { TcpNoDelay = Defaults.TcpNoDelay }),
        Change("TcpFastOpen", o => o with { TcpFastOpen = true }, o => o with { TcpFastOpen = Defaults.TcpFastOpen }),
        Change("TcpKeepAlive", o => o with { TcpKeepAlive = true }, o => o with { TcpKeepAlive = Defaults.TcpKeepAlive }),
        Change("TcpKeepIdle", o => o with { TcpKeepIdle = TimeSpan.FromSeconds(5) }, o => o with { TcpKeepIdle = Defaults.TcpKeepIdle }),
        Change("TcpKeepInterval", o => o with { TcpKeepInterval = TimeSpan.FromSeconds(5) }, o => o with { TcpKeepInterval = Defaults.TcpKeepInterval }),
        Change("TcpKeepCount", o => o with { TcpKeepCount = 3 }, o => o with { TcpKeepCount = Defaults.TcpKeepCount }),
        Change("AddressScope", o => o with { AddressScope = 2 }, o => o with { AddressScope = Defaults.AddressScope }),
        Change("UnixSocketPath", o => o with { UnixSocketPath = "/tmp/s" }, o => o with { UnixSocketPath = Defaults.UnixSocketPath }),
        Change("AbstractUnixSocket", o => o with { AbstractUnixSocket = "s" }, o => o with { AbstractUnixSocket = Defaults.AbstractUnixSocket }),
        Change("ConnectTimeout", o => o with { ConnectTimeout = TimeSpan.FromSeconds(3) }, o => o with { ConnectTimeout = Defaults.ConnectTimeout }),
        Change("HappyEyeballsTimeout", o => o with { HappyEyeballsTimeout = TimeSpan.FromMilliseconds(50) }, o => o with { HappyEyeballsTimeout = Defaults.HappyEyeballsTimeout }),
        Change("FreshConnect", o => o with { FreshConnect = true }, o => o with { FreshConnect = Defaults.FreshConnect }),
        Change("ForbidReuse", o => o with { ForbidReuse = true }, o => o with { ForbidReuse = Defaults.ForbidReuse }),
        Change("MaxConnects", o => o with { MaxConnects = 1 }, o => o with { MaxConnects = Defaults.MaxConnects }),
        Change("MaxIdleConnectionAge", o => o with { MaxIdleConnectionAge = TimeSpan.FromSeconds(1) }, o => o with { MaxIdleConnectionAge = Defaults.MaxIdleConnectionAge }),
        Change("MaxConnectionLifetime", o => o with { MaxConnectionLifetime = TimeSpan.Zero }, o => o with { MaxConnectionLifetime = Defaults.MaxConnectionLifetime }),
        Change("ConnectOnly", o => o with { ConnectOnly = ConnectOnly.WebSocket }, o => o with { ConnectOnly = Defaults.ConnectOnly }),
        Change("AcceptTimeout", o => o with { AcceptTimeout = TimeSpan.FromSeconds(1) }, o => o with { AcceptTimeout = Defaults.AcceptTimeout }),
        Change("UpkeepInterval", o => o with { UpkeepInterval = TimeSpan.FromSeconds(1) }, o => o with { UpkeepInterval = Defaults.UpkeepInterval }),
        Change("ServerResponseTimeout", o => o with { ServerResponseTimeout = TimeSpan.FromSeconds(1) }, o => o with { ServerResponseTimeout = Defaults.ServerResponseTimeout }),
        Change("UseSsl", o => o with { UseSsl = UseSsl.All }, o => o with { UseSsl = Defaults.UseSsl }),
    ];

    [TestMethod]
    [DynamicData(nameof(SingleOptionChanges))]
    public void With_OneOption_ChangesOnlyThatOption(
        string option,
        Func<EasyNetworkOptions, EasyNetworkOptions> set,
        Func<EasyNetworkOptions, EasyNetworkOptions> restore)
    {
        EasyNetworkOptions changed = set(Defaults);

        Assert.AreNotEqual(Defaults, changed, $"setting {option} left the record unchanged");
        Assert.AreEqual(Defaults, restore(changed), $"setting {option} changed another option");
    }

    [TestMethod]
    public void SingleOptionChanges_CoverEveryProperty()
    {
        int properties = typeof(EasyNetworkOptions).GetProperties().Length;

        Assert.HasCount(properties, SingleOptionChanges);
    }

    private static object[] Change(
        string option,
        Func<EasyNetworkOptions, EasyNetworkOptions> set,
        Func<EasyNetworkOptions, EasyNetworkOptions> restore) => [option, set, restore];
}
