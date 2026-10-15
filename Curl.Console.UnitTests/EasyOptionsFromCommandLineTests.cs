using Curl.Api.Options.Network;
using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.NetworkOf" />: each network-group command-line option
/// given sets its <see cref="EasyNetworkOptions" /> member, and a command line without it keeps
/// libcurl's default (ADR-0513).
/// </summary>
[TestClass]
public sealed partial class EasyOptionsFromCommandLineTests
{
    private const string Url = "https://example.com/";

    private static readonly EasyNetworkOptions LibcurlDefaults = new();

    public static IEnumerable<object?[]> GivenOptions =>
    [
        Given("--path-as-is", o => o.PathAsIs, true, "--path-as-is"),
        Given("--proto", o => o.Protocols, "http,https", "--proto", "=https,http"),
        Given("--proto-redir", o => o.RedirectProtocols, "https", "--proto-redir", "=https"),
        Given("--proto-default", o => o.DefaultProtocol, "ftp", "--proto-default", "ftp"),
        Given("--interface", o => o.Interface, "if!eth0", "--interface", "if!eth0"),
        Given("--local-port first", o => o.LocalPort, 40000, "--local-port", "40000-40009"),
        Given("--local-port count", o => o.LocalPortRange, 10, "--local-port", "40000-40009"),
        Given("--connect-to", o => string.Join('|', o.ConnectTo), "a:80:b:8080|c:443:d:8443", "--connect-to", "a:80:b:8080", "--connect-to", "c:443:d:8443"),
        Given("--resolve", o => string.Join('|', o.Resolve), "a:80:127.0.0.1", "--resolve", "a:80:127.0.0.1"),
        Given("--dns-servers", o => o.DnsServers, "192.0.2.53", "--dns-servers", "192.0.2.53"),
        Given("--dns-interface", o => o.DnsInterface, "eth0", "--dns-interface", "eth0"),
        Given("--dns-ipv4-addr", o => o.DnsLocalIPv4, "192.0.2.1", "--dns-ipv4-addr", "192.0.2.1"),
        Given("--dns-ipv6-addr", o => o.DnsLocalIPv6, "2001:db8::1", "--dns-ipv6-addr", "2001:db8::1"),
        Given("--doh-url", o => o.DohUrl, "https://doh.example/dns-query", "--doh-url", "https://doh.example/dns-query"),
        Given("-4", o => o.IpResolve, IpResolve.V4, "-4"),
        Given("-6", o => o.IpResolve, IpResolve.V6, "-6"),
        Given("--no-tcp-nodelay", o => o.TcpNoDelay, false, "--no-tcp-nodelay"),
        Given("--tcp-fastopen", o => o.TcpFastOpen, true, "--tcp-fastopen"),
        Given("--keepalive-time idle", o => o.TcpKeepIdle, TimeSpan.FromSeconds(7), "--keepalive-time", "7"),
        Given("--keepalive-time interval", o => o.TcpKeepInterval, TimeSpan.FromSeconds(7), "--keepalive-time", "7"),
        Given("--keepalive-cnt", o => o.TcpKeepCount, 3, "--keepalive-cnt", "3"),
        Given("--no-keepalive", o => o.TcpKeepAlive, false, "--no-keepalive"),
        Given("--no-keepalive drops --keepalive-time", o => o.TcpKeepIdle, LibcurlDefaults.TcpKeepIdle, "--keepalive-time", "7", "--no-keepalive"),
        Given("--no-keepalive drops --keepalive-cnt", o => o.TcpKeepCount, LibcurlDefaults.TcpKeepCount, "--keepalive-cnt", "3", "--no-keepalive"),
        Given("--ip-tos", o => o.IpTypeOfService, 16, "--ip-tos", "16"),
        Given("--vlan-priority", o => o.VlanPriority, 5, "--vlan-priority", "5"),
        Given("--mptcp", o => o.MultipathTcp, true, "--mptcp"),
        Given("--unix-socket", o => o.UnixSocketPath, "/tmp/curl.sock", "--unix-socket", "/tmp/curl.sock"),
        Given("--unix-socket abstract", o => o.AbstractUnixSocket, null, "--unix-socket", "/tmp/curl.sock"),
        Given("--abstract-unix-socket", o => o.AbstractUnixSocket, "curl", "--abstract-unix-socket", "curl"),
        Given("--abstract-unix-socket path", o => o.UnixSocketPath, null, "--abstract-unix-socket", "curl"),
        Given("--connect-timeout", o => o.ConnectTimeout, TimeSpan.FromSeconds(2.5), "--connect-timeout", "2.5"),
        Given("--connect-timeout 0", o => o.ConnectTimeout, TimeSpan.Zero, "--connect-timeout", "0"),
        Given("--happy-eyeballs-timeout-ms", o => o.HappyEyeballsTimeout, TimeSpan.FromMilliseconds(50), "--happy-eyeballs-timeout-ms", "50"),
        Given("--happy-eyeballs-timeout-ms 0", o => o.HappyEyeballsTimeout, LibcurlDefaults.HappyEyeballsTimeout, "--happy-eyeballs-timeout-ms", "0"),
        Given("--ssl", o => o.UseSsl, UseSsl.Try, "--ssl"),
        Given("--ssl-reqd", o => o.UseSsl, UseSsl.All, "--ssl-reqd"),
        Given("--ftp-ssl-control", o => o.UseSsl, UseSsl.Control, "--ftp-ssl-control"),
        Given("--ftp-ssl-control outranks --ssl-reqd", o => o.UseSsl, UseSsl.Control, "--ssl-reqd", "--ftp-ssl-control"),
        Given("--ssl-reqd outranks --ssl", o => o.UseSsl, UseSsl.All, "--ssl", "--ssl-reqd"),
    ];

