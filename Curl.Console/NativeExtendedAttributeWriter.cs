using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Curl.Console;

/// <summary>
/// The real-disk <see cref="IExtendedAttributeWriter" />: the libc call the operating system's curl
/// build makes in <c>src/tool_xattr.c</c> - <c>setxattr</c> on Linux (five arguments) and macOS
/// (six), and <c>extattr_set_file</c> in the user namespace on FreeBSD (ADR-0320).
/// </summary>
/// <remarks>
/// curl writes through the open file's descriptor (<c>fsetxattr</c>, <c>extattr_set_fd</c>); this
/// writes through its path, which names the same file, because .NET does not hand out a stream's
/// descriptor. Windows has no curl build that writes extended attributes, so
/// <see cref="ForCurrentPlatform" /> gives none there. Every line needs the real call, so per
/// ADR-0083 the type is measured by <c>NativeExtendedAttributeWriterTests</c>' Integration run on
/// Linux and macOS rather than by the fast run.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0320: a thin libc adapter, measured by the Integration run.")]
internal sealed partial class NativeExtendedAttributeWriter : IExtendedAttributeWriter
{
    private const string LibC = "libc";

    private const int FreeBsdUserNamespace = 1;

    private readonly Func<string, string, byte[], int> setAttribute;

    private NativeExtendedAttributeWriter(Func<string, string, byte[], int> setAttribute) =>
        this.setAttribute = setAttribute;

    /// <summary>
    /// Gives the writer for the running operating system, or <see langword="null" /> where curl
    /// writes no extended attributes, as on Windows.
    /// </summary>
    /// <returns>The writer, or <see langword="null" />.</returns>
    internal static NativeExtendedAttributeWriter? ForCurrentPlatform()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
        {
            return new((path, name, value) => LinuxSetXattr(path, name, value, (nuint)value.Length, 0));
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst())
        {
            return new((path, name, value) => MacSetXattr(path, name, value, (nuint)value.Length, 0, 0));
        }

        if (OperatingSystem.IsFreeBSD())
        {
            return new((path, name, value) =>
                FreeBsdExtattrSetFile(path, FreeBsdUserNamespace, name, value, (nuint)value.Length) < 0 ? -1 : 0);
        }

        return null;
    }

    /// <inheritdoc />
    public bool TryWrite(string path, string name, string value, out string errorText)
    {
        if (setAttribute(path, name, Encoding.UTF8.GetBytes(value)) == 0)
        {
            errorText = string.Empty;
            return true;
        }

        errorText = Marshal.GetPInvokeErrorMessage(Marshal.GetLastPInvokeError());
        return false;
    }

    [LibraryImport(LibC, EntryPoint = "setxattr", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LinuxSetXattr(string path, string name, byte[] value, nuint size, int flags);

    [LibraryImport(LibC, EntryPoint = "setxattr", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int MacSetXattr(string path, string name, byte[] value, nuint size, uint position, int options);

    [LibraryImport(LibC, EntryPoint = "extattr_set_file", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial nint FreeBsdExtattrSetFile(string path, int attributeNamespace, string name, byte[] value, nuint size);
}
