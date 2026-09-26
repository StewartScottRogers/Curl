using System.Security.Authentication;
using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Maps the TLS options of a parsed command line onto the <see cref="TlsClientOptions" />
/// <see cref="SslStreamTlsProvider" /> applies to every handshake.
/// </summary>
internal static class TlsClientOptionsMapping
{
    /// <summary>
    /// Copies <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, <c>--cert</c>, <c>--key</c>,
    /// <c>--ciphers</c>, <c>--tls13-ciphers</c> and the minimum TLS version from
    /// <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>
    /// The TLS settings: <see cref="CommandLineOptions.Insecure" /> and
    /// <see cref="CommandLineOptions.CaCertificateFile" />,
    /// <see cref="CommandLineOptions.CaCertificateDirectory" />,
    /// <see cref="CommandLineOptions.ClientCertificate" />,
    /// <see cref="CommandLineOptions.PrivateKey" />, <see cref="CommandLineOptions.Ciphers" />
    /// and <see cref="CommandLineOptions.Tls13Ciphers" /> verbatim, <c>--tlsv1.2</c> as
    /// <see cref="TlsMinimumVersion.Tls12" />, <c>--tlsv1.3</c> as
    /// <see cref="TlsMinimumVersion.Tls13" />, and neither as
    /// <see cref="TlsMinimumVersion.SystemDefault" />.
    /// </returns>
    internal static TlsClientOptions FromCommandLine(CommandLineOptions options) =>
        new(
            options.Insecure,
            ToTlsMinimumVersion(options.MinimumTlsVersion),
            options.CaCertificateFile,
            options.CaCertificateDirectory,
            options.ClientCertificate,
            options.PrivateKey,
            options.Ciphers,
            options.Tls13Ciphers);

    /// <summary>
    /// Maps a minimum TLS version from the command line onto the one the TLS provider applies.
    /// </summary>
    /// <param name="minimumTlsVersion">
    /// <see cref="CommandLineOptions.MinimumTlsVersion" />, or <see langword="null" /> when no
    /// version option was given.
    /// </param>
    /// <returns>
    /// <see cref="TlsMinimumVersion.Tls12" /> for <see cref="SslProtocols.Tls12" />,
    /// <see cref="TlsMinimumVersion.Tls13" /> for <see cref="SslProtocols.Tls13" />, and
    /// <see cref="TlsMinimumVersion.SystemDefault" /> for <see langword="null" /> or any other value.
    /// </returns>
    internal static TlsMinimumVersion ToTlsMinimumVersion(SslProtocols? minimumTlsVersion) => minimumTlsVersion switch
    {
        SslProtocols.Tls12 => TlsMinimumVersion.Tls12,
        SslProtocols.Tls13 => TlsMinimumVersion.Tls13,
        _ => TlsMinimumVersion.SystemDefault,
    };
}
