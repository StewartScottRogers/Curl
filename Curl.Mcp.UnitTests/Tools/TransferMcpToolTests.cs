using System.Text;
using System.Text.Json;
using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Mcp.Protocol.Model;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp.Tools;

[TestClass]
public sealed class TransferMcpToolTests
{
    private static readonly McpResponseCap Cap = new(McpResponseCap.DefaultMaxBytes);

    /// <summary>The golden <c>transfer</c> input schema of <c>Curl.Mcp.IntegrationTests/Golden/tools-list.json</c> (BL-3374).</summary>
    private const string GoldenInputSchema = """
        {"type":"object","properties":{
        "url":{"description":"The absolute URL to transfer, e.g. https://example.com/; file: URLs are refused.","type":"string"},
        "method":{"description":"The request method, sent as given; GET when omitted.","type":"string","default":"GET"},
        "headers":{"description":"Extra request headers, in the order sent; an empty value sends the header empty.","type":["array","null"],
          "items":{"type":["object","null"],"properties":{"name":{"type":["string","null"]},"value":{"type":["string","null"]}},"required":["name","value"]},"default":null},
        "body":{"description":"The request body: inline text sent as UTF-8, or base64 when isBase64 is true. Never a file.","type":["object","null"],
          "properties":{"content":{"type":["string","null"]},"isBase64":{"type":"boolean"}},"required":["content","isBase64"],"default":null},
        "followRedirects":{"description":"Whether redirects are followed, as curl -L follows them.","type":"boolean","default":false},
        "timeoutSeconds":{"description":"The longest the whole transfer may take, in seconds; omit or 0 for no limit.","type":["number","null"],"default":null},
        "insecure":{"description":"Whether the server's certificate goes unverified, as curl -k leaves it.","type":"boolean","default":false},
        "proxy":{"description":"The proxy to use, as curl -x takes it; the environment's when omitted.","type":["string","null"],"default":null},
        "userAgent":{"description":"The User-Agent header; none is sent when omitted.","type":["string","null"],"default":null}},
        "required":["url"]}
        """;

    [TestMethod]
    public void Description_NameAndAnnotations_AreTheSdkToolsAndTheSchemaIsTheGoldenOne()
    {
        TransferMcpTool tool = Tool(new FakeEasyTransfer());

        Assert.AreEqual("transfer", tool.Name);
        Assert.AreEqual(CurlTransferTool.ToolDescription, tool.Description);
        Assert.IsNull(tool.Annotations);
        using JsonDocument schema = JsonDocument.Parse(tool.InputSchemaJson);
        using JsonDocument golden = JsonDocument.Parse(GoldenInputSchema);
        Assert.IsTrue(JsonElement.DeepEquals(golden.RootElement, schema.RootElement));
    }

