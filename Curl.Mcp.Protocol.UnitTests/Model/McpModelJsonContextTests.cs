using System.Text.Json;

namespace Curl.Mcp.Protocol.Model;

[TestClass]
public sealed class McpModelJsonContextTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [TestMethod]
    public void ToolListResult_WithAnnotations_WritesNameDescriptionSchemaAndBothHints()
    {
        McpToolListResult result = new([new McpToolListEntry("version", "Shows the version.", Json("""{"type":"object","properties":{}}"""), new McpToolAnnotations(true, true))]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpToolListResult);

        Assert.AreEqual("""{"tools":[{"name":"version","description":"Shows the version.","inputSchema":{"type":"object","properties":{}},"annotations":{"idempotentHint":true,"readOnlyHint":true}}]}""", json);
    }

    [TestMethod]
    public void ToolListResult_NoAnnotations_OmitsAnnotations()
    {
        McpToolListResult result = new([new McpToolListEntry("curl", "Runs curl.", Json("""{"type":"object"}"""), null)]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpToolListResult);

        Assert.AreEqual("""{"tools":[{"name":"curl","description":"Runs curl.","inputSchema":{"type":"object"}}]}""", json);
    }

    [TestMethod]
    public void ToolListResult_OneHintUnset_OmitsThatHint()
    {
        McpToolListResult result = new([new McpToolListEntry("t", "d", Json("""{"type":"object"}"""), new McpToolAnnotations(null, false))]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpToolListResult);

        Assert.AreEqual("""{"tools":[{"name":"t","description":"d","inputSchema":{"type":"object"},"annotations":{"readOnlyHint":false}}]}""", json);
    }

    [TestMethod]
    public void ToolCallResult_Success_WritesTextBlockAndIsErrorFalse()
    {
        string json = JsonSerializer.Serialize(McpToolCallResult.Success("done"), McpModelJsonContext.Default.McpToolCallResult);

        Assert.AreEqual("""{"content":[{"type":"text","text":"done"}],"isError":false}""", json);
    }

    [TestMethod]
    public void ToolCallResult_Error_WritesTextBlockAndIsErrorTrue()
    {
        string json = JsonSerializer.Serialize(McpToolCallResult.Error("failed"), McpModelJsonContext.Default.McpToolCallResult);

        Assert.AreEqual("""{"content":[{"type":"text","text":"failed"}],"isError":true}""", json);
    }

    [TestMethod]
    public void ToolCallResult_TwoBlocks_WritesBothInOrder()
    {
        McpToolCallResult result = new([new McpTextContent("a"), new McpTextContent("b")], isError: false);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpToolCallResult);

        Assert.AreEqual("""{"content":[{"type":"text","text":"a"},{"type":"text","text":"b"}],"isError":false}""", json);
    }

    [TestMethod]
    public void ResourceListResult_AllProperties_WritesNameUriDescriptionMimeType()
    {
        McpResourceListResult result = new([new McpResource("ai-help", "curl://ai-help", "Index of options.", "text/markdown")]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpResourceListResult);

        Assert.AreEqual("""{"resources":[{"name":"ai-help","uri":"curl://ai-help","description":"Index of options.","mimeType":"text/markdown"}]}""", json);
    }

    [TestMethod]
    public void ResourceListResult_NullDescriptionAndMimeType_OmitsThem()
    {
        McpResourceListResult result = new([new McpResource("r", "curl://r", null, null)]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpResourceListResult);

        Assert.AreEqual("""{"resources":[{"name":"r","uri":"curl://r"}]}""", json);
    }

    [TestMethod]
    public void ResourceReadResult_WithMimeType_WritesUriMimeTypeText()
    {
        McpResourceReadResult result = new([new McpResourceContents("curl://ai-help", "text/markdown", "# help\n")]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpResourceReadResult);

        Assert.AreEqual("""{"contents":[{"uri":"curl://ai-help","mimeType":"text/markdown","text":"# help\n"}]}""", json);
    }

    [TestMethod]
    public void ResourceReadResult_NullMimeType_OmitsMimeType()
    {
        McpResourceReadResult result = new([new McpResourceContents("curl://r", null, "x")]);

        string json = JsonSerializer.Serialize(result, McpModelJsonContext.Default.McpResourceReadResult);

        Assert.AreEqual("""{"contents":[{"uri":"curl://r","text":"x"}]}""", json);
    }
}
