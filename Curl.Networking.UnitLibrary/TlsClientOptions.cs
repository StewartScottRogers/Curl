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
public sealed record TlsClientOptions(
    bool Insecure = false,
    TlsMinimumVersion MinimumVersion = TlsMinimumVersion.SystemDefault);
