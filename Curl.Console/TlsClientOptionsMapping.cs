using System.Security.Authentication;
using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Maps the TLS options of a parsed command line onto the <see cref="TlsClientOptions" />
/// an <see cref="SslStreamTlsProvider" /> applies: the target's, and an HTTPS proxy's.
/// </summary>
internal static class TlsClientOptionsMapping
{
    /// <summary>
    /// Copies <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, <c>--cert</c>, <c>--key</c>,
    /// <c>--ciphers</c>, <c>--tls13-ciphers</c>, <c>--cert-type</c>, <c>--key-type</c>,
    /// <c>--pass</c>, <c>--ssl-no-revoke</c> and the minimum TLS version from
    /// <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>
    /// The TLS settings: <see cref="CommandLineOptions.Insecure" /> and
    /// <see cref="CommandLineOptions.CaCertificateFile" />,
    /// <see cref="CommandLineOptions.CaCertificateDirectory" />,
    /// <see cref="CommandLineOptions.ClientCertificate" />,
    /// <see cref="CommandLineOptions.PrivateKey" />, <see cref="CommandLineOptions.Ciphers" />
    /// <see cref="CommandLineOptions.Tls13Ciphers" />,
    /// <see cref="CommandLineOptions.ClientCertificateType" /> (as
    /// <see cref="TlsClientOptions.CertificateType" />),
    /// <see cref="CommandLineOptions.PrivateKeyType" />,
    /// <see cref="CommandLineOptions.Passphrase" /> and
    /// <see cref="CommandLineOptions.SkipRevocationCheck" /> verbatim, <c>--tlsv1.2</c> as
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
            options.Tls13Ciphers,
            options.ClientCertificateType,
            options.PrivateKeyType,
            options.Passphrase,
            options.SkipRevocationCheck);

    /// <summary>
    /// Maps the proxy TLS options of a parsed command line onto the <see cref="TlsClientOptions" />
    /// the handshake to an HTTPS proxy applies, as curl 8.21.0 keeps them apart from the target's
    /// (measured, ADR-0061): <c>-k</c> and <c>--cacert</c> never reach the proxy.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>
    /// <see cref="CommandLineOptions.ProxyInsecure" /> as <see cref="TlsClientOptions.Insecure" />,
    /// <see cref="CommandLineOptions.ProxyCaCertificateFile" /> as
    /// <see cref="TlsClientOptions.CaCertificateFile" />, and
    /// <see cref="CommandLineOptions.ProxyCaCertificateDirectory" />, or
    /// <see cref="CommandLineOptions.CaCertificateDirectory" /> without it, as
    /// <see cref="TlsClientOptions.CaCertificateDirectory" />, since curl falls back to
    /// <c>--capath</c> for the proxy; every other setting is its default.
    /// </returns>
    internal static TlsClientOptions ProxyFromCommandLine(CommandLineOptions options) =>
        new(
            Insecure: options.ProxyInsecure,
            CaCertificateFile: options.ProxyCaCertificateFile,
            CaCertificateDirectory: options.ProxyCaCertificateDirectory ?? options.CaCertificateDirectory);

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
