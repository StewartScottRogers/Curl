using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.CompressionDictionary.UnitTests;

[TestClass]
public sealed class CompressionDictionaryExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<CompressionDictionaryTransferHook>(CompressionDictionaryExtension.HookFor(Given("dictionaries")));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(CompressionDictionaryExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => CompressionDictionaryExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(CompressionDictionaryExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(CompressionDictionaryExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("dictionaries"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<CompressionDictionaryTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<CompressionDictionaryTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("compression-dictionary", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), CompressionDictionaryExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--compression-dictionary");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [CompressionDictionaryExtension.Option.Name] = value });
}
