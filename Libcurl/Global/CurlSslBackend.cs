namespace Libcurl.Global;

/// <summary>One entry of <c>curl_global_sslset</c>'s <c>avail</c> list (<c>curl_ssl_backend</c>).</summary>
/// <param name="Id">The backend's id.</param>
/// <param name="Name">The backend's name, as <c>curl_global_sslset</c> matches it, such as <c>schannel</c>.</param>
public sealed record CurlSslBackend(CurlSslBackendId Id, string Name)
{
    /// <summary>The Schannel backend, the one Curl's Windows build has.</summary>
    internal static CurlSslBackend Schannel { get; } = new(CurlSslBackendId.Schannel, "schannel");

    /// <summary>The OpenSSL backend, the one Curl's Linux and macOS builds have.</summary>
    internal static CurlSslBackend OpenSsl { get; } = new(CurlSslBackendId.OpenSsl, "openssl");

    /// <summary>The backend of the build this process matches: <see cref="Schannel"/> on Windows, else <see cref="OpenSsl"/>.</summary>
    internal static CurlSslBackend Current { get; } = For(OperatingSystem.IsWindows());

    /// <summary>The backend of a platform's build.</summary>
    /// <param name="windows">Whether the platform is Windows.</param>
    /// <returns><see cref="Schannel"/> on Windows, else <see cref="OpenSsl"/>.</returns>
    internal static CurlSslBackend For(bool windows) => windows ? Schannel : OpenSsl;
}
