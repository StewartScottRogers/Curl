using System.Text.Json;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp;

/// <summary>
/// Advertises the <c>logging</c> capability, as the SDK host this server replaced did, and answers
/// <c>logging/setLevel</c> with an empty result. The server sends no log notifications at any level, so the
/// level asked for changes nothing; log lines go only to standard error (BL-3388).
/// </summary>
internal sealed class LoggingSetLevelMcpHandler : IMcpMethodHandler, IMcpCapabilityContributor
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// <inheritdoc />
    public string Method => "logging/setLevel";

    /// <inheritdoc />
    public string CapabilityName => "logging";

    /// <inheritdoc />
    public JsonElement Capability => EmptyObject;

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken) => ValueTask.FromResult(EmptyObject);
}
