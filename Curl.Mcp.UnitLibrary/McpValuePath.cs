namespace Curl.Mcp;

/// <summary>
/// One local path found in an option's value, or a value whose path cannot be checked before curl
/// runs, which the argument check refuses (ADR-0564).
/// </summary>
/// <param name="Path">The path, or the value that names an uncheckable one.</param>
/// <param name="Uncheckable">Why the path cannot be checked; <see langword="null" /> when <see cref="McpConfinementRoot.Check(string)" /> decides it.</param>
internal readonly record struct McpValuePath(string Path, string? Uncheckable = null);
