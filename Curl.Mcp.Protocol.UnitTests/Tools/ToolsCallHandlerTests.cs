using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp.Protocol.Tools;

[TestClass]
public sealed class ToolsCallHandlerTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static ToolsCallHandler Handler(params IMcpTool[] tools)
    {
        McpServerRegistry registry = new();
        foreach (IMcpTool tool in tools)
        {
            registry.Add(tool);
        }

        return new ToolsCallHandler(registry);
    }

    private static async Task<McpProtocolException> CallFailingAsync(ToolsCallHandler handler, JsonElement? parameters) =>
        await Assert.ThrowsExactlyAsync<McpProtocolException>(async () => await handler.HandleAsync(parameters, CancellationToken.None));

    [TestMethod]
    public void Constructor_NullRegistry_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ToolsCallHandler(null!));

    [TestMethod]
    public void Method_IsToolsCall() => Assert.AreEqual("tools/call", Handler().Method);

    [TestMethod]
    public async Task HandleAsync_ToolSucceeds_AnswersItsContentWithIsErrorFalse()
    {
        FakeMcpTool tool = FakeMcpTool.Succeeding("echo", "hello");

        JsonElement result = await Handler(tool).HandleAsync(Json("""{"name":"echo","arguments":{"text":"hello"}}"""), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(Json("""{"content":[{"type":"text","text":"hello"}],"isError":false}"""), result), result.GetRawText());
        Assert.IsTrue(JsonElement.DeepEquals(Json("""{"text":"hello"}"""), tool.LastArguments!.Value));
    }

    [TestMethod]
    public async Task HandleAsync_ToolReturnsAnErrorResult_PassesItThroughWithIsErrorTrue()
    {
        FakeMcpTool tool = new("fails", (_, _) => McpToolCallResult.Error("it went wrong"));

        JsonElement result = await Handler(tool).HandleAsync(Json("""{"name":"fails","arguments":{}}"""), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(Json("""{"content":[{"type":"text","text":"it went wrong"}],"isError":true}"""), result), result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_ToolThrows_AnswersAToolErrorWithoutTheExceptionText()
    {
        FakeMcpTool tool = new("curl", (_, _) => throw new InvalidOperationException("secret detail"));

        JsonElement result = await Handler(tool).HandleAsync(Json("""{"name":"curl","arguments":{}}"""), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(Json("""{"content":[{"type":"text","text":"An error occurred invoking 'curl'."}],"isError":true}"""), result), result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_ToolThrowsCancellationOfItsOwn_AnswersAToolError()
    {
        FakeMcpTool tool = new("slow", (_, _) => throw new OperationCanceledException());

        JsonElement result = await Handler(tool).HandleAsync(Json("""{"name":"slow"}"""), CancellationToken.None);

        Assert.IsTrue(result.GetProperty("isError").GetBoolean());
    }

    [TestMethod]
    public async Task HandleAsync_RequestCancelled_ToolSeesTheTokenAndTheCancellationPropagates()
    {
        using CancellationTokenSource cancellation = new();
        FakeMcpTool tool = new("slow", (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return McpToolCallResult.Success("never");
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await Handler(tool).HandleAsync(Json("""{"name":"slow"}"""), cancellation.Token));

        Assert.AreEqual(cancellation.Token, tool.LastCancellationToken);
    }

    [TestMethod]
    public async Task HandleAsync_NoArguments_CallsTheToolWithAnEmptyObject()
    {
        FakeMcpTool tool = FakeMcpTool.Succeeding("version");

        await Handler(tool).HandleAsync(Json("""{"name":"version"}"""), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(Json("{}"), tool.LastArguments!.Value));
    }

    [TestMethod]
    public async Task HandleAsync_NullArguments_CallsTheToolWithAnEmptyObject()
    {
        FakeMcpTool tool = FakeMcpTool.Succeeding("version");

        await Handler(tool).HandleAsync(Json("""{"name":"version","arguments":null}"""), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(Json("{}"), tool.LastArguments!.Value));
    }

    [TestMethod]
    [DataRow("""{"name":"version","arguments":[1]}""")]
    [DataRow("""{"name":"version","arguments":"text"}""")]
    public async Task HandleAsync_ArgumentsNotAnObject_IsInvalidParams(string parameters)
    {
        FakeMcpTool tool = FakeMcpTool.Succeeding("version");

        McpProtocolException exception = await CallFailingAsync(Handler(tool), Json(parameters));

        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, exception.Code);
        Assert.AreEqual("The tools/call arguments must be a JSON object.", exception.Message);
        Assert.IsNull(tool.LastArguments);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"name":7}""")]
    [DataRow("[]")]
    public async Task HandleAsync_MissingName_IsInvalidParams(string parameters)
    {
        McpProtocolException exception = await CallFailingAsync(Handler(FakeMcpTool.Succeeding("version")), Json(parameters));

        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, exception.Code);
        Assert.AreEqual("The tools/call request needs a name string in its params.", exception.Message);
    }

    [TestMethod]
    public async Task HandleAsync_NoParams_IsInvalidParams()
    {
        McpProtocolException exception = await CallFailingAsync(Handler(), null);

        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, exception.Code);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownTool_AnswersTheGoldenError()
    {
        McpProtocolException exception = await CallFailingAsync(Handler(FakeMcpTool.Succeeding("version")), Json("""{"name":"no_such_tool","arguments":{}}"""));

        JsonElement golden = Json(GoldenToolsResponses.UnknownToolError);
        Assert.AreEqual(golden.GetProperty("code").GetInt32(), exception.Code);
        Assert.AreEqual(golden.GetProperty("message").GetString(), exception.Message);
    }
}
