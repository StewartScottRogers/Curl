namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>Checks that an MCP server started through the host's process starter (ADR-1183) is that process, seen as a server.</summary>
[TestClass]
public sealed class ExtensionMcpServerProcessStarterTests
{
    [TestMethod]
    public async Task Start_GivesTheHostsProcessWithTheCommandAndArguments()
    {
        StandInExtensionProcess process = new(string.Empty);
        StandInExtensionProcessStarter starter = new(process);

        using IMcpServerProcess server = new ExtensionMcpServerProcessStarter(starter).Start("npx", ["-y", "server"]);
        await server.WaitForExitAsync(CancellationToken.None);

        Assert.AreEqual("npx", starter.StartedProgram);
        CollectionAssert.AreEqual(new[] { "-y", "server" }, starter.StartedArguments!.ToArray());
        Assert.AreSame(process.StandardInput, server.StandardInput);
        Assert.AreSame(process.StandardOutput, server.StandardOutput);
    }

    [TestMethod]
    public void KillAndDispose_EndAndReleaseTheHostsProcess()
    {
        StandInExtensionProcess process = new(string.Empty);
        IMcpServerProcess server = new ExtensionMcpServerProcessStarter(new StandInExtensionProcessStarter(process)).Start("server", []);

        server.Kill();
        server.Dispose();

        Assert.IsTrue(process.IsKilled);
        Assert.IsTrue(process.IsDisposed);
    }

    [TestMethod]
    public void Start_WhenTheHostCannotStartIt_ThrowsInvalidOperationWithTheHostsMessage()
    {
        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(() => new ExtensionMcpServerProcessStarter(new StandInExtensionProcessStarter(null)).Start("missing", []));

        Assert.AreEqual("The system cannot find the file specified.", failure.Message);
        Assert.IsInstanceOfType<IOException>(failure.InnerException);
    }
}
