using System.Text;
using System.Text.Json;
using Curl.Api.Codes;
using Curl.Api.Info;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp;

[TestClass]
public sealed class CurlTransferToolTests
{
    private static readonly McpResponseCap Cap = new(McpResponseCap.DefaultMaxBytes);

    [TestMethod]
    public async Task TransferAsync_Success_PassesEveryFieldAndReturnsTheResultAsJsonTextThatIsNotAnError()
    {
        FakeEasyTransfer transfer = new()
        {
            HeaderLines = ["HTTP/1.1 200 OK\r\n", "Content-Type: text/plain\r\n", "\r\n"],
            BodyChunks = [Encoding.UTF8.GetBytes("hello")],
            InfoToReport = new TransferInfo { ResponseCode = 200 },
        };
        CurlTransferTool tool = new(new McpTransferTool(new FakeTransferEngine(transfer), Cap));

        CallToolResult result = await tool.TransferAsync(
            "https://example.test/",
            "PUT",
            [new McpTransferHeader("X-One", "1")],
            new McpTransferBody("payload", false),
            followRedirects: true,
            timeoutSeconds: 3,
            insecure: true,
            proxy: "http://proxy.test:3128",
            userAgent: "agent/1");

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("https://example.test/", transfer.Options.Network.Url);
        Assert.AreEqual("PUT", transfer.Options.Transfer.CustomRequest);
        Assert.AreEqual(TimeSpan.FromSeconds(3), transfer.Options.Transfer.Timeout);
        CollectionAssert.AreEqual(new[] { "X-One: 1" }, transfer.Options.Http.HttpHeaders.ToArray());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("payload"), transfer.Options.Http.PostFields!.Value.ToArray());
        Assert.AreEqual("agent/1", transfer.Options.Http.UserAgent);
        Assert.AreEqual("http://proxy.test:3128", transfer.Options.Proxy.Proxy);
        Assert.IsFalse(transfer.Options.Tls.Origin.VerifyPeer);
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual(200, json.RootElement.GetProperty("statusCode").GetInt64());
        Assert.AreEqual("hello", json.RootElement.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual("Content-Type", json.RootElement.GetProperty("headers")[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task TransferAsync_OnlyAUrl_SendsAGetWithNoHeaders()
    {
        FakeEasyTransfer transfer = new() { InfoToReport = new TransferInfo { ResponseCode = 204 } };
        CurlTransferTool tool = new(new McpTransferTool(new FakeTransferEngine(transfer), Cap));

        CallToolResult result = await tool.TransferAsync("http://example.test/");

        Assert.IsFalse(result.IsError);
        Assert.IsNull(transfer.Options.Transfer.CustomRequest);
        Assert.IsEmpty(transfer.Options.Http.HttpHeaders);
        Assert.IsNull(transfer.Options.Http.PostFields);
    }

    [TestMethod]
    public async Task TransferAsync_FailedTransfer_IsAToolErrorCarryingTheCode()
    {
        FakeEasyTransfer transfer = new() { Code = CurlCode.CouldntConnect, ErrorTextToReport = "Failed to connect" };
        CurlTransferTool tool = new(new McpTransferTool(new FakeTransferEngine(transfer), Cap));

        CallToolResult result = await tool.TransferAsync("http://example.test/");

        Assert.IsTrue(result.IsError);
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual(7, json.RootElement.GetProperty("failure").GetProperty("exitCode").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, json.RootElement.GetProperty("statusCode").ValueKind);
    }
}
