using System.ComponentModel;
using Curl.Extension.McpClient.Stdio;
using Curl.Extension.McpClient.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.McpClient.Client;

/// <summary>An MCP session over stdio with the local server an <c>mcp+stdio://</c> URL names (<see cref="McpStdioCommand"/>).</summary>
/// <param name="session">The stdio session with the started server.</param>
internal sealed class McpStdioClientSession(McpStdioSession session) : IMcpSession
{
    /// <summary>Starts the server <paramref name="command"/> names.</summary>
    /// <exception cref="McpClientException">The server could not be started: exit 7, as when curl cannot reach a server.</exception>
    public static McpStdioClientSession Start(IMcpServerProcessStarter starter, McpStdioCommand command, TimeProvider timeProvider)
    {
        IMcpServerProcess process;
        try
        {
            process = starter.Start(command.FileName, command.Arguments);
        }
        catch (Exception failure) when (failure is Win32Exception or InvalidOperationException)
        {
            throw new McpClientException(CurlExitCode.CouldntConnect, $"Failed to start the MCP server {command.FileName}: {failure.Message}");
        }

        return new McpStdioClientSession(new McpStdioSession(process, timeProvider));
    }

    /// <inheritdoc />
    public Task<McpResponse> SendRequestAsync(McpOutgoingRequest request, CancellationToken cancellationToken) =>
        session.SendRequestAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task SendNotificationAsync(byte[] message, CancellationToken cancellationToken) =>
        session.SendMessageAsync(message, cancellationToken);

    /// <summary>Does nothing: the stdio transport sends no protocol version header.</summary>
    public void UseProtocolVersion(string protocolVersion)
    {
    }

    /// <summary>Closes the server's standard input and waits for it to exit, killing it after the grace period.</summary>
    public Task EndAsync(CancellationToken cancellationToken) => session.EndAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => session.DisposeAsync();
}
