using System.Text.Json;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp.Protocol.Tools;

/// <summary>
/// Answers <c>tools/list</c> with every tool of the registry in the order it was added, on one page: a
/// <c>cursor</c> param is accepted and ignored, as the SDK 2.2.0 server did. Brings the <c>tools</c>
/// capability, <c>{"listChanged":true}</c> as the SDK server announced it.
/// </summary>
public sealed class ToolsListHandler : IMcpMethodHandler, IMcpCapabilityContributor
{
    private static readonly JsonElement ToolsCapability = JsonDocument.Parse("""{"listChanged":true}""").RootElement.Clone();

    private readonly McpServerRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="ToolsListHandler" /> class.</summary>
    /// <param name="registry">The registry whose tools are listed.</param>
    public ToolsListHandler(McpServerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public string Method => "tools/list";

    /// <inheritdoc />
    public string CapabilityName => "tools";

    /// <inheritdoc />
    public JsonElement Capability => ToolsCapability;

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken) =>
        ValueTask.FromResult(JsonSerializer.SerializeToElement(new McpToolListResult(_registry.ToolEntries), McpModelJsonContext.Default.McpToolListResult));
}
