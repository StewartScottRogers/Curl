using System.Runtime.InteropServices;
using System.Text;
using Curl.Conformance.Differential.Libcurl.Printf;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlPrintf;

/// <summary>
/// Runs printf scenarios through the reference libcurl 8.22.0's variadic <c>curl_msnprintf</c>,
/// loaded with <see cref="NativeLibrary"/> and called through an unmanaged function pointer as in
/// ADR-0792. On x64 Windows every variadic argument is one 8-byte slot and a <c>double</c> is read
/// from that slot's bits, so each argument is passed as <see cref="LibcurlPrintfArgument.Slot"/>
/// (a string as the address of its UTF-8 copy) and the unused slots as 0 (ADR-0876).
/// </summary>
internal sealed unsafe class ReferenceLibcurlPrintfDriver : IDisposable
{
    private readonly nint _library;
    private readonly delegate* unmanaged<byte*, nuint, byte*, long, long, long, long, long, long, long, long, long, long, int> _msnprintf;

    public ReferenceLibcurlPrintfDriver(string libraryPath)
    {
        _library = NativeLibrary.Load(libraryPath);
        _msnprintf = (delegate* unmanaged<byte*, nuint, byte*, long, long, long, long, long, long, long, long, long, long, int>)NativeLibrary.GetExport(_library, "curl_msnprintf");
    }

    /// <summary>Runs one scenario; one that would crash the reference build is not run.</summary>
    public string Run(LibcurlPrintfScenario scenario)
    {
        if (scenario.CrashesReference)
        {
            return LibcurlPrintfLineWriter.NotRun;
        }

        var strings = new List<nint>();
        try
        {
            var slots = new long[LibcurlPrintfScenario.MaximumArguments];
            for (var i = 0; i < scenario.Arguments.Count; i++)
            {
                slots[i] = SlotOf(scenario.Arguments[i], strings);
            }

            var format = Encoding.UTF8.GetBytes(scenario.Format + "\0");
            var buffer = new byte[LibcurlPrintfLineWriter.BufferSize];
            int count;
            fixed (byte* output = buffer)
            fixed (byte* formatBytes = format)
            {
                count = _msnprintf(output, (nuint)buffer.Length, formatBytes, slots[0], slots[1], slots[2], slots[3], slots[4], slots[5], slots[6], slots[7], slots[8], slots[9]);
            }

            return LibcurlPrintfLineWriter.Result(count, buffer);
        }
        finally
        {
            strings.ForEach(Marshal.FreeCoTaskMem);
        }
    }

    public void Dispose() => NativeLibrary.Free(_library);

    private static long SlotOf(LibcurlPrintfArgument argument, List<nint> strings)
    {
        if (argument.Kind != 's')
        {
            return argument.Slot;
        }

        var text = Marshal.StringToCoTaskMemUTF8(argument.Text);
        strings.Add(text);
        return text;
    }
}
