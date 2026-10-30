using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Composition;

namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class CertificateTransparencyExtensionTests
{
    [TestMethod]
    public async Task HookFor_TheOptionGiven_GivesTheHookReadingTheNamedLogList()
    {
        var directory = Directory.CreateTempSubdirectory("curl-ct-");
        try
        {
            using var chain = new CertificateTransparencyChain();
            var logList = Path.Combine(directory.FullName, "log_list.json");
            await File.WriteAllBytesAsync(logList, CertificateTransparencyChain.LogListOf(chain.LogKey.ExportSubjectPublicKeyInfo()));
            var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));
            var hook = CertificateTransparencyExtension.HookFor(Given(logList))!;

            var refusal = await hook.RefusalOfAsync([leaf, chain.IssuerDer], "example.com", 443, CancellationToken.None);

            Assert.IsInstanceOfType<CertificateTransparencyCheckHook>(hook);
            Assert.IsNull(refusal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNoneSoTransfersAreUnchanged()
    {
        Assert.IsNull(CertificateTransparencyExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => CertificateTransparencyExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(CertificateTransparencyExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(CertificateTransparencyExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.Value, CertificateTransparencyExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(CertificateCheckHooks.ActivatedBy(Given("logs.json"), ExtensionCertificateCheckHookCatalogue.Entries).Hooks.OfType<CertificateTransparencyCheckHook>().Any());
        Assert.IsEmpty(CertificateCheckHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionCertificateCheckHookCatalogue.Entries).Hooks.OfType<CertificateTransparencyCheckHook>());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("certificate-transparency", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), CertificateTransparencyExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--require-sct");
    }

    private static ExtensionOptionValues Given(string logList) => new(new Dictionary<string, string> { [CertificateTransparencyExtension.Option.Name] = logList });
}
