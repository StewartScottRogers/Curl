using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>One entry of the <c>resources</c> array of a <c>resources/list</c> result.</summary>
public sealed class McpResource
{
    /// <summary>Initializes a new instance of the <see cref="McpResource" /> class.</summary>
    /// <param name="name">The resource's name.</param>
    /// <param name="uri">The resource's URI, unique within the server.</param>
    /// <param name="description">The resource's description, or <see langword="null" /> for none.</param>
    /// <param name="mimeType">The resource's media type, or <see langword="null" /> when unknown.</param>
    public McpResource(string name, string uri, string? description, string? mimeType)
    {
        Name = name;
        Uri = uri;
        Description = description;
        MimeType = mimeType;
    }

    /// <summary>Gets the resource's name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>Gets the resource's URI, unique within the server.</summary>
    [JsonPropertyName("uri")]
    public string Uri { get; }

    /// <summary>Gets the resource's description; not written when <see langword="null" />.</summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }

    /// <summary>Gets the resource's media type; not written when <see langword="null" />.</summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; }
}
