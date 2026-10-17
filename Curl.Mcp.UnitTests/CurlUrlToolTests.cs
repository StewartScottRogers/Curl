using System.Text.Json;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp;

[TestClass]
public sealed class CurlUrlToolTests
{
    [TestMethod]
    public void ParseUrl_AbsentPart_IsNullInTheJsonNotEmpty()
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

        CallToolResult result = new CurlUrlTool(new McpUrlTool(() => handle)).ParseUrl("https://h/");

        Assert.IsFalse(result.IsError);
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual("https://h/", json.RootElement.GetProperty("url").GetString());
        JsonElement parts = json.RootElement.GetProperty("parts");
        Assert.AreEqual("h", parts.GetProperty("host").GetString());
        Assert.AreEqual(JsonValueKind.Null, parts.GetProperty("query").ValueKind);
    }

    [TestMethod]
    public void BuildUrl_RefusedPart_IsAToolErrorWhoseTextNamesTheCode()
    {
        FakeUrlHandle handle = new();
        handle.SetResults[CurlUrlPart.Port] = CurlUrlCode.BadPortNumber;

        CallToolResult result = new CurlUrlTool(new McpUrlTool(() => handle)).BuildUrl(scheme: "https", host: "h", port: "x");

        Assert.IsTrue(result.IsError);
        StringAssert.Contains(((TextContentBlock)result.Content.Single()).Text, "CURLUcode 4 (BadPortNumber)");
    }

    [TestMethod]
    public void BuildUrl_EveryPart_IsForwardedToItsSet()
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.Ok, "built");

        CallToolResult result = new CurlUrlTool(new McpUrlTool(() => handle))
            .BuildUrl("s", "u", "pw", "o", "h", "1", "/p", "q", "f", "z", ["UrlEncode"]);

        Assert.IsFalse(result.IsError);
        CollectionAssert.AreEqual(
            new string?[] { "s", "u", "pw", "o", "h", "1", "/p", "q", "f", "z" },
            handle.Sets.Select(set => set.Value).ToArray());
        Assert.IsTrue(handle.Sets.All(set => set.Flags == CurlUrlFlags.UrlEncode));
    }
}
