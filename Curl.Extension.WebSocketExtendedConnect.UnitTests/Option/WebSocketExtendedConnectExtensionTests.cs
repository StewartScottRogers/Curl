using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

[TestClass]
public sealed class WebSocketExtendedConnectExtensionTests
{
    [TestMethod]
    public void HookFor_GivesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsInstanceOfType<ExtendedConnectTransferHook>(WebSocketExtendedConnectExtension.HookFor(Given("h2")));
        Assert.IsNull(WebSocketExtendedConnectExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketExtendedConnectExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        // With CURL_EXTENSIONS=0 the option is not registered, so the command line refuses it as curl refuses an unknown option (exit 2).
        Assert.Contains(WebSocketExtendedConnectExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(WebSocketExtendedConnectExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyForTheOption()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("h3"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ExtendedConnectTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ExtendedConnectTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("ws-extended-connect", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), WebSocketExtendedConnectExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--ws-extended-connect");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [WebSocketExtendedConnectExtension.Option.Name] = value });
}
