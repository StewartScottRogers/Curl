using System.Net;
using System.Text;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// Windows Credential Manager (ADR-1144 section 2): the generic credential whose target name is the name given,
/// its <c>UserName</c> the user and its <c>CredentialBlob</c> the password.
/// </summary>
/// <param name="reader">Reads the credential with <c>CredReadW</c>.</param>
internal sealed class WindowsCredentialManagerStore(IWindowsCredentialReader reader) : ICredentialStore
{
    public string Name => "Windows Credential Manager";

    public ValueTask<NetworkCredential?> FindAsync(string name, CancellationToken cancellationToken) =>
        ValueTask.FromResult(reader.Read(name) is { } credential
            ? new NetworkCredential(credential.UserName, PasswordOf(credential.CredentialBlob))
            : null);

    /// <summary>
    /// Decodes a <c>CredentialBlob</c>: UTF-16LE, as Credential Manager, <c>cmdkey</c> and <c>CredWriteW</c> callers
    /// passing a wide string store it, when its length is even and it holds a zero byte; otherwise UTF-8, as tools
    /// such as Git Credential Manager store it (UTF-8 text never holds a zero byte).
    /// </summary>
    /// <param name="blob">The blob.</param>
    /// <returns>The password.</returns>
    internal static string PasswordOf(byte[] blob) =>
        blob.Length % 2 == 0 && Array.IndexOf(blob, (byte)0) >= 0
            ? Encoding.Unicode.GetString(blob)
            : Encoding.UTF8.GetString(blob);
}
