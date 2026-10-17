using Curl.Engine;

namespace Curl.Console;

/// <summary>Pins that disposing a <see cref="ToolEasyHandle" /> is <c>curl_easy_cleanup</c> of its handle.</summary>
[TestClass]
public sealed class ToolEasyHandleTests
{
    [TestMethod]
    public async Task DisposeAsync_CleansUpTheHandle()
    {
        ToolEasyHandle easy = new ToolCurlEngine(new ManagedCurlEngine()).EasyInit();

        await easy.DisposeAsync();

        Assert.IsTrue(easy.Easy.IsCleanedUp);
    }
}
