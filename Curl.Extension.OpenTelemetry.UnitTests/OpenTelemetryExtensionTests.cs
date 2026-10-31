using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.OpenTelemetry.UnitTests;

[TestClass]
public sealed class OpenTelemetryExtensionTests
{
    [TestMethod]
    public void HookFor_TheOptionGiven_GivesTheHook()
    {
        Assert.IsInstanceOfType<OpenTelemetryTransferHook>(OpenTelemetryExtension.HookFor(Given()));
    }

    [TestMethod]
    public void HookFor_TheOptionNotGiven_GivesNoneSoTransfersAreUnchanged()
    {
        Assert.IsNull(OpenTelemetryExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => OpenTelemetryExtension.HookFor(null!));
    }

    [TestMethod]
    public void Composition_RegistersTheOptionOnlyWhileExtensionsAreOn()
    {
        Assert.Contains(OpenTelemetryExtension.Option, ExtensionCommandLineOptions.For(_ => null, _ => false));
        Assert.DoesNotContain(OpenTelemetryExtension.Option, ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false));
        Assert.AreEqual(ExtensionOptionArgument.Value, OpenTelemetryExtension.Option.Argument);
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven()
    {
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<OpenTelemetryTransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<OpenTelemetryTransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("otel", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), OpenTelemetryExtension.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--otel-endpoint");
    }

    private static ExtensionOptionValues Given() =>
        new(new Dictionary<string, string> { [OpenTelemetryExtension.Option.Name] = "http://localhost:4318/v1/traces" });
}
