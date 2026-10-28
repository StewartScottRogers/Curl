using System.Text;
using Curl.Cli;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-163 (ADR-0859): curl 8.22.0's release build uses only an <c>https</c> DoH URL; an <c>http</c>
/// <c>--doh-url</c> fails every name with exit 6 and <c>Could not resolve host</c> without
/// contacting the DoH server (measured with curl 8.21.0's release build on Windows). The debug build's
/// allowance stays behind <c>allowsPlainHttpDohUrl</c> for the conformance harness.
/// </summary>
[TestClass]
public sealed class Cgr163Tests
{
    private static readonly byte[] Forbidden = Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

    [TestMethod]
    [DataRow("http://127.0.0.1:21800/dns-query")]
    [DataRow("HTTP://127.0.0.1:21800/dns-query")]
    public async Task CreateDnsResolver_DohUrlThatIsNotHttps_ResolvesNothingAndNeverDialsTheDohServer(string dohUrl)
    {
        ScriptedConnector dohServer = new([Forbidden, Forbidden]);
        IDnsResolver resolver = CreateDnsResolver(dohUrl, dohServer, allowsPlainHttpDohUrl: false);

        IReadOnlyList<System.Net.IPAddress> addresses = await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsEmpty(dohServer.Targets);
    }

    [TestMethod]
    public async Task CreateDnsResolver_PlainHttpDohUrlAllowed_DialsTheDohServer()
    {
        ScriptedConnector dohServer = new([Forbidden, Forbidden]);
        IDnsResolver resolver = CreateDnsResolver("http://127.0.0.1:21800/dns-query", dohServer, allowsPlainHttpDohUrl: true);

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.IsNotEmpty(dohServer.Targets);
    }

    [TestMethod]
    [DataRow("https://127.0.0.1/dns-query", true)]
    [DataRow("HTTPS://127.0.0.1/dns-query", true)]
    [DataRow("http://127.0.0.1/dns-query", false)]
    [DataRow("127.0.0.1/dns-query", true)]
    [DataRow("ftp://127.0.0.1/dns-query", false)]
    [DataRow("bogus", true)]
    public void IsHttpsDohUrl_Value_IsTrueOnlyForAnHttpsUrl(string dohUrl, bool expected)
    {
        Assert.AreEqual(expected, EngineComposition.IsHttpsDohUrl(dohUrl));
    }

    private static IDnsResolver CreateDnsResolver(string dohUrl, ScriptedConnector dohServer, bool allowsPlainHttpDohUrl)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["--doh-url", dohUrl, "http://foo.test:21802/x"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return EngineComposition.CreateDnsResolver(
            EasyOptionsFromCommandLine.NetworkOf(parsed.Options), EasyOptionsFromCommandLine.TlsOf(parsed.Options), TimeProvider.System, new ScriptedTcpDialer(dohServer), allowsPlainHttpDohUrl: allowsPlainHttpDohUrl);
    }
}
