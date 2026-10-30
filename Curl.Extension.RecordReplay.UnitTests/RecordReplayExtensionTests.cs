using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class RecordReplayExtensionTests
{
    [TestMethod]
    public void HookFor_EitherOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<RecordReplayTransferHook>(RecordReplayExtension.HookFor(Given(RecordReplayExtension.RecordOption)));
        Assert.IsInstanceOfType<RecordReplayTransferHook>(RecordReplayExtension.HookFor(Given(RecordReplayExtension.ReplayOption)));
    }

    [TestMethod]
    public void HookFor_NeitherOptionGiven_GivesNone()
    {
        Assert.IsNull(RecordReplayExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => RecordReplayExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersBothOptionsOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(RecordReplayExtension.RecordOption, on);
        Assert.Contains(RecordReplayExtension.ReplayOption, on);
        Assert.DoesNotContain(RecordReplayExtension.RecordOption, off);
        Assert.DoesNotContain(RecordReplayExtension.ReplayOption, off);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenAnOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(RecordReplayExtension.ReplayOption), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<RecordReplayTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<RecordReplayTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("record-replay", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), RecordReplayExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--record");
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--replay");
    }

    private static ExtensionOptionValues Given(ExtensionOption option) => new(new Dictionary<string, string> { [option.Name] = "dir" });
}
