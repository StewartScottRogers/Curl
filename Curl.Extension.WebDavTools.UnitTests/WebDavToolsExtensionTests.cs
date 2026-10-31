using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Extension.WebDavTools.Transfer;

namespace Curl.Extension.WebDavTools.UnitTests;

[TestClass]
public sealed class WebDavToolsExtensionTests
{
    [TestMethod]
    public void HookFor_WebDavList_GivesTheListingHook()
    {
        Assert.IsInstanceOfType<WebDavListTransferHook>(WebDavToolsExtension.HookFor(Given(list: true)));
    }

    [TestMethod]
    public void HookFor_WebDavSync_GivesTheSyncHookIntoThatFolder()
    {
        var hook = WebDavToolsExtension.HookFor(Given(sync: "mirror"));

        Assert.AreEqual("mirror", ((WebDavSyncTransferHook)hook!).LocalFolder);
    }

    [TestMethod]
    [DataRow(true, "mirror", WebDavToolsExtension.BothOptionsMessage)]
    [DataRow(true, "", WebDavToolsExtension.BothOptionsMessage)]
    [DataRow(false, "", WebDavToolsExtension.EmptyFolderMessage)]
    public async Task HookFor_OptionsThatCannotBeUsed_GivesAHookThatRefusesTheTransferWithExit2(bool list, string sync, string message)
    {
        var hook = WebDavToolsExtension.HookFor(Given(list, sync))!;

        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(message, ((RefusedWebDavTransferHook)hook).Message);
    }

    [TestMethod]
    public void HookFor_NeitherOptionGiven_GivesNone()
    {
        Assert.IsNull(WebDavToolsExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => WebDavToolsExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionsOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(WebDavToolsExtension.ListOption, on);
        Assert.Contains(WebDavToolsExtension.SyncOption, on);
        Assert.DoesNotContain(WebDavToolsExtension.ListOption, off);
        Assert.DoesNotContain(WebDavToolsExtension.SyncOption, off);
    }

    [TestMethod]
    public void Composition_ActivatesAHookOnlyWhenAnOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(list: true), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<WebDavListTransferHook>().Any());
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(sync: "mirror"), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<WebDavSyncTransferHook>().Any());
        var hooks = TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks;
        Assert.IsFalse(hooks.Any(hook => hook is WebDavListTransferHook or WebDavSyncTransferHook or RefusedWebDavTransferHook));
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("webdav-tools", StringComparison.Ordinal)));
        var markdown = ExtensionListing.Markdown(_ => null);
        StringAssert.Contains(markdown, WebDavToolsExtension.Description.Summary);
        StringAssert.Contains(markdown, "--webdav-list");
        StringAssert.Contains(markdown, "--webdav-sync");
    }

    private static ExtensionOptionValues Given(bool list = false, string? sync = null)
    {
        var values = new Dictionary<string, string>();
        if (list)
        {
            values[WebDavToolsExtension.ListOption.Name] = "";
        }

        if (sync is not null)
        {
            values[WebDavToolsExtension.SyncOption.Name] = sync;
        }

        return new ExtensionOptionValues(values);
    }
}
