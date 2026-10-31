using System.Text;
using System.Text.Json;

namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>
/// Each test reads recorded server message bytes against one rule of the Model Context Protocol
/// 2025-06-18, and names the specification section that sets it.
/// </summary>
[TestClass]
public sealed class McpResponseReaderTests
{
    [TestMethod]
    public void ReadResponse_ResponsesResultForTheAwaitedId_ReturnsTheResult()
    {
        // "Base Protocol > Overview > Responses": a response carries the request's id and a result.
        var response = Read("""{"jsonrpc":"2.0","id":2,"result":{"tools":[]}}""", awaitedId: 2);

        Assert.IsNotNull(response);
        Assert.AreEqual(2L, response.Id);
        Assert.IsNull(response.Error);
        Assert.AreEqual("""{"tools":[]}""", response.Result!.Value.GetRawText());
    }

    [TestMethod]
    public void ReadResponse_ResponsesErrorForTheAwaitedId_ReturnsCodeAndMessage()
    {
        // "Server Features > Tools > Error Handling": the specification's unknown-tool protocol error.
        var response = Read("""{"jsonrpc":"2.0","id":3,"error":{"code":-32602,"message":"Unknown tool: invalid_tool_name"}}""", awaitedId: 3);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Result);
        Assert.AreEqual(new McpError(-32602, "Unknown tool: invalid_tool_name"), response.Error);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/tools/list_changed"}""", DisplayName = "server notification")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", DisplayName = "server request with the awaited id")]
    [DataRow("""{"jsonrpc":"2.0","id":7,"result":{}}""", DisplayName = "response to another id")]
    [DataRow("""{"jsonrpc":"2.0","id":"1","result":{}}""", DisplayName = "string id")]
    [DataRow("""{"jsonrpc":"2.0","id":1.5,"result":{}}""", DisplayName = "fractional id")]
    [DataRow("""{"jsonrpc":"2.0","result":{}}""", DisplayName = "no id")]
    public void ReadResponse_BaseProtocolMessageThatIsNotTheAwaitedResponse_ReturnsNull(string message)
    {
        // "Base Protocol > Overview": requests and notifications have a method; responses carry the request's id.
        Assert.IsNull(Read(message, awaitedId: 1));
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0",""", DisplayName = "not JSON")]
    [DataRow("""[{"jsonrpc":"2.0","id":1,"result":{}}]""", DisplayName = "batch array")]
    [DataRow("""{"id":1,"result":{}}""", DisplayName = "no jsonrpc")]
    [DataRow("""{"jsonrpc":2,"id":1,"result":{}}""", DisplayName = "numeric jsonrpc")]
    [DataRow("""{"jsonrpc":"1.0","id":1,"result":{}}""", DisplayName = "jsonrpc 1.0")]
    [DataRow("""{"jsonrpc":"2.0","id":1}""", DisplayName = "neither result nor error")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"error":"failed"}""", DisplayName = "error not an object")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"error":{"message":"m"}}""", DisplayName = "error without code")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"error":{"code":"x","message":"m"}}""", DisplayName = "string code")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"error":{"code":1.5,"message":"m"}}""", DisplayName = "fractional code")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"error":{"code":-32603}}""", DisplayName = "error without message")]
    public void ReadResponse_BaseProtocolMalformedMessage_Throws(string message)
    {
        // "Base Protocol > Overview": every message is one JSON-RPC 2.0 object (2025-06-18 has no batches),
        // and a response holds a result or an error with an integer code and a string message.
        Assert.ThrowsExactly<McpWireFormatException>(() => Read(message, awaitedId: 1));
    }

    [TestMethod]
    public void ReadInitializeResult_LifecycleInitialization_ReturnsTheServersProtocolVersion()
    {
        // "Lifecycle > Initialization": the specification's example InitializeResult.
        var result = ReadResult("""
            {"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{"tools":{"listChanged":true}},
            "serverInfo":{"name":"ExampleServer","title":"Example Server Display Name","version":"1.0.0"},"instructions":"Optional instructions for the client"}}
            """);

        Assert.AreEqual(new McpInitializeResult("2024-11-05"), McpResponseReader.ReadInitializeResult(result));
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":[]}""", DisplayName = "not an object")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"capabilities":{}}}""", DisplayName = "no protocolVersion")]
    public void ReadInitializeResult_LifecycleVersionNegotiationMalformed_Throws(string message)
    {
        // "Lifecycle > Version Negotiation": the server must answer with a protocol version.
        var result = ReadResult(message);

        Assert.ThrowsExactly<McpWireFormatException>(() => McpResponseReader.ReadInitializeResult(result));
    }

    [TestMethod]
    public void ReadToolsListResult_ListingTools_ReturnsToolsAndNextCursor()
    {
        // "Server Features > Tools > Listing Tools": the specification's example response.
        var result = ReadResult("""
            {"jsonrpc":"2.0","id":1,"result":{"tools":[{"name":"get_weather","title":"Weather Information Provider",
            "description":"Get current weather information for a location",
            "inputSchema":{"type":"object","properties":{"location":{"type":"string","description":"City name or zip code"}},"required":["location"]}}],
            "nextCursor":"next-page-cursor"}}
            """);

        var list = McpResponseReader.ReadToolsListResult(result);

        Assert.AreEqual("next-page-cursor", list.NextCursor);
        Assert.HasCount(1, list.Tools);
        Assert.AreEqual("get_weather", list.Tools[0].Name);
        Assert.AreEqual("Get current weather information for a location", list.Tools[0].Description);
        Assert.AreEqual(
            """{"type":"object","properties":{"location":{"type":"string","description":"City name or zip code"}},"required":["location"]}""",
            list.Tools[0].InputSchemaJson);
    }

    [TestMethod]
    public void ReadToolsListResult_ListingToolsLastPage_ReturnsNoCursorAndOptionalMembersAsNull()
    {
        // "Server Features > Tools > Listing Tools": the last page has no nextCursor; a tool's description is optional.
        var list = McpResponseReader.ReadToolsListResult(ReadResult("""{"jsonrpc":"2.0","id":1,"result":{"tools":[{"name":"t"}]}}"""));

        Assert.IsNull(list.NextCursor);
        Assert.AreEqual(new McpTool("t", null, null), list.Tools[0]);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":"tools"}""", DisplayName = "not an object")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{}}""", DisplayName = "no tools")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"tools":{}}}""", DisplayName = "tools not an array")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"tools":["t"]}}""", DisplayName = "tool not an object")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"tools":[{"description":"d"}]}}""", DisplayName = "tool without name")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"tools":[{"name":"t","description":1}]}}""", DisplayName = "numeric description")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"tools":[],"nextCursor":2}}""", DisplayName = "numeric nextCursor")]
    public void ReadToolsListResult_ListingToolsMalformed_Throws(string message)
    {
        // "Server Features > Tools > Tool": a tool is an object with a string name; the result holds a tools array.
        var result = ReadResult(message);

        Assert.ThrowsExactly<McpWireFormatException>(() => McpResponseReader.ReadToolsListResult(result));
    }

    [TestMethod]
    public void ReadCallToolResult_CallingTools_ReturnsTextContentAndIsErrorFalse()
    {
        // "Server Features > Tools > Calling Tools": the specification's example response.
        var result = ReadResult("""
            {"jsonrpc":"2.0","id":2,"result":{"content":[{"type":"text","text":"Current weather in New York:\nTemperature: 72°F\nConditions: Partly cloudy"}],"isError":false}}
            """);

        var call = McpResponseReader.ReadCallToolResult(result);

        Assert.IsFalse(call.IsError);
        Assert.HasCount(1, call.Content);
        Assert.AreEqual("text", call.Content[0].Type);
        Assert.AreEqual("Current weather in New York:\nTemperature: 72°F\nConditions: Partly cloudy", call.Content[0].Text);
    }

    [TestMethod]
    public void ReadCallToolResult_ToolResultImageContent_KeepsTheItemAsJsonWithNoText()
    {
        // "Server Features > Tools > Tool Result > Image Content": the specification's example item; isError absent means false.
        var call = McpResponseReader.ReadCallToolResult(ReadResult("""
            {"jsonrpc":"2.0","id":2,"result":{"content":[{"type":"image","data":"base64-encoded-data","mimeType":"image/png"}]}}
            """));

        Assert.IsFalse(call.IsError);
        Assert.AreEqual(
            new McpContent("image", null, """{"type":"image","data":"base64-encoded-data","mimeType":"image/png"}"""),
            call.Content[0]);
    }

    [TestMethod]
    public void ReadCallToolResult_ErrorHandlingToolExecutionError_ReturnsIsErrorTrue()
    {
        // "Server Features > Tools > Error Handling": the specification's tool execution error example.
        var call = McpResponseReader.ReadCallToolResult(ReadResult("""
            {"jsonrpc":"2.0","id":4,"result":{"content":[{"type":"text","text":"Failed to fetch weather data: API rate limit exceeded"}],"isError":true}}
            """));

        Assert.IsTrue(call.IsError);
        Assert.AreEqual("Failed to fetch weather data: API rate limit exceeded", call.Content[0].Text);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":1}""", DisplayName = "not an object")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"isError":false}}""", DisplayName = "no content")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"content":[1]}}""", DisplayName = "item not an object")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"text":"t"}]}}""", DisplayName = "item without type")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text"}]}}""", DisplayName = "text item without text")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"result":{"content":[],"isError":"true"}}""", DisplayName = "string isError")]
    public void ReadCallToolResult_ToolResultMalformed_Throws(string message)
    {
        // "Server Features > Tools > Tool Result": content is an array of typed items; isError is a boolean.
        var result = ReadResult(message);

        Assert.ThrowsExactly<McpWireFormatException>(() => McpResponseReader.ReadCallToolResult(result));
    }

    private static McpResponse? Read(string message, long awaitedId) =>
        McpResponseReader.ReadResponse(Encoding.UTF8.GetBytes(message), awaitedId);

    private static JsonElement ReadResult(string message) =>
        Read(message, awaitedId: ReadId(message))!.Result!.Value;

    private static long ReadId(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.GetProperty("id").GetInt64();
    }
}
