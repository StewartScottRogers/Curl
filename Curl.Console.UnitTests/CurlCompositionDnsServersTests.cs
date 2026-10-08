using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and
/// <c>--dns-ipv6-addr</c> replace the system resolver with the hand-built <see cref="DnsServerResolver" />
/// for every transfer of the run, as curl's c-ares build resolves then (ADR-0170, BL-694).
/// </summary>
[TestClass]
public sealed class CurlCompositionDnsServersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CreateDnsResolver_WithoutACaresOption_IsTheSystemResolver()
    {
        IDnsResolver resolver = CurlComposition.CreateDnsResolver(Parse(), TimeProvider.System, new TcpDialer());
        Diagnostics.Act("resolver type", resolver.GetType().Name);

        Diagnostics.Assert("resolver type", nameof(SystemDnsResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<SystemDnsResolver>(resolver);
    }

    [TestMethod]
    [DataRow("--dns-servers", "192.0.2.1")]
    [DataRow("--dns-interface", "eth0")]
    [DataRow("--dns-ipv4-addr", "10.1.2.3")]
    [DataRow("--dns-ipv6-addr", "::1")]
    public void CreateDnsResolver_WithACaresOption_IsTheHandBuiltResolver(string option, string value)
    {
        IDnsResolver resolver = CurlComposition.CreateDnsResolver(Parse(option, value), TimeProvider.System, new TcpDialer());
        Diagnostics.Act("resolver type", resolver.GetType().Name);

        Diagnostics.Assert("resolver type", nameof(DnsServerResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<DnsServerResolver>(resolver);
    }

    [TestMethod]
    public async Task CreateTransports_WithDnsServers_ResolvesTheTransfersHostThroughThemNotTheSystem()
    {
        // curl --dns-servers bogus http://bl694.example/ -> curl: (43) Error 43 resolving bl694.example:80 (c-ares build);
        // the system resolver has no such failure, so the exit shows which resolver ran. Nothing is sent.
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--dns-servers", "bogus"));

        ConnectResult tcp;
        DatagramOpenResult udp;
        using (Diagnostics.Phase("connect and open"))
        {
            tcp = await transports.TcpConnector.ConnectAsync(new ConnectTarget("bl694.example", 80, false), CancellationToken.None);
            udp = await transports.UdpDatagramConnector.OpenAsync("bl694.example", 69, CancellationToken.None);
        }

        Diagnostics.Act("TCP exit code", tcp.ExitCode);
        Diagnostics.Act("TCP error message", tcp.ErrorMessage);
        Diagnostics.Act("UDP exit code", udp.ExitCode);

        Diagnostics.Assert("resolver type", nameof(DnsServerResolver), transports.DnsResolver.GetType().Name);
        Assert.IsInstanceOfType<DnsServerResolver>(transports.DnsResolver);
        Diagnostics.Assert("TCP exit code", CurlExitCode.BadFunctionArgument, tcp.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, tcp.ExitCode);
        Diagnostics.Assert("TCP error message", "Error 43 resolving bl694.example:80", tcp.ErrorMessage);
        Assert.AreEqual("Error 43 resolving bl694.example:80", tcp.ErrorMessage);
        Diagnostics.Assert("UDP exit code", CurlExitCode.BadFunctionArgument, udp.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, udp.ExitCode);
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("http://bl694.example/")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://bl694.example/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
