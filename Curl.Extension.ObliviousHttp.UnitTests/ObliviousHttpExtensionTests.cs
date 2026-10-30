using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Extension.ObliviousHttp.Relay;

namespace Curl.Extension.ObliviousHttp;

[TestClass]
public sealed class ObliviousHttpExtensionTests
{
    [TestMethod]
    public void HookFor_RelayGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<ObliviousHttpTransferHook>(ObliviousHttpExtension.HookFor(Given(ObliviousHttpExtension.RelayOption)));
    }

    [TestMethod]
    public void HookFor_RelayNotGiven_GivesNone()
    {
        Assert.IsNull(ObliviousHttpExtension.HookFor(ExtensionOptionValues.None));
        Assert.IsNull(ObliviousHttpExtension.HookFor(Given(ObliviousHttpExtension.KeysOption)));
        Assert.ThrowsExactly<ArgumentNullException>(() => ObliviousHttpExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionsOnlyWhileExtensionsAreOn()
    {
        IReadOnlyList<ExtensionOption> on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        IReadOnlyList<ExtensionOption> off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(ObliviousHttpExtension.RelayOption, on);
        Assert.Contains(ObliviousHttpExtension.KeysOption, on);
        Assert.DoesNotContain(ObliviousHttpExtension.RelayOption, off);
        Assert.DoesNotContain(ObliviousHttpExtension.KeysOption, off);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheRelayIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(ObliviousHttpExtension.RelayOption), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ObliviousHttpTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ObliviousHttpTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("ohttp-relay", StringComparison.Ordinal)));
        string markdown = ExtensionListing.Markdown(_ => null);
        StringAssert.Contains(markdown, ObliviousHttpExtension.Description.Summary);
        StringAssert.Contains(markdown, "--ohttp-relay");
        StringAssert.Contains(markdown, "--ohttp-keys");
    }

    private static ExtensionOptionValues Given(ExtensionOption option) => new(new Dictionary<string, string> { [option.Name] = "https://relay.example/ohttp" });
}
