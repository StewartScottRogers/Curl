using System.Text;
using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

[TestClass]
public sealed class CurlCommandMcpToolTests
{
    private const string GoldenSchema = """
        {"type":"object","properties":{"args":{"description":"curl's command-line arguments, without the program name.","type":"array","items":{"type":"string"}},
        "stdin":{"description":"The text curl reads as standard input, as UTF-8; omit for none.","type":["string","null"],"default":null}},"required":["args"]}
        """;

    private static CurlCommandMcpTool NewTool(StubRunner runner) =>
        new(new McpCurlTool(runner, McpConfinementRoot.NotConfigured, new McpResponseCap(McpResponseCap.DefaultMaxBytes)));

    private static JsonElement Arguments(string json) => JsonDocument.Parse(json).RootElement;

    [TestMethod]
    public void Identity_IsTheGoldenNameDescriptionAndAnnotations()
    {
        CurlCommandMcpTool tool = NewTool(new StubRunner(0, string.Empty));

        Assert.AreEqual("curl", tool.Name);
        StringAssert.StartsWith(tool.Description, "Runs curl in-process, exactly as the curl command line would.");
        StringAssert.EndsWith(tool.Description, "Returns curl's standard output, standard error and exit code; a nonzero exit is a tool error.");
        Assert.IsNull(tool.Annotations);
    }

    [TestMethod]
    public void InputSchemaJson_EqualsTheGoldenToolsListSchema()
    {
        CurlCommandMcpTool tool = NewTool(new StubRunner(0, string.Empty));

        Assert.AreEqual(Canonical(GoldenSchema), Canonical(tool.InputSchemaJson));
    }

    [TestMethod]
    public async Task CallAsync_ExitZero_ReturnsTheResultAsJsonTextThatIsNotAnError()
    {
        StubRunner runner = new(0, "hello");
        CurlCommandMcpTool tool = NewTool(runner);

        McpToolCallResult result = await tool.CallAsync(Arguments("""{"args":["-s","http://example.invalid/"],"stdin":"input"}"""), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        CollectionAssert.AreEqual(new[] { "-s", "http://example.invalid/" }, runner.Arguments!.ToArray());
        Assert.AreEqual("input", runner.StandardInput);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual(0, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.AreEqual("hello", json.RootElement.GetProperty("standardOutput").GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task CallAsync_NonzeroExit_IsAToolErrorNamingTheFailureKind()
    {
        CurlCommandMcpTool tool = NewTool(new StubRunner(6, string.Empty));

        McpToolCallResult result = await tool.CallAsync(Arguments("""{"args":["http://example.invalid/"],"stdin":null}"""), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual(6, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.AreEqual(JsonValueKind.String, json.RootElement.GetProperty("failure").GetProperty("kind").ValueKind);
    }

    [TestMethod]
    public async Task CallAsync_StdinOmitted_RunsCurlWithEmptyStandardInput()
    {
        StubRunner runner = new(0, string.Empty);

        McpToolCallResult result = await NewTool(runner).CallAsync(Arguments("""{"args":["-s"]}"""), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(string.Empty, runner.StandardInput);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("""{"args":"-s"}""")]
    [DataRow("""{"args":["-s",1]}""")]
    [DataRow("""{"args":["-s"],"stdin":5}""")]
    public async Task CallAsync_MissingOrWrongTypedArguments_AreTheGoldenToolError(string argumentsJson)
    {
        CurlCommandMcpTool tool = NewTool(new StubRunner(0, string.Empty));

        McpToolCallResult result = await tool.CallAsync(Arguments(argumentsJson), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("An error occurred invoking 'curl'.", result.Content.Single().Text);
    }

    [TestMethod]
    public async Task CallAsync_UndefinedArguments_AreTheGoldenToolError()
    {
        CurlCommandMcpTool tool = NewTool(new StubRunner(0, string.Empty));

        McpToolCallResult result = await tool.CallAsync(default, CancellationToken.None);

        Assert.IsTrue(result.IsError);
    }

    private static string Canonical(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

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
