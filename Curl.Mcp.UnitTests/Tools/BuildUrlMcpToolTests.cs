using System.Text.Json;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Mcp.Protocol.Model;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp.Tools;

[TestClass]
public sealed class BuildUrlMcpToolTests
{
    private const string GoldenSchema = """
        {"type":"object","properties":{
        "scheme":{"description":"The scheme, e.g. https.","type":["string","null"],"default":null},
        "user":{"description":"The user name.","type":["string","null"],"default":null},
        "password":{"description":"The password.","type":["string","null"],"default":null},
        "options":{"description":"The login options (IMAP, POP3 and SMTP).","type":["string","null"],"default":null},
        "host":{"description":"The host name or address.","type":["string","null"],"default":null},
        "port":{"description":"The port number, as text.","type":["string","null"],"default":null},
        "path":{"description":"The path, e.g. /a/b.","type":["string","null"],"default":null},
        "query":{"description":"The query, without its '?'.","type":["string","null"],"default":null},
        "fragment":{"description":"The fragment, without its '#'.","type":["string","null"],"default":null},
        "zoneId":{"description":"The IPv6 zone identifier.","type":["string","null"],"default":null},
        "flags":{"description":"libcurl URL flags by name (CURLU_* without the prefix, in CamelCase), e.g. NonSupportScheme, DefaultPort, UrlDecode, GuessScheme, PathAsIs; none when omitted.","type":["array","null"],"items":{"type":["string","null"]},"default":null}}}
        """;

    private static JsonElement Arguments(string json) => JsonDocument.Parse(json).RootElement;

    private static string Canonical(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    [TestMethod]
    public void Identity_MatchesTheSdkTool()
    {
        BuildUrlMcpTool tool = new(new McpUrlTool(() => new FakeUrlHandle()));

        Assert.AreEqual("build_url", tool.Name);
        Assert.AreEqual(CurlUrlTool.BuildDescription, tool.Description);
        Assert.IsTrue(tool.Annotations!.ReadOnlyHint);
        Assert.IsTrue(tool.Annotations.IdempotentHint);
        Assert.AreEqual(Canonical(GoldenSchema), Canonical(tool.InputSchemaJson));
    }

    [TestMethod]
    public async Task CallAsync_RefusedPart_IsAToolErrorWhoseTextNamesTheCode()
    {
        FakeUrlHandle handle = new();
        handle.SetResults[CurlUrlPart.Port] = CurlUrlCode.BadPortNumber;
        BuildUrlMcpTool tool = new(new McpUrlTool(() => handle));

        McpToolCallResult result = await tool.CallAsync(Arguments("""{"scheme":"https","host":"h","port":"x"}"""), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        StringAssert.Contains(result.Content.Single().Text, "CURLUcode 4 (BadPortNumber)");
    }

    [TestMethod]
    public async Task CallAsync_EveryPart_IsForwardedToItsSet()
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.Ok, "built");
        BuildUrlMcpTool tool = new(new McpUrlTool(() => handle));

        McpToolCallResult result = await tool.CallAsync(
            Arguments("""{"scheme":"s","user":"u","password":"pw","options":"o","host":"h","port":"1","path":"/p","query":"q","fragment":"f","zoneId":"z","flags":["UrlEncode"]}"""),
            CancellationToken.None);

        Assert.IsFalse(result.IsError);
        CollectionAssert.AreEqual(
            new string?[] { "s", "u", "pw", "o", "h", "1", "/p", "q", "f", "z" },
            handle.Sets.Select(set => set.Value).ToArray());
        Assert.IsTrue(handle.Sets.All(set => set.Flags == CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("""{"host":5}""")]
    [DataRow("""{"host":"h","flags":"x"}""")]
    public async Task CallAsync_WrongTypedArguments_AreAToolError(string argumentsJson)
    {
        BuildUrlMcpTool tool = new(new McpUrlTool(() => new FakeUrlHandle()));

        McpToolCallResult result = await tool.CallAsync(Arguments(argumentsJson), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(BuildUrlMcpTool.InvalidArgumentsMessage, result.Content.Single().Text);
    }

    [TestMethod]
    public async Task CallAsync_UndefinedArguments_AreAToolError()
    {
        BuildUrlMcpTool tool = new(new McpUrlTool(() => new FakeUrlHandle()));

        McpToolCallResult result = await tool.CallAsync(default, CancellationToken.None);

        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CallAsync_SameInputs_SerializeLikeTheSdkTool(bool refused)
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.Ok, "built");
        if (refused)
        {
            handle.SetResults[CurlUrlPart.Port] = CurlUrlCode.BadPortNumber;
        }

        McpUrlTool logic = new(() => handle);

        McpToolCallResult ported = await new BuildUrlMcpTool(logic).CallAsync(Arguments("""{"host":"h","port":"x"}"""), CancellationToken.None);
        CallToolResult original = new CurlUrlTool(logic).BuildUrl(host: "h", port: "x");

        Assert.AreEqual(((TextContentBlock)original.Content.Single()).Text, ported.Content.Single().Text);
        Assert.AreEqual(original.IsError, ported.IsError);
    }
}
