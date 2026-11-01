using System.Text;
using Curl.Api.EngineContracts;
using Curl.Cli;
using Curl.Engine;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class McpVersionToolTests
{
    [TestMethod]
    public void Report_FakeEngine_PassesTheVersionProtocolsFeaturesAndTlsBackendThroughUnchanged()
    {
        CurlVersionInfo info = new()
        {
            Version = "9.1.2",
            Protocols = ["dict", "file", "http", "https", "ws"],
            Features = ["alt-svc", "AsynchDNS", "IPv6", "SSL"],
            TlsBackend = "Schannel",
        };
        McpVersionTool tool = new(new FakeTransferEngine(new FakeEasyTransfer()) { VersionInfoToReport = info }, []);

        McpVersionToolResult result = tool.Report();

        Assert.AreEqual("9.1.2", result.Version);
        CollectionAssert.AreEqual(new[] { "dict", "file", "http", "https", "ws" }, result.Protocols.ToArray());
        CollectionAssert.AreEqual(new[] { "alt-svc", "AsynchDNS", "IPv6", "SSL" }, result.Features.ToArray());
        Assert.AreEqual("Schannel", result.TlsBackend);
    }

    [TestMethod]
    public void Report_FakeEngineWithNoTls_ReportsNoTlsBackend()
    {
        McpVersionTool tool = new(new FakeTransferEngine(new FakeEasyTransfer()) { VersionInfoToReport = new CurlVersionInfo() }, []);

        Assert.IsNull(tool.Report().TlsBackend);
    }

    [TestMethod]
    public void Report_VersionLine_IsThePlatformsFirstCurlVersionLine()
    {
        McpVersionTool tool = new(new FakeTransferEngine(new FakeEasyTransfer()) { VersionInfoToReport = new CurlVersionInfo() }, []);

        Assert.AreEqual(CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())[0], tool.Report().VersionLine);
    }

    [TestMethod]
    public async Task Report_RealEngine_VersionAndVersionLineMatchWhatCurlVersionPrints()
    {
        using MemoryStream standardOutput = new();
        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["--version"], new MemoryStream(), standardOutput, new MemoryStream(), null, CancellationToken.None);
        string firstLine = Encoding.UTF8.GetString(standardOutput.ToArray()).Split('\n')[0].TrimEnd('\r');
        await using ManagedCurlEngine engine = new();

        McpVersionToolResult result = new McpVersionTool(engine, []).Report();

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(firstLine.Split(' ')[1], result.Version);
        Assert.AreEqual(firstLine, result.VersionLine);
    }
}
