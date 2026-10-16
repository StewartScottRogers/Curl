using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp;

[TestClass]
public sealed class CurlCommandToolTests
{
    [TestMethod]
    public async Task RunCurlAsync_ExitZero_ReturnsTheResultAsJsonTextThatIsNotAnError()
    {
        StubRunner runner = new(0, "hello");
        CurlCommandTool tool = new(new McpCurlTool(runner, McpConfinementRoot.NotConfigured, new McpResponseCap(McpResponseCap.DefaultMaxBytes)));

        CallToolResult result = await tool.RunCurlAsync(["-s", "http://example.invalid/"], "input");

        Assert.IsFalse(result.IsError);
        CollectionAssert.AreEqual(new[] { "-s", "http://example.invalid/" }, runner.Arguments!.ToArray());
        Assert.AreEqual("input", runner.StandardInput);
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual(0, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.AreEqual("hello", json.RootElement.GetProperty("standardOutput").GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task RunCurlAsync_NonzeroExit_IsAToolErrorNamingTheFailureKind()
    {
        CurlCommandTool tool = new(new McpCurlTool(new StubRunner(6, string.Empty), McpConfinementRoot.NotConfigured, new McpResponseCap(McpResponseCap.DefaultMaxBytes)));

        CallToolResult result = await tool.RunCurlAsync(["http://example.invalid/"]);

        Assert.IsTrue(result.IsError);
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual(6, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.AreEqual(JsonValueKind.String, json.RootElement.GetProperty("failure").GetProperty("kind").ValueKind);
    }

    private sealed class StubRunner(int exitCode, string output) : ICurlCommandRunner
    {
        public IReadOnlyList<string>? Arguments { get; private set; }

        public string? StandardInput { get; private set; }

        public async Task<int> RunAsync(IReadOnlyList<string> arguments, Stream standardInput, Stream standardOutput, Stream standardError, string? workingDirectory, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            using StreamReader reader = new(standardInput, Encoding.UTF8, leaveOpen: true);
            StandardInput = await reader.ReadToEndAsync(cancellationToken);
            await standardOutput.WriteAsync(Encoding.UTF8.GetBytes(output), cancellationToken);
            return exitCode;
        }
    }
}
