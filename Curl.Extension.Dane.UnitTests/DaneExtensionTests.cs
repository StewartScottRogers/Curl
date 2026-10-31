using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Composition;
using Curl.Extension.Dane.Lookup;

namespace Curl.Extension.Dane;

[TestClass]
public sealed class DaneExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheDaneHook()
    {
        ICertificateCheckHook? hook = DaneExtension.HookFor(Given("192.0.2.53"));

        Assert.IsInstanceOfType<DaneCheckHook>(hook);
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNoneSoTransfersAreUnchanged()
    {
        Assert.IsNull(DaneExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => DaneExtension.HookFor(null!));
    }

    [TestMethod]
    public void LookupThrough_AResolver_BuildsALookupWithoutTouchingTheNetwork()
    {
        Assert.IsInstanceOfType<TlsaLookup>(DaneExtension.LookupThrough(new DaneResolverAddress("192.0.2.53", 53)));
        Assert.AreEqual(TimeSpan.FromSeconds(5), DaneExtension.QueryTimeout);
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(DaneExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(DaneExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.Value, DaneExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(CertificateCheckHooks.ActivatedBy(Given("192.0.2.53"), ExtensionCertificateCheckHookCatalogue.Entries).Hooks.OfType<DaneCheckHook>().Any());
        Assert.IsEmpty(CertificateCheckHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionCertificateCheckHookCatalogue.Entries).Hooks.OfType<DaneCheckHook>());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("dane", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), DaneExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--dane");
    }

    private static ExtensionOptionValues Given(string resolver) => new(new Dictionary<string, string> { [DaneExtension.Option.Name] = resolver });
}
