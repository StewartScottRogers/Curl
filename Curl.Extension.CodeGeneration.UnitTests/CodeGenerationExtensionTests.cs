using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.CodeGeneration.UnitTests;

[TestClass]
public sealed class CodeGenerationExtensionTests
{
    [TestMethod]
    public void HookFor_BothOptionsGiven_GivesOneHookWritingBothFiles()
    {
        var hook = CodeGenerationExtension.HookFor(Given(("csharp", "a.cs"), ("powershell", "a.ps1")));

        var files = Assert.IsInstanceOfType<CodeGenerationTransferHook>(hook).Files;
        Assert.AreEqual("a.cs", files[0].Path);
        Assert.AreEqual("a.ps1", files[1].Path);
    }

    [TestMethod]
    public void HookFor_OneOptionGiven_WritesOnlyItsFile()
    {
        var csharp = (CodeGenerationTransferHook)CodeGenerationExtension.HookFor(Given(("csharp", "a.cs")))!;
        var powershell = (CodeGenerationTransferHook)CodeGenerationExtension.HookFor(Given(("powershell", "a.ps1")))!;

        StringAssert.StartsWith(csharp.Files.Single().Writer([Request()]), "// Written by curl --csharp");
        StringAssert.StartsWith(powershell.Files.Single().Writer([Request()]), "# Written by curl --powershell");
    }

    // Without --csharp or --powershell no hook is activated, so every transfer runs unchanged.
    [TestMethod]
    public void HookFor_NeitherOptionGiven_GivesNoneSoTransfersAreUnchanged()
    {
        Assert.IsNull(CodeGenerationExtension.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => CodeGenerationExtension.HookFor(null!));
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<CodeGenerationTransferHook>().Any());
        Assert.IsTrue(TransferHooks.ActivatedBy(Given(("csharp", "a.cs")), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<CodeGenerationTransferHook>().Any());
    }

    // With CURL_EXTENSIONS=0 the options are not registered, so curl's parser refuses them as unknown options (exit 2).
    [TestMethod]
    public void Composition_RegistersBothOptionsOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        Assert.Contains(CodeGenerationExtension.CSharpOption, on);
        Assert.Contains(CodeGenerationExtension.PowerShellOption, on);
        Assert.DoesNotContain(CodeGenerationExtension.CSharpOption, off);
        Assert.DoesNotContain(CodeGenerationExtension.PowerShellOption, off);
        Assert.AreEqual(ExtensionOptionArgument.Value, CodeGenerationExtension.CSharpOption.Argument);
        Assert.AreEqual(ExtensionOptionArgument.Value, CodeGenerationExtension.PowerShellOption.Argument);
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("code-generation", StringComparison.Ordinal)));
        var markdown = ExtensionListing.Markdown(_ => null);
        StringAssert.Contains(markdown, CodeGenerationExtension.Description.Summary);
        StringAssert.Contains(markdown, "--csharp");
        StringAssert.Contains(markdown, "--powershell");
    }

    internal static GeneratedRequest Request(string method = "GET", byte[]? body = null, params KeyValuePair<string, string>[] headers) =>
        new(method, "https://example.com/", headers, body, false, 50, false);

    private static ExtensionOptionValues Given(params (string Name, string Value)[] options) =>
        new(options.ToDictionary(option => option.Name, option => option.Value));
}
