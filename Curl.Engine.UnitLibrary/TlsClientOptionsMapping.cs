using System.Collections.Frozen;
using Curl.Api.Options.Tls;
using Curl.Networking;

namespace Curl.Engine;

/// <summary>
/// Maps an option group's <see cref="EasyTlsOptions" /> onto the <see cref="TlsClientOptions" />
/// an <see cref="SslStreamTlsProvider" /> applies: the target's, an HTTPS proxy's and the
/// DNS-over-HTTPS server's (ADR-0514, BL-2324).
/// </summary>
internal static class TlsClientOptionsMapping
{
    // The lowest versions the record names, and the TlsVersion each is to the provider; CURL_SSLVERSION_TLSv1 is TLS 1.0 or later.
    private static readonly FrozenDictionary<SslVersion, TlsVersion> TlsVersionsBySslVersion =
        new Dictionary<SslVersion, TlsVersion>
        {
            [SslVersion.Tls1] = TlsVersion.Tls10,
            [SslVersion.Tls10] = TlsVersion.Tls10,
            [SslVersion.Tls11] = TlsVersion.Tls11,
            [SslVersion.Tls12] = TlsVersion.Tls12,
            [SslVersion.Tls13] = TlsVersion.Tls13,
        }.ToFrozenDictionary();

    // The ceilings the record names, and the TlsVersion each is to the provider.
    private static readonly FrozenDictionary<SslVersionMaximum, TlsVersion> TlsVersionsBySslVersionMaximum =
        new Dictionary<SslVersionMaximum, TlsVersion>
        {
            [SslVersionMaximum.Tls10] = TlsVersion.Tls10,
            [SslVersionMaximum.Tls11] = TlsVersion.Tls11,
            [SslVersionMaximum.Tls12] = TlsVersion.Tls12,
            [SslVersionMaximum.Tls13] = TlsVersion.Tls13,
        }.ToFrozenDictionary();

