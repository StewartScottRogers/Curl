namespace Curl.Cli;

/// <summary>
/// Parses a command line as a curl built with c-ares reads it (<see cref="CommandLineOptions.ActsAsCAresBuild"/>,
/// ADR-0841), otherwise as the Linux and macOS OpenSSL build reads it, on every platform, so tests of
/// <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and <c>--dns-ipv6-addr</c>, which every
/// reference build refuses, still reach what they set.
/// </summary>
internal static class CAresBuildParser
{
    private static readonly DefaultConfigFileSearch noDefaultConfigFile = new(_ => null, isWindows: false, null, null);

    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string})"/> does, but as the c-ares build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, Path.Exists, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess);

    /// <summary>Parses <paramref name="arguments"/> as the four-argument <see cref="CommandLineParser"/> overload does, but as the c-ares build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists, IPasswordPrompt passwordPrompt, IDataFileReader dataFileReader) =>
        CommandLineParser.Parse(arguments, pathExists, passwordPrompt, dataFileReader, noDefaultConfigFile, isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: false, actsAsCAresBuild: true);
}
