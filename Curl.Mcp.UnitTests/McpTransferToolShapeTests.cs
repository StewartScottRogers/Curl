using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

/// <summary>
/// The <c>transfer</c> tool refuses a wrong argument shape naming the field and the shape it must have,
/// never with the SDK's bare <c>An error occurred invoking 'transfer'</c> (ADR-0771, BL-3188).
/// </summary>
[TestClass]
public sealed class McpTransferToolShapeTests
{
    private const string SdkBindingError = "An error occurred invoking 'transfer'";

    /// <summary>The server a request context names; never run, so its empty streams carry nothing.</summary>
    private static McpServer server = null!;

    [ClassInitialize]
    public static void CreateServer(TestContext context) =>
        server = McpServer.Create(new StreamServerTransport(Stream.Null, Stream.Null), new McpServerOptions());

    [ClassCleanup]
    public static async Task DisposeServerAsync() => await server.DisposeAsync();

    [TestMethod]
    [DataRow("""{"url":"https://example.test/","headers":{"X-One":"1"}}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an object.")]
    [DataRow("""{"url":"https://example.test/","headers":[{"name":"X-One","value":1}]}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an array.")]
    [DataRow("""{"url":"https://example.test/","headers":[{"value":"1"}]}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an array.")]
    [DataRow("""{"url":"https://example.test/","headers":["X-One: 1"]}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an array.")]
    [DataRow("""{"url":"https://example.test/","body":"payload"}""", "Invalid body: it must be an object with a string content and a boolean isBase64, not a string.")]
    [DataRow("""{"url":"https://example.test/","body":{"content":"eA==","isBase64":"yes"}}""", "Invalid body: it must be an object with a string content and a boolean isBase64, not an object.")]
    [DataRow("""{"url":"https://example.test/","body":{"isBase64":false}}""", "Invalid body: it must be an object with a string content and a boolean isBase64, not an object.")]
    [DataRow("""{"url":["https://example.test/"]}""", "Invalid url: it must be a string, not an array.")]
    [DataRow("""{"url":42}""", "Invalid url: it must be a string, not a number.")]
    [DataRow("""{"url":"https://example.test/","method":true}""", "Invalid method: it must be a string, not a boolean.")]
    [DataRow("""{"url":"https://example.test/","followRedirects":"true"}""", "Invalid followRedirects: it must be a boolean, not a string.")]
    [DataRow("""{"url":"https://example.test/","insecure":null}""", "Invalid insecure: it must be a boolean, not null.")]
    [DataRow("""{"url":"https://example.test/","timeoutSeconds":"3"}""", "Invalid timeoutSeconds: it must be a number, not a string.")]
    [DataRow("""{"url":"https://example.test/","proxy":{"host":"proxy.test"}}""", "Invalid proxy: it must be a string, not an object.")]
    [DataRow("""{"url":"https://example.test/","userAgent":1}""", "Invalid userAgent: it must be a string, not a number.")]
    public async Task RefuseWrongArgumentShapes_WrongShape_AnswersAToolErrorNamingTheFieldAndItsShape(string arguments, string expected)
    {
        bool reachedTheTool = false;
        McpRequestHandler<CallToolRequestParams, CallToolResult> handler = CurlTransferTool.RefuseWrongArgumentShapes((_, _) =>
        {
            reachedTheTool = true;
            return ValueTask.FromResult(new CallToolResult());
        });

        CallToolResult result = await handler(Context("transfer", arguments), CancellationToken.None);

        Assert.IsFalse(reachedTheTool);
        Assert.IsTrue(result.IsError);
        string text = ((TextContentBlock)result.Content.Single()).Text;
        Assert.DoesNotContain(SdkBindingError, text);
        using JsonDocument json = JsonDocument.Parse(text);
        Assert.AreEqual(expected, json.RootElement.GetProperty("failure").GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("""{"url":"https://example.test/"}""")]
    [DataRow("""{"url":"https://example.test/","method":"PUT","headers":[{"name":"X-One","value":"1"}],"body":{"content":"payload","isBase64":false},"followRedirects":true,"timeoutSeconds":3,"insecure":false,"proxy":"http://proxy.test:3128","userAgent":"agent/1"}""")]
    [DataRow("""{"url":"https://example.test/","headers":null,"body":null,"timeoutSeconds":null,"proxy":null,"userAgent":null,"method":null}""")]
    [DataRow("""{"url":"https://example.test/","body":{"content":"payload"},"headers":[],"notAField":{}}""")]
    public async Task RefuseWrongArgumentShapes_RightShapes_GoOnToTheTool(string arguments)
    {
        CallToolResult toolResult = new();
        McpRequestHandler<CallToolRequestParams, CallToolResult> handler = CurlTransferTool.RefuseWrongArgumentShapes((_, _) => ValueTask.FromResult(toolResult));

        CallToolResult result = await handler(Context("transfer", arguments), CancellationToken.None);

        Assert.AreSame(toolResult, result);
    }

    [TestMethod]
    public async Task RefuseWrongArgumentShapes_AnotherToolOrNoParameters_GoOnUnchecked()
    {
        CallToolResult toolResult = new();
        McpRequestHandler<CallToolRequestParams, CallToolResult> handler = CurlTransferTool.RefuseWrongArgumentShapes((_, _) => ValueTask.FromResult(toolResult));

        Assert.AreSame(toolResult, await handler(Context("parse_url", """{"url":42}"""), CancellationToken.None));
        Assert.AreSame(toolResult, await handler(new RequestContext<CallToolRequestParams>(server, new JsonRpcRequest { Method = "tools/call" }, null!), CancellationToken.None));
    }

    [TestMethod]
    public void CheckArgumentShapes_NoArguments_IsNull()
    {
        Assert.IsNull(McpTransferRequestCheck.CheckArgumentShapes(null));
    }

    [TestMethod]
    [DoNotParallelize]
    public void Build_RegistersTheShapeCheckAsACallToolFilter()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);

        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        Assert.HasCount(1, options.Filters.Request.CallToolFilters);
    }

    private static RequestContext<CallToolRequestParams> Context(string tool, string arguments) =>
        new(server, new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = tool,
            Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments)!,
        });
}
