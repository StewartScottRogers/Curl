using System.Text;
using System.Text.Json;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using Curl.Conformance.Extended.McpClient.Stdio;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.McpClient.McpClientRun;

namespace Curl.Conformance.Extended.UnitTests.McpClient.Stdio;

/// <summary>MCP 2025-06-18 "Transports &gt; stdio" and "Lifecycle &gt; Shutdown" cases against an in-memory stand-in server process.</summary>
[TestClass]
public sealed class McpStdioCases
{
    private const string StdioUrl = "mcp+stdio://localhost/reference-server?--stdio&--name%20x";

    private const string CallEcho = """{"name":"echo","arguments":{"text":"a\nb"}}""";

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Stdio-1")]
    [SpecRequirement("McpClient:MCP20250618-Stdio-2")]
    public async Task List_StartsTheProgramWithItsArgumentsAndWritesEachTool()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour());

        CurlRunResult result = await RunStdioAsync(starter, StdioUrl);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
        McpStdioReferenceProcess server = starter.Started.Single();
        Assert.AreEqual("reference-server", server.Program);
        CollectionAssert.AreEqual(new[] { "--stdio", "--name x" }, server.Arguments.ToArray());
        CollectionAssert.AreEqual(new[] { "initialize", "notifications/initialized", "tools/list" }, Methods(server));
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Stdio-1")]
    public async Task Call_WritesTheToolsContent()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour());

        CurlRunResult result = await RunStdioAsync(starter, "-d", CallEcho, StdioUrl);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("hello\n", Text(result.StandardOutput));
        CollectionAssert.AreEqual(new[] { "initialize", "notifications/initialized", "tools/call" }, Methods(starter.Started.Single()));
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Stdio-3")]
    [SpecRequirement("McpClient:MCP20250618-Stdio-4")]
    public async Task EveryMessage_IsOneJsonRpcLineWithNoEmbeddedNewline()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour());

        CurlRunResult result = await RunStdioAsync(starter, "-d", CallEcho, StdioUrl);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        string written = Text(starter.Started.Single().StandardInputBytes);
        Assert.IsTrue(written.EndsWith('\n'), written);
        Assert.DoesNotContain("\r", written);
        string[] lines = written[..^1].Split('\n');
        Assert.HasCount(3, lines);
        foreach (string line in lines)
        {
            using JsonDocument message = JsonDocument.Parse(line);
            Assert.AreEqual("2.0", message.RootElement.GetProperty("jsonrpc").GetString());
        }

        using JsonDocument call = JsonDocument.Parse(lines[2]);
        Assert.AreEqual("a\nb", call.RootElement.GetProperty("params").GetProperty("arguments").GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task ServerLogsToItsStandardError_CurlsOutputIsUnchanged()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour());

        CurlRunResult result = await RunStdioAsync(starter, StdioUrl);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
        CollectionAssert.AreEqual(
            new[] { "reference-mcp-server: received message 1", "reference-mcp-server: received message 2", "reference-mcp-server: received message 3" },
            starter.Started.Single().StandardErrorLines.ToArray());
    }

    [TestMethod]
    public async Task ServerExitsBeforeAnsweringTheList_CurlExits8()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour { ExitBeforeAnsweringMessage = 3 });

        CurlRunResult result = await RunStdioAsync(starter, StdioUrl);

        Assert.AreEqual(8, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        StringAssert.StartsWith(Text(result.StandardError), "curl: (8) ");
        StringAssert.Contains(Text(result.StandardError), "exited before it answered");
        McpStdioReferenceProcess server = starter.Started.Single();
        Assert.AreEqual(McpStdioReferenceProcess.ExitedEarlyCode, await server.WaitForExitAsync(CancellationToken.None));
        Assert.IsFalse(server.Killed);
    }

    [TestMethod]
    public async Task ServerWritesANonJsonLine_CurlExits8()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour { LineBeforeEachAnswer = "not json\n" });

        CurlRunResult result = await RunStdioAsync(starter, StdioUrl);

        Assert.AreEqual(8, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        StringAssert.StartsWith(Text(result.StandardError), "curl: (8) ");
        StringAssert.Contains(Text(result.StandardError), "not valid JSON");
        Assert.IsTrue(starter.Started.Single().StandardInputClosed);
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Shutdown-1")]
    public async Task TransferEnds_CurlClosesTheServersStandardInputAndWaitsForItToExit()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour());

        CurlRunResult result = await RunStdioAsync(starter, StdioUrl);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        McpStdioReferenceProcess server = starter.Started.Single();
        Assert.IsTrue(server.StandardInputClosed);
        Assert.IsFalse(server.Killed);
        Assert.AreEqual(0, await server.WaitForExitAsync(CancellationToken.None));
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Shutdown-2")]
    public async Task ServerKeepsRunningAfterItsInputCloses_CurlEndsItAfterTheGracePeriod()
    {
        McpStdioReferenceProcessStarter starter = Starter(new McpStdioServerBehaviour { IgnoresClosedStandardInput = true });

        CurlRunResult result = await RunStdioAsync(starter, StdioUrl);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\n", Text(result.StandardOutput));
        McpStdioReferenceProcess server = starter.Started.Single();
        Assert.IsTrue(server.StandardInputClosed);
        Assert.IsTrue(server.Killed);
    }

    private static McpStdioReferenceProcessStarter Starter(McpStdioServerBehaviour behaviour) =>
        new(new McpReferenceToolServer([Echo]), behaviour);

    private static Task<CurlRunResult> RunStdioAsync(McpStdioReferenceProcessStarter starter, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [], ["-sS", "-m", "30", .. arguments], starter);

    private static string[] Methods(McpStdioReferenceProcess server) =>
        [.. Encoding.UTF8.GetString(server.StandardInputBytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.GetProperty("method").GetString() ?? string.Empty)];
}
