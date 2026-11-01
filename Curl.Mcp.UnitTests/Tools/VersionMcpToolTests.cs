using System.Text.Json;
using Curl.Api.EngineContracts;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

[TestClass]
public sealed class VersionMcpToolTests
{
    private static readonly CurlVersionInfo Info = new() { Version = "9.1.2", Protocols = ["file", "http"], Features = ["IPv6", "SSL"], TlsBackend = "OpenSSL/3.5.0" };

    [TestMethod]
    public void Description_NameAndAnnotations_AreTheGoldenOnesAndSoIsTheSchema()
    {
        VersionMcpTool tool = Tool();

        Assert.AreEqual("version", tool.Name);
        StringAssert.StartsWith(tool.Description, "Reports what `curl -V` reports about Curl's transfer engine");
        StringAssert.EndsWith(tool.Description, "Takes no arguments.");
        StringAssert.Contains(tool.Description, "`extendedProtocols`");
        Assert.AreEqual(true, tool.Annotations!.IdempotentHint);
        Assert.AreEqual(true, tool.Annotations.ReadOnlyHint);
        using JsonDocument schema = JsonDocument.Parse(tool.InputSchemaJson);
        using JsonDocument golden = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        Assert.IsTrue(JsonElement.DeepEquals(golden.RootElement, schema.RootElement));
    }

    [TestMethod]
    public async Task CallAsync_FakeEngine_ReturnsTheReportAsJsonTextThatIsNotAnError()
    {
        McpToolCallResult result = await Tool().CallAsync(Arguments("{}"), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        using JsonDocument json = JsonDocument.Parse(result.Content.Single().Text);
        Assert.AreEqual("9.1.2", json.RootElement.GetProperty("version").GetString());
        CollectionAssert.AreEqual(new[] { "file", "http" }, json.RootElement.GetProperty("protocols").EnumerateArray().Select(item => item.GetString()).ToArray());
        CollectionAssert.AreEqual(new[] { "coap", "redis" }, json.RootElement.GetProperty("extendedProtocols").EnumerateArray().Select(item => item.GetString()).ToArray());
        CollectionAssert.AreEqual(new[] { "IPv6", "SSL" }, json.RootElement.GetProperty("features").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual("OpenSSL/3.5.0", json.RootElement.GetProperty("tlsBackend").GetString());
        StringAssert.StartsWith(json.RootElement.GetProperty("versionLine").GetString(), "curl ");
    }

    [TestMethod]
    public async Task CallAsync_ArgumentsSupplied_AreIgnored()
    {
        McpToolCallResult result = await Tool().CallAsync(Arguments("""{"unexpected":1}"""), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual((await Tool().CallAsync(Arguments("{}"), CancellationToken.None)).Content.Single().Text, result.Content.Single().Text);
    }

    private static FakeTransferEngine Engine() => new(new FakeEasyTransfer()) { VersionInfoToReport = Info };

    private static VersionMcpTool Tool() => new(new McpVersionTool(Engine(), ["coap", "redis"]));

    private static JsonElement Arguments(string arguments)
    {
        using JsonDocument document = JsonDocument.Parse(arguments);
        return document.RootElement.Clone();
    }
}
