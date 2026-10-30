using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.SegmentedDownload.UnitTests;

[TestClass]
public sealed class SegmentedDownloadExtensionTests
{
    [TestMethod]
    [DataRow("1", 1)]
    [DataRow("4", 4)]
    [DataRow("64", 64)]
    public void HookFor_AWholeNumberFrom1To64_GivesTheHookSplittingIntoThatMany(string value, int count)
    {
        var hook = SegmentedDownloadExtension.HookFor(Given(value));

        Assert.AreEqual(count, ((SegmentedDownloadTransferHook)hook!).Count);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("65")]
    [DataRow("-2")]
    [DataRow("two")]
    [DataRow("")]
    public async Task HookFor_AnythingElse_GivesAHookThatRefusesTheTransferWithExit2(string value)
    {
        var hook = SegmentedDownloadExtension.HookFor(Given(value))!;

        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual(InvalidSegmentCountTransferHook.Message, result.ErrorMessage);
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(SegmentedDownloadExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => SegmentedDownloadExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(SegmentedDownloadExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(SegmentedDownloadExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("4"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SegmentedDownloadTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<SegmentedDownloadTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("segments", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), SegmentedDownloadExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--segments");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [SegmentedDownloadExtension.Option.Name] = value });
}
