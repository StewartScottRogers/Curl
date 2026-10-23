using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp.Protocol.Resources;

/// <summary>
/// Answers <c>resources/read</c> with the one text content of the resource its <c>uri</c> names, read from the
/// registered <see cref="IMcpResourceProvider" /> that listed it. An unknown URI, or no string <c>uri</c> in the
/// params, is error -32002 with the message <c>Resource not found: '&lt;uri&gt;'.</c>, as the SDK server answers.
/// </summary>
public sealed class ResourcesReadHandler : IMcpMethodHandler
{
    private readonly McpServerRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="ResourcesReadHandler" /> class.</summary>
    /// <param name="registry">The registry whose providers are read.</param>
    public ResourcesReadHandler(McpServerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public string Method => "resources/read";

    /// <inheritdoc />
    public async ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        // Params that name no resource fall through to the not-found error with an empty URI.
        string uri = ReadUri(parameters);
        IMcpResourceProvider? provider = _registry.FindResourceProvider(uri);
        McpResourceContents? contents = provider is null ? null : await provider.ReadAsync(uri, cancellationToken).ConfigureAwait(false);
        if (contents is null)
        {
            throw new McpProtocolException(JsonRpcErrorCodes.ResourceNotFound, $"Resource not found: '{uri}'.");
        }

        return JsonSerializer.SerializeToElement(new McpResourceReadResult([contents]), McpModelJsonContext.Default.McpResourceReadResult);
    }

    private static string ReadUri(JsonElement? parameters) =>
        parameters is { ValueKind: JsonValueKind.Object } value
        && value.TryGetProperty("uri", out JsonElement uri)
        && uri.ValueKind == JsonValueKind.String
            ? uri.GetString()!
            : string.Empty;
}
