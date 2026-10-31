using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.ClientHints.UnitTests;

[TestClass]
public sealed class ClientHintsExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHookForThisPlatform()
    {
        var hook = ClientHintsExtension.HookFor(Given());

        Assert.IsInstanceOfType<ClientHintsTransferHook>(hook);
        Assert.HasCount(3, hook.RequestHeadersFor(new TransferContext { Url = CurlUrl.Parse("https://example.com/"), Output = Stream.Null }).ToArray());
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNoneSoTransfersAreUnchanged()
    {
        Assert.IsNull(ClientHintsExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ClientHintsExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(ClientHintsExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(ClientHintsExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.None, ClientHintsExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ClientHintsTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ClientHintsTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("client-hints", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), ClientHintsExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--client-hints");
    }

    private static ExtensionOptionValues Given() => new(new Dictionary<string, string> { [ClientHintsExtension.Option.Name] = string.Empty });
}
