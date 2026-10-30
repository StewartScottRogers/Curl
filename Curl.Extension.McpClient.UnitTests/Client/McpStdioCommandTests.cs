using Curl.Protocol.Abstractions;

namespace Curl.Extension.McpClient.Client;

/// <summary>Checks how an <c>mcp+stdio</c> URL names the local program and its arguments (ADR-1074).</summary>
[TestClass]
public sealed class McpStdioCommandTests
{
    [TestMethod]
    [DataRow("mcp+stdio://localhost/npx", "npx")]
    [DataRow("mcp+stdio://localhost/my%20server", "my server")]
    [DataRow("mcp+stdio://localhost/usr/local/bin/server", "/usr/local/bin/server")]
    [DataRow("mcp+stdio://localhost/C:/servers/x.exe", "C:/servers/x.exe")]
    [DataRow("mcp+stdio://localhost/C/x", "/C/x")]
    [DataRow("mcp+stdio://localhost/a", "a")]
    [DataRow("mcp+stdio://localhost/1/x", "/1/x")]
    [DataRow("mcp+stdio://localhost//", "//")]
    public void From_Path_GivesTheProgram(string url, string fileName)
    {
        var command = McpStdioCommand.From(CurlUrl.Parse(url));

        Assert.AreEqual(fileName, command!.FileName);
        Assert.IsEmpty(command.Arguments);
    }

    [TestMethod]
    [DataRow("mcp+stdio://localhost/x?", 0)]
    [DataRow("mcp+stdio://localhost/x?a", 1)]
    [DataRow("mcp+stdio://localhost/x?a&&b", 3)]
    public void From_Query_GivesOneArgumentPerPart(string url, int count) =>
        Assert.HasCount(count, McpStdioCommand.From(CurlUrl.Parse(url))!.Arguments);

    [TestMethod]
    [DataRow("mcp+stdio://example.com/x")]
    [DataRow("mcp+stdio://localhost/")]
    [DataRow("mcp+stdio:///npx")]
    public void From_RemoteHostOrNoProgram_GivesNull(string url) =>
        Assert.IsNull(McpStdioCommand.From(CurlUrl.Parse(url)));
}
