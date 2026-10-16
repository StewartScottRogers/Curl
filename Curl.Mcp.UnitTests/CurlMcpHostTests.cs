using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class CurlMcpHostTests
{
    [TestMethod]
    public void Build_RegistersExactlyTheCurlToolWithItsDescriptionAndArgumentSchema()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);

        McpServerTool tool = host.Services.GetServices<McpServerTool>().Single();
        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        Assert.AreEqual(1, options.ToolCollection?.Count ?? 1);
        Assert.AreEqual("curl", tool.ProtocolTool.Name);
        Assert.AreEqual(CurlCommandTool.ToolDescription, tool.ProtocolTool.Description);
        StringAssert.Contains(tool.ProtocolTool.Description, "without the program name");
        StringAssert.Contains(tool.ProtocolTool.Description, "CURL_MCP_ROOT");
        StringAssert.Contains(tool.ProtocolTool.Description, "--ai-help");

        JsonElement schema = tool.ProtocolTool.InputSchema;
        JsonElement properties = schema.GetProperty("properties");
        Assert.AreEqual("array", properties.GetProperty("args").GetProperty("type").GetString());
        Assert.AreEqual("string", properties.GetProperty("args").GetProperty("items").GetProperty("type").GetString());
        Assert.AreEqual(2, properties.EnumerateObject().Count());
        StringAssert.Contains(properties.GetProperty("stdin").GetProperty("type").GetRawText(), "string");
        CollectionAssert.AreEqual(new[] { "args" }, schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ToArray());
    }

    [TestMethod]
    public void Build_WithNoRoot_LeavesTheCurrentDirectoryAsItWas()
    {
        string before = Directory.GetCurrentDirectory();

        using IHost host = CurlMcpHost.Build([], _ => null);

        Assert.AreEqual(before, Directory.GetCurrentDirectory());
    }

    [TestMethod]
    public void Build_WithARoot_MakesTheResolvedRootTheCurrentDirectory()
    {
        string before = Directory.GetCurrentDirectory();
        string root = Directory.CreateTempSubdirectory("curl-mcp-host-").FullName;
        try
        {
            using IHost host = CurlMcpHost.Build([], name => name == McpConfinementRoot.VariableName ? root : null);

            Assert.AreEqual(
                Path.TrimEndingDirectorySeparator(new McpConfinementRoot(root).ResolvedRoot!),
                Path.TrimEndingDirectorySeparator(Directory.GetCurrentDirectory()));
        }
        finally
        {
            Directory.SetCurrentDirectory(before);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Build_WithABadResponseCap_RefusesToBuild()
    {
        Assert.ThrowsExactly<McpConfigurationException>(() => CurlMcpHost.Build([], name => name == McpResponseCap.VariableName ? "lots" : null));
    }

    [TestMethod]
    public void Build_OverTheProcessEnvironment_BuildsAHost()
    {
        using IHost host = CurlMcpHost.Build([]);

        Assert.IsNotNull(host.Services.GetRequiredService<McpCurlTool>());
    }

    [TestMethod]
    public void Build_SendsEveryLogLineToStandardErrorAndNothingToStandardOutput()
    {
        TextWriter originalOut = System.Console.Out;
        TextWriter originalError = System.Console.Error;
        using StringWriter standardOutput = new();
        using StringWriter standardError = new();
        System.Console.SetOut(standardOutput);
        System.Console.SetError(standardError);
        try
        {
            using (IHost host = CurlMcpHost.Build([], _ => null))
            {
                ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Curl.Mcp.Test");
                logger.LogInformation("information line");
                logger.LogCritical("critical line");
            }

            Assert.AreEqual(string.Empty, standardOutput.ToString());
            StringAssert.Contains(standardError.ToString(), "information line");
            StringAssert.Contains(standardError.ToString(), "critical line");
        }
        finally
        {
            System.Console.SetOut(originalOut);
            System.Console.SetError(originalError);
        }
    }
}
