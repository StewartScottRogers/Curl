using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// What the two options asking about Curl's TLS build print on standard output, as ADR-0151 decides:
/// <c>--engine list</c> lists no build-time engines (<see cref="NoCryptoEngines.ListLines" />) and
/// <c>--dump-ca-embed</c> prints nothing, since Curl embeds no CA bundle and trusts the operating
/// system's store, as the Schannel and OpenSSL builds do.
/// </summary>
internal static class TlsBuildInformation
{
    /// <summary>The lines to print for <paramref name="options" />, if it asks about the TLS build.</summary>
    /// <param name="options">The parsed options.</param>
    /// <returns>
    /// <see cref="NoCryptoEngines.ListLines" /> for <c>--engine list</c>, no lines for
    /// <c>--dump-ca-embed</c>, and <see langword="null" /> when neither was given.
    /// </returns>
    internal static IReadOnlyList<string>? Lines(CommandLineOptions options) => options switch
    {
        { EngineListRequested: true } => NoCryptoEngines.ListLines,
        { CaEmbedDumpRequested: true } => [],
        _ => null,
    };
}
