using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Composition;
using Curl.Extension.Masque.Tunnel;
using Curl.Extension.Masque.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque;

/// <summary>Checks the <c>--masque</c> option's activation and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class MasqueExtensionTests
{
    [TestMethod]
    public void Activate_WithoutTheOption_GivesNoHook() =>
        Assert.IsNull(MasqueExtension.Activate(ExtensionOptionValues.None));

    [TestMethod]
    public void Activate_TemplateNamingTargetHostAndPort_GivesAConnectUdpHook()
    {
        var hook = (MasqueConnectionHook)MasqueExtension.Activate(ValuesOf(MasqueProxyBytes.UdpTemplate))!;

        Assert.AreSame(MasqueExtension.Option, hook.Option);
        Assert.AreEqual(MasqueTunnelKind.ConnectUdp, hook.Kind);
        Assert.AreEqual(MasqueProxyBytes.UdpTemplate, hook.UriTemplate);
    }

    [TestMethod]
    public void Activate_TemplateNamingTargetAndIpproto_GivesAConnectIpHook() =>
        Assert.AreEqual(MasqueTunnelKind.ConnectIp, ((MasqueConnectionHook)MasqueExtension.Activate(ValuesOf(MasqueProxyBytes.IpTemplate))!).Kind);

    [TestMethod]
    [DataRow("https://proxy.example/{target_port}", (int)MasqueTunnelKind.ConnectUdp)]
    [DataRow("https://proxy.example/{target_host}", (int)MasqueTunnelKind.ConnectUdp)]
    [DataRow("https://proxy.example/{target}", (int)MasqueTunnelKind.ConnectIp)]
    [DataRow("https://proxy.example/{ipproto}", (int)MasqueTunnelKind.ConnectIp)]
    public void KindOf_TemplateVariables_PickTheTunnel(string template, int expected) =>
        Assert.AreEqual((MasqueTunnelKind)expected, MasqueExtension.KindOf(template));

    [TestMethod]
    [DataRow("https://proxy.example/{target_host}")]
    [DataRow("https://proxy.example/fixed")]
    [DataRow("ftp://proxy.example/{target}")]
    public void Activate_TextThatIsNotAMasqueTemplate_IsTheCommandLineError(string template)
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => MasqueExtension.Activate(ValuesOf(template)));

        Assert.StartsWith("--masque needs a CONNECT-UDP or CONNECT-IP URI template: ", error.Message);
    }

    [TestMethod]
    public void Activate_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => MasqueExtension.Activate(null!));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveMasqueOut_SoTheParserRefusesItAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == "masque"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldMasqueWithAValue()
    {
        ExtensionOption option = ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == "masque");

        Assert.AreSame(MasqueExtension.Option, option);
        Assert.AreEqual(ExtensionOptionArgument.Value, option.Argument);
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithoutTheOption_KeepsCurlsConnectorAndDatagramConnector()
    {
        var curlConnector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "unused"));
        var curlDatagramConnector = new LateBoundConnectorTests.UnusedDatagramConnector();

        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionConnectionHookCatalogue.Entries);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curlConnector, hooks.ConnectorOver(curlConnector));
        Assert.AreSame(curlDatagramConnector, hooks.DatagramConnectorOver(curlDatagramConnector));
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithTheOption_ActivatesTheMasqueHook() =>
        Assert.IsInstanceOfType<MasqueConnectionHook>(ConnectionHooks.ActivatedBy(ValuesOf(MasqueProxyBytes.UdpTemplate), ExtensionConnectionHookCatalogue.Entries).Hook);

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOption()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("masque", StringComparison.Ordinal)));
        Assert.Contains("--masque", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://www.rfc-editor.org/rfc/rfc9298"), MasqueExtension.Description.Specification);
    }

    private static ExtensionOptionValues ValuesOf(string template) => new(new Dictionary<string, string> { ["masque"] = template });
}
