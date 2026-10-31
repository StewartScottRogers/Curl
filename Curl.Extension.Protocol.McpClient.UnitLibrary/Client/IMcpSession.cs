using Curl.Extension.Protocol.McpClient.Wire;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>One MCP session over either transport, as <see cref="McpClientFlow"/> drives it.</summary>
internal interface IMcpSession : IAsyncDisposable
{
    /// <summary>Sends <paramref name="request"/> and returns the server's response to it.</summary>
    Task<McpResponse> SendRequestAsync(McpOutgoingRequest request, CancellationToken cancellationToken);

    /// <summary>Sends a notification, which has no response.</summary>
    Task SendNotificationAsync(byte[] message, CancellationToken cancellationToken);

    /// <summary>Takes the protocol version the server chose in <c>initialize</c>, for a transport that sends it on later messages.</summary>
    void UseProtocolVersion(string protocolVersion);

    /// <summary>Ends the session as its transport's "Lifecycle &gt; Shutdown" says.</summary>
    Task EndAsync(CancellationToken cancellationToken);
}
