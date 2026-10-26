namespace Curl.Cli;

/// <summary>
/// Parses a command line as curl's Linux and macOS OpenSSL build reads it, on every platform, so tests of
/// the options curl's Windows Schannel build refuses (<see cref="CommandLineOptions.ActsAsWindowsSchannelBuild"/>,
/// ADR-0397) - <c>--http2</c>, <c>--http3</c>, the TLS-SRP options and <c>--ssl-sessions</c> - run on Windows too.
/// </summary>
internal static class OpenSslBuildParser
{
    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string})"/> does, but as the OpenSSL build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, Path.Exists);

    /// <summary>Parses <paramref name="arguments"/> as <see cref="CommandLineParser.Parse(IReadOnlyList{string}, Func{string, bool})"/> does, but as the OpenSSL build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists) =>
        Parse(arguments, pathExists, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess);

    /// <summary>Parses <paramref name="arguments"/> as the four-argument <see cref="CommandLineParser"/> overload does, but as the OpenSSL build.</summary>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists, IPasswordPrompt passwordPrompt, IDataFileReader dataFileReader) =>
        CommandLineParser.Parse(arguments, pathExists, passwordPrompt, dataFileReader, isWindows: false);
}

/// <summary>Answers every password prompt with an empty password, so a test that parses a <c>-u</c> user without a password never waits on the process console.</summary>
internal sealed class EmptyPasswordPrompt : IPasswordPrompt
{
    /// <inheritdoc/>
    public string ReadPassword(string prompt) => string.Empty;
}
