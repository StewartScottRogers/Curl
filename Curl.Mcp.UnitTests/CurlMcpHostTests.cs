using System.Text;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class CurlMcpHostTests
{
    private static readonly McpResponseCap Cap = new(McpResponseCap.DefaultMaxBytes);

    [TestMethod]
    public void ServerInfo_IsTheAssemblyNameAndVersion()
    {
        Assert.AreEqual("Curl.Mcp", CurlMcpHost.ServerInfo.Name);
        Assert.AreEqual(typeof(CurlMcpHost).Assembly.GetName().Version!.ToString(), CurlMcpHost.ServerInfo.Version);
    }

    [TestMethod]
    public void CreateRegistry_RegistersExactlyTheFiveToolsInTheGoldenOrder()
    {
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, _ => null, _ => []);

        CollectionAssert.AreEqual(
            new[] { "parse_url", "transfer", "curl", "build_url", "version" },
            registry.ToolEntries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void CreateRegistry_RegistersEveryAiHelpResource()
    {
        McpServerRegistry registry = CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, _ => null, _ => []);

        CollectionAssert.AreEqual(
            McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Uri).Append("curl://ai-help/extensions").ToArray(),
            registry.Resources.Select(resource => resource.Uri).ToArray());
    }

    [TestMethod]
    public void CreateHandlers_AnswersTheToolResourceAndLoggingMethods()
    {
        IEnumerable<IMcpMethodHandler> handlers = CurlMcpHost.CreateHandlers(McpConfinementRoot.NotConfigured, Cap, _ => null, _ => { });

        CollectionAssert.AreEquivalent(
            new[] { "tools/list", "tools/call", "resources/list", "resources/read", "logging/setLevel" },
            handlers.Select(handler => handler.Method).ToArray());
    }

    [TestMethod]
    public async Task Build_ServesInitializeAndToolsListOnOutputAndLogsOnlyToError()
    {
        const string Requests =
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""" + "\n"
            + """{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n"
            + """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""" + "\n";
        using MemoryStream input = new(Encoding.UTF8.GetBytes(Requests));
        using MemoryStream output = new();
        using StringWriter error = new();
        string directory = Directory.GetCurrentDirectory();

        using McpStdioHost host = CurlMcpHost.Build(_ => null, input, output, error);
        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(directory, Directory.GetCurrentDirectory());
        string written = Encoding.UTF8.GetString(output.ToArray());
        StringAssert.Contains(written, "\"serverInfo\":{\"name\":\"Curl.Mcp\"");
        StringAssert.Contains(written, "\"name\":\"build_url\"");
        StringAssert.Contains(error.ToString(), "Curl.Mcp stopped.");
        Assert.IsFalse(written.Contains("Curl.Mcp stopped.", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Build_WithABadResponseCap_RefusesToServe()
    {
        using StringWriter error = new();
        using McpStdioHost host = CurlMcpHost.Build(name => name == McpResponseCap.VariableName ? "lots" : null, Stream.Null, Stream.Null, error);

        await Assert.ThrowsExactlyAsync<McpConfigurationException>(() => host.RunAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Build_OverTheProcess_ReadsTheResponseCapFromTheProcessEnvironment()
    {
        string? before = Environment.GetEnvironmentVariable(McpResponseCap.VariableName);
        Environment.SetEnvironmentVariable(McpResponseCap.VariableName, "lots");
        try
        {
            using McpStdioHost host = CurlMcpHost.Build();

            McpConfigurationException refused = await Assert.ThrowsExactlyAsync<McpConfigurationException>(() => host.RunAsync(CancellationToken.None));
            StringAssert.Contains(refused.Message, McpResponseCap.VariableName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpResponseCap.VariableName, before);
        }
    }

    [TestMethod]
    public void Build_OverTheProcessTwice_BuildsTwoSeparateHosts()
    {
        // The second call reuses the delegate the compiler cached for the environment reader on the first.
        using McpStdioHost first = CurlMcpHost.Build();

        using McpStdioHost second = CurlMcpHost.Build();

        Assert.AreNotSame(first, second);
    }
}
