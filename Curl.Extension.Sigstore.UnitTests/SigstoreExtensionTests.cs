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
    public void HookFor_NoSigstoreOptionGiven_GivesNone()
    {
        Assert.IsNull(SigstoreExtension.HookFor(ExtensionOptionValues.None));
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
    public void Composition_ActivatesTheHookOnlyWhenASigstoreOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(("sigstore-bundle", "a.sigstore.json"), ("sigstore-identity", "me"), ("sigstore-root", "r.pem")), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SigstoreTransferHook>().Any());
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(("sigstore-bundle", "a.sigstore.json"), ("sigstore-identity", "me"), ("sigstore-root", "r.pem")), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SigstoreTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SigstoreTransferHook>().Any());
    }

    [TestMethod]
    [DataRow("sigstore-identity", "me", "--sigstore-identity")]
    [DataRow("sigstore-root", "r.pem", "--sigstore-root")]
    [DataRow("sigstore-issuer", "https://issuer", "--sigstore-issuer")]
    public void HookFor_ACompanionWithoutTheBundle_IsRefusedAtActivationNamingTheBundle(string name, string value, string named)
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => SigstoreExtension.HookFor(Given((name, value))));

        Assert.AreEqual(named + " needs --sigstore-bundle", error.Message.Replace(" (Parameter 'values')", string.Empty, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("sigstore-root", "r.pem")]
    [DataRow("sigstore-identity", "me")]
    public void HookFor_BundleWithoutIdentityOrRoot_IsRefusedAtActivationWithoutParameterText(string name, string value)
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => SigstoreExtension.HookFor(Given(("sigstore-bundle", "x.json"), (name, value))));

        Assert.AreEqual("--sigstore-bundle needs --sigstore-identity and --sigstore-root", error.Message);
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
