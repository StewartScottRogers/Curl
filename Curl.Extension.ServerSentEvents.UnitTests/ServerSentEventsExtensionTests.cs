using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.ServerSentEvents;

[TestClass]
public sealed class ServerSentEventsExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<ServerSentEventsTransferHook>(ServerSentEventsExtension.HookFor(Given()));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNone()
    {
        Assert.IsNull(ServerSentEventsExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ServerSentEventsExtension.HookFor(null!));
    }

    [TestMethod]
    public void Option_IsAFlag()
    {
        Assert.AreEqual("sse", ServerSentEventsExtension.Option.Name);
        Assert.AreEqual(ExtensionOptionArgument.None, ServerSentEventsExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(ServerSentEventsExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(ServerSentEventsExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ServerSentEventsTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<ServerSentEventsTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("sse", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), ServerSentEventsExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--sse");
    }

    private static ExtensionOptionValues Given() => new(new Dictionary<string, string> { [ServerSentEventsExtension.Option.Name] = string.Empty });
}
