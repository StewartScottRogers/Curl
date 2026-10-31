using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.JsonRpc;

[TestClass]
public sealed class JsonRpcExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<JsonRpcTransferHook>(JsonRpcExtension.HookFor(Given("sum [1,2]")));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(JsonRpcExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => JsonRpcExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(JsonRpcExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(JsonRpcExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("sum"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<JsonRpcTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<JsonRpcTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("jsonrpc", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), JsonRpcExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--jsonrpc");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [JsonRpcExtension.Option.Name] = value });
}
