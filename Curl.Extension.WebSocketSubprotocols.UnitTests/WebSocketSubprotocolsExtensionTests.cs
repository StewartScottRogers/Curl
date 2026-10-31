using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class WebSocketSubprotocolsExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<WebSocketSubprotocolTransferHook>(WebSocketSubprotocolsExtension.HookFor(Given("v12.stomp /topic/a")));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(WebSocketSubprotocolsExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketSubprotocolsExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(WebSocketSubprotocolsExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(WebSocketSubprotocolsExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("wamp.2.json realm1"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<WebSocketSubprotocolTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<WebSocketSubprotocolTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("ws-subprotocol", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), WebSocketSubprotocolsExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--ws-subprotocol");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [WebSocketSubprotocolsExtension.Option.Name] = value });
}
