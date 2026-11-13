using Curl.Extended.Protocol.McpClient.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.McpClient.Stdio;

[TestClass]
public sealed class McpBatchArgumentRefusalTests
{
    [TestMethod]
    [DataRow("srv.cmd", "a&b")]
    [DataRow("srv.CMD", "a|b")]
    [DataRow("srv.bat", "a<b")]
    [DataRow("srv.BAT", "a>b")]
    [DataRow("srv.cmd", "a^b")]
    [DataRow("srv.cmd", "a\"b")]
    [DataRow("srv.cmd", "a%b")]
    [DataRow("srv.cmd", "a\u0001b")]
    [DataRow("srv.cmd", "a\nb")]
    [DataRow("srv.cmd", "a&echo.>pwn_mcp1.txt")]
    [DataRow("srv.cmd", "a\"&echo.>pwn_mcp2.txt&\"")]
    [DataRow("srv.cmd", "%PATH%")]
    [DataRow("C:/t/mcpsrv/srv.cmd", "a&echo.>pwn_mcp3.txt")]
    public void Start_UnsafeArgumentToBatchFile_ExitsSevenBeforeAnyProcessStarts(string program, string argument)
    {
        var starter = new ScriptedMcpServerProcessStarter(new InMemoryMcpServerProcess(string.Empty));
        var command = new McpStdioCommand(program, ["ok", argument]);

        var failure = Assert.ThrowsExactly<McpClientException>(() => McpStdioClientSession.Start(starter, command, TimeProvider.System));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, $"Failed to start the MCP server {program}: ");
        Assert.IsNull(starter.Command);
    }

    [TestMethod]
    public void Start_PlainArgumentsToBatchFile_ArePassedUnchanged()
    {
        var starter = new ScriptedMcpServerProcessStarter(new InMemoryMcpServerProcess(string.Empty));

        _ = McpStdioClientSession.Start(starter, new McpStdioCommand("srv.cmd", ["plain", "args"]), TimeProvider.System);

        CollectionAssert.AreEqual(new[] { "plain", "args" }, starter.Arguments!.ToArray());
    }

    [TestMethod]
    [DataRow("srv.exe")]
    [DataRow("srv")]
    public void Start_UnsafeArgumentToOtherProgram_IsPassedUnchanged(string program)
    {
        var starter = new ScriptedMcpServerProcessStarter(new InMemoryMcpServerProcess(string.Empty));

        _ = McpStdioClientSession.Start(starter, new McpStdioCommand(program, ["a&b", "%PATH%"]), TimeProvider.System);

        CollectionAssert.AreEqual(new[] { "a&b", "%PATH%" }, starter.Arguments!.ToArray());
    }
}
