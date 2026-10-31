namespace Curl.Extension.Protocol.McpClient.Stdio;

/// <summary>Starts a local MCP server as a subprocess ("Base Protocol &gt; Transports &gt; stdio").</summary>
public interface IMcpServerProcessStarter
{
    /// <summary>Starts <paramref name="command"/> with <paramref name="arguments"/>, its standard input and output redirected to the client.</summary>
    IMcpServerProcess Start(string command, IReadOnlyList<string> arguments);
}
