using System.Runtime.InteropServices;
using System.Text;
using Curl.Conformance.Differential.Libcurl.Mime;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMime;

/// <summary>
/// Runs a MIME case through the reference libcurl 8.22.0 shared library, as the tester's
/// <c>mimetest.py</c> did through <c>ctypes</c>: loaded with <see cref="NativeLibrary"/> and
/// called through unmanaged function pointers (ADR-0792).
/// </summary>
internal sealed unsafe class ReferenceLibcurlMimeDriver : IDisposable
{
    private const int UrlOption = 10002;

    private const int WriteFunctionOption = 20011;

    private const int MimePostOption = 10269;

    private readonly nint _library;
    private readonly delegate* unmanaged<nint> _easyInit;
    private readonly delegate* unmanaged<nint, int, nint, int> _setOption;
    private readonly delegate* unmanaged<nint, int> _perform;
    private readonly delegate* unmanaged<nint, void> _cleanup;
    private readonly delegate* unmanaged<nint, nint> _mimeInit;
    private readonly delegate* unmanaged<nint, void> _mimeFree;
    private readonly delegate* unmanaged<nint, nint> _addPart;
    private readonly delegate* unmanaged<nint, byte*, int> _name;
    private readonly delegate* unmanaged<nint, byte*, int> _fileName;
    private readonly delegate* unmanaged<nint, byte*, int> _type;
    private readonly delegate* unmanaged<nint, byte*, int> _encoder;
    private readonly delegate* unmanaged<nint, byte*, int> _fileData;
    private readonly delegate* unmanaged<nint, byte*, nuint, int> _data;
    private readonly delegate* unmanaged<nint, nint, int, int> _headers;
    private readonly delegate* unmanaged<nint, nint, int> _subParts;
    private readonly delegate* unmanaged<nint, byte*, nint> _slistAppend;

    public ReferenceLibcurlMimeDriver(string libraryPath)
    {
        _library = NativeLibrary.Load(libraryPath);
        _easyInit = (delegate* unmanaged<nint>)Export("curl_easy_init");
        _setOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_setopt");
        _perform = (delegate* unmanaged<nint, int>)Export("curl_easy_perform");
        _cleanup = (delegate* unmanaged<nint, void>)Export("curl_easy_cleanup");
        _mimeInit = (delegate* unmanaged<nint, nint>)Export("curl_mime_init");
        _mimeFree = (delegate* unmanaged<nint, void>)Export("curl_mime_free");
        _addPart = (delegate* unmanaged<nint, nint>)Export("curl_mime_addpart");
        _name = (delegate* unmanaged<nint, byte*, int>)Export("curl_mime_name");
        _fileName = (delegate* unmanaged<nint, byte*, int>)Export("curl_mime_filename");
        _type = (delegate* unmanaged<nint, byte*, int>)Export("curl_mime_type");
        _encoder = (delegate* unmanaged<nint, byte*, int>)Export("curl_mime_encoder");
        _fileData = (delegate* unmanaged<nint, byte*, int>)Export("curl_mime_filedata");
        _data = (delegate* unmanaged<nint, byte*, nuint, int>)Export("curl_mime_data");
        _headers = (delegate* unmanaged<nint, nint, int, int>)Export("curl_mime_headers");
        _subParts = (delegate* unmanaged<nint, nint, int>)Export("curl_mime_subparts");
        _slistAppend = (delegate* unmanaged<nint, byte*, nint>)Export("curl_slist_append");
        ((delegate* unmanaged<long, int>)Export("curl_global_init"))(3);
    }

    /// <summary>Runs <paramref name="mimeCase"/> against <paramref name="url"/>.</summary>
    /// <returns>The result-code log, entries separated by spaces.</returns>
    public string Run(LibcurlMimeCase mimeCase, string url, string directory)
    {
        var easy = _easyInit();
        var log = new List<string>();
        var mime = _mimeInit(easy);
        try
        {
            _setOption(easy, WriteFunctionOption, (nint)(delegate* unmanaged<byte*, nuint, nuint, nint, nuint>)&Discard);
            fixed (byte* text = Terminated(url))
            {
                _setOption(easy, UrlOption, (nint)text);
            }

            Fill(easy, mime, mimeCase.Parts, log, directory);
            var setOption = _setOption(easy, MimePostOption, mime);
            log.Add(LibcurlMimeLogWriter.Transfer(setOption, _perform(easy)));
            return string.Join(' ', log);
        }
        finally
        {
            _cleanup(easy);
            _mimeFree(mime);
        }
    }

    public void Dispose() => NativeLibrary.Free(_library);

    private void Fill(nint easy, nint mime, IReadOnlyList<LibcurlMimePart> parts, List<string> log, string directory)
    {
        foreach (var part in parts)
        {
            var added = _addPart(mime);
            foreach (var step in part.Steps)
            {
                log.Add(LibcurlMimeLogWriter.Step(step.Kind, Apply(easy, added, step, log, directory)));
            }
        }
    }

    private int Apply(nint easy, nint part, LibcurlMimeStep step, List<string> log, string directory) => step.Kind switch
    {
        LibcurlMimeStepKind.Name => CallText(_name, part, step.Text),
        LibcurlMimeStepKind.FileName => CallText(_fileName, part, step.Text),
        LibcurlMimeStepKind.Type => CallText(_type, part, step.Text),
        LibcurlMimeStepKind.Encoder => CallText(_encoder, part, step.Text),
        LibcurlMimeStepKind.Data => CallData(part, step.Text),
        LibcurlMimeStepKind.File => CallText(_fileData, part, Path.Combine(directory, step.Text!)),
        LibcurlMimeStepKind.Headers => _headers(part, List(step.Headers), 1),
        _ => _subParts(part, SubParts(easy, step, log, directory)),
    };

    private nint SubParts(nint easy, LibcurlMimeStep step, List<string> log, string directory)
    {
        var sub = _mimeInit(easy);
        Fill(easy, sub, step.SubParts, log, directory);
        return sub;
    }

    private static int CallText(delegate* unmanaged<nint, byte*, int> call, nint part, string? text)
    {
        if (text is null)
        {
            return call(part, null);
        }

        fixed (byte* bytes = Terminated(text))
        {
            return call(part, bytes);
        }
    }

    private int CallData(nint part, string? hex)
    {
        if (hex is null)
        {
            return _data(part, null, 0);
        }

        // One spare byte, so empty data passes a non-null pointer as ctypes did.
        var bytes = Convert.FromHexString(hex);
        var buffer = new byte[bytes.Length + 1];
        bytes.CopyTo(buffer, 0);
        fixed (byte* data = buffer)
        {
            return _data(part, data, (nuint)bytes.Length);
        }
    }

    private nint List(IReadOnlyList<string> lines)
    {
        nint list = 0;
        foreach (var line in lines)
        {
            fixed (byte* text = Terminated(line))
            {
                list = _slistAppend(list, text);
            }
        }

        return list;
    }

    private static byte[] Terminated(string text) => Encoding.UTF8.GetBytes(text + "\0");

    [UnmanagedCallersOnly]
    private static nuint Discard(byte* data, nuint size, nuint count, nint user) => size * count;

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);
}
