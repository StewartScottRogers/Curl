using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Composition;

namespace Curl.Extension.MlDsaCertificates.UnitTests;

[TestClass]
public sealed class MlDsaCertificatesExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<MlDsaCertificateCheckHook>(MlDsaCertificatesExtension.HookFor(Given()));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(MlDsaCertificatesExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => MlDsaCertificatesExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(MlDsaCertificatesExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(MlDsaCertificatesExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.None, MlDsaCertificatesExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(CertificateCheckHooks.ActivatedBy(Given(), ExtensionCertificateCheckHookCatalogue.Entries).Hooks.OfType<MlDsaCertificateCheckHook>().Any());
        Assert.IsFalse(CertificateCheckHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionCertificateCheckHookCatalogue.Entries).Hooks.OfType<MlDsaCertificateCheckHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("mldsa-verify", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), MlDsaCertificatesExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--mldsa-verify");
    }

    private static ExtensionOptionValues Given() => new(new Dictionary<string, string> { [MlDsaCertificatesExtension.Option.Name] = string.Empty });
}
