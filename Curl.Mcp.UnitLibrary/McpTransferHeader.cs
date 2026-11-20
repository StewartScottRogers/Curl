using System.Text.Json.Serialization;

namespace Curl.Mcp;

/// <summary>
/// One HTTP header of the <c>transfer</c> tool (ADR-0548, ADR-0593): a request header it sends, or a
/// response header it returns.
/// </summary>
/// <param name="Name">The header's name, e.g. <c>Content-Type</c>.</param>
/// <param name="Value">
/// The header's value, without leading and trailing white space; may be empty. An empty request header
/// value removes the header, as <c>curl -H "Name:"</c> does, unless <paramref name="SendEmpty" /> is set.
/// </param>
/// <param name="SendEmpty">
/// Whether a request header with an empty value is sent empty, as <c>curl -H "Name;"</c> sends it, rather
/// than removed; never written for a response header.
/// </param>
public sealed record McpTransferHeader(
    string Name,
    string Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SendEmpty = false);
