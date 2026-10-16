using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Curl.Cli;

/// <summary>
/// Reads the effective user's home directory from the user database, as curl's
/// <c>getpwuid(geteuid())-&gt;pw_dir</c> does, through the .NET runtime's own shims
/// (<c>SystemNative_GetEUid</c> and <c>SystemNative_GetPwUidR</c> in <c>libSystem.Native</c>), whose
/// <c>Passwd</c> record has the same layout on Linux and macOS (BL-2336).
/// </summary>
/// <remarks>
/// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> returns <c>$HOME</c> off Windows
/// whenever it is set, so it cannot stand in for the user database: with <c>HOME</c> naming another
/// directory curl still finds the account's <c>.ssh/known_hosts</c> and <c>.curlrc</c>. Every line needs
/// the real call, which Windows has not, so per ADR-0083 the type is left out of the Windows coverage
/// measure and checked by <c>UserDatabaseHomeDirectoryIntegrationTests</c> on Linux and macOS.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin getpwuid_r adapter, tested off Windows.")]
public static partial class UserDatabaseHomeDirectory
{
    private const int BufferLength = 64 * 1024;

    /// <summary>
    /// Reads the effective user's home directory from the user database; never call it on Windows,
    /// which has no <c>libSystem.Native</c>.
    /// </summary>
    /// <returns>The directory; <see langword="null"/> when the user database has no entry for the user.</returns>
    public static string? ReadForEffectiveUser()
    {
        IntPtr buffer = Marshal.AllocHGlobal(BufferLength);
        try
        {
            return GetPwUidR(GetEUid(), out Passwd entry, buffer, BufferLength) == 0
                ? Marshal.PtrToStringUTF8(entry.HomeDirectory)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_GetEUid")]
    private static partial uint GetEUid();

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_GetPwUidR", SetLastError = true)]
    private static partial int GetPwUidR(uint uid, out Passwd entry, IntPtr buffer, int bufferLength);

    /// <summary>The runtime's normalised <c>Passwd</c> record; its strings point into the caller's buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Passwd
    {
        public IntPtr Name;

        public IntPtr Password;

        public uint UserId;

        public uint GroupId;

        public IntPtr UserInfo;

        public IntPtr HomeDirectory;

        public IntPtr Shell;
    }
}
