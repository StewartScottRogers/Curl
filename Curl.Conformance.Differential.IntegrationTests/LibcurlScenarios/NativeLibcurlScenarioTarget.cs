using System.Runtime.InteropServices;
using System.Text;
using Curl.Conformance.Differential.Libcurl.Scenarios;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlScenarios;

/// <summary>
/// Replays <c>scen.json</c> scenarios against the reference libcurl 8.22.0, a native library
/// loaded from a path with <see cref="NativeLibrary"/> and called through delegates over its
/// exports, as <c>libtest.py</c> called it through ctypes (ADR-0791).
/// </summary>
internal sealed class NativeLibcurlScenarioTarget : ILibcurlScenarioTarget
{
    /// <summary><c>CURL_GLOBAL_ALL</c>.</summary>
    private const int GlobalAll = 3;

    private readonly IntPtr library;
    private readonly IntToText easyStrError;
    private readonly IntToText multiStrError;
    private readonly IntToText shareStrError;
    private readonly IntToText urlStrError;
    private readonly VersionText version;
    private readonly UrlCreate urlCreate;
    private readonly UrlCleanup urlCleanup;
    private readonly UrlSet urlSet;
    private readonly UrlGet urlGet;
    private readonly Free free;
    private readonly EasyEscape easyEscape;
    private readonly EasyUnescape easyUnescape;
    private readonly GetDate getDate;

    public NativeLibcurlScenarioTarget(string path)
    {
        library = NativeLibrary.Load(path);
        easyStrError = Export<IntToText>("curl_easy_strerror");
        multiStrError = Export<IntToText>("curl_multi_strerror");
        shareStrError = Export<IntToText>("curl_share_strerror");
        urlStrError = Export<IntToText>("curl_url_strerror");
        version = Export<VersionText>("curl_version");
        urlCreate = Export<UrlCreate>("curl_url");
        urlCleanup = Export<UrlCleanup>("curl_url_cleanup");
        urlSet = Export<UrlSet>("curl_url_set");
        urlGet = Export<UrlGet>("curl_url_get");
        free = Export<Free>("curl_free");
        easyEscape = Export<EasyEscape>("curl_easy_escape");
        easyUnescape = Export<EasyUnescape>("curl_easy_unescape");
        getDate = Export<GetDate>("curl_getdate");
        Export<GlobalInit>("curl_global_init")(new CLong(GlobalAll));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GlobalInit(CLong flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr IntToText(int code);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr VersionText();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr UrlCreate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UrlCleanup(IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int UrlSet(IntPtr handle, int part, byte[]? content, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int UrlGet(IntPtr handle, int part, out IntPtr content, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Free(IntPtr pointer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr EasyEscape(IntPtr easy, byte[] text, int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr EasyUnescape(IntPtr easy, byte[] text, int length, out int outputLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CLong GetDate(byte[] text, IntPtr unused);

    public string Version() => Text(version());

    public string EasyErrorText(int code) => Text(easyStrError(code));

    public string MultiErrorText(int code) => Text(multiStrError(code));

    public string ShareErrorText(int code) => Text(shareStrError(code));

    public string UrlErrorText(int code) => Text(urlStrError(code));

    public ILibcurlScenarioUrl CreateUrl() => new NativeUrl(this, urlCreate());

    public string Escape(byte[] bytes)
    {
        var escaped = easyEscape(IntPtr.Zero, Terminated(bytes), bytes.Length);
        var text = Text(escaped);
        free(escaped);
        return text;
    }

    public byte[]? Unescape(byte[] bytes)
    {
        var unescaped = easyUnescape(IntPtr.Zero, Terminated(bytes), bytes.Length, out var length);
        if (unescaped == IntPtr.Zero)
        {
            return null;
        }

        var result = new byte[length];
        Marshal.Copy(unescaped, result, 0, length);
        free(unescaped);
        return result;
    }

    public long ParseDate(string text) => getDate(Terminated(Encoding.UTF8.GetBytes(text)), IntPtr.Zero).Value;

    private static string Text(IntPtr text) => Marshal.PtrToStringUTF8(text) ?? string.Empty;

    private static byte[] Terminated(byte[] bytes) => [.. bytes, 0];

    private T Export<T>(string name)
        where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    private sealed class NativeUrl(NativeLibcurlScenarioTarget target, IntPtr handle) : ILibcurlScenarioUrl
    {
        public int Set(int part, string? value, int flags) =>
            target.urlSet(handle, part, value is null ? null : Terminated(Encoding.UTF8.GetBytes(value)), (uint)flags);

        public int Get(int part, int flags, out byte[]? value)
        {
            var code = target.urlGet(handle, part, out var content, (uint)flags);
            value = code == 0 && content != IntPtr.Zero ? ReadTerminated(content) : null;
            if (content != IntPtr.Zero)
            {
                target.free(content);
            }

            return code;
        }

        public void Dispose() => target.urlCleanup(handle);

        private static byte[] ReadTerminated(IntPtr content)
        {
            var length = 0;
            while (Marshal.ReadByte(content, length) != 0)
            {
                length++;
            }

            var bytes = new byte[length];
            Marshal.Copy(content, bytes, 0, length);
            return bytes;
        }
    }
}
