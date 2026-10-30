namespace Curl.Extension.OsCredentialStore;

/// <summary>The two fields of a Windows <c>CREDENTIALW</c> that <c>--user-from-keychain</c> uses.</summary>
/// <param name="UserName">The <c>UserName</c> field; empty when it is null.</param>
/// <param name="CredentialBlob">The <c>CredentialBlob</c> bytes, <c>CredentialBlobSize</c> of them.</param>
internal sealed record WindowsCredential(string UserName, byte[] CredentialBlob);
