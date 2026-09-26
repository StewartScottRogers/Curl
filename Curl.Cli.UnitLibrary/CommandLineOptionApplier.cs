namespace Curl.Cli;

/// <summary>
/// Applies one option's value to <paramref name="options"/>.
/// </summary>
/// <param name="options">The options being filled in.</param>
/// <param name="value">The option's value; <see cref="string.Empty"/> for a flag.</param>
/// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
/// <param name="pathExists">
/// Reports whether a file or directory exists at a path, for an option curl checks while parsing
/// (<c>--cacert</c>); injected so tests never touch the disk.
/// </param>
/// <param name="dataFileReader">
/// Reads the file, or standard input, a <c>-d @file</c> value names; injected so tests never touch
/// the disk or the console.
/// </param>
/// <returns><see langword="null"/> when the value was applied; otherwise why it was refused.</returns>
public delegate CommandLineRefusal? CommandLineOptionApplier(CommandLineOptions options, string value, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader);
