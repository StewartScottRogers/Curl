namespace Curl.Mcp;

/// <summary>
/// One long option an extension registers (BL-5564): the name without <c>--</c> and whether it takes a
/// value. The host builds the list from the extension registry, so <see cref="McpArgumentChecker" />
/// knows the same options the console's parser does; <see cref="McpExtensionOptionPathTable" /> says
/// which of them name a local path.
/// </summary>
/// <param name="Name">The long name without <c>--</c>.</param>
/// <param name="TakesValue"><see langword="true" /> for an option with a value, <see langword="false" /> for a flag (which <c>--no-&lt;name&gt;</c> also spells).</param>
public sealed record McpExtensionOption(string Name, bool TakesValue);
