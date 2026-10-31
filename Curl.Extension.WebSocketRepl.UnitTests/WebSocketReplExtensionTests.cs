using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Extension.WebSocketRepl.Transfer;

namespace Curl.Extension.WebSocketRepl;

/// <summary>Pins how <c>--ws-repl</c> is registered and turns its hook on (ADR-1081).</summary>
[TestClass]
public sealed class WebSocketReplExtensionTests
{
    private static ExtensionOptionValues Given { get; } = new(new Dictionary<string, string> { ["ws-repl"] = string.Empty });

    [TestMethod]
    public void HookFor_OptionGiven_GivesTheHook() =>
        Assert.IsInstanceOfType<WebSocketReplTransferHook>(WebSocketReplExtension.HookFor(Given));

    [TestMethod]
    public void HookFor_OptionNotGiven_GivesNoHook() =>
        Assert.IsNull(WebSocketReplExtension.HookFor(ExtensionOptionValues.None));

    [TestMethod]
    public void HookFor_WithoutValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketReplExtension.HookFor(null!));

    [TestMethod]
    public void Description_NamesTheOptionAndRfc6455()
    {
        Assert.AreEqual("ws-repl", WebSocketReplExtension.Description.Name);
        Assert.AreSame(WebSocketReplExtension.Option, WebSocketReplExtension.Description.Options.Single());
    }

    // With CURL_EXTENSIONS=0 the option is not registered, so curl's parser refuses --ws-repl as an unknown option (exit 2).
    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(WebSocketReplExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(
            WebSocketReplExtension.Option,
            ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    // Without --ws-repl no hook is activated, so every transfer runs unchanged.
    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<WebSocketReplTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries)
            .Hooks.OfType<WebSocketReplTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("ws-repl", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), WebSocketReplExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--ws-repl");
    }
}
