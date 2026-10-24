using System.Buffers;
using System.Text.Json;
using Curl.Mcp.Protocol.JsonRpc;

namespace Curl.Mcp.Protocol.Server;

/// <summary>
/// Answers <c>initialize</c>: echoes the client's protocol version when the server supports it and
/// answers its latest otherwise, with the server's <c>serverInfo</c> and the capabilities of the
/// handlers it was built with, as the SDK 2.2.0 server did.
/// </summary>
public sealed class McpInitializeHandler : IMcpMethodHandler
{
    /// <summary>The protocol revisions the server speaks, oldest first; the last is answered to a client version it does not know.</summary>
    public static readonly IReadOnlyList<string> SupportedProtocolVersions = ["2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"];

    private readonly McpServerInfo _serverInfo;

    private readonly IReadOnlyList<IMcpCapabilityContributor> _capabilities;

    /// <summary>Initializes a new instance of the <see cref="McpInitializeHandler" /> class.</summary>
    /// <param name="serverInfo">The server's name and version.</param>
    /// <param name="handlers">The server's other handlers; those that are <see cref="IMcpCapabilityContributor" /> give the capabilities.</param>
    public McpInitializeHandler(McpServerInfo serverInfo, IEnumerable<IMcpMethodHandler> handlers)
    {
        _serverInfo = serverInfo;
        _capabilities = [.. handlers.OfType<IMcpCapabilityContributor>().OrderBy(contributor => contributor.CapabilityName, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public string Method => "initialize";

    /// <inheritdoc />
    public ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
    {
        string clientVersion = ReadProtocolVersion(parameters);
        string version = SupportedProtocolVersions.Contains(clientVersion) ? clientVersion : SupportedProtocolVersions[^1];
        return ValueTask.FromResult(WriteResult(version));
    }

    private static string ReadProtocolVersion(JsonElement? parameters)
    {
        if (parameters is { ValueKind: JsonValueKind.Object } value
            && value.TryGetProperty("protocolVersion", out JsonElement version)
            && version.ValueKind == JsonValueKind.String)
        {
            return version.GetString()!;
        }

        throw new McpProtocolException(JsonRpcErrorCodes.InvalidParams, "The initialize request needs a protocolVersion string in its params.");
    }

    private JsonElement WriteResult(string version)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("protocolVersion", version);
            writer.WriteStartObject("capabilities");
            foreach (IMcpCapabilityContributor contributor in _capabilities)
            {
                writer.WritePropertyName(contributor.CapabilityName);
                contributor.Capability.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteStartObject("serverInfo");
            writer.WriteString("name", _serverInfo.Name);
            writer.WriteString("version", _serverInfo.Version);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
