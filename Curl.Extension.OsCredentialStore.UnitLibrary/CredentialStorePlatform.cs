namespace Curl.Extension.OsCredentialStore;

/// <summary>Which OS credential store <c>--user-from-keychain</c> reads.</summary>
internal enum CredentialStorePlatform
{
    /// <summary>Windows Credential Manager.</summary>
    Windows,

    /// <summary>The macOS keychain.</summary>
    MacOs,

    /// <summary>libsecret's Secret Service, on Linux and every other Unix.</summary>
    Libsecret,
}
