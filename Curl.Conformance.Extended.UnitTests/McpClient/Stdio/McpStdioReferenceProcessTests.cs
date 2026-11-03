using System.Text;
using Curl.Conformance.Extended.McpClient;
using Curl.Conformance.Extended.McpClient.Stdio;

namespace Curl.Conformance.Extended.UnitTests.McpClient.Stdio;

/// <summary>Tests of the stdio stand-in server process itself, apart from curl.</summary>
[TestClass]
public sealed class McpStdioReferenceProcessTests
{
    [TestMethod]
    public async Task Kill_WhileWaitingForInput_ExitsEarlyWithoutClosingInput()
    {
        McpStdioReferenceProcessStarter starter = new(new McpReferenceToolServer([]), new McpStdioServerBehaviour());
        using McpStdioReferenceProcess process = (McpStdioReferenceProcess)starter.Start("server", []);

        process.Kill();

        Assert.AreEqual(McpStdioReferenceProcess.ExitedEarlyCode, await process.WaitForExitAsync(CancellationToken.None));
        Assert.IsTrue(process.Killed);
        Assert.IsFalse(process.StandardInputClosed);
        Assert.IsNull(starter.ReadEnvironmentVariable("PATH"));
        Assert.AreSame(process, starter.Started.Single());
    }

    [TestMethod]
    public async Task PartialLine_IsKeptUntilItsNewlineArrives()
    {
        using McpStdioReferenceProcess process = new(new McpReferenceToolServer([]), new McpStdioServerBehaviour(), "server", []);
        byte[] message = Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        await process.StandardInput.WriteAsync(message.AsMemory(0, 10));
        await process.StandardInput.FlushAsync();
        await process.StandardInput.WriteAsync(message.AsMemory(10));
        await process.StandardInput.WriteAsync("\n"u8.ToArray());
        await process.StandardInput.DisposeAsync();

        Assert.AreEqual(0, await process.WaitForExitAsync(CancellationToken.None));
        CollectionAssert.AreEqual(message.Concat("\n"u8.ToArray()).ToArray(), process.StandardInputBytes);
        Assert.AreEqual(-1, process.StandardOutput.ReadByte());
    }

    [TestMethod]
    public void Construct_WithoutServerOrBehaviour_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpStdioReferenceProcess(null!, new McpStdioServerBehaviour(), "s", []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpStdioReferenceProcess(new McpReferenceToolServer([]), null!, "s", []));
    }
}
