using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.Checksum.UnitTests;

[TestClass]
public sealed class ChecksumExtensionTests
{
    [TestMethod]
    public void HookFor_AnAlgorithmAndDigest_GivesTheHookExpectingThatDigest()
    {
        var hook = ChecksumExtension.HookFor(Given("sha1:a9993e364706816aba3e25717850c26c9cd0d89d"));

        Assert.AreEqual("sha1", ((ChecksumTransferHook)hook!).Expected.AlgorithmName);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("65")]
    [DataRow("-2")]
    [DataRow("two")]
    [DataRow("")]
    public async Task HookFor_AnythingElse_GivesAHookThatRefusesTheTransferWithExit2(string value)
    {
        var hook = ChecksumExtension.HookFor(Given(value))!;

        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual(InvalidChecksumTransferHook.Message, result.ErrorMessage);
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(ChecksumExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ChecksumExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(ChecksumExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(ChecksumExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given("sha1:a9993e364706816aba3e25717850c26c9cd0d89d"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ChecksumTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ChecksumTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("checksum", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), ChecksumExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--checksum");
    }

    private static ExtensionOptionValues Given(string value) => new(new Dictionary<string, string> { [ChecksumExtension.Option.Name] = value });
}
