using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.OsCredentialStore.UnitTests;

[TestClass]
public sealed class OsCredentialStoreExtensionTests
{
    [TestMethod]
    public void HookFor_OptionGiven_GivesTheHook() =>
        Assert.IsInstanceOfType<OsCredentialStoreTransferHook>(OsCredentialStoreExtension.HookFor(Given()));

    [TestMethod]
    public void HookFor_OptionNotGiven_GivesNoneSoTheTransferIsUnchanged()
    {
        Assert.IsNull(OsCredentialStoreExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => OsCredentialStoreExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(OsCredentialStoreExtension.UserFromKeychainOption, on);
        Assert.DoesNotContain(OsCredentialStoreExtension.UserFromKeychainOption, off);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<OsCredentialStoreTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<OsCredentialStoreTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("os-credential-store", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), OsCredentialStoreExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--user-from-keychain");
    }

    private static ExtensionOptionValues Given() =>
        new(new Dictionary<string, string> { [OsCredentialStoreExtension.UserFromKeychainOption.Name] = "example.com" });
}
