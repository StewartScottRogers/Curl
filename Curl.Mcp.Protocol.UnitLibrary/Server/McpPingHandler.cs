using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>Answers <c>ping</c> with an empty result.</summary>
public sealed class McpPingHandler : IMcpMethodHandler
{
    private static readonly JsonElement EmptyResult = JsonDocument.Parse("{}").RootElement.Clone();

    /// <inheritdoc />
    public string Method => "ping";

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken) => ValueTask.FromResult(EmptyResult);
}
