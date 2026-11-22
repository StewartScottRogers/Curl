using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

/// <summary>Runs curl in process against real sockets: output into a missing folder after a connection, and a refused connection (BL-5761, BL-5767).</summary>
[TestClass]
[DoNotParallelize]
[TestCategory("Integration")]
public sealed class InProcessCurlCommandRunnerIntegrationTests
{
    [TestMethod]
    [DataRow("-o", "nosuch-folder-bl5761/msg#1.eml", "pop3://127.0.0.1:1/[1-3]")]
    public async Task RunAsync_RefusedConnectionWithGlobbedOutput_GivesAnExitCodeWithoutThrowing(string first, string second, string third)
    {
        using MemoryStream standardError = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync([first, second, third], new MemoryStream(), new MemoryStream(), standardError, null, CancellationToken.None);

        Assert.AreNotEqual(0, exitCode);
        Assert.IsGreaterThan(0, standardError.Length);
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task RunAsync_GlobbedOutputIntoAMissingFolderAfterAConnection_GivesCurlsExitCodeWithoutThrowing()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    _ = await LoopbackHttpResponder.ServeOneResponseAsync(listener, "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nhi");
                }
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
            }
        });
        using MemoryStream standardError = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["-sS", "-o", "nosuch-folder-bl5761/msg#1.eml", $"http://127.0.0.1:{port}/[1-3]"], new MemoryStream(), new MemoryStream(), standardError, null, CancellationToken.None);

        Assert.AreEqual(23, exitCode);
        Assert.IsGreaterThan(0, standardError.Length);
        listener.Stop();
        await server;
    }

    [TestMethod]
    [DataRow("nosuch/msg#1.eml", "pop3://127.0.0.1:1/[1-3]")]
    [DataRow("nosuch/out.bin", "http://127.0.0.1:1/")]
    public async Task CallAsync_OutputIntoAMissingFolder_GivesAResultWithAnExitCodeNotTheGenericError(string output, string url)
    {
        string root = Directory.CreateTempSubdirectory("mcp-missing-folder-").FullName;
        string previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            CurlCommandMcpTool tool = new(new McpCurlTool(new InProcessCurlCommandRunner(), new McpConfinementRoot(root), new McpResponseCap(McpResponseCap.DefaultMaxBytes)));
            JsonElement arguments = JsonSerializer.SerializeToElement(new Dictionary<string, string[]> { ["args"] = ["-sS", "-u", "bob:pw", "-o", output, url] });

            Curl.Mcp.Protocol.Model.McpToolCallResult result = await tool.CallAsync(arguments, CancellationToken.None);

            string text = JsonSerializer.Serialize(result);
            Assert.IsFalse(text.Contains("An error occurred invoking", StringComparison.Ordinal), text);
            StringAssert.Contains(text, "exitCode");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(root, recursive: true);
        }
    }
}
