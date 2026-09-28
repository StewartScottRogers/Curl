using System.Collections.Frozen;
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
    // The versions the parser records, as SslProtocols, and the TlsVersion each is to the provider.
    private static readonly FrozenDictionary<SslProtocols, TlsVersion> TlsVersionsByProtocol =
        new Dictionary<SslProtocols, TlsVersion>
        {
            [ObsoleteTlsProtocols.Tls10] = TlsVersion.Tls10,
            [ObsoleteTlsProtocols.Tls11] = TlsVersion.Tls11,
            [SslProtocols.Tls12] = TlsVersion.Tls12,
            [SslProtocols.Tls13] = TlsVersion.Tls13,
        }.ToFrozenDictionary();

    /// <summary>
    /// Copies <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, <c>--cert</c>, <c>--key</c>,
    /// <c>--ciphers</c>, <c>--tls13-ciphers</c>, <c>--cert-type</c>, <c>--key-type</c>,
    /// <c>--pass</c>, <c>--ssl-no-revoke</c>, <c>--ssl-revoke-best-effort</c>, <c>--no-alpn</c> and the
    /// minimum TLS version from <paramref name="options" />. <c>--ca-native</c>
    /// (<see cref="CommandLineOptions.UseNativeCaStore" />) maps to nothing: without
    /// <c>--cacert</c> the provider already verifies against the operating system's store, and
    /// with it curl 8.21.0 still verifies against the file on both builds (measured, ADR-0124).
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
    /// <see cref="CommandLineOptions.Passphrase" />,
    /// <see cref="CommandLineOptions.SkipRevocationCheck" />,
    /// <see cref="CommandLineOptions.RevocationCheckBestEffort" /> and
    /// <see cref="CommandLineOptions.UseAlpn" /> verbatim, and
    /// <see cref="CommandLineOptions.MinimumTlsVersion" /> and
    /// <see cref="CommandLineOptions.MaximumTlsVersion" /> (<c>--tls-max</c>) as the
    /// <see cref="TlsVersion" /> <see cref="ToTlsVersion" /> maps them to.
    /// </returns>
    internal static TlsClientOptions FromCommandLine(CommandLineOptions options) =>
        new(
            options.Insecure,
            ToTlsVersion(options.MinimumTlsVersion),
            options.CaCertificateFile,
            options.CaCertificateDirectory,
            options.ClientCertificate,
            options.PrivateKey,
            options.Ciphers,
            options.Tls13Ciphers,
            options.ClientCertificateType,
            options.PrivateKeyType,
            options.Passphrase,
            options.SkipRevocationCheck,
            options.RevocationCheckBestEffort,
            options.UseAlpn,
            ToTlsVersion(options.MaximumTlsVersion));

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
    /// <c>--capath</c> for the proxy, and <see cref="CommandLineOptions.ProxyMinimumTlsVersion" />
    /// (<c>--proxy-tlsv1</c>) as <see cref="TlsClientOptions.MinimumVersion" />; every other setting is
    /// its default. Neither the target's minimum nor <c>--tls-max</c> reaches the proxy: curl 8.21.0
    /// completes the handshake with a TLS 1.2-only HTTPS proxy under <c>--tlsv1.3</c> and under
    /// <c>--tls-max 1.1</c> (measured, BL-502).
    /// </returns>
    internal static TlsClientOptions ProxyFromCommandLine(CommandLineOptions options) =>
        new(
            Insecure: options.ProxyInsecure,
            MinimumVersion: ToTlsVersion(options.ProxyMinimumTlsVersion),
            CaCertificateFile: options.ProxyCaCertificateFile,
            CaCertificateDirectory: options.ProxyCaCertificateDirectory ?? options.CaCertificateDirectory);

    /// <summary>
    /// Maps a TLS version from the command line, a minimum or a ceiling, onto the one the TLS
    /// provider applies.
    /// </summary>
    /// <param name="tlsVersion">
    /// <see cref="CommandLineOptions.MinimumTlsVersion" />, <see cref="CommandLineOptions.MaximumTlsVersion" />
    /// or <see cref="CommandLineOptions.ProxyMinimumTlsVersion" />, or <see langword="null" /> when not given.
    /// </param>
    /// <returns>
    /// <see cref="TlsVersion.Tls10" /> for <see cref="ObsoleteTlsProtocols.Tls10" />,
    /// <see cref="TlsVersion.Tls11" /> for <see cref="ObsoleteTlsProtocols.Tls11" />,
    /// <see cref="TlsVersion.Tls12" /> for <see cref="SslProtocols.Tls12" />,
    /// <see cref="TlsVersion.Tls13" /> for <see cref="SslProtocols.Tls13" />, and
    /// <see cref="TlsVersion.SystemDefault" /> for <see langword="null" /> or any other value.
    /// </returns>
    internal static TlsVersion ToTlsVersion(SslProtocols? tlsVersion) =>
        TlsVersionsByProtocol.GetValueOrDefault(tlsVersion ?? SslProtocols.None, TlsVersion.SystemDefault);
}
