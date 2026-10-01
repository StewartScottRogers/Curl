using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The production <see cref="IPageantWindow" />: the Win32 calls of libssh2 1.11.1's
/// <c>agent_connect_pageant</c> and <c>agent_transact_pageant</c>, made only on Windows.
/// </summary>
// Excluded per ADR-0083: every line needs a live window, so the Integration run measures it
// against a fake Pageant window the test serves.
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin Win32 adapter, measured by the Integration run.")]
internal sealed partial class WindowsPageantWindow : IPageantWindow
{
    private const string Name = "Pageant";

    private const uint WmCopyData = 0x004A;

    // libssh2's PAGEANT_COPYDATA_ID.
    private const nuint CopyDataId = 0x804e50ba;

    /// <inheritdoc />
    public bool IsRunning() => FindWindowW(Name, Name) != 0;

    /// <inheritdoc />
    public bool Exchange(byte[] mapping)
    {
        nint window = FindWindowW(Name, Name);
        if (window == 0)
        {
            return false;
        }

        string mapName = $"PageantRequest{GetCurrentThreadId():x8}";
        try
        {
            using MemoryMappedFile map = MemoryMappedFile.CreateNew(mapName, mapping.Length);
            using MemoryMappedViewAccessor view = map.CreateViewAccessor();
            view.WriteArray(0, mapping, 0, mapping.Length);
            if (!SendMapName(window, mapName))
            {
                return false;
            }

            view.ReadArray(0, mapping, 0, mapping.Length);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // WM_COPYDATA with the mapping's name as a zero-terminated ANSI string.
    private static bool SendMapName(nint window, string mapName)
    {
        nint name = Marshal.StringToHGlobalAnsi(mapName);
        try
        {
            CopyDataStruct data = new() { Id = CopyDataId, Size = (uint)mapName.Length + 1, Data = name };
            return SendMessageW(window, WmCopyData, 0, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(name);
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowW(string className, string windowName);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessageW(nint window, uint message, nint wParam, ref CopyDataStruct lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static partial uint GetCurrentThreadId();

    // COPYDATASTRUCT.
    [StructLayout(LayoutKind.Sequential)]
    private struct CopyDataStruct
    {
        public nuint Id;
        public uint Size;
        public nint Data;
    }
}
