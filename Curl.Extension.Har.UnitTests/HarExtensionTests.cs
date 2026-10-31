using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.Har.UnitTests;

[TestClass]
public sealed class HarExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<HarTransferHook>(HarExtension.HookFor(Given()));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNoneSoEveryTransferIsUnchanged()
    {
        Assert.IsNull(HarExtension.HookFor(ExtensionOptionValues.None));
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HarTransferHook>().Any());
        Assert.ThrowsExactly<ArgumentNullException>(() => HarExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn_SoCurlExtensions0RefusesItAsUnknown()
    {
        Assert.Contains(HarExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(HarExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.Value, HarExtension.Option.Argument);
        Assert.AreEqual("har", HarExtension.Option.Name);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HarTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HarTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("har", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), HarExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--har");
    }

    private static ExtensionOptionValues Given() => new(new Dictionary<string, string> { [HarExtension.Option.Name] = "out.har" });
}
