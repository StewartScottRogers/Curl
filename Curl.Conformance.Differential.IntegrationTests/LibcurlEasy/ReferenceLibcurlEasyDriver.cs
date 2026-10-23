using System.Runtime.InteropServices;
using System.Text;
using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlEasy;

/// <summary>
/// Runs an easy-handle case's rounds through the reference libcurl 8.22.0 shared library, as the
/// tester's <c>easytest.py</c> did through <c>ctypes</c>: loaded with <see cref="NativeLibrary"/>
/// and called through unmanaged function pointers (ADR-0792). The variadic
/// <c>curl_easy_setopt</c> and <c>curl_easy_getinfo</c> are called with one pointer-sized
/// argument, which x64 Windows and x64 System V pass as a fixed argument.
/// </summary>
internal sealed unsafe class ReferenceLibcurlEasyDriver : IDisposable
{
    private const int ErrorBufferSize = 256;

    private readonly nint _library;
    private readonly delegate* unmanaged<nint> _easyInit;
    private readonly delegate* unmanaged<nint, int, nint, int> _setOption;
    private readonly delegate* unmanaged<nint, int> _perform;
    private readonly delegate* unmanaged<nint, int, nint, int> _getInfo;
    private readonly delegate* unmanaged<nint, void> _reset;
    private readonly delegate* unmanaged<nint, void> _cleanup;
    private readonly delegate* unmanaged<nint, byte*, nint> _slistAppend;
    private readonly delegate* unmanaged<nint, void> _slistFreeAll;
    private readonly delegate* unmanaged<nint, byte*, nuint, uint, int, nint*, int> _header;
    private readonly delegate* unmanaged<nint, uint, int, nint, nint> _nextHeader;

    public ReferenceLibcurlEasyDriver(string libraryPath)
    {
        _library = NativeLibrary.Load(libraryPath);
        _easyInit = (delegate* unmanaged<nint>)Export("curl_easy_init");
        _setOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_setopt");
        _perform = (delegate* unmanaged<nint, int>)Export("curl_easy_perform");
        _getInfo = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_getinfo");
        _reset = (delegate* unmanaged<nint, void>)Export("curl_easy_reset");
        _cleanup = (delegate* unmanaged<nint, void>)Export("curl_easy_cleanup");
        _slistAppend = (delegate* unmanaged<nint, byte*, nint>)Export("curl_slist_append");
        _slistFreeAll = (delegate* unmanaged<nint, void>)Export("curl_slist_free_all");
        _header = (delegate* unmanaged<nint, byte*, nuint, uint, int, nint*, int>)Export("curl_easy_header");
        _nextHeader = (delegate* unmanaged<nint, uint, int, nint, nint>)Export("curl_easy_nextheader");
        ((delegate* unmanaged<long, int>)Export("curl_global_init"))(3);
    }

    /// <summary>Runs <paramref name="rounds"/> on one easy handle.</summary>
    public IReadOnlyList<string> Run(IReadOnlyList<LibcurlEasyRound> rounds)
    {
        var body = new MemoryStream();
        var header = new MemoryStream();
        var bodyHandle = GCHandle.Alloc(body);
        var headerHandle = GCHandle.Alloc(header);
        var kept = new List<nint>();
        var lists = new List<nint>();
        var errorBuffer = Marshal.AllocHGlobal(ErrorBufferSize);
        var easy = _easyInit();
        try
        {
            var lines = new List<string>();
            foreach (var round in rounds)
            {
                body.SetLength(0);
                header.SetLength(0);
                new Span<byte>((void*)errorBuffer, ErrorBufferSize).Clear();
                _setOption(easy, 10010, errorBuffer);
                var failures = new List<string>();
                foreach (var option in round.Options)
                {
                    var code = _setOption(easy, option.Id, Value(option, easy, GCHandle.ToIntPtr(bodyHandle), GCHandle.ToIntPtr(headerHandle), kept, lists));
                    if (code != 0)
                    {
                        failures.Add(LibcurlEasyLineWriter.SetFailure(option.Id, code));
                    }
                }

                var performed = _perform(easy);
                var line = new StringBuilder(LibcurlEasyLineWriter.Round(lines.Count, performed, Terminated((byte*)errorBuffer), failures, body.ToArray(), header.ToArray()));
                AppendReads(easy, round, line);
                lines.Add(line.ToString());
                if (round.Resets)
                {
                    _reset(easy);
                }
            }

            return lines;
        }
        finally
        {
            _cleanup(easy);
            lists.ForEach(list => _slistFreeAll(list));
            kept.ForEach(Marshal.FreeCoTaskMem);
            Marshal.FreeHGlobal(errorBuffer);
            bodyHandle.Free();
            headerHandle.Free();
        }
    }

