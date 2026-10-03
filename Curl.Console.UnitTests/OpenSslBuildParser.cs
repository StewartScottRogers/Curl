using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Parses a command line as curl's Linux and macOS OpenSSL build reads it, on every platform, so tests of
/// the options curl's Windows Schannel build refuses (<see cref="CommandLineOptions.ActsAsWindowsSchannelBuild"/>,
/// ADR-0395) - <c>--http2</c>, <c>--http3</c>, the TLS-SRP options and <c>--ssl-sessions</c> - run on Windows too.
/// </summary>
internal static class OpenSslBuildParser
{
    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string})"/> does, but as the OpenSSL build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, Path.Exists);

    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string}, Func{string, bool})"/> does, but as the OpenSSL build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists) =>
        CommandLineParser.Parse(arguments, pathExists, ConsolePasswordPrompt.ForProcessConsole, DiskDataFileReader.ForProcess, isWindows: false);
}
