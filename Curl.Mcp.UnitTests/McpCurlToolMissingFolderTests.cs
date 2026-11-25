using System.Text.Json;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class McpCurlToolMissingFolderTests
{
    [TestMethod]
    [DataRow("--no-such-option-bl5761")]
    [DataRow("http://")]
    public async Task CallAsync_UnknownOptionOrBadUrl_GivesAResultWithAnExitCode(string argument)
    {
        CurlCommandMcpTool tool = new(new McpCurlTool(new InProcessCurlCommandRunner(), McpConfinementRoot.NotConfigured, new McpResponseCap(McpResponseCap.DefaultMaxBytes)));
        JsonElement arguments = JsonSerializer.SerializeToElement(new Dictionary<string, string[]> { ["args"] = [argument] });

        Curl.Mcp.Protocol.Model.McpToolCallResult result = await tool.CallAsync(arguments, CancellationToken.None);

        string text = JsonSerializer.Serialize(result);
        Assert.IsFalse(text.Contains("An error occurred invoking", StringComparison.Ordinal), text);
        StringAssert.Contains(text, "exitCode");
    }
}