    /// <summary>
    /// Maps the target's TLS options: <see cref="EasyTlsOptions.Origin" />'s verification (<c>-k</c>
    /// turns <see cref="TlsPeerOptions.VerifyPeer" /> off, as <see cref="TlsClientOptions.Insecure" />),
    /// CA file and directory, client certificate, key, their types and pass phrase, cipher lists,
    /// pinned public key, revocation list, versions (<see cref="ToTlsVersion(SslVersion)" /> and
    /// <see cref="ToTlsVersion(SslVersionMaximum)" />) and <c>CURLSSLOPT_*</c> bits
    /// (<see cref="SslOptions.NoRevoke" />, <see cref="SslOptions.RevokeBestEffort" />,
    /// <see cref="SslOptions.EarlyData" />, <see cref="SslOptions.AutoClientCert" /> and
    /// <see cref="SslOptions.AllowBeast" />); and the record's <see cref="EasyTlsOptions.EllipticCurves" />,
    /// <see cref="EasyTlsOptions.SignatureAlgorithms" />, the three Encrypted Client Hello members,
    /// <see cref="EasyTlsOptions.Engine" />, <see cref="EasyTlsOptions.VerifyStatus" /> and
    /// <see cref="EasyTlsOptions.SessionIdCache" /> negated as <see cref="TlsClientOptions.NoSessionId" />.
    /// <see cref="SslOptions.NativeCa" /> (<c>--ca-native</c>) maps to nothing: without <c>--cacert</c>
    /// the provider already verifies against the operating system's store, and with it curl 8.21.0
    /// still verifies against the file on both builds (measured, ADR-0124). The record's
    /// <see cref="TlsPeerOptions.Certificate" /> and <see cref="TlsPeerOptions.KeyPassword" /> arrive
    /// already split from <c>-E</c> (ADR-0514), so they are copied as they are.
    /// </summary>
    /// <param name="tls">The option group's TLS options.</param>
    /// <param name="useAlpn">Whether ALPN is offered: <see langword="false" /> under <c>--no-alpn</c>, which is not on the TLS record (ADR-0514).</param>
    /// <param name="sslSessionsFile">The <c>--ssl-sessions</c> file, the tool's own and not on the TLS record (ADR-0514), or <see langword="null" />.</param>
    /// <param name="readEnvironmentVariable">
    /// Reads the run's environment for <c>SSLKEYLOGFILE</c> (<see cref="KeyLogFileFromEnvironment" />): the
    /// conformance harness's in-process run gives its case's <c>&lt;setenv&gt;</c> here (BL-2716);
    /// <see langword="null" /> for the process's environment.
    /// </param>
    /// <returns>The target's TLS settings.</returns>
    internal static TlsClientOptions Of(EasyTlsOptions tls, bool useAlpn, string? sslSessionsFile, Func<string, string?>? readEnvironmentVariable = null)
    {
        TlsPeerOptions origin = tls.Origin;
        return new(
            !origin.VerifyPeer,
            ToTlsVersion(origin.SslVersion),
            origin.CertificateAuthorityFile,
            origin.CertificateAuthorityPath,
            origin.Certificate,
            origin.Key,
            origin.CipherList,
            origin.Tls13Ciphers,
            origin.CertificateType,
            origin.KeyType,
            origin.KeyPassword,
            origin.SslOptions.HasFlag(SslOptions.NoRevoke),
            origin.SslOptions.HasFlag(SslOptions.RevokeBestEffort),
            useAlpn,
            ToTlsVersion(origin.SslVersionMaximum),
            tls.EllipticCurves,
            tls.SignatureAlgorithms,
            origin.SslOptions.HasFlag(SslOptions.EarlyData),
            tls.EncryptedClientHello,
            tls.EncryptedClientHelloPublicName,
            tls.EncryptedClientHelloConfigList,
            sslSessionsFile,
            tls.Engine,
            tls.VerifyStatus,
            origin.SslOptions.HasFlag(SslOptions.AutoClientCert),
            origin.PinnedPublicKey,
            origin.RevocationListFile,
            !tls.SessionIdCache,
            origin.SslOptions.HasFlag(SslOptions.AllowBeast),
            KeyLogFile(readEnvironmentVariable),
            ChecksHostNameWhenInsecure(origin),
            origin.CertificateAuthorityBlob);
    }