    [TestMethod]
    public async Task CallAsync_EveryField_PassesEachAndReturnsTheResultAsJsonTextThatIsNotAnError()
    {
        FakeEasyTransfer transfer = new()
        {
            HeaderLines = ["HTTP/1.1 200 OK\r\n", "Content-Type: text/plain\r\n", "\r\n"],
            BodyChunks = [Encoding.UTF8.GetBytes("hello")],
            InfoToReport = new TransferInfo { ResponseCode = 200 },
        };

        McpToolCallResult result = await CallAsync(transfer, """
            {"url":"https://example.test/","method":"PUT","headers":[{"name":"X-One","value":"1"},{"name":"X-Empty","value":""}],
             "body":{"content":"payload","isBase64":false},"followRedirects":true,"timeoutSeconds":3,"insecure":true,
             "proxy":"http://proxy.test:3128","userAgent":"agent/1"}
            """);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("https://example.test/", transfer.Options.Network.Url);
        Assert.AreEqual("PUT", transfer.Options.Transfer.CustomRequest);
        Assert.AreEqual(TimeSpan.FromSeconds(3), transfer.Options.Transfer.Timeout);
        CollectionAssert.AreEqual(new[] { "X-One: 1", "X-Empty;" }, transfer.Options.Http.HttpHeaders.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("payload"), transfer.Options.Http.PostFields!.Value.ToArray());
        Assert.AreEqual("agent/1", transfer.Options.Http.UserAgent);
        Assert.AreEqual("http://proxy.test:3128", transfer.Options.Proxy.Proxy);
        Assert.IsFalse(transfer.Options.Tls.Origin.VerifyPeer);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual(200, json.RootElement.GetProperty("statusCode").GetInt64());
        Assert.AreEqual("hello", json.RootElement.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual("Content-Type", json.RootElement.GetProperty("headers")[0].GetProperty("name").GetString());
    }

    [TestMethod]
    [DataRow("""{"url":"http://example.test/"}""")]
    [DataRow("""{"url":"http://example.test/","method":null,"headers":null,"body":null,"timeoutSeconds":null,"proxy":null,"userAgent":null}""")]
    public async Task CallAsync_OnlyAUrl_SendsAGetWithNoHeadersBodyOrLimit(string arguments)
    {
        FakeEasyTransfer transfer = new() { InfoToReport = new TransferInfo { ResponseCode = 204 } };

        McpToolCallResult result = await CallAsync(transfer, arguments);

        Assert.IsFalse(result.IsError);
        Assert.IsNull(transfer.Options.Transfer.CustomRequest);
        Assert.IsEmpty(transfer.Options.Http.HttpHeaders);
        Assert.IsNull(transfer.Options.Http.PostFields);
        Assert.IsNull(transfer.Options.Http.UserAgent);
        Assert.IsTrue(transfer.Options.Tls.Origin.VerifyPeer);
    }

    [TestMethod]
    [DataRow("""{"content":"aGk=","isBase64":true}""")]
    [DataRow("""{"content":"hi"}""")]
    public async Task CallAsync_Body_SendsTheDecodedBytes(string body)
    {
        FakeEasyTransfer transfer = new() { InfoToReport = new TransferInfo { ResponseCode = 200 } };

        McpToolCallResult result = await CallAsync(transfer, $$$"""{"url":"https://example.test/","method":"POST","body":{{{body}}},"headers":[],"notAField":{}}""");

        Assert.IsFalse(result.IsError);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hi"), transfer.Options.Http.PostFields!.Value.ToArray());
    }

    [TestMethod]
    public async Task CallAsync_FailedTransfer_IsAToolErrorCarryingTheCode()
    {
        FakeEasyTransfer transfer = new() { Code = CurlCode.CouldntConnect, ErrorTextToReport = "Failed to connect" };

        McpToolCallResult result = await CallAsync(transfer, """{"url":"http://example.test/"}""");

        Assert.IsTrue(result.IsError);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual(7, json.RootElement.GetProperty("failure").GetProperty("exitCode").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, json.RootElement.GetProperty("statusCode").ValueKind);
    }

    [TestMethod]
    public async Task CallAsync_Cancelled_ReachesTheTransferToolAsTheCancelledError()
    {
        FakeEasyTransfer transfer = new();

        McpToolCallResult result = await Tool(transfer).CallAsync(Arguments("""{"url":"http://example.test/"}"""), new CancellationToken(canceled: true));

        Assert.IsTrue(result.IsError);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual(McpToolFailure.CancelledText, json.RootElement.GetProperty("failure").GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("""{"url":"https://example.test/","headers":{"X-One":"1"}}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an object.")]
    [DataRow("""{"url":"https://example.test/","headers":[{"name":"X-One","value":1}]}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an array.")]
    [DataRow("""{"url":"https://example.test/","headers":[{"value":"1"}]}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an array.")]
    [DataRow("""{"url":"https://example.test/","headers":["X-One: 1"]}""", "Invalid headers: it must be an array of objects with a string name and a string value, not an array.")]
    [DataRow("""{"url":"http://127.0.0.1:1/","headers":"X-Test: yes"}""", "Invalid headers: it must be an array of objects with a string name and a string value, not a string.")]
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
    [DataRow("""{}""", "Invalid url: it is empty.")]
    [DataRow("""{"url":null}""", "Invalid url: it is empty.")]
    [DataRow("""[]""", "Invalid url: it is empty.")]
    public async Task CallAsync_WrongShape_AnswersAToolErrorNamingTheFieldWithoutATransfer(string arguments, string expected)
    {
        FakeEasyTransfer transfer = new();

        McpToolCallResult result = await CallAsync(transfer, arguments);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(transfer.Options.Network.Url);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual("InvalidRequest", json.RootElement.GetProperty("failure").GetProperty("kind").GetString());
        Assert.AreEqual(expected, json.RootElement.GetProperty("failure").GetProperty("text").GetString());
    }

    [TestMethod]
    [DataRow("http://example.test/")]
    [DataRow("")]
    public async Task CallAsync_SameResult_SerializesAsTheSdkToolDoes(string url)
    {
        CurlTransferTool sdkTool = new(new McpTransferTool(new FakeTransferEngine(Transfer()), Cap));
        CallToolResult expected = await sdkTool.TransferAsync(url);

        McpToolCallResult result = await CallAsync(Transfer(), $$"""{"url":"{{url}}"}""");

        Assert.AreEqual(expected.IsError, result.IsError);
        Assert.AreEqual(((TextContentBlock)expected.Content.Single()).Text, result.Content.Single().Text);
    }

    private static FakeEasyTransfer Transfer() => new()
    {
        HeaderLines = ["HTTP/1.1 200 OK\r\n", "Content-Type: text/plain\r\n", "\r\n"],
        BodyChunks = [[0xff, 0x00]],
        InfoToReport = new TransferInfo { ResponseCode = 200 },
    };

    private static TransferMcpTool Tool(FakeEasyTransfer transfer) => new(new McpTransferTool(new FakeTransferEngine(transfer), Cap));

    private static async Task<McpToolCallResult> CallAsync(FakeEasyTransfer transfer, string arguments) =>
        await Tool(transfer).CallAsync(Arguments(arguments), CancellationToken.None);

    private static JsonElement Arguments(string arguments)
    {
        using JsonDocument document = JsonDocument.Parse(arguments);
        return document.RootElement.Clone();
    }
}
