using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.Dpop.UnitTests;

[TestClass]
public sealed class DpopExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<DpopTransferHook>(DpopExtension.HookFor(new(new Dictionary<string, string> { [DpopExtension.Option.Name] = "key.pem" })));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(DpopExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => DpopExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(DpopExtension.Option, on);
        Assert.DoesNotContain(DpopExtension.Option, off);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        var given = new ExtensionOptionValues(new Dictionary<string, string> { [DpopExtension.Option.Name] = "key.pem" });

        Assert.IsTrue(TransferHooks.ActivatedBy(given, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<DpopTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<DpopTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("dpop", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), DpopExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--dpop");
    }
}