    /// <summary>
    /// Maps the TLS options of the handshake to an HTTPS proxy, which curl 8.21.0 keeps apart from the
    /// target's (measured, ADR-0061): <see cref="EasyTlsOptions.Proxy" />'s verification, CA file and
    /// directory (which already falls back to <c>--capath</c>, ADR-0514), client certificate, key, their
    /// types and pass phrase, cipher lists, pinned public key, revocation list, minimum version
    /// (<c>--proxy-tlsv1</c>) and its <see cref="SslOptions.AutoClientCert" /> and
    /// <see cref="SslOptions.AllowBeast" /> bits; <see cref="EasyTlsOptions.SessionIdCache" /> negated as
    /// <see cref="TlsClientOptions.NoSessionId" />, as for the target (BL-713); and
    /// <paramref name="useAlpn" />, since curl 8.21.0 offers no ALPN to the proxy under <c>--no-alpn</c>
    /// (measured, ADR-0190, BL-871). <c>--proxy-ca-native</c> maps to nothing, as <c>--ca-native</c> does
    /// (ADR-0124); every other setting is its default. Neither the target's minimum nor <c>--tls-max</c>
    /// reaches the proxy: curl 8.21.0 completes the handshake with a TLS 1.2-only HTTPS proxy under
    /// <c>--tlsv1.3</c> and under <c>--tls-max 1.1</c> (measured, BL-502).
    /// </summary>
    /// <param name="tls">The option group's TLS options.</param>
    /// <param name="useAlpn">Whether ALPN is offered: <see langword="false" /> under <c>--no-alpn</c>.</param>
    /// <param name="readEnvironmentVariable">Reads the run's environment for <c>SSLKEYLOGFILE</c>, as for <see cref="Of" />; <see langword="null" /> for the process's.</param>
    /// <returns>The HTTPS proxy's TLS settings.</returns>
    internal static TlsClientOptions ProxyOf(EasyTlsOptions tls, bool useAlpn, Func<string, string?>? readEnvironmentVariable = null)
    {
        TlsPeerOptions proxy = tls.Proxy;
        return new(
            Insecure: !proxy.VerifyPeer,
            MinimumVersion: ToTlsVersion(proxy.SslVersion),
            CaCertificateFile: proxy.CertificateAuthorityFile,
            CaCertificateDirectory: proxy.CertificateAuthorityPath,
            ClientCertificate: proxy.Certificate,
            PrivateKey: proxy.Key,
            Ciphers: proxy.CipherList,
            Tls13Ciphers: proxy.Tls13Ciphers,
            CertificateType: proxy.CertificateType,
            PrivateKeyType: proxy.KeyType,
            Passphrase: proxy.KeyPassword,
            AutoClientCertificate: proxy.SslOptions.HasFlag(SslOptions.AutoClientCert),
            PinnedPublicKey: proxy.PinnedPublicKey,
            CertificateRevocationListFile: proxy.RevocationListFile,
            UseAlpn: useAlpn,
            NoSessionId: !tls.SessionIdCache,
            AllowBeast: proxy.SslOptions.HasFlag(SslOptions.AllowBeast),
            KeyLogFile: KeyLogFile(readEnvironmentVariable),
            ChecksHostNameWhenInsecure: ChecksHostNameWhenInsecure(proxy),
            CaCertificateBlob: proxy.CertificateAuthorityBlob);
    }

    /// <summary>
    /// Whether the name is still checked with the peer not verified: libcurl's <c>CURLOPT_SSL_VERIFYPEER</c> 0
    /// with <c>CURLOPT_SSL_VERIFYHOST</c> on (upstream test2118, ADR-0703). <c>-k</c> turns both off.
    /// </summary>
    /// <param name="peer">The origin's or the proxy's verification settings.</param>
    /// <returns><see langword="true" /> when <see cref="TlsPeerOptions.VerifyPeer" /> is off and <see cref="TlsPeerOptions.VerifyHost" /> on.</returns>
    internal static bool ChecksHostNameWhenInsecure(TlsPeerOptions peer) => !peer.VerifyPeer && peer.VerifyHost;

    /// <summary>
    /// Maps the TLS options that reach the DNS-over-HTTPS server onto the <see cref="TlsClientOptions" />
    /// its handshakes apply (ADR-0152, BL-642): the ones curl 8.21.0's <c>lib/doh.c</c> copies onto each
    /// DoH transfer, of which <c>--cacert</c> and <c>--ssl-no-revoke</c> were measured to reach it, and
    /// the two DoH options themselves. <c>-k</c> and <c>--cert-status</c> never reach it (measured).
    /// </summary>
    /// <param name="tls">The option group's TLS options.</param>
    /// <returns>
    /// <see cref="EasyTlsOptions.DohVerifyPeer" /> negated as <see cref="TlsClientOptions.Insecure" />,
    /// <see cref="EasyTlsOptions.DohVerifyStatus" /> as <see cref="TlsClientOptions.RequireCertificateStatus" />,
    /// and the origin's CA file and directory, revocation list, <see cref="SslOptions.NoRevoke" />,
    /// <see cref="SslOptions.RevokeBestEffort" /> and <see cref="SslOptions.AutoClientCert" />, and
    /// <see cref="EasyTlsOptions.EllipticCurves" />, verbatim; every other setting is its default, so ALPN
    /// offers <c>http/1.1</c> only.
    /// </returns>
    internal static TlsClientOptions DohOf(EasyTlsOptions tls)
    {
        TlsPeerOptions origin = tls.Origin;
        return new(
            Insecure: !tls.DohVerifyPeer,
            CaCertificateFile: origin.CertificateAuthorityFile,
            CaCertificateDirectory: origin.CertificateAuthorityPath,
            SkipRevocationCheck: origin.SslOptions.HasFlag(SslOptions.NoRevoke),
            RevocationCheckBestEffort: origin.SslOptions.HasFlag(SslOptions.RevokeBestEffort),
            Curves: tls.EllipticCurves,
            RequireCertificateStatus: tls.DohVerifyStatus,
            AutoClientCertificate: origin.SslOptions.HasFlag(SslOptions.AutoClientCert),
            CertificateRevocationListFile: origin.RevocationListFile,
            KeyLogFile: KeyLogFile(null),
            CaCertificateBlob: origin.CertificateAuthorityBlob);
    }

