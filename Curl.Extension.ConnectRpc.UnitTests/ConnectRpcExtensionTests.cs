using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.ConnectRpc;

[TestClass]
public sealed class ConnectRpcExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<ConnectTransferHook>(ConnectRpcExtension.HookFor(Given("unary")));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(ConnectRpcExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConnectRpcExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(ConnectRpcExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(ConnectRpcExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("stream"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ConnectTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ConnectTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("connect-rpc", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), ConnectRpcExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--connect-rpc");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [ConnectRpcExtension.Option.Name] = value });
}
