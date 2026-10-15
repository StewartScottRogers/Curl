using Curl.Api.Options.Tls;

namespace Curl.Engine;

/// <summary>
/// What the two options asking about Curl's TLS build print on standard output, as ADR-0151 decides:
/// <c>--engine list</c> lists no build-time engines (<see cref="NoCryptoEngines.ListLines" />) and
/// <c>--dump-ca-embed</c> prints nothing, since Curl embeds no CA bundle and trusts the operating
/// system's store, as the Schannel and OpenSSL builds do.
/// </summary>
internal static class TlsBuildInformation
{
    /// <summary>The engine name that asks for the engine list rather than naming an engine, as curl 8.21.0 takes it, exactly.</summary>
    private const string EngineListName = "list";

    /// <summary>The lines to print for an option group, if it asks about the TLS build.</summary>
    /// <param name="tls">
    /// The option group's TLS options: an <see cref="EasyTlsOptions.Engine" /> of exactly <c>list</c> is
    /// <c>--engine list</c>, which ends the command line as it asks.
    /// </param>
    /// <param name="caEmbedDumpRequested">Whether <c>--dump-ca-embed</c> was given, which is the tool's and not on the TLS record.</param>
    /// <returns>
    /// <see cref="NoCryptoEngines.ListLines" /> for <c>--engine list</c>, no lines for
    /// <c>--dump-ca-embed</c>, and <see langword="null" /> when neither was given.
    /// </returns>
    internal static IReadOnlyList<string>? Lines(EasyTlsOptions tls, bool caEmbedDumpRequested) =>
        tls.Engine == EngineListName ? NoCryptoEngines.ListLines
        : caEmbedDumpRequested ? []
        : null;
}
