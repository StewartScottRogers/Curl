using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.RateLimit.UnitTests;

[TestClass]
public sealed class RateLimitExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<RateLimitTransferHook>(RateLimitExtension.HookFor(Given()));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(RateLimitExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => RateLimitExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(RateLimitExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(RateLimitExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.None, RateLimitExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<RateLimitTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<RateLimitTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("ratelimit-pace", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), RateLimitExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--ratelimit-pace");
    }

    private static ExtensionOptionValues Given() => new(new Dictionary<string, string> { [RateLimitExtension.Option.Name] = string.Empty });
}
