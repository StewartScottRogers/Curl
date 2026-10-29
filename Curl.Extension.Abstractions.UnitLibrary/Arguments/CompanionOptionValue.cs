namespace Curl.Extension.Abstractions.Arguments;

/// <summary>One companion option an <see cref="ITrailingArgumentExpander" /> took, with its value (ADR-1051).</summary>
/// <param name="Option">The companion option.</param>
/// <param name="Value">Its value; the empty string for a flag.</param>
public sealed record CompanionOptionValue(ExtensionOption Option, string Value);
