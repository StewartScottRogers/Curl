using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Curl.Console;

/// <summary>
/// Tells whether a file descriptor is open and was inherited from the process that started Curl,
/// through <c>fcntl(F_GETFD)</c> in the runtime's own <c>SystemNative_FcntlGetFD</c> shim in
/// <c>libSystem.Native</c>, whose fixed signature a plain import can call on macOS arm64, unlike
/// libc's variadic <c>fcntl</c> (ADR-0868).
/// </summary>
/// <remarks>
/// curl's <c>main_checkfds</c> opens <c>/dev/null</c> on descriptors 0, 1 and 2 before anything
/// else, so a closed one (<c>2&gt;&amp;-</c>) is never reused. Curl cannot run before the .NET
/// runtime, which takes the lowest free descriptor for its own pipes and so may take 2. Every
/// descriptor the runtime opens is close-on-exec, while one inherited across <c>exec</c> cannot
/// be, so a descriptor that is open and not close-on-exec is the one the shell gave. Every line
/// needs the real call, which Windows has not, so per ADR-0083 the type is left out of the
/// Windows coverage measure and checked by <c>NativeInheritedFileDescriptorTests</c> on Linux and
/// macOS.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0868: a thin fcntl adapter, tested off Windows.")]
internal static partial class NativeInheritedFileDescriptor
{
    /// <summary>POSIX <c>FD_CLOEXEC</c>, the same on Linux and macOS.</summary>
    private const int CloseOnExecFlag = 1;

    /// <summary>
    /// Tells whether <paramref name="descriptor" /> is open and not close-on-exec, so was inherited.
    /// </summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <returns><see langword="false" /> when it is closed or the runtime opened it.</returns>
    internal static bool IsInherited(int descriptor)
    {
        int flags = GetDescriptorFlags(descriptor);
        return flags != -1 && (flags & CloseOnExecFlag) == 0;
    }

    /// <summary>The runtime's <c>SystemNative_FcntlGetFD</c>, <c>fcntl(descriptor, F_GETFD)</c>.</summary>
    /// <param name="descriptor">The file descriptor.</param>
    /// <returns>The descriptor flags, or -1 when it is not open.</returns>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_FcntlGetFD")]
    private static partial int GetDescriptorFlags(nint descriptor);
}
