namespace Curl.Output;

/// <summary>
/// Chooses the <see cref="TlsBackend"/> whose wording this tool matches: Schannel on
/// Windows, OpenSSL everywhere else, as ADR-0009 matches each platform's usual curl build.
/// </summary>
public static class PlatformTlsBackend
{
    /// <summary>
    /// Gets the backend for the running system.
    /// </summary>
    public static TlsBackend ForProcess { get; } = ForPlatform(OperatingSystem.IsWindows());

    /// <summary>Returns the backend for a platform.</summary>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns><see cref="TlsBackend.Schannel"/> on Windows, else <see cref="TlsBackend.OpenSsl"/>.</returns>
    public static TlsBackend ForPlatform(bool isWindows)
    {
        return isWindows ? TlsBackend.Schannel : TlsBackend.OpenSsl;
    }
}
