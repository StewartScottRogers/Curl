using Curl.Authentication;
using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins the <c>with</c> behaviour of the <see cref="CurlTransports" /> record: a copy holds
/// every property it names and carries over every property it does not; and the
/// <see cref="HttpProxyTunnelOptions" /> the run's <see cref="TcpConnector" /> is built with.
/// </summary>
[TestClass]
public sealed class CurlTransportsTests
{
    [TestMethod]
    public void CurlTransports_WithExpressionNamingEveryProperty_ReplacesEachProperty()
    {
        CurlTransports original = CurlComposition.CreateTransports(NoOptions());
        CurlTransports replacement = CurlComposition.CreateTransports(NoOptions());
        TimeProvider replacementTimeProvider = new ReplacementTimeProvider();

        CurlTransports copy = original with
        {
            DnsResolver = replacement.DnsResolver,
            TimeProvider = replacementTimeProvider,
            TcpDialer = replacement.TcpDialer,
            TlsClientOptions = replacement.TlsClientOptions with { Insecure = true },
            TlsProvider = replacement.TlsProvider,
            ProxyTunnelOptions = replacement.ProxyTunnelOptions with { UserAgent = "replaced" },
            TcpConnector = replacement.TcpConnector,
            UdpDatagramConnector = replacement.UdpDatagramConnector,
        };

        Assert.AreSame(replacement.DnsResolver, copy.DnsResolver);
        Assert.AreSame(replacementTimeProvider, copy.TimeProvider);
        Assert.AreSame(replacement.TcpDialer, copy.TcpDialer);
        Assert.IsTrue(copy.TlsClientOptions.Insecure);
        Assert.AreSame(replacement.TlsProvider, copy.TlsProvider);
        Assert.AreEqual("replaced", copy.ProxyTunnelOptions.UserAgent);
        Assert.AreSame(replacement.TcpConnector, copy.TcpConnector);
        Assert.AreSame(replacement.UdpDatagramConnector, copy.UdpDatagramConnector);
        Assert.AreSame(TimeProvider.System, original.TimeProvider);
        Assert.IsFalse(original.TlsClientOptions.Insecure);
    }

    [TestMethod]
    public void CurlTransports_WithExpressionNamingOneProperty_CopiesTheRest()
    {
        CurlTransports original = CurlComposition.CreateTransports(NoOptions());
        CurlTransports replacement = CurlComposition.CreateTransports(NoOptions());

        CurlTransports copy = original with { DnsResolver = replacement.DnsResolver };

        Assert.AreSame(replacement.DnsResolver, copy.DnsResolver);
        Assert.AreSame(original.TimeProvider, copy.TimeProvider);
        Assert.AreSame(original.TcpDialer, copy.TcpDialer);
        Assert.AreSame(original.TlsClientOptions, copy.TlsClientOptions);
        Assert.AreSame(original.TlsProvider, copy.TlsProvider);
        Assert.AreSame(original.ProxyTunnelOptions, copy.ProxyTunnelOptions);
        Assert.AreSame(original.TcpConnector, copy.TcpConnector);
        Assert.AreSame(original.UdpDatagramConnector, copy.UdpDatagramConnector);
    }

    [TestMethod]
    public void CreateTransports_WithUserAgent_TunnelOptionsCarryIt()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Options("-A", "agent/1.0", "gophers://example.com/"));

        Assert.AreEqual("agent/1.0", transports.ProxyTunnelOptions.UserAgent);
    }

    [TestMethod]
    public void CreateTransports_WithEmptyUserAgent_TunnelOptionsUserAgentIsNull()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Options("-A", "", "gophers://example.com/"));

        Assert.IsNull(transports.ProxyTunnelOptions.UserAgent);
    }

    [TestMethod]
    public void CreateTransports_WithoutUserAgent_TunnelOptionsUserAgentIsCurl8210()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Assert.AreEqual("curl/8.21.0", transports.ProxyTunnelOptions.UserAgent);
    }

    [TestMethod]
    public void CreateTransports_TunnelOptionsCredentialEncoding_IsForPlatform()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Assert.AreEqual(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), transports.ProxyTunnelOptions.CredentialEncoding);
    }

    private static CommandLineOptions NoOptions() => Options("gophers://example.com/");

    private static CommandLineOptions Options(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>
    /// A clock that behaves like <see cref="TimeProvider.System" /> but is a different
    /// instance, so a test can tell a replaced <see cref="CurlTransports.TimeProvider" />
    /// from the original.
    /// </summary>
    private sealed class ReplacementTimeProvider : TimeProvider;
}
