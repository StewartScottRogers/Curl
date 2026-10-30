namespace Curl.Extension.OsCredentialStore;

/// <summary>Reads one generic credential from Windows Credential Manager.</summary>
internal interface IWindowsCredentialReader
{
    /// <summary>Reads the <c>CRED_TYPE_GENERIC</c> credential whose <c>TargetName</c> is <paramref name="targetName" /> with <c>CredReadW</c>.</summary>
    /// <param name="targetName">The credential's target name.</param>
    /// <returns>Its <c>UserName</c> and <c>CredentialBlob</c>, or <see langword="null" /> when <c>CredReadW</c> finds none.</returns>
    WindowsCredential? Read(string targetName);
}
