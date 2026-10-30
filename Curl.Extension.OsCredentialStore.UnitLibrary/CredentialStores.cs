namespace Curl.Extension.OsCredentialStore;

/// <summary>Picks the credential store of the platform Curl runs on (ADR-1139 section 1).</summary>
internal static class CredentialStores
{
    /// <summary>Gives the store the running OS has.</summary>
    /// <returns>Credential Manager on Windows, the keychain on macOS, libsecret elsewhere.</returns>
    internal static ICredentialStore ForThisPlatform() => For(PlatformOf(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));

    /// <summary>Names the store of an OS.</summary>
    /// <param name="isWindows">Whether the OS is Windows.</param>
    /// <param name="isMacOs">Whether the OS is macOS.</param>
    /// <returns>The store's platform.</returns>
    internal static CredentialStorePlatform PlatformOf(bool isWindows, bool isMacOs) =>
        isWindows ? CredentialStorePlatform.Windows
        : isMacOs ? CredentialStorePlatform.MacOs
        : CredentialStorePlatform.Libsecret;

    /// <summary>Gives the store of <paramref name="platform" />, over the OS itself.</summary>
    /// <param name="platform">The platform.</param>
    /// <returns>The store.</returns>
    internal static ICredentialStore For(CredentialStorePlatform platform) => platform switch
    {
        CredentialStorePlatform.Windows => new WindowsCredentialManagerStore(new NativeWindowsCredentialReader()),
        CredentialStorePlatform.MacOs => new MacKeychainStore(new ProcessCommandRunner()),
        _ => new LibsecretStore(new ProcessCommandRunner()),
    };
}
