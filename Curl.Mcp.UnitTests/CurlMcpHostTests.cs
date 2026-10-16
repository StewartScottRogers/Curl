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
    public void Build_RegistersAnMcpServerThatListsZeroTools()
    {
        using IHost host = CurlMcpHost.Build([]);

        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;

        Assert.AreEqual(0, options.ToolCollection?.Count ?? 0);
        Assert.IsEmpty(host.Services.GetServices<McpServerTool>());
    }

    [TestMethod]
    public void Build_SendsEveryLogLineToStandardErrorAndNothingToStandardOutput()
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter standardOutput = new();
        using StringWriter standardError = new();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);
        try
        {
            using (IHost host = CurlMcpHost.Build([]))
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
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
