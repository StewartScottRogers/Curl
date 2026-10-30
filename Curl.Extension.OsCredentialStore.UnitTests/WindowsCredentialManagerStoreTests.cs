using System.Text;

namespace Curl.Extension.OsCredentialStore.UnitTests;

/// <summary>Against wincred.h's CREDENTIALW: UserName and CredentialBlob of the CRED_TYPE_GENERIC credential CredReadW returns.</summary>
[TestClass]
public sealed class WindowsCredentialManagerStoreTests
{
    [TestMethod]
    public async Task FindAsync_GenericCredential_GivesItsUserNameAndBlobAsTheCredential()
    {
        RecordingReader reader = new(new WindowsCredential("alice", Encoding.Unicode.GetBytes("s3cret")));

        var credential = await new WindowsCredentialManagerStore(reader).FindAsync("example.com", CancellationToken.None);

        Assert.AreEqual("example.com", reader.TargetName);
        Assert.AreEqual("alice", credential!.UserName);
        Assert.AreEqual("s3cret", credential.Password);
    }

    [TestMethod]
    public async Task FindAsync_CredReadFindsNone_GivesNone()
    {
        var store = new WindowsCredentialManagerStore(new RecordingReader(null));

        Assert.IsNull(await store.FindAsync("missing", CancellationToken.None));
        Assert.AreEqual("Windows Credential Manager", store.Name);
    }

    [TestMethod]
    public void PasswordOf_WideStringBlob_IsUtf16LittleEndian() =>
        Assert.AreEqual("pässword", WindowsCredentialManagerStore.PasswordOf(Encoding.Unicode.GetBytes("pässword")));

    [TestMethod]
    public void PasswordOf_Utf8BlobOfEvenLength_IsUtf8() =>
        Assert.AreEqual("päss", WindowsCredentialManagerStore.PasswordOf(Encoding.UTF8.GetBytes("päss")));

    [TestMethod]
    public void PasswordOf_BlobOfOddLength_IsUtf8() =>
        Assert.AreEqual("abc", WindowsCredentialManagerStore.PasswordOf([0x61, 0x62, 0x63]));

    [TestMethod]
    public void PasswordOf_EmptyBlob_IsEmpty() =>
        Assert.AreEqual(string.Empty, WindowsCredentialManagerStore.PasswordOf([]));

    private sealed class RecordingReader(WindowsCredential? credential) : IWindowsCredentialReader
    {
        public string? TargetName { get; private set; }

        public WindowsCredential? Read(string targetName)
        {
            TargetName = targetName;
            return credential;
        }
    }
}
