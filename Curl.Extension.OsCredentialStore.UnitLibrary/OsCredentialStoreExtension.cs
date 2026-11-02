using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// The <c>--user-from-keychain &lt;name&gt;</c> extension (ADR-1144): takes the transfer's user name and password from
/// Windows Credential Manager, the macOS keychain or libsecret's Secret Service. <c>Curl.Extension.Composition</c>
/// registers its option, description and transfer hook.
/// </summary>
public static class OsCredentialStoreExtension
{
    /// <summary>Gets the <c>--user-from-keychain &lt;name&gt;</c> option.</summary>
    public static ExtensionOption UserFromKeychainOption { get; } = new(
        "user-from-keychain",
        ExtensionOptionArgument.Value,
        "Use the user name and password stored under <name> in the OS credential store");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "os-credential-store",
        "Takes the user name and password from Windows Credential Manager, the macOS keychain or libsecret (ADR-1144).",
        [],
        [UserFromKeychainOption],
        new Uri("https://github.com/StewartScottRogers/Curl/blob/master/Documentation/Planning/Decisions/ADR-1144-user-from-keychain-reads-the-platform-credential-store.md"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--user-from-keychain</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook over this platform's store, or <see langword="null" /> when the option was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(UserFromKeychainOption) is { } name
            ? new OsCredentialStoreTransferHook(CredentialStores.ForThisPlatform(), name)
            : null;
    }
}
