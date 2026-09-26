namespace Curl.Cli;

/// <summary>
/// Applies one option's value to <paramref name="options"/>.
/// </summary>
/// <param name="options">The options being filled in.</param>
/// <param name="value">The option's value; <see cref="string.Empty"/> for a flag.</param>
/// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
/// <returns><see langword="null"/> when the value was applied; otherwise why it was refused.</returns>
public delegate CommandLineRefusal? CommandLineOptionApplier(CommandLineOptions options, string value, string spelledOption);
