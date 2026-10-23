using System.Text.Json;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp.Protocol.Resources;

/// <summary>
/// Answers <c>resources/list</c> with every resource of the registered <see cref="IMcpResourceProvider" />s, in the
/// order they were added, and brings the <c>resources</c> capability (<c>{}</c>, as the SDK server's answer carries).
/// The request's <c>cursor</c> is ignored: the list always fits one page.
/// </summary>
public sealed class ResourcesListHandler : IMcpMethodHandler, IMcpCapabilityContributor
{
    private static readonly JsonElement EmptyCapability = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly McpServerRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="ResourcesListHandler" /> class.</summary>
    /// <param name="registry">The registry whose resources are listed.</param>
    public ResourcesListHandler(McpServerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public string Method => "resources/list";

    /// <inheritdoc />
    public string CapabilityName => "resources";

    /// <inheritdoc />
    public JsonElement Capability => EmptyCapability;

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken) =>
        ValueTask.FromResult(JsonSerializer.SerializeToElement(new McpResourceListResult(_registry.Resources), McpModelJsonContext.Default.McpResourceListResult));
}
