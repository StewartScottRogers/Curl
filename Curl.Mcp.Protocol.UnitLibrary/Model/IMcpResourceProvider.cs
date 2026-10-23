namespace Curl.Mcp.Protocol.Model;

/// <summary>
/// Supplies a fixed set of resources the MCP server offers: what <c>resources/list</c> lists and what
/// <c>resources/read</c> reads. The host adds each provider to <see cref="McpServerRegistry" />.
/// </summary>
public interface IMcpResourceProvider
{
    /// <summary>Lists the provider's resources; the registry reads the list once, when the provider is added.</summary>
    /// <returns>The resources, each with a URI unique within the server.</returns>
    IReadOnlyList<McpResource> ListResources();

    /// <summary>Reads one resource.</summary>
    /// <param name="uri">The resource's URI.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The resource's contents, or <see langword="null" /> when the provider has no resource with that URI.</returns>
    ValueTask<McpResourceContents?> ReadAsync(string uri, CancellationToken cancellationToken);
}
