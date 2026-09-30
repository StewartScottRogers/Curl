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
    /// <see cref="TlsVersion" /> <see cref="ToTlsVersion" /> maps them to; and the ten options of
    /// ADR-0151 verbatim: <see cref="CommandLineOptions.Curves" />,
    /// <see cref="CommandLineOptions.SignatureAlgorithms" />,
    /// <see cref="CommandLineOptions.TlsEarlyData" /> (as <see cref="TlsClientOptions.AllowEarlyData" />),
    /// <see cref="CommandLineOptions.Ech" />, <see cref="CommandLineOptions.EchPublicName" />,
    /// <see cref="CommandLineOptions.EchConfigList" />, <see cref="CommandLineOptions.SslSessionsFile" />,
    /// <see cref="CommandLineOptions.Engine" />, <see cref="CommandLineOptions.TlsUser" />,
    /// <see cref="CommandLineOptions.TlsPassword" /> and <see cref="CommandLineOptions.TlsAuthType" />; and
    /// ADR-0191's <see cref="CommandLineOptions.RequireCertificateStatus" /> (<c>--cert-status</c>) and
    /// <see cref="CommandLineOptions.AutoClientCertificate" /> (<c>--ssl-auto-client-cert</c>); and
    /// ADR-0193's <see cref="CommandLineOptions.PinnedPublicKey" /> (<c>--pinnedpubkey</c>); and ADR-0197's
    /// <see cref="CommandLineOptions.CertificateRevocationListFile" /> (<c>--crlfile</c>).
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
            ToTlsVersion(options.MaximumTlsVersion),
            options.Curves,
            options.SignatureAlgorithms,
            options.TlsEarlyData,
            options.Ech,
            options.EchPublicName,
            options.EchConfigList,
            options.SslSessionsFile,
            options.Engine,
            options.TlsUser,
            options.TlsPassword,
            options.TlsAuthType,
            options.RequireCertificateStatus,
            options.AutoClientCertificate,
            options.PinnedPublicKey,
            options.CertificateRevocationListFile);

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
    /// (<c>--proxy-tlsv1</c>) as <see cref="TlsClientOptions.MinimumVersion" />, and
    /// <see cref="CommandLineOptions.ProxyAutoClientCertificate" /> (<c>--proxy-ssl-auto-client-cert</c>) as
    /// <see cref="TlsClientOptions.AutoClientCertificate" /> (ADR-0191), and the proxy's client certificate
    /// and cipher lists as the target's counterparts map (BL-606):
    /// <see cref="CommandLineOptions.ProxyClientCertificate" /> (<c>--proxy-cert</c>),
    /// <see cref="CommandLineOptions.ProxyPrivateKey" /> (<c>--proxy-key</c>),
    /// <see cref="CommandLineOptions.ProxyClientCertificateType" /> (<c>--proxy-cert-type</c>, as
    /// <see cref="TlsClientOptions.CertificateType" />),
    /// <see cref="CommandLineOptions.ProxyPrivateKeyType" /> (<c>--proxy-key-type</c>),
    /// <see cref="CommandLineOptions.ProxyPassphrase" /> (<c>--proxy-pass</c>),
    /// <see cref="CommandLineOptions.ProxyCiphers" /> (<c>--proxy-ciphers</c>) and
    /// <see cref="CommandLineOptions.ProxyTls13Ciphers" /> (<c>--proxy-tls13-ciphers</c>), each verbatim;
    /// every other setting is its default. Neither the target's minimum nor <c>--tls-max</c> reaches the proxy: curl 8.21.0
    /// completes the handshake with a TLS 1.2-only HTTPS proxy under <c>--tlsv1.3</c> and under
    /// <c>--tls-max 1.1</c> (measured, BL-502).
    /// </returns>
    internal static TlsClientOptions ProxyFromCommandLine(CommandLineOptions options) =>
        new(
            Insecure: options.ProxyInsecure,
            MinimumVersion: ToTlsVersion(options.ProxyMinimumTlsVersion),
            CaCertificateFile: options.ProxyCaCertificateFile,
            CaCertificateDirectory: options.ProxyCaCertificateDirectory ?? options.CaCertificateDirectory,
            ClientCertificate: options.ProxyClientCertificate,
            PrivateKey: options.ProxyPrivateKey,
            Ciphers: options.ProxyCiphers,
            Tls13Ciphers: options.ProxyTls13Ciphers,
            CertificateType: options.ProxyClientCertificateType,
            PrivateKeyType: options.ProxyPrivateKeyType,
            Passphrase: options.ProxyPassphrase,
            AutoClientCertificate: options.ProxyAutoClientCertificate);

    /// <summary>
    /// Maps the TLS options that reach the DNS-over-HTTPS server onto the <see cref="TlsClientOptions" />
    /// its handshakes apply (ADR-0152, BL-642): the ones curl 8.21.0's <c>lib/doh.c</c> copies onto each
    /// DoH transfer, of which <c>--cacert</c> and <c>--ssl-no-revoke</c> were measured to reach it, and
    /// the two DoH options themselves. <c>-k</c> and <c>--cert-status</c> never reach it (measured).
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>
    /// <see cref="CommandLineOptions.DohInsecure" /> as <see cref="TlsClientOptions.Insecure" />,
    /// <see cref="CommandLineOptions.DohCertificateStatus" /> as
    /// <see cref="TlsClientOptions.RequireCertificateStatus" />, and
    /// <see cref="CommandLineOptions.CaCertificateFile" />, <see cref="CommandLineOptions.CaCertificateDirectory" />,
    /// <see cref="CommandLineOptions.CertificateRevocationListFile" />, <see cref="CommandLineOptions.Curves" />,
    /// <see cref="CommandLineOptions.SkipRevocationCheck" />, <see cref="CommandLineOptions.RevocationCheckBestEffort" />
    /// and <see cref="CommandLineOptions.AutoClientCertificate" /> verbatim; every other setting is its
    /// default, so ALPN offers <c>http/1.1</c> only.
    /// </returns>
    internal static TlsClientOptions DohFromCommandLine(CommandLineOptions options) =>
        new(
            Insecure: options.DohInsecure,
            CaCertificateFile: options.CaCertificateFile,
            CaCertificateDirectory: options.CaCertificateDirectory,
            SkipRevocationCheck: options.SkipRevocationCheck,
            RevocationCheckBestEffort: options.RevocationCheckBestEffort,
            Curves: options.Curves,
            RequireCertificateStatus: options.DohCertificateStatus,
            AutoClientCertificate: options.AutoClientCertificate,
            CertificateRevocationListFile: options.CertificateRevocationListFile);

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
