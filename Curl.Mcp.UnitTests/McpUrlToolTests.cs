using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Engine;

namespace Curl.Mcp;

[TestClass]
public sealed class McpUrlToolTests
{
    private const string RoundTripUrl = "https://user:pw@example.com:8443/a/b?x=1#f";

    [TestMethod]
    public void Parse_FakeHandleWithEveryPart_ReturnsEveryPartAndTheWholeUrl()
    {
        FakeUrlHandle handle = HandleWithEveryPart();

        McpParseUrlToolResult result = new McpUrlTool(() => handle).Parse("anything", null);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("whole", result.Url);
        Assert.AreEqual(new McpUrlParts("s", "u", "pw", "o", "h", "1", "/p", "q", "f", "z"), result.Parts);
        CollectionAssert.AreEqual(new[] { (CurlUrlPart.Url, (string?)"anything", CurlUrlFlags.None) }, handle.Sets);
        Assert.IsTrue(handle.IsDisposed);
    }

    [TestMethod]
    public void Parse_FakeHandleReportingPartsNotSet_ReportsThemAbsentNotEmpty()
    {
        FakeUrlHandle handle = HandleWithEveryPart();
        handle.Parts[CurlUrlPart.Scheme] = (CurlUrlCode.NoScheme, null);
        handle.Parts[CurlUrlPart.Query] = (CurlUrlCode.NoQuery, null);
        handle.Parts[CurlUrlPart.ZoneId] = (CurlUrlCode.NoZoneId, null);

        McpParseUrlToolResult result = new McpUrlTool(() => handle).Parse("anything", null);

        Assert.IsFalse(result.IsError);
        Assert.IsNull(result.Parts!.Scheme);
        Assert.IsNull(result.Parts.Query);
        Assert.IsNull(result.Parts.ZoneId);
        Assert.AreEqual("h", result.Parts.Host);
    }

    [TestMethod]
    public void Parse_MalformedUrl_IsAToolErrorNamingTheCode()
    {
        FakeUrlHandle handle = new();
        handle.SetResults[CurlUrlPart.Url] = CurlUrlCode.MalformedInput;

        McpParseUrlToolResult result = new McpUrlTool(() => handle).Parse("bad", null);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(result.Parts);
        Assert.IsNull(result.Url);
        Assert.AreEqual(McpToolFailureKind.UrlRefused, result.Failure!.Kind);
        Assert.AreEqual("The URL parser refused url: CURLUcode 3 (MalformedInput)", result.Failure.Text);
    }

    [TestMethod]
    public void Parse_GetFailingWithACodeThatIsNotNotSet_IsAToolErrorNamingThePart()
    {
        FakeUrlHandle handle = HandleWithEveryPart();
        handle.Parts[CurlUrlPart.Path] = (CurlUrlCode.UrlDecode, null);

        McpParseUrlToolResult result = new McpUrlTool(() => handle).Parse("anything", ["UrlDecode"]);

        Assert.AreEqual("The URL parser refused path: CURLUcode 6 (UrlDecode)", result.Failure!.Text);
    }

    [TestMethod]
    public void Parse_FlagNames_AreMatchedWithoutCaseAndPassedToEverySetAndGet()
    {
        FakeUrlHandle handle = HandleWithEveryPart();

        new McpUrlTool(() => handle).Parse("anything", ["defaultport", "NonSupportScheme"]);

        Assert.AreEqual(CurlUrlFlags.DefaultPort | CurlUrlFlags.NonSupportScheme, handle.Sets.Single().Flags);
        Assert.IsTrue(handle.GetFlags.All(flags => flags == (CurlUrlFlags.DefaultPort | CurlUrlFlags.NonSupportScheme)));
    }

    [TestMethod]
    public void Parse_UnknownFlag_IsAnInvalidRequestAndMakesNoHandle()
    {
        bool created = false;

        McpParseUrlToolResult result = new McpUrlTool(() =>
        {
            created = true;
            return new FakeUrlHandle();
        }).Parse("anything", ["DefaultPort", "None"]);

        Assert.IsFalse(created);
        Assert.AreEqual(McpToolFailureKind.InvalidRequest, result.Failure!.Kind);
        StringAssert.StartsWith(result.Failure.Text, "Invalid flags[1]: 'None' is not a URL flag; use one of DefaultPort, ");
    }

