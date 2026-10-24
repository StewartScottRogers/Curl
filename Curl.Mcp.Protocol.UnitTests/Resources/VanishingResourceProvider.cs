using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Protocol.Resources;

/// <summary>A resource provider that lists one resource but finds nothing when it is read.</summary>
internal sealed class VanishingResourceProvider(string uri) : IMcpResourceProvider
{
    public IReadOnlyList<McpResource> ListResources() => [new McpResource("gone", uri, null, null)];

    public ValueTask<McpResourceContents?> ReadAsync(string uri, CancellationToken cancellationToken) => ValueTask.FromResult<McpResourceContents?>(null);
}
