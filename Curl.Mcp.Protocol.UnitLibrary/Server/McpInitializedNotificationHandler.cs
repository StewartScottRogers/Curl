using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>
/// Accepts <c>notifications/initialized</c>. The server answers every request before and after it
/// alike, as the SDK 2.2.0 server did, so the notification changes nothing.
/// </summary>
public sealed class McpInitializedNotificationHandler : IMcpMethodHandler
{
    /// <inheritdoc />
    public string Method => "notifications/initialized";

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken) => ValueTask.FromResult(default(JsonElement));
}