    public static IEnumerable<object?[]> AbsentOptions =>
    [
        Absent("PathAsIs", o => o.PathAsIs),
        Absent("Protocols", o => o.Protocols),
        Absent("RedirectProtocols", o => o.RedirectProtocols),
        Absent("DefaultProtocol", o => o.DefaultProtocol),
        Absent("Interface", o => o.Interface),
        Absent("LocalPort", o => o.LocalPort),
        Absent("LocalPortRange", o => o.LocalPortRange),
        Absent("ConnectTo", o => o.ConnectTo.Count),
        Absent("Resolve", o => o.Resolve.Count),
        Absent("DnsServers", o => o.DnsServers),
        Absent("DnsInterface", o => o.DnsInterface),
        Absent("DnsLocalIPv4", o => o.DnsLocalIPv4),
        Absent("DnsLocalIPv6", o => o.DnsLocalIPv6),
        Absent("DohUrl", o => o.DohUrl),
        Absent("IpResolve", o => o.IpResolve),
        Absent("TcpNoDelay", o => o.TcpNoDelay),
        Absent("TcpFastOpen", o => o.TcpFastOpen),
        Absent("TcpKeepIdle", o => o.TcpKeepIdle),
        Absent("TcpKeepInterval", o => o.TcpKeepInterval),
        Absent("TcpKeepCount", o => o.TcpKeepCount),
        Absent("IpTypeOfService", o => o.IpTypeOfService),
        Absent("VlanPriority", o => o.VlanPriority),
        Absent("MultipathTcp", o => o.MultipathTcp),
        Absent("UnixSocketPath", o => o.UnixSocketPath),
        Absent("AbstractUnixSocket", o => o.AbstractUnixSocket),
        Absent("ConnectTimeout", o => o.ConnectTimeout),
        Absent("HappyEyeballsTimeout", o => o.HappyEyeballsTimeout),
        Absent("UseSsl", o => o.UseSsl),
        Absent("Url", o => o.Url),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenOptions))]
    public void NetworkOf_OptionGiven_SetsItsMember(string option, Func<EasyNetworkOptions, object?> read, object? expected, string[] arguments)
    {
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(Parse(arguments));

        Assert.AreEqual(expected, read(network), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentOptions))]
    public void NetworkOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyNetworkOptions, object?> read)
    {
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(Parse());

        Assert.AreEqual(read(LibcurlDefaults), read(network), member);
    }

    [TestMethod]
    public void NetworkOf_NoOption_TurnsTcpKeepAliveOnAsCurlDoes()
    {
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(Parse());

        Assert.IsTrue(network.TcpKeepAlive);
    }

    [TestMethod]
    public void NetworkOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.NetworkOf(null!));
    }

    private static object?[] Given(string option, Func<EasyNetworkOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] Absent(string member, Func<EasyNetworkOptions, object?> read) => [member, read];

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. arguments, Url], _ => true);
        Assert.IsTrue(result.IsAccepted, string.Join(' ', arguments));
        return result.Options;
    }
}
