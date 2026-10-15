using Curl.Api.Options.Behaviour;
using Curl.Cli;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Pins how <c>--happy-eyeballs-timeout-ms</c> reaches <see cref="HttpRequestOptions.HappyEyeballsTimeout" />
/// through <see cref="HttpRequestOptionsMapping.Of" />, the QUIC-against-TCP race of a
/// <c>--http3</c> transfer (ADR-0144), and <see cref="TcpConnector.HappyEyeballsTimeout" /> through
/// <see cref="EngineComposition.CreateTcpConnector" />, the IPv4-against-IPv6 race (ADR-0254, BL-889).
/// </summary>
[TestClass]
public sealed class HappyEyeballsTimeoutMappingTests
{
    private const string Url = "https://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromCommandLine_HappyEyeballsTimeout1000_RacesQuicFor1000Milliseconds()
    {
        Diagnostics.Arrange("arguments", "--http3 --happy-eyeballs-timeout-ms 1000");

        HttpRequestOptions http = HttpRequestOptionsMapping.Of(Parse("--http3", "--happy-eyeballs-timeout-ms", "1000"), EasyOptionsFromCommandLine.HttpOf(Parse("--http3", "--happy-eyeballs-timeout-ms", "1000")), new EasyBehaviourOptions(), new EasyTransferOptions());

        Diagnostics.Act("HappyEyeballsTimeout", http.HappyEyeballsTimeout);

        Diagnostics.Assert("HappyEyeballsTimeout", TimeSpan.FromMilliseconds(1000), http.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1000), http.HappyEyeballsTimeout);
    }

    [TestMethod]
    public void FromCommandLine_NoHappyEyeballsTimeout_RacesQuicForTheDefault200Milliseconds()
    {
        Diagnostics.Arrange("arguments", "--http3");

        HttpRequestOptions http = HttpRequestOptionsMapping.Of(Parse("--http3"), EasyOptionsFromCommandLine.HttpOf(Parse("--http3")), new EasyBehaviourOptions(), new EasyTransferOptions());

        Diagnostics.Act("HappyEyeballsTimeout", http.HappyEyeballsTimeout);

        Diagnostics.Assert("HappyEyeballsTimeout", TimeSpan.FromMilliseconds(200), http.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), http.HappyEyeballsTimeout);
    }

    // Only a 64-bit C long reaches past the longest timer delay; on Windows LONG_MAX is 2147483647 ms.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void FromCommandLine_HappyEyeballsTimeoutPastTheLongestTimerDelay_IsHeldToIt()
    {
        Diagnostics.Arrange("arguments", "--happy-eyeballs-timeout-ms 9000000000000");

        HttpRequestOptions http = HttpRequestOptionsMapping.Of(Parse("--happy-eyeballs-timeout-ms", "9000000000000"), EasyOptionsFromCommandLine.HttpOf(Parse("--happy-eyeballs-timeout-ms", "9000000000000")), new EasyBehaviourOptions(), new EasyTransferOptions());

        Diagnostics.Act("HappyEyeballsTimeout", http.HappyEyeballsTimeout);

        Diagnostics.Assert("HappyEyeballsTimeout", TimeSpan.FromMilliseconds(uint.MaxValue - 1), http.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(uint.MaxValue - 1), http.HappyEyeballsTimeout);
    }

    [TestMethod]
    public void CreateTcpConnector_HappyEyeballsTimeout50_RacesAddressFamiliesFor50Milliseconds()
    {
        Diagnostics.Arrange("arguments", "--happy-eyeballs-timeout-ms 50");

        TcpConnector connector = CreateTcpConnector(Parse("--happy-eyeballs-timeout-ms", "50"));

        Diagnostics.Act("HappyEyeballsTimeout", connector.HappyEyeballsTimeout);

        Diagnostics.Assert("HappyEyeballsTimeout", TimeSpan.FromMilliseconds(50), connector.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(50), connector.HappyEyeballsTimeout);
    }

    [TestMethod]
    public void CreateTcpConnector_NoHappyEyeballsTimeout_RacesAddressFamiliesForTheDefault()
    {
        Diagnostics.Arrange("arguments", "(none)");

        TcpConnector connector = CreateTcpConnector(Parse());

        Diagnostics.Act("HappyEyeballsTimeout", connector.HappyEyeballsTimeout);

        Diagnostics.Assert("HappyEyeballsTimeout", TcpConnector.DefaultHappyEyeballsTimeout, connector.HappyEyeballsTimeout);
        Assert.AreEqual(TcpConnector.DefaultHappyEyeballsTimeout, connector.HappyEyeballsTimeout);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. arguments, Url], _ => true);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static TcpConnector CreateTcpConnector(CommandLineOptions options) =>
        EngineComposition.CreateTcpConnector(
            options, EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options),
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
}
