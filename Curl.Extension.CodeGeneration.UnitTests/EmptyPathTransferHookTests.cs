using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.CodeGeneration.UnitTests;

[TestClass]
public sealed class EmptyPathTransferHookTests
{
    public static IEnumerable<object[]> Options()
    {
        yield return [CodeGenerationExtension.CSharpOption];
        yield return [CodeGenerationExtension.PowerShellOption];
    }

    [TestMethod]
    [DynamicData(nameof(Options))]
    public async Task HookFor_AnEmptyPath_RefusesTheTransferWithExit2NamingTheOption(ExtensionOption option)
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { [option.Name] = string.Empty });

        var hook = CodeGenerationExtension.HookFor(values)!;
        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("--" + option.Name + " expects a non-empty path", result.ErrorMessage);
    }

    [TestMethod]
    [DynamicData(nameof(Options))]
    public async Task HookFor_AStandardOutputDash_RefusesTheTransferWithExit2NamingTheOption(ExtensionOption option)
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { [option.Name] = "-" });

        var hook = CodeGenerationExtension.HookFor(values)!;
        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("--" + option.Name + " expects a file path; - (standard output) is not accepted", result.ErrorMessage);
    }
}
