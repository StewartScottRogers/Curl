using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>Answers one MCP method: a request, whose result <see cref="McpServer" /> sends back, or a notification, whose result it discards.</summary>
public interface IMcpMethodHandler
{
    /// <summary>Gets the JSON-RPC method name this handler answers, e.g. <c>tools/list</c>.</summary>
    string Method { get; }

    /// <summary>Handles one call of <see cref="Method" />.</summary>
    /// <param name="parameters">The call's <c>params</c>, or <see langword="null" /> when it has none.</param>
    /// <param name="cancellationToken">Cancelled by <c>notifications/cancelled</c> for the request, by the transport's end or by the host.</param>
    /// <returns>The JSON-RPC <c>result</c>; a notification handler returns <see langword="default" />.</returns>
    /// <exception cref="McpProtocolException">The call fails with a JSON-RPC error code, e.g. -32602 for invalid params.</exception>
    ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken);
}
