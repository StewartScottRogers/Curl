namespace Curl.Cli;

/// <summary>
/// A long option the host adds beyond curl's own for an extension (ADR-1006): the parser reads it only
/// where <see cref="CommandLineOptionTable"/> has no row of that name, and records its value in
/// <see cref="CommandLineOptions.ExtensionOptionValues"/>. The parser is given none when extensions are off,
/// so every such name is then refused as curl refuses an unknown option.
/// </summary>
/// <param name="LongName">The long name without its leading <c>--</c>.</param>
/// <param name="TakesValue">Whether a value follows, attached or as the next argument; otherwise a flag.</param>
public sealed record CommandLineExtensionOption(string LongName, bool TakesValue);
