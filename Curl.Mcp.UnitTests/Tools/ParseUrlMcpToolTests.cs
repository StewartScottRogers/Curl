using System.Text.Json;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

[TestClass]
public sealed class ParseUrlMcpToolTests
{
    private const string GoldenSchema = """
        {"type":"object","properties":{"url":{"description":"The URL to parse, e.g. https://user:pw@example.com:8443/a?x=1#f.","type":"string"},
        "flags":{"description":"libcurl URL flags by name (CURLU_* without the prefix, in CamelCase), e.g. NonSupportScheme, DefaultPort, UrlDecode, GuessScheme, PathAsIs; none when omitted.","type":["array","null"],"items":{"type":["string","null"]},"default":null}},"required":["url"]}
        """;

    private static FakeUrlHandle NewHandle()
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.Ok, "https://h/");
        handle.Parts[CurlUrlPart.Scheme] = (CurlUrlCode.Ok, "https");
        handle.Parts[CurlUrlPart.User] = (CurlUrlCode.NoUser, null);
        handle.Parts[CurlUrlPart.Password] = (CurlUrlCode.NoPassword, null);
        handle.Parts[CurlUrlPart.Options] = (CurlUrlCode.NoOptions, null);
        handle.Parts[CurlUrlPart.Host] = (CurlUrlCode.Ok, "h");
        handle.Parts[CurlUrlPart.Port] = (CurlUrlCode.NoPort, null);
        handle.Parts[CurlUrlPart.Path] = (CurlUrlCode.Ok, "/");
        handle.Parts[CurlUrlPart.Query] = (CurlUrlCode.NoQuery, null);
        handle.Parts[CurlUrlPart.Fragment] = (CurlUrlCode.NoFragment, null);
        handle.Parts[CurlUrlPart.ZoneId] = (CurlUrlCode.NoZoneId, null);
        return handle;
    }

    private static JsonElement Arguments(string json) => JsonDocument.Parse(json).RootElement;

    private static string Canonical(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    [TestMethod]
    public void Identity_IsTheGoldenNameDescriptionAndAnnotations()
    {
        ParseUrlMcpTool tool = new(new McpUrlTool(NewHandle));

        Assert.AreEqual("parse_url", tool.Name);
        StringAssert.StartsWith(tool.Description, "Parses a URL with libcurl's URL API");
        StringAssert.EndsWith(tool.Description, "A URL the parser refuses is a tool error naming the CURLUcode.");
        Assert.IsTrue(tool.Annotations!.ReadOnlyHint);
        Assert.IsTrue(tool.Annotations.IdempotentHint);
        Assert.AreEqual(Canonical(GoldenSchema), Canonical(tool.InputSchemaJson));
    }

    [TestMethod]
    public async Task CallAsync_AbsentPart_IsNullInTheJsonNotEmpty()
    {
        ParseUrlMcpTool tool = new(new McpUrlTool(NewHandle));

        McpToolCallResult result = await tool.CallAsync(Arguments("""{"url":"https://h/","flags":null}"""), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual("https://h/", json.RootElement.GetProperty("url").GetString());
        JsonElement parts = json.RootElement.GetProperty("parts");
        Assert.AreEqual("h", parts.GetProperty("host").GetString());
        Assert.AreEqual(JsonValueKind.Null, parts.GetProperty("query").ValueKind);
    }

    [TestMethod]
    public async Task CallAsync_RefusedUrl_IsAToolErrorNamingTheCode()
    {
        FakeUrlHandle handle = NewHandle();
        handle.SetResults[CurlUrlPart.Url] = CurlUrlCode.NoHost;
        ParseUrlMcpTool tool = new(new McpUrlTool(() => handle));

        McpToolCallResult result = await tool.CallAsync(Arguments("""{"url":"http://","flags":["UrlDecode"]}"""), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        StringAssert.Contains(result.Content.Single().Text, "CURLUcode 14 (NoHost)");
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("""{"url":5}""")]
    [DataRow("""{"url":"u","flags":"x"}""")]
    [DataRow("""{"url":"u","flags":[1]}""")]
    [DataRow("""{"url":"u","flags":[null]}""")]
    public async Task CallAsync_MissingOrWrongTypedArguments_AreAToolError(string argumentsJson)
    {
        ParseUrlMcpTool tool = new(new McpUrlTool(NewHandle));

        McpToolCallResult result = await tool.CallAsync(Arguments(argumentsJson), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(ParseUrlMcpTool.InvalidArgumentsMessage, result.Content.Single().Text);
    }

    [TestMethod]
    public async Task CallAsync_UndefinedArguments_AreAToolError()
    {
        ParseUrlMcpTool tool = new(new McpUrlTool(NewHandle));

        McpToolCallResult result = await tool.CallAsync(default, CancellationToken.None);

        Assert.IsTrue(result.IsError);
    }
}
