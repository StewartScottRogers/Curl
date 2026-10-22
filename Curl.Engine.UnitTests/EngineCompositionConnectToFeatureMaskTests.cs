using Curl.Api.Options.Network;
using Curl.Console;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that the TCP connector the composition builds parses its <c>--connect-to</c> mappings under
/// the run's <see cref="EngineFeatureMask" />: hiding <see cref="EngineFeatureMask.Ipv6" /> fails a
/// bracketed destination with exit 4, as a curl built without IPv6 does (upstream test1454, BL-2996).
/// </summary>
[TestClass]
public sealed class EngineCompositionConnectToFeatureMaskTests
{
    private const string BracketedConnectTo = "localhost:80:[::1]:80";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task CreateTcpConnector_MaskHidingIpv6WithBracketedConnectTo_FailsWithExit4AndDialsNothing()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, EngineFeatureMask.Ipv6);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", ConnectToMappings.Ipv6NotBuiltInMessage, result.ErrorMessage);
        Assert.AreEqual(ConnectToMappings.Ipv6NotBuiltInMessage, result.ErrorMessage);
        Diagnostics.Assert("server.Targets count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress()
    {
        ScriptedConnector server = new([]);

        ConnectResult result = await ConnectAsync(server, EngineFeatureMask.None);

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("server.Targets count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Assert("server.Targets[0].Port", 80, server.Targets[0].Port);
        Assert.AreEqual(80, server.Targets[0].Port);
    }

    [TestMethod]
    public void ConnectToMappingsOf_MaskHidingIpv6AndProxy_MapsBracketedDestinationToExit4()
    {
        EasyNetworkOptions network = Arrange();

        ConnectDestination destination = EngineComposition.ConnectToMappingsOf(network, EngineFeatureMask.Ipv6 | EngineFeatureMask.Proxy).Map("localhost", 80);

        Diagnostics.Act("destination.ParseErrorCode", destination.ParseErrorCode);
        Diagnostics.Assert("destination.ParseErrorCode", CurlExitCode.NotBuiltIn, destination.ParseErrorCode);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, destination.ParseErrorCode);
    }

    [TestMethod]
    public void ConnectToMappingsOf_MaskHidingOnlyProxy_MapsBracketedDestinationToTheIpv6Host()
    {
        EasyNetworkOptions network = Arrange();

        ConnectDestination destination = EngineComposition.ConnectToMappingsOf(network, EngineFeatureMask.Proxy).Map("localhost", 80);

        Diagnostics.Act("destination.Host", destination.Host);
        Diagnostics.Assert("destination.ParseError", null, destination.ParseError);
        Assert.IsNull(destination.ParseError);
        Diagnostics.Assert("destination.Host", "::1", destination.Host);
        Assert.AreEqual("::1", destination.Host);
    }

    private EasyNetworkOptions Arrange()
    {
        Diagnostics.Arrange("--connect-to", BracketedConnectTo);
        CommandLineParseResult parsed = CommandLineParser.Parse(["--connect-to", BracketedConnectTo, "http://localhost/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return EasyOptionsFromCommandLine.NetworkOf(parsed.Options);
    }

    /// <summary>
    /// Builds the production TCP connector for <c>--connect-to localhost:80:[::1]:80</c> under
    /// <paramref name="featureMask" />, over a <see cref="LoopbackDnsResolver" /> and
    /// <paramref name="server" />, and connects to <c>localhost</c> on port 80.
    /// </summary>
    private async Task<ConnectResult> ConnectAsync(ScriptedConnector server, EngineFeatureMask featureMask)
    {
        Diagnostics.Arrange("featureMask", featureMask);
        Diagnostics.Arrange("--connect-to", BracketedConnectTo);
        CommandLineParseResult parsed = CommandLineParser.Parse(["--connect-to", BracketedConnectTo, "http://localhost/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        TcpConnector connector = EngineComposition.CreateTcpConnector(
            EasyOptionsFromCommandLine.ToolOptionsOf(parsed.Options), EasyOptionsFromCommandLine.TraceComponentsOf(parsed.Options), EasyOptionsFromCommandLine.NetworkOf(parsed.Options), EasyOptionsFromCommandLine.HttpOf(parsed.Options), EasyOptionsFromCommandLine.ProxyOf(parsed.Options), EasyOptionsFromCommandLine.AuthenticationOf(parsed.Options),
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(server),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default,
            featureMask: featureMask);

        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await connector.ConnectAsync(new ConnectTarget("localhost", 80, false), CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("dialled targets", string.Join(", ", server.Targets.Select(target => $"{target.Host}:{target.Port}")));
        return result;
    }
}
