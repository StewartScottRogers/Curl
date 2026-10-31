using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.Sigstore.UnitTests;

[TestClass]
public sealed class SigstoreExtensionTests
{
    [TestMethod]
    public void HookFor_TheBundleGiven_GivesTheHookWithEveryValue()
    {
        var hook = (SigstoreTransferHook)SigstoreExtension.HookFor(Given(
            ("sigstore-bundle", "a.sigstore.json"), ("sigstore-identity", "me@example.com"), ("sigstore-issuer", "https://issuer"), ("sigstore-root", "fulcio.pem")))!;

        Assert.AreEqual(new SigstoreExpectation("a.sigstore.json", "me@example.com", "https://issuer", "fulcio.pem"), hook.Expectation);
    }

    [TestMethod]
    public void HookFor_NoBundleGiven_GivesNone()
    {
        Assert.IsNull(SigstoreExtension.HookFor(ExtensionOptionValues.None));
        Assert.IsNull(SigstoreExtension.HookFor(Given(("sigstore-identity", "me@example.com"))));
        Assert.ThrowsExactly<ArgumentNullException>(() => SigstoreExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionsOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        foreach (var option in new[] { SigstoreExtension.BundleOption, SigstoreExtension.IdentityOption, SigstoreExtension.IssuerOption, SigstoreExtension.RootOption })
        {
            Assert.Contains(option, on);
            Assert.DoesNotContain(option, off);
        }
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheBundleIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(("sigstore-bundle", "a.sigstore.json")), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SigstoreTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SigstoreTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("sigstore", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), SigstoreExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--sigstore-identity");
    }

    private static ExtensionOptionValues Given(params (string Name, string Value)[] options) =>
        new(options.ToDictionary(option => option.Name, option => option.Value));
}
