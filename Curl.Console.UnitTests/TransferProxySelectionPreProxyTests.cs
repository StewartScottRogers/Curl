using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how <c>--preproxy</c> picks a transfer's proxy and the connector's pre-proxy, as curl
/// 8.21.0 does (measured 2026-09-30; the commands and results are in BL-614's Notes).
/// </summary>
[TestClass]
public sealed class TransferProxySelectionPreProxyTests
{
    private static readonly ProxySelector EnvironmentWithAnHttpProxy =
        new(name => name == "http_proxy" ? "http://env.example:8080" : null);

    [TestMethod]
    [DataRow("http://10.0.0.1:3128", ProxyKind.Http)]
    [DataRow("https://10.0.0.1:3128", ProxyKind.Https)]
    public void TrySelect_WithAPreProxyAndAnHttpProxy_ChoosesTheHttpProxy(string proxyText, ProxyKind kind)
    {
        ProxyEndpoint? proxy = Select(out TransferResult? failure, "--preproxy", "socks5://127.0.0.1:41080", "-x", proxyText, "http://h/");

        Assert.IsNull(failure);
        Assert.AreEqual(new ProxyEndpoint(kind, "10.0.0.1", 3128, null), proxy);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyAndAnHttp10Proxy_ChoosesTheHttp10Proxy()
    {
        ProxyEndpoint? proxy = Select(out _, "--preproxy", "socks5://127.0.0.1:41080", "--proxy1.0", "10.0.0.1:3128", "http://h/");

        Assert.AreEqual(ProxyKind.Http10, proxy?.Kind);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyAndProxyUser_GivesTheHttpProxyTheCredential()
    {
        ProxyEndpoint? proxy = Select(out _, "--preproxy", "socks5://127.0.0.1:41080", "-x", "http://10.0.0.1:3128", "-U", "u:p", "http://h/");

        Assert.AreEqual("u", proxy?.Credential?.UserName);
    }

    [TestMethod]
    public void TrySelect_WithOnlyAPreProxy_ChoosesItAsTheSocksProxyAndReadsNoEnvironment()
    {
        // curl --preproxy socks5://127.0.0.1:41080 http://h/ runs the SOCKS5 handshake for h itself.
        ProxyEndpoint? proxy = Select(out TransferResult? failure, "--preproxy", "socks5://127.0.0.1:41080", "http://h/");

        Assert.IsNull(failure);
        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 41080, null), proxy);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyAndAnEmptyProxy_ChoosesThePreProxy()
    {
        ProxyEndpoint? proxy = Select(out _, "--preproxy", "socks5h://127.0.0.1:41080", "-x", string.Empty, "http://h/");

        Assert.AreEqual(ProxyKind.Socks5Hostname, proxy?.Kind);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyWithoutAScheme_ReadsItAsSocks4()
    {
        // curl --preproxy 127.0.0.1:41080 ... sent 04 01 0c 38 0a 00 00 01 00, a SOCKS4 request.
        ProxyEndpoint? proxy = Select(out _, "--preproxy", "127.0.0.1:41080", "http://h/");

        Assert.AreEqual(ProxyKind.Socks4, proxy?.Kind);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyAndAMatchingNoProxy_ChoosesNoProxy()
    {
        // curl --preproxy socks5://127.0.0.1:41081 -x http://10.0.0.1:3128 --noproxy h http://h:41081/ -> (6) Could not resolve host: h
        ProxyEndpoint? proxy = Select(out TransferResult? failure, "--preproxy", "socks5://127.0.0.1:41080", "-x", "http://10.0.0.1:3128", "--noproxy", "h", "http://h/");

        Assert.IsNull(failure);
        Assert.IsNull(proxy);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:41081")]
    [DataRow("https://127.0.0.1:41081")]
    public void TrySelect_WithAnHttpPreProxy_FailsWithCouldntResolveProxy(string preProxyText)
    {
        // curl --preproxy http://127.0.0.1:41081 -x socks5://127.0.0.1:41099 http://h/ -> curl: (5) Unsupported pre-proxy type for 'http://127.0.0.1:41081'
        Select(out TransferResult? failure, "--preproxy", preProxyText, "-x", "socks5://127.0.0.1:41099", "http://h/");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntResolveProxy, $"Unsupported pre-proxy type for '{preProxyText}'"), failure);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyThatDoesNotParse_FailsBeforeReadingTheProxy()
    {
        // curl --preproxy "bad host" -x "also bad" http://h/ -> curl: (5) Unsupported proxy syntax in 'bad host': Malformed input to a URL function
        Select(out TransferResult? failure, "--preproxy", "bad host", "-x", "also bad", "http://h/");

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.CouldntResolveProxy, "Unsupported proxy syntax in 'bad host': Malformed input to a URL function"),
            failure);
    }

    [TestMethod]
    public void TrySelect_WithAPreProxyAndAProxyThatDoesNotParse_FailsWithTheProxysSyntax()
    {
        Select(out TransferResult? failure, "--preproxy", "socks5://127.0.0.1:41080", "-x", "also bad", "http://h/");

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.CouldntResolveProxy, "Unsupported proxy syntax in 'also bad': Malformed input to a URL function"),
            failure);
    }

    [TestMethod]
    [DataRow("-x", "socks5://127.0.0.1:41099")]
    [DataRow("--socks5", "127.0.0.1:41099")]
    public void TrySelect_WithAPreProxyAndASocksProxy_FailsWithCouldntResolveProxy(string option, string proxyText)
    {
        // curl --preproxy socks5://127.0.0.1:41081 --socks5 127.0.0.1:41099 http://h/ ->
        // curl: (5) Having a SOCKS pre-proxy and proxy is not supported with '127.0.0.1:41099'
        Select(out TransferResult? failure, "--preproxy", "socks5://127.0.0.1:41080", option, proxyText, "http://h/");

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.CouldntResolveProxy, $"Having a SOCKS pre-proxy and proxy is not supported with '{proxyText}'"),
            failure);
    }

    [TestMethod]
    public void PreProxyOf_WithASocksPreProxy_GivesTheConnectorIt()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--preproxy", "socks5://127.0.0.1:41080", "-x", "http://10.0.0.1:3128", "http://h/"));

        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 41080, null), transports.TcpConnector.PreProxy);
    }

    [TestMethod]
    [DataRow(new[] { "http://h/" })]
    [DataRow(new[] { "--preproxy", "bad host", "http://h/" })]
    [DataRow(new[] { "--preproxy", "http://127.0.0.1:41080", "http://h/" })]
    [DataRow(new[] { "--preproxy", "https://127.0.0.1:41080", "http://h/" })]
    public void PreProxyOf_WithNoUsableSocksPreProxy_GivesNone(string[] arguments)
    {
        Assert.IsNull(CurlComposition.PreProxyOf(Parse(arguments)));
    }

    private static ProxyEndpoint? Select(out TransferResult? failure, params string[] arguments)
    {
        TransferProxySelection.TrySelect(EnvironmentWithAnHttpProxy, Parse(arguments), CurlUrl.Parse(arguments[^1]), out ProxyEndpoint? proxy, out failure);
        return proxy;
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
