using Curl.Cli;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins that a plain <c>http://</c> <c>--doh-url</c> is refused as curl 8.22.0's release build refuses it:
/// the production caller of <c>CreateDnsResolver</c> relies on its default, which must not allow it (AF-0771).
/// </summary>
[TestClass]
public sealed class EngineCompositionPlainHttpDohUrlTests
{
    [TestMethod]
    public void CreateDnsResolver_PlainHttpDohUrlByDefault_IsTheUnusableDohUrlResolver()
    {
        CommandLineOptions options = Parse("http://127.0.0.1:21800/dns-query");

        IDnsResolver resolver = EngineComposition.CreateDnsResolver(EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), TimeProvider.System, new TcpDialer());

        Assert.IsInstanceOfType<UnusableDohUrlResolver>(resolver);
    }

    [TestMethod]
    public void CreateTransports_PlainHttpDohUrl_ResolvesThroughTheUnusableDohUrlResolver()
    {
        CommandLineOptions options = Parse("http://127.0.0.1:21800/dns-query");

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options));

        Assert.IsInstanceOfType<UnusableDohUrlResolver>(transports.DnsResolver);
    }

    private static CommandLineOptions Parse(string dohUrl)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["--doh-url", dohUrl, "http://bl5220.example/"], _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, new DefaultConfigFileSearch(_ => null, isWindows: false, null, null), isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: false, actsAsCAresBuild: true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
