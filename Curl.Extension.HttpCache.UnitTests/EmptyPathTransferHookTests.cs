using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class EmptyPathTransferHookTests
{
    public static IEnumerable<object[]> Options()
    {
        yield return [HttpCacheExtension.Option];
    }

    [TestMethod]
    [DynamicData(nameof(Options))]
    public async Task HookFor_AnEmptyPath_RefusesTheTransferWithExit2NamingTheOption(ExtensionOption option)
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { [option.Name] = string.Empty });

        var hook = HttpCacheExtension.HookFor(values)!;
        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("The server was contacted."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("--" + option.Name + " expects a non-empty path", result.ErrorMessage);
    }
}
