using System.Diagnostics;
using Curl.Extension.Protocol.McpClient.Wire;

namespace Curl.Extension.Protocol.McpClient.Stdio;

/// <summary>
/// Starts a real MCP server child process through <see cref="McpServerProcessStarter"/> and drives it
/// with <see cref="McpStdioSession"/> (Model Context Protocol 2025-06-18, "Base Protocol &gt;
/// Transports &gt; stdio" and "Lifecycle &gt; Shutdown &gt; stdio").
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class McpServerProcessStarterIntegrationTests
{
    private static readonly TimeSpan ProcessGoneTimeout = TimeSpan.FromSeconds(30);

    private static EchoMcpServer? echoServer;

    [ClassInitialize]
    public static async Task BuildEchoServerAsync(TestContext context)
    {
        _ = context;
        echoServer = await EchoMcpServer.BuildAsync();
    }

    [ClassCleanup]
    public static void DeleteEchoServer() => echoServer?.Dispose();

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SendRequestAsync_RealServer_ReceivesItsResponse()
    {
        // "Transports > stdio": the server reads JSON-RPC from its stdin and writes its answers to stdout.
        await using var session = new McpStdioSession(StartEchoServer(EchoMcpServer.ExitOnEndOfInputArgument), TimeProvider.System);
        var request = new McpRequestWriter().WriteListTools(cursor: null);

        var response = await session.SendRequestAsync(request, CancellationToken.None);

        Assert.AreEqual(request.Id, response.Id);
        Assert.IsNull(response.Error);
        Assert.IsGreaterThan(0, response.Result!.Value.GetProperty("processId").GetInt32());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task EndAsync_ServerExitsOnEndOfInput_EndsItWithoutKillingIt()
    {
        // "Lifecycle > Shutdown > stdio": the client closes the server's input stream and waits for it to exit.
        var process = new KillCountingMcpServerProcess(StartEchoServer(EchoMcpServer.ExitOnEndOfInputArgument));
        var session = new McpStdioSession(process, TimeProvider.System);
        var processId = await ReadServerProcessIdAsync(session);

        await session.EndAsync();

        Assert.AreEqual(0, process.KillCount);
        await AssertProcessGoneAsync(processId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task EndAsync_ServerIgnoresEndOfInput_KillsItAfterTheGracePeriod()
    {
        // "Lifecycle > Shutdown > stdio": a server that does not exit in time is killed.
        var process = new KillCountingMcpServerProcess(StartEchoServer());
        var session = new McpStdioSession(process, TimeProvider.System);
        var processId = await ReadServerProcessIdAsync(session);

        await session.EndAsync();

        Assert.AreEqual(1, process.KillCount);
        await AssertProcessGoneAsync(processId);
    }

    private static IMcpServerProcess StartEchoServer(params string[] serverArguments) =>
        new McpServerProcessStarter().Start(EchoMcpServer.DotnetHost, [echoServer!.AssemblyPath, .. serverArguments]);

    private static async Task<int> ReadServerProcessIdAsync(McpStdioSession session)
    {
        var response = await session.SendRequestAsync(new McpRequestWriter().WriteListTools(cursor: null), CancellationToken.None);
        return response.Result!.Value.GetProperty("processId").GetInt32();
    }

    private static async Task AssertProcessGoneAsync(int processId)
    {
        Process server;
        try
        {
            server = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (server)
        {
            using var timeout = new CancellationTokenSource(ProcessGoneTimeout);
            await server.WaitForExitAsync(timeout.Token);
            Assert.IsTrue(server.HasExited);
        }
    }
}
