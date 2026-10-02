using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Curl.Console;

/// <summary>
/// Tells whether a path names a regular file, as curl's <c>stat</c> and <c>S_ISREG</c> do before a
/// <c>--remove-on-error</c> delete, through the .NET runtime's own <c>stat</c> shim
/// (<c>SystemNative_Stat</c> in <c>libSystem.Native</c>), whose status record has the same layout
/// on Linux and macOS and gives the file type in its <c>Mode</c> (ADR-0332).
/// </summary>
/// <remarks>
/// .NET has no public way to tell a character device such as <c>/dev/null</c> from a regular file:
/// <see cref="File.Exists(string)" /> and <see cref="File.GetAttributes(string)" /> say the same of
/// both. Every line needs the real call, which Windows has not, so per ADR-0083 the type is
/// left out of the Windows coverage measure and checked by <c>NativeRegularFileTestTests</c> on
/// Linux and macOS.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0332: a thin stat adapter, tested off Windows.")]
internal static partial class NativeRegularFileTest
{
    private const int FileTypeMask = 0xF000;

    private const int RegularFileType = 0x8000;

    /// <summary>
    /// Gives the regular-file test for the running operating system, or <see langword="null" /> on
    /// Windows, where curl 8.21.0 is measured to try the delete of <c>-o NUL</c> and warn
    /// <c>Failed removing</c> rather than skip it.
    /// </summary>
    /// <returns>The test, or <see langword="null" />.</returns>
    internal static Func<string, bool>? ForCurrentPlatform() => OperatingSystem.IsWindows() ? null : IsRegularFile;

    /// <summary>
    /// Tells whether <paramref name="path" />, followed through symbolic links, is a regular file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="false" /> when <c>stat</c> fails or finds anything else there.</returns>
    internal static bool IsRegularFile(string path) =>
        Stat(path, out FileStatus status) == 0 && (status.Mode & FileTypeMask) == RegularFileType;

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Stat(string path, out FileStatus status);

    /// <summary>
    /// The head of the runtime's <c>FileStatus</c> record: its flags and mode, padded past the
    /// record's full length so the shim's write stays inside it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 256)]
    private struct FileStatus
    {
        public int Flags;

        public int Mode;
    }
}
