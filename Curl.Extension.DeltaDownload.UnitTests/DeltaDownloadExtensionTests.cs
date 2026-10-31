using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class DeltaDownloadExtensionTests
{
    [TestMethod]
    public void HookFor_TheControlFileGiven_GivesTheHookWithItAndTheSeed()
    {
        var hook = (DeltaDownloadTransferHook)DeltaDownloadExtension.HookFor(Given(("zsync", "f.zsync"), ("zsync-seed", "old.bin")))!;
        var noSeed = (DeltaDownloadTransferHook)DeltaDownloadExtension.HookFor(Given(("zsync", "f.zsync")))!;

        Assert.AreEqual(("f.zsync", "old.bin"), (hook.ControlPath, hook.SeedPath));
        Assert.IsNull(noSeed.SeedPath);
    }

    [TestMethod]
    public void HookFor_NoControlFileGiven_GivesNone()
    {
        Assert.IsNull(DeltaDownloadExtension.HookFor(ExtensionOptionValues.None));
        Assert.IsNull(DeltaDownloadExtension.HookFor(Given(("zsync-seed", "old.bin"))));
        Assert.ThrowsExactly<ArgumentNullException>(() => DeltaDownloadExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionsOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(DeltaDownloadExtension.Option, on);
        Assert.Contains(DeltaDownloadExtension.SeedOption, on);
        Assert.DoesNotContain(DeltaDownloadExtension.Option, off);
        Assert.DoesNotContain(DeltaDownloadExtension.SeedOption, off);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(("zsync", "f.zsync")), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<DeltaDownloadTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<DeltaDownloadTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("zsync", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), DeltaDownloadExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--zsync-seed");
    }

    private static ExtensionOptionValues Given(params (string Name, string Value)[] options) =>
        new(options.ToDictionary(option => option.Name, option => option.Value));
}
