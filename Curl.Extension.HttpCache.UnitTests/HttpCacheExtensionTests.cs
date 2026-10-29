using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Extension.HttpCache;

namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class HttpCacheExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<HttpCacheTransferHook>(HttpCacheExtension.HookFor(Given("cache")));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(HttpCacheExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => HttpCacheExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(HttpCacheExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(HttpCacheExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("cache"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HttpCacheTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HttpCacheTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("cache-dir", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), HttpCacheExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--cache-dir");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [HttpCacheExtension.Option.Name] = value });
}