    /// <summary>Maps a lowest TLS version from the record onto the one the TLS provider applies.</summary>
    /// <param name="sslVersion">The origin's or the proxy's <see cref="TlsPeerOptions.SslVersion" />.</param>
    /// <returns>
    /// <see cref="TlsVersion.Tls10" /> for <see cref="SslVersion.Tls1" /> and <see cref="SslVersion.Tls10" />,
    /// <see cref="TlsVersion.Tls11" />, <see cref="TlsVersion.Tls12" /> and <see cref="TlsVersion.Tls13" /> for
    /// their own, and <see cref="TlsVersion.SystemDefault" /> for <see cref="SslVersion.Default" /> or any other
    /// value (ADR-0514).
    /// </returns>
    internal static TlsVersion ToTlsVersion(SslVersion sslVersion) =>
        TlsVersionsBySslVersion.GetValueOrDefault(sslVersion, TlsVersion.SystemDefault);

    /// <summary>Maps a TLS version ceiling from the record onto the one the TLS provider applies.</summary>
    /// <param name="sslVersionMaximum">The origin's <see cref="TlsPeerOptions.SslVersionMaximum" />.</param>
    /// <returns>
    /// <see cref="TlsVersion.Tls10" /> to <see cref="TlsVersion.Tls13" /> for their own, and
    /// <see cref="TlsVersion.SystemDefault" /> for <see cref="SslVersionMaximum.None" />,
    /// <see cref="SslVersionMaximum.Default" /> or any other value.
    /// </returns>
    internal static TlsVersion ToTlsVersion(SslVersionMaximum sslVersionMaximum) =>
        TlsVersionsBySslVersionMaximum.GetValueOrDefault(sslVersionMaximum, TlsVersion.SystemDefault);

    /// <summary>
    /// Reads the file the OpenSSL build logs TLS secrets to from <c>SSLKEYLOGFILE</c>, for the target's,
    /// an HTTPS proxy's and the DoH server's handshakes alike, as OpenSSL's key-log callback serves every
    /// connection of the process (BL-2327, ADR-0518).
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads an environment variable, <see langword="null" /> when unset.</param>
    /// <param name="matchesSchannelBuild"><see langword="true" /> on Windows, whose Schannel build ignores the variable.</param>
    /// <returns>The variable's value, or <see langword="null" /> when it is unset or empty or the build is Schannel's.</returns>
    internal static string? KeyLogFileFromEnvironment(Func<string, string?> readEnvironmentVariable, bool matchesSchannelBuild) =>
        !matchesSchannelBuild && readEnvironmentVariable("SSLKEYLOGFILE") is { Length: > 0 } path ? path : null;

    private static string? KeyLogFile(Func<string, string?>? readEnvironmentVariable) =>
        KeyLogFileFromEnvironment(readEnvironmentVariable ?? Environment.GetEnvironmentVariable, OperatingSystem.IsWindows());
}
