namespace Curl.Mcp;

/// <summary>
/// What the <c>build_url</c> tool gives back (ADR-0548): the URL built from the supplied parts, or
/// only a tool error when a flag is unknown or the parser refused a part or the URL they make.
/// </summary>
/// <param name="Url">The whole URL; <see langword="null" /> on failure.</param>
/// <param name="Failure">The tool error; <see langword="null" /> when the URL was built.</param>
public sealed record McpBuildUrlToolResult(string? Url, McpToolFailure? Failure)
{
    /// <summary>Whether the result is a tool error.</summary>
    public bool IsError => Failure is not null;

    /// <summary>The result for parts that make no URL.</summary>
    /// <param name="failure">Why.</param>
    /// <returns>A result holding only the tool error.</returns>
    public static McpBuildUrlToolResult Failed(McpToolFailure failure) => new(null, failure);
}
