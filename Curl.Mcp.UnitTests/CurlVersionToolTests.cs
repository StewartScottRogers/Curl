using System.Text.Json;
using Curl.Api.EngineContracts;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp;

[TestClass]
public sealed class CurlVersionToolTests
{
    [TestMethod]
    public void Version_FakeEngine_ReturnsTheReportAsJsonTextThatIsNotAnError()
    {
        CurlVersionInfo info = new() { Version = "9.1.2", Protocols = ["file", "http"], Features = ["IPv6", "SSL"], TlsBackend = "OpenSSL/3.5.0" };
        CurlVersionTool tool = new(new McpVersionTool(new FakeTransferEngine(new FakeEasyTransfer()) { VersionInfoToReport = info }));

        CallToolResult result = tool.Version();

        Assert.IsFalse(result.IsError);
        using JsonDocument json = JsonDocument.Parse(((TextContentBlock)result.Content.Single()).Text);
        Assert.AreEqual("9.1.2", json.RootElement.GetProperty("version").GetString());
        CollectionAssert.AreEqual(new[] { "file", "http" }, json.RootElement.GetProperty("protocols").EnumerateArray().Select(item => item.GetString()).ToArray());
        CollectionAssert.AreEqual(new[] { "IPv6", "SSL" }, json.RootElement.GetProperty("features").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual("OpenSSL/3.5.0", json.RootElement.GetProperty("tlsBackend").GetString());
        StringAssert.StartsWith(json.RootElement.GetProperty("versionLine").GetString(), "curl ");
    }
}
