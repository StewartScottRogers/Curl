namespace Curl.Networking;

/// <summary>
/// The command-line settings <see cref="SslStreamTlsProvider" /> applies to every
/// handshake it runs.
/// </summary>
/// <param name="Insecure">
/// <see langword="true" /> for curl's <c>-k</c>/<c>--insecure</c>: the server certificate
/// and host name are not verified.
/// </param>
/// <param name="MinimumVersion">
/// The lowest TLS version offered; <see cref="TlsMinimumVersion.SystemDefault" /> leaves
/// the choice to the operating system.
/// </param>
/// <param name="CaCertificateFile">
/// curl's <c>--cacert</c>: the path of a PEM file whose certificates are the only roots
/// the server's chain may lead to, in place of the system store; <see langword="null" />
/// verifies against the system store. Ignored when <paramref name="Insecure" /> is set.
/// </param>
public sealed record TlsClientOptions(
    bool Insecure = false,
    TlsMinimumVersion MinimumVersion = TlsMinimumVersion.SystemDefault,
    string? CaCertificateFile = null);
