using System.Runtime.InteropServices;
using System.Text;
using Curl.Conformance.Differential.Libcurl.Formadd;
using Curl.Conformance.Differential.Libcurl.Mime;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlFormadd;

/// <summary>
/// Runs a formadd case through the reference libcurl 8.22.0 shared library, loaded with
/// <see cref="NativeLibrary"/> and called through unmanaged function pointers (ADR-0792).
/// <c>curl_formadd</c> is variadic; it is called with a fixed run of pointer-sized arguments
/// ending in <c>CURLFORM_END</c>, which the x64 calling conventions pass as a variadic call's.
/// Every string and buffer stays allocated until <c>curl_formfree</c>, as <c>CURLFORM_PTR*</c> needs.
/// </summary>
internal sealed unsafe class ReferenceLibcurlFormaddDriver : IDisposable
{
    private const int UrlOption = 10002;

    private const int WriteFunctionOption = 20011;

    private const int HttpPostOption = 10024;

    private const int ArgumentSlots = 24;

    private readonly nint _library;
    private readonly delegate* unmanaged<nint> _easyInit;
    private readonly delegate* unmanaged<nint, int, nint, int> _setOption;
    private readonly delegate* unmanaged<nint, int> _perform;
    private readonly delegate* unmanaged<nint, void> _cleanup;
    private readonly delegate* unmanaged<nint*, nint*, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, int> _formAdd;
    private readonly delegate* unmanaged<nint, nint, delegate* unmanaged<nint, byte*, nuint, nuint>, int> _formGet;
    private readonly delegate* unmanaged<nint, void> _formFree;
    private readonly delegate* unmanaged<nint, byte*, nint> _slistAppend;
    private readonly delegate* unmanaged<nint, void> _slistFree;

    public ReferenceLibcurlFormaddDriver(string libraryPath)
    {
        _library = NativeLibrary.Load(libraryPath);
        _easyInit = (delegate* unmanaged<nint>)Export("curl_easy_init");
        _setOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_setopt");
        _perform = (delegate* unmanaged<nint, int>)Export("curl_easy_perform");
        _cleanup = (delegate* unmanaged<nint, void>)Export("curl_easy_cleanup");
        _formAdd = (delegate* unmanaged<nint*, nint*, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, int>)Export("curl_formadd");
        _formGet = (delegate* unmanaged<nint, nint, delegate* unmanaged<nint, byte*, nuint, nuint>, int>)Export("curl_formget");
        _formFree = (delegate* unmanaged<nint, void>)Export("curl_formfree");
        _slistAppend = (delegate* unmanaged<nint, byte*, nint>)Export("curl_slist_append");
        _slistFree = (delegate* unmanaged<nint, void>)Export("curl_slist_free_all");
        ((delegate* unmanaged<long, int>)Export("curl_global_init"))(3);
    }

    /// <summary>Runs <paramref name="formCase"/> against <paramref name="url"/>.</summary>
    /// <returns>The result-code log, entries separated by spaces, and the bytes <c>curl_formget</c> gave.</returns>
    public (string Log, byte[] FormGet) Run(LibcurlFormaddCase formCase, string url, string directory)
    {
        var log = new List<string>();
        var memory = new List<nint>();
        var lists = new List<nint>();
        nint first = 0;
        nint last = 0;
        var formGet = new MemoryStream();
        try
        {
            foreach (var call in formCase.Calls)
            {
                log.Add(LibcurlFormaddLogWriter.Add(Add(&first, &last, call, directory, memory, lists)));
            }

            if (formCase.Sends)
            {
                log.Add(first == 0 ? LibcurlFormaddLogWriter.NoForm : LibcurlFormaddLogWriter.Get(Get(first, formGet)));
                log.Add(Post(first, url));
            }

            return (string.Join(' ', log), formGet.ToArray());
        }
        finally
        {
            _formFree(first);
            lists.ForEach(list => _slistFree(list));
            memory.ForEach(Marshal.FreeHGlobal);
        }
    }

    public void Dispose() => NativeLibrary.Free(_library);

