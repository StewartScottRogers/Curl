namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// A running local MCP server process seen through its standard streams (Model Context Protocol
/// 2025-06-18, "Base Protocol &gt; Transports &gt; stdio"). Its standard error is the server's log
/// and is not part of this seam.
/// </summary>
public interface IMcpServerProcess : IDisposable
{
    /// <summary>The server's standard input, which the client writes messages to.</summary>
    Stream StandardInput { get; }

    /// <summary>The server's standard output, which the client reads messages from.</summary>
    Stream StandardOutput { get; }

    /// <summary>Completes when the process has exited.</summary>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Ends the process at once, with its child processes.</summary>
    void Kill();
}
