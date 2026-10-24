using Curl.Api.Options.Network;
using Curl.Cli;
using Curl.Console;
using Curl.Networking;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that the pooling connector the composition builds carries the handle's
/// <c>CURLOPT_FRESH_CONNECT</c> and <c>CURLOPT_FORBID_REUSE</c> (BL-3326).
/// </summary>
[TestClass]
public sealed class EngineCompositionPoolingConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task CreatePoolingConnector_WithTheHandlesReuseOptions_CarriesThemOnTheConnector(bool freshConnect, bool forbidReuse)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["http://bl3326.example/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        EasyNetworkOptions network = EasyOptionsFromCommandLine.NetworkOf(parsed.Options) with { FreshConnect = freshConnect, ForbidReuse = forbidReuse };

        await using PoolingConnector alone = EngineComposition.CreatePoolingConnector(EasyOptionsFromCommandLine.ToolOptionsOf(parsed.Options), network, EasyOptionsFromCommandLine.TlsOf(parsed.Options), EasyOptionsFromCommandLine.HttpOf(parsed.Options), EasyOptionsFromCommandLine.ProxyOf(parsed.Options), new ScriptedConnector([]), TimeProvider.System, runConnections: null);
        await using ConnectionCache cache = new(TimeProvider.System);
        PoolingConnector shared = EngineComposition.CreatePoolingConnector(EasyOptionsFromCommandLine.ToolOptionsOf(parsed.Options), network, EasyOptionsFromCommandLine.TlsOf(parsed.Options), EasyOptionsFromCommandLine.HttpOf(parsed.Options), EasyOptionsFromCommandLine.ProxyOf(parsed.Options), new ScriptedConnector([]), TimeProvider.System, cache);

        Diagnostics.Assert("alone", (freshConnect, forbidReuse), (alone.FreshConnect, alone.ForbidReuse));
        Assert.AreEqual((freshConnect, forbidReuse), (alone.FreshConnect, alone.ForbidReuse));
        Assert.AreEqual((freshConnect, forbidReuse), (shared.FreshConnect, shared.ForbidReuse));
    }
}
