namespace Curl.Mcp;

/// <summary>
/// What the <c>parse_url</c> tool gives back (ADR-0548): the URL as the parser rebuilt it and its
/// parts, or only a tool error when a flag is unknown or the parser refused the URL.
/// </summary>
/// <param name="Url">The whole URL as the parser rebuilt it; <see langword="null" /> on failure.</param>
/// <param name="Parts">The URL's parts, each absent one <see langword="null" />; <see langword="null" /> on failure.</param>
/// <param name="Failure">The tool error; <see langword="null" /> when the URL parsed.</param>
public sealed record McpParseUrlToolResult(string? Url, McpUrlParts? Parts, McpToolFailure? Failure)
{
    /// <summary>Whether the result is a tool error.</summary>
    public bool IsError => Failure is not null;

    /// <summary>The result for a URL that did not parse.</summary>
    /// <param name="failure">Why.</param>
    /// <returns>A result holding only the tool error.</returns>
    public static McpParseUrlToolResult Failed(McpToolFailure failure) => new(null, null, failure);
}
