using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.ContentDigest.UnitTests;

[TestClass]
public sealed class ContentDigestExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<ContentDigestTransferHook>(ContentDigestExtension.HookFor(Given()));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(ContentDigestExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentDigestExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(ContentDigestExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(ContentDigestExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.None, ContentDigestExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ContentDigestTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ContentDigestTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("content-digest", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), ContentDigestExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--content-digest");
    }

    private static ExtensionOptionValues Given() => new(new Dictionary<string, string> { [ContentDigestExtension.Option.Name] = string.Empty });
}