    private int Add(nint* first, nint* last, IReadOnlyList<LibcurlFormaddArgument> call, string directory, List<nint> memory, List<nint> lists)
    {
        var a = new nint[ArgumentSlots];
        var index = 0;
        foreach (var argument in call)
        {
            a[index++] = argument.Option;
            if (argument.Kind != LibcurlFormaddValueKind.None)
            {
                a[index++] = ValueOf(argument, directory, memory, lists);
            }
        }

        a[index] = LibcurlFormaddCases.End;
        return _formAdd(first, last, a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15], a[16], a[17], a[18], a[19], a[20], a[21], a[22], a[23]);
    }

    private int Get(nint form, MemoryStream formGet)
    {
        var handle = GCHandle.Alloc(formGet);
        try
        {
            return _formGet(form, GCHandle.ToIntPtr(handle), &Append);
        }
        finally
        {
            handle.Free();
        }
    }

    private string Post(nint form, string url)
    {
        var easy = _easyInit();
        try
        {
            _setOption(easy, WriteFunctionOption, (nint)(delegate* unmanaged<byte*, nuint, nuint, nint, nuint>)&Discard);
            fixed (byte* text = Terminated(url))
            {
                _setOption(easy, UrlOption, (nint)text);
            }

            var setOption = _setOption(easy, HttpPostOption, form);
            return LibcurlMimeLogWriter.Transfer(setOption, _perform(easy));
        }
        finally
        {
            _cleanup(easy);
        }
    }

    private nint ValueOf(LibcurlFormaddArgument argument, string directory, List<nint> memory, List<nint> lists) => argument.Kind switch
    {
        LibcurlFormaddValueKind.Text => argument.Text is null ? 0 : Allocate(Terminated(argument.Text), memory),
        LibcurlFormaddValueKind.Bytes => Allocate([.. Convert.FromHexString(argument.Text!), 0], memory),
        LibcurlFormaddValueKind.Length => (nint)argument.Number,
        LibcurlFormaddValueKind.File => Allocate(Terminated(Path.Combine(directory, argument.Text!)), memory),
        LibcurlFormaddValueKind.Headers => List(argument.Lines, lists),
        LibcurlFormaddValueKind.Array => Forms(argument.Array, directory, memory, lists),
        _ => Allocate([0], memory),
    };

    /// <summary>A <c>struct curl_forms</c> array (an <c>int</c> option and a pointer, 16 bytes each) ending in <c>CURLFORM_END</c>.</summary>
    private nint Forms(IReadOnlyList<LibcurlFormaddArgument> forms, string directory, List<nint> memory, List<nint> lists)
    {
        var array = Marshal.AllocHGlobal(16 * (forms.Count + 1));
        memory.Add(array);
        for (var index = 0; index < forms.Count; index++)
        {
            Marshal.WriteInt64(array, 16 * index, forms[index].Option);
            Marshal.WriteIntPtr(array, (16 * index) + 8, ValueOf(forms[index], directory, memory, lists));
        }

        Marshal.WriteInt64(array, 16 * forms.Count, LibcurlFormaddCases.End);
        Marshal.WriteIntPtr(array, (16 * forms.Count) + 8, 0);
        return array;
    }

    private nint List(IReadOnlyList<string> lines, List<nint> lists)
    {
        nint list = 0;
        foreach (var line in lines)
        {
            fixed (byte* text = Terminated(line))
            {
                list = _slistAppend(list, text);
            }
        }

        lists.Add(list);
        return list;
    }

    private static nint Allocate(byte[] bytes, List<nint> memory)
    {
        var block = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, block, bytes.Length);
        memory.Add(block);
        return block;
    }

    private static byte[] Terminated(string text) => Encoding.UTF8.GetBytes(text + "\0");

    [UnmanagedCallersOnly]
    private static nuint Append(nint arg, byte* buffer, nuint length)
    {
        ((MemoryStream)GCHandle.FromIntPtr(arg).Target!).Write(new ReadOnlySpan<byte>(buffer, (int)length));
        return length;
    }

    [UnmanagedCallersOnly]
    private static nuint Discard(byte* data, nuint size, nuint count, nint user) => size * count;

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);
}
