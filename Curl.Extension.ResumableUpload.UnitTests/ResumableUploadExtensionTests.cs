using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Extension.ResumableUpload.Transfer;

namespace Curl.Extension.ResumableUpload.UnitTests;

[TestClass]
public sealed class ResumableUploadExtensionTests
{
    private static readonly ExtensionOptionValues Given = new(new Dictionary<string, string> { [ResumableUploadExtension.Option.Name] = string.Empty });

    [TestMethod]
    public void HookFor_TheFlagGiven_GivesTheResumableUploadHook()
    {
        var hook = ResumableUploadExtension.HookFor(Given);

        Assert.AreEqual(ResumableUploadTransferHook.DefaultMaxResumptions, ((ResumableUploadTransferHook)hook!).MaxResumptions);
        Assert.AreEqual(ExtensionOptionArgument.None, ResumableUploadExtension.Option.Argument);
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(ResumableUploadExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ResumableUploadExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(ResumableUploadExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(ResumableUploadExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven_SoEveryOtherTransferIsUnchanged()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ResumableUploadTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ResumableUploadTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("resumable-upload", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), ResumableUploadExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--resumable-upload");
    }
}
