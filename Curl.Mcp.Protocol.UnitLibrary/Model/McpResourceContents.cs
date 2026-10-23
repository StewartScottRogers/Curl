using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>One entry of the <c>contents</c> array of a <c>resources/read</c> result: a resource's text.</summary>
public sealed class McpResourceContents
{
    /// <summary>Initializes a new instance of the <see cref="McpResourceContents" /> class.</summary>
    /// <param name="uri">The URI of the resource read.</param>
    /// <param name="mimeType">The resource's media type, or <see langword="null" /> when unknown.</param>
    /// <param name="text">The resource's text.</param>
    public McpResourceContents(string uri, string? mimeType, string text)
    {
        Uri = uri;
        MimeType = mimeType;
        Text = text;
    }

    /// <summary>Gets the URI of the resource read.</summary>
    [JsonPropertyName("uri")]
    public string Uri { get; }

    /// <summary>Gets the resource's media type; not written when <see langword="null" />.</summary>
    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; }

    /// <summary>Gets the resource's text.</summary>
    [JsonPropertyName("text")]
    public string Text { get; }
}
