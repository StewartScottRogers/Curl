using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Protocol.Resources;

/// <summary>A resource provider whose resources each read as <c>text of &lt;uri&gt;</c>; it records the URIs read.</summary>
internal sealed class FakeResourceProvider(params McpResource[] resources) : IMcpResourceProvider
{
    public List<string> UrisRead { get; } = [];

    public IReadOnlyList<McpResource> ListResources() => resources;

    public ValueTask<McpResourceContents?> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        UrisRead.Add(uri);
        McpResource? resource = Array.Find(resources, candidate => candidate.Uri == uri);
        return ValueTask.FromResult(resource is null ? null : new McpResourceContents(uri, resource.MimeType, $"text of {uri}"));
    }
}