    [TestMethod]
    public void Parse_NullUrl_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpUrlTool(() => new FakeUrlHandle()).Parse(null!, null));
    }

    [TestMethod]
    public void Build_SetsOnlyTheSuppliedPartsInLibcurlsOrderAndReturnsTheUrl()
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.Ok, "https://example.com:8443/");

        McpBuildUrlToolResult result = new McpUrlTool(() => handle).Build(new McpUrlParts(Host: "example.com", Scheme: "https", Port: "8443"), ["UrlEncode"]);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("https://example.com:8443/", result.Url);
        CollectionAssert.AreEqual(
            new[]
            {
                (CurlUrlPart.Scheme, (string?)"https", CurlUrlFlags.UrlEncode),
                (CurlUrlPart.Host, (string?)"example.com", CurlUrlFlags.UrlEncode),
                (CurlUrlPart.Port, (string?)"8443", CurlUrlFlags.UrlEncode),
            },
            handle.Sets);
        Assert.IsTrue(handle.IsDisposed);
    }

    [TestMethod]
    public void Build_BadPort_IsAToolErrorNamingThePartAndTheCode()
    {
        FakeUrlHandle handle = new();
        handle.SetResults[CurlUrlPart.Port] = CurlUrlCode.BadPortNumber;

        McpBuildUrlToolResult result = new McpUrlTool(() => handle).Build(new McpUrlParts(Scheme: "https", Port: "x", Path: "/"), null);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(result.Url);
        Assert.AreEqual("The URL parser refused port: CURLUcode 4 (BadPortNumber)", result.Failure!.Text);
        Assert.AreEqual(2, handle.Sets.Count);
    }

    [TestMethod]
    public void Build_NoHost_IsAToolErrorNamingTheCode()
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.NoHost, null);

        McpBuildUrlToolResult result = new McpUrlTool(() => handle).Build(new McpUrlParts(Scheme: "https"), null);

        Assert.AreEqual("The URL parser refused url: CURLUcode 14 (NoHost)", result.Failure!.Text);
    }

    [TestMethod]
    public void Build_UnknownFlag_IsAnInvalidRequest()
    {
        McpBuildUrlToolResult result = new McpUrlTool(() => new FakeUrlHandle()).Build(new McpUrlParts(), ["Sideways"]);

        Assert.AreEqual(McpToolFailureKind.InvalidRequest, result.Failure!.Kind);
        StringAssert.StartsWith(result.Failure.Text, "Invalid flags[0]: 'Sideways' is not a URL flag");
    }

    [TestMethod]
    public void Build_NullParts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpUrlTool(() => new FakeUrlHandle()).Build(null!, null));
    }

    [TestMethod]
    public void Parse_RealHandle_ReturnsEveryPartOfTheUrl()
    {
        McpParseUrlToolResult result = new McpUrlTool(new ManagedCurlEngine().CreateUrl).Parse(RoundTripUrl, null);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(RoundTripUrl, result.Url);
        Assert.AreEqual(new McpUrlParts("https", "user", "pw", null, "example.com", "8443", "/a/b", "x=1", "f", null), result.Parts);
    }

    [TestMethod]
    [DataRow("coap://example.com/sensors?x=1")]
    [DataRow("https://example.com/sensors?x=1")]
    public void Parse_RealHandleWithExtendedSchemes_AcceptsExtendedAndOriginalSchemesWithNoFlag(string url)
    {
        McpParseUrlToolResult result = new McpUrlTool(new ManagedCurlEngine().CreateUrl, ["COAP"]).Parse(url, null);

        Assert.IsFalse(result.IsError, result.Failure?.Text);
        Assert.AreEqual(url, result.Url);
        Assert.AreEqual("example.com", result.Parts!.Host);
    }

    [TestMethod]
    public void Parse_RealHandleWithoutExtendedSchemes_RefusesAnExtendedSchemeAsUnsupported()
    {
        McpParseUrlToolResult parsed = new McpUrlTool(new ManagedCurlEngine().CreateUrl, []).Parse("coap://example.com/", null);
        McpParseUrlToolResult original = new McpUrlTool(new ManagedCurlEngine().CreateUrl, []).Parse("https://example.com/", null);

        Assert.AreEqual(McpToolFailure.UrlRefused("url", CurlUrlCode.UnsupportedScheme), parsed.Failure);
        Assert.IsFalse(original.IsError);
    }

    [TestMethod]
    public void Parse_UrlWithNoColon_IsNotTakenForAnExtendedScheme()
    {
        McpParseUrlToolResult result = new McpUrlTool(new ManagedCurlEngine().CreateUrl, [""]).Parse("example.com", null);
        McpParseUrlToolResult plain = new McpUrlTool(new ManagedCurlEngine().CreateUrl, []).Parse("example.com", null);

        Assert.AreEqual(plain, result);
    }

    [TestMethod]
    [DataRow("coap")]
    [DataRow("https")]
    public void Build_RealHandleWithExtendedSchemes_BuildsExtendedAndOriginalSchemesWithNoFlag(string scheme)
    {
        McpBuildUrlToolResult result = new McpUrlTool(new ManagedCurlEngine().CreateUrl, ["coap"])
            .Build(new McpUrlParts(Scheme: scheme, Host: "example.com", Path: "/a"), null);

        Assert.IsFalse(result.IsError, result.Failure?.Text);
        Assert.AreEqual(scheme + "://example.com/a", result.Url);
    }

    [TestMethod]
    public void Build_RealHandleWithoutExtendedSchemes_RefusesAnExtendedSchemeAsUnsupported()
    {
        McpUrlTool tool = new(new ManagedCurlEngine().CreateUrl, []);

        McpBuildUrlToolResult extended = tool.Build(new McpUrlParts(Scheme: "coap", Host: "example.com"), null);
        McpBuildUrlToolResult original = tool.Build(new McpUrlParts(Scheme: "https", Host: "example.com"), null);

        Assert.AreEqual(McpToolFailure.UrlRefused("scheme", CurlUrlCode.UnsupportedScheme), extended.Failure);
        Assert.IsFalse(original.IsError);
    }

    [TestMethod]
    public void Build_RealHandle_RebuildsTheUrlFromItsParts()
    {
        McpUrlTool tool = new(new ManagedCurlEngine().CreateUrl);
        McpUrlParts parts = tool.Parse(RoundTripUrl, null).Parts!;

        McpBuildUrlToolResult result = tool.Build(parts, null);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(RoundTripUrl, result.Url);
    }

    private static FakeUrlHandle HandleWithEveryPart()
    {
        FakeUrlHandle handle = new();
        handle.Parts[CurlUrlPart.Url] = (CurlUrlCode.Ok, "whole");
        handle.Parts[CurlUrlPart.Scheme] = (CurlUrlCode.Ok, "s");
        handle.Parts[CurlUrlPart.User] = (CurlUrlCode.Ok, "u");
        handle.Parts[CurlUrlPart.Password] = (CurlUrlCode.Ok, "pw");
        handle.Parts[CurlUrlPart.Options] = (CurlUrlCode.Ok, "o");
        handle.Parts[CurlUrlPart.Host] = (CurlUrlCode.Ok, "h");
        handle.Parts[CurlUrlPart.Port] = (CurlUrlCode.Ok, "1");
        handle.Parts[CurlUrlPart.Path] = (CurlUrlCode.Ok, "/p");
        handle.Parts[CurlUrlPart.Query] = (CurlUrlCode.Ok, "q");
        handle.Parts[CurlUrlPart.Fragment] = (CurlUrlCode.Ok, "f");
        handle.Parts[CurlUrlPart.ZoneId] = (CurlUrlCode.Ok, "z");
        return handle;
    }
}
