using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Parses a command line as a curl built with a QUIC library reads it (<see cref="CommandLineOptions.ActsAsQuicBuild"/>,
/// ADR-0540) on every platform, as the OpenSSL build reads it otherwise, so tests of <c>--http3</c> and <c>--http3-only</c>,
/// which every reference build refuses, still reach Curl's hand-built HTTP/3.
/// </summary>
internal static class QuicBuildParser
{
    private static readonly DefaultConfigFileSearch NoDefaultConfigFile = new(_ => null, isWindows: false, null, null);

    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string})"/> does, but as the QUIC build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, Path.Exists);

    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string}, Func{string, bool})"/> does, but as the QUIC build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists) =>
        CommandLineParser.Parse(arguments, pathExists, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, NoDefaultConfigFile, isWindows: false, _ => null, actsAsQuicBuild: true);
}
