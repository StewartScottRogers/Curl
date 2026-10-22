using Curl.Api.Options.Proxy;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins when <see cref="TransferContextFactory" /> gives a transfer events that write each CONNECT
/// reply head to the header output: only through an HTTP or HTTPS proxy, with a header output, and
/// without <c>--suppress-connect-headers</c> (AF-0298).
/// </summary>
[TestClass]
public sealed class TransferContextFactoryConnectReplyHeadTests
{
    private const string Url = "https://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Create_HttpProxyWithHeaderOutput_WritesConnectReplyHeads()
    {
        using MemoryStream headerOutput = new();

        bool writesHeads = WritesConnectReplyHeads([Url], new ProxyEndpoint(ProxyKind.Http, "proxy", 3128, null), headerOutput);

        Diagnostics.Assert("events write CONNECT reply heads", true, writesHeads);
        Assert.IsTrue(writesHeads);
    }

    [TestMethod]
    public void Create_HttpProxyWithoutHeaderOutput_LeavesTheEventsAsTheyAre()
    {
        bool writesHeads = WritesConnectReplyHeads([Url], new ProxyEndpoint(ProxyKind.Http, "proxy", 3128, null), null);

        Diagnostics.Assert("events write CONNECT reply heads", false, writesHeads);
        Assert.IsFalse(writesHeads);
    }

    [TestMethod]
    public void Create_SuppressConnectHeaders_LeavesTheEventsAsTheyAre()
    {
        using MemoryStream headerOutput = new();

        bool writesHeads = WritesConnectReplyHeads(["--suppress-connect-headers", Url], new ProxyEndpoint(ProxyKind.Http, "proxy", 3128, null), headerOutput);

        Diagnostics.Assert("events write CONNECT reply heads", false, writesHeads);
        Assert.IsFalse(writesHeads);
    }

    [TestMethod]
    public void Create_SocksProxyWithHeaderOutput_LeavesTheEventsAsTheyAre()
    {
        using MemoryStream headerOutput = new();

        bool writesHeads = WritesConnectReplyHeads([Url], new ProxyEndpoint(ProxyKind.Socks5, "proxy", 1080, null), headerOutput);

        Diagnostics.Assert("events write CONNECT reply heads", false, writesHeads);
        Assert.IsFalse(writesHeads);
    }

    private static bool WritesConnectReplyHeads(string[] arguments, ProxyEndpoint proxy, Stream? headerOutput)
    {
        using MemoryStream standardInput = new();
        using MemoryStream output = new();
        CommandLineOptions options = CommandLineParser.Parse(arguments, _ => true).Options!;

        TransferContext context = new TransferContextFactory(standardInput)
            .Create(EasyOptionsFromCommandLine.TransferSettingsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.BehaviourOf(options), EasyOptionsFromCommandLine.TransferOf(options), EasyOptionsFromCommandLine.ProtocolOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), EasyOptionsFromCommandLine.NetworkOf(options), CurlUrl.Parse(Url), output, null, null, headerOutput, proxy: proxy);
        return context.Events is IConnectReplyHeadWritingEvents;
    }
}
