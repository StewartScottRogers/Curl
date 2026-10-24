using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>Handles <c>notifications/cancelled</c> by cancelling the request in flight its <c>requestId</c> names; a request it cancels gets no response.</summary>
public sealed class McpCancelledNotificationHandler : IMcpMethodHandler
{
    private readonly McpRequestTracker _tracker;

    /// <summary>Initializes a new instance of the <see cref="McpCancelledNotificationHandler" /> class.</summary>
    /// <param name="tracker">The server's requests in flight.</param>
    public McpCancelledNotificationHandler(McpRequestTracker tracker) => _tracker = tracker;

    /// <inheritdoc />
    public string Method => "notifications/cancelled";

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (parameters is { ValueKind: JsonValueKind.Object } value
            && value.TryGetProperty("requestId", out JsonElement requestId)
            && requestId.ValueKind is JsonValueKind.String or JsonValueKind.Number)
        {
            _tracker.Cancel(requestId.GetRawText());
        }

        return ValueTask.FromResult(default(JsonElement));
    }
}
