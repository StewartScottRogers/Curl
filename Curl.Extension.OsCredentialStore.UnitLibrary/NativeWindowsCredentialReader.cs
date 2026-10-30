using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// Reads a generic credential with <c>CredReadW</c> and frees it with <c>CredFree</c> (wincred.h), copying the
/// <c>UserName</c> and <c>CredentialBlob</c> fields of the <c>CREDENTIALW</c> it returns.
/// </summary>
/// <remarks>It calls advapi32.dll, which a unit test run must not, so per ADR-0083 the type is left out of the coverage measure.</remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin CredReadW and CredFree adapter; the blob is decoded and tested in WindowsCredentialManagerStore.")]
internal sealed partial class NativeWindowsCredentialReader : IWindowsCredentialReader
{
    private const uint GenericCredentialType = 1;

    public WindowsCredential? Read(string targetName)
    {
        if (!CredReadW(targetName, GenericCredentialType, 0, out var pointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CredentialW>(pointer);
            var blob = new byte[credential.CredentialBlobSize];
            if (blob.Length > 0)
            {
                Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            }

            return new WindowsCredential(Marshal.PtrToStringUni(credential.UserName) ?? string.Empty, blob);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredReadW(string targetName, uint type, uint flags, out nint credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    private static partial void CredFree(nint buffer);

    /// <summary>wincred.h's <c>CREDENTIALW</c>, with <c>LastWritten</c> as its two <c>FILETIME</c> halves.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CredentialW
    {
        public uint Flags;
        public uint Type;
        public nint TargetName;
        public nint Comment;
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }
}