    public void Dispose() => NativeLibrary.Free(_library);

    private nint Value(LibcurlEasyOption option, nint easy, nint bodyHandle, nint headerHandle, List<nint> kept, List<nint> lists)
    {
        switch (option.Kind)
        {
            case LibcurlEasyOptionKind.Number:
                return (nint)option.Number;
            case LibcurlEasyOptionKind.Text:
                return Keep(option.Text, kept);
            case LibcurlEasyOptionKind.List:
                var list = option.List.Aggregate((nint)0, (current, text) => _slistAppend(current, (byte*)Keep(text, kept)));
                lists.Add(list);
                return list;
            case LibcurlEasyOptionKind.WriteCallback:
                _setOption(easy, 10001, bodyHandle);
                return (nint)(delegate* unmanaged<byte*, nuint, nuint, nint, nuint>)&Collect;
            default:
                _setOption(easy, 10029, headerHandle);
                return (nint)(delegate* unmanaged<byte*, nuint, nuint, nint, nuint>)&Collect;
        }
    }

    private void AppendReads(nint easy, LibcurlEasyRound round, StringBuilder line)
    {
        foreach (var id in round.Info)
        {
            var (code, value) = Info(easy, id);
            line.Append(LibcurlEasyLineWriter.Info(id, code, value));
        }

        foreach (var query in round.HeaderQueries)
        {
            var name = query.Name is null ? null : Encoding.UTF8.GetBytes(query.Name + "\0");
            nint found = 0;
            int code;
            fixed (byte* namePointer = name)
            {
                code = _header(easy, namePointer, (nuint)query.Index, (uint)query.Origin, query.Request, &found);
            }

            line.Append(LibcurlEasyLineWriter.HeaderQuery(query, code, code == 0 ? Fields(found) : null));
        }

        foreach (var walk in round.HeaderWalks)
        {
            var headers = new List<LibcurlEasyHeaderFields>();
            nint previous = 0;
            while (headers.Count < LibcurlEasyLineWriter.WalkLimit && (previous = _nextHeader(easy, (uint)walk.Origin, walk.Request, previous)) != 0)
            {
                headers.Add(Fields(previous));
            }

            line.Append(LibcurlEasyLineWriter.HeaderWalk(walk, headers));
        }
    }

    private (int Code, string Value) Info(nint easy, int id)
    {
        long slot = 0;
        var code = _getInfo(easy, id, (nint)(&slot));
        return (id >> 20) switch
        {
            1 => (code, slot == 0 ? "null" : Encoding.Latin1.GetString(Terminated((byte*)slot))),
            2 => (code, (OperatingSystem.IsWindows() ? (int)slot : slot).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            6 => (code, slot.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ => (-1, "?"),
        };
    }

    private static LibcurlEasyHeaderFields Fields(nint header)
    {
        // struct curl_header { char *name; char *value; size_t amount; size_t index; unsigned int origin; void *anchor; }
        var fields = (nint*)header;
        return new LibcurlEasyHeaderFields(Terminated((byte*)fields[0]), Terminated((byte*)fields[1]), fields[2], fields[3], *(int*)(fields + 4));
    }

    private static byte[] Terminated(byte* text) => MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text).ToArray();

    private static nint Keep(string? text, List<nint> kept)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(text);
        kept.Add(pointer);
        return pointer;
    }

    [UnmanagedCallersOnly]
    private static nuint Collect(byte* data, nuint size, nuint count, nint sink)
    {
        var length = size * count;
        ((MemoryStream)GCHandle.FromIntPtr(sink).Target!).Write(new ReadOnlySpan<byte>(data, (int)length));
        return length;
    }

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);
}
