namespace Curl.Extension.Abstractions;

/// <summary>
/// One long command-line option an extension adds beyond upstream curl's (ADR-1006). An extension
/// declares it in one file and registers it with one line in <c>Curl.Extension.Composition</c>'s
/// <c>ExtensionOptionCatalogue</c>; the parser accepts it only when curl has no option of that name
/// and extensions are on.
/// </summary>
/// <param name="Name">The long name without its leading <c>--</c>, such as <c>cache-dir</c>.</param>
/// <param name="Argument">What the option takes after its name.</param>
/// <param name="Help">The option's one-line help.</param>
public sealed record ExtensionOption(string Name, ExtensionOptionArgument Argument, string Help);
