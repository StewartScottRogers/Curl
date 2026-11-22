using System.Text.Json;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class McpCurlToolMissingFolderTests
{
    [TestMethod]
    [DataRow("nosuch/msg#1.eml", "pop3://127.0.0.1:1/[1-3]")]
    [DataRow("nosuch/out.bin", "http://127.0.0.1:1/")]
    public async Task CallAsync_OutputIntoAMissingFolder_GivesAResultWithAnExitCodeNotTheGenericError(string output, string url)
    {
        string root = Directory.CreateTempSubdirectory("mcp-missing-folder-").FullName;
        string previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            CurlCommandMcpTool tool = new(new McpCurlTool(new InProcessCurlCommandRunner(), new McpConfinementRoot(root), new McpResponseCap(McpResponseCap.DefaultMaxBytes)));
            JsonElement arguments = JsonSerializer.SerializeToElement(new Dictionary<string, string[]> { ["args"] = ["-sS", "-u", "bob:pw", "-o", output, url] });

            Curl.Mcp.Protocol.Model.McpToolCallResult result = await tool.CallAsync(arguments, CancellationToken.None);

            string text = JsonSerializer.Serialize(result);
            Assert.IsFalse(text.Contains("An error occurred invoking", StringComparison.Ordinal), text);
            StringAssert.Contains(text, "exitCode");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(root, recursive: true);
        }
    }

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
