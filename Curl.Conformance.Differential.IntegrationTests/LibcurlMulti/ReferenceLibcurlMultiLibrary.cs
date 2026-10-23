using System.Runtime.InteropServices;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMulti;

/// <summary>
/// The reference libcurl 8.22.0 shared library's multi, share and easy functions, loaded with
/// <see cref="NativeLibrary"/> and called through unmanaged function pointers (ADR-0792,
/// ADR-0793). The variadic <c>*_setopt</c> and <c>curl_easy_getinfo</c> are called with one
/// pointer-sized argument, which x64 Windows and x64 System V pass as a fixed argument.
/// </summary>
internal sealed unsafe class ReferenceLibcurlMultiLibrary : IDisposable
{
    private readonly nint _library;

    public ReferenceLibcurlMultiLibrary(string libraryPath)
    {
        _library = NativeLibrary.Load(libraryPath);
        EasyInit = (delegate* unmanaged<nint>)Export("curl_easy_init");
        EasySetOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_setopt");
        EasyPerform = (delegate* unmanaged<nint, int>)Export("curl_easy_perform");
        EasyGetInfo = (delegate* unmanaged<nint, int, nint, int>)Export("curl_easy_getinfo");
        EasyCleanup = (delegate* unmanaged<nint, void>)Export("curl_easy_cleanup");
        MultiInit = (delegate* unmanaged<nint>)Export("curl_multi_init");
        MultiAddHandle = (delegate* unmanaged<nint, nint, int>)Export("curl_multi_add_handle");
        MultiRemoveHandle = (delegate* unmanaged<nint, nint, int>)Export("curl_multi_remove_handle");
        MultiSetOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_multi_setopt");
        MultiPerform = (delegate* unmanaged<nint, int*, int>)Export("curl_multi_perform");
        MultiPoll = (delegate* unmanaged<nint, nint, uint, int, int*, int>)Export("curl_multi_poll");
        MultiWait = (delegate* unmanaged<nint, nint, uint, int, int*, int>)Export("curl_multi_wait");
        MultiInfoRead = (delegate* unmanaged<nint, int*, nint>)Export("curl_multi_info_read");
        MultiGetHandles = (delegate* unmanaged<nint, nint*>)Export("curl_multi_get_handles");
        MultiCleanup = (delegate* unmanaged<nint, int>)Export("curl_multi_cleanup");
        ShareInit = (delegate* unmanaged<nint>)Export("curl_share_init");
        ShareSetOption = (delegate* unmanaged<nint, int, nint, int>)Export("curl_share_setopt");
        ShareCleanup = (delegate* unmanaged<nint, int>)Export("curl_share_cleanup");
        Free = (delegate* unmanaged<nint, void>)Export("curl_free");
        ((delegate* unmanaged<long, int>)Export("curl_global_init"))(3);
    }

    public delegate* unmanaged<nint> EasyInit { get; }

    public delegate* unmanaged<nint, int, nint, int> EasySetOption { get; }

    public delegate* unmanaged<nint, int> EasyPerform { get; }

    public delegate* unmanaged<nint, int, nint, int> EasyGetInfo { get; }

    public delegate* unmanaged<nint, void> EasyCleanup { get; }

    public delegate* unmanaged<nint> MultiInit { get; }

    public delegate* unmanaged<nint, nint, int> MultiAddHandle { get; }

    public delegate* unmanaged<nint, nint, int> MultiRemoveHandle { get; }

    public delegate* unmanaged<nint, int, nint, int> MultiSetOption { get; }

    public delegate* unmanaged<nint, int*, int> MultiPerform { get; }

    public delegate* unmanaged<nint, nint, uint, int, int*, int> MultiPoll { get; }

    public delegate* unmanaged<nint, nint, uint, int, int*, int> MultiWait { get; }

    public delegate* unmanaged<nint, int*, nint> MultiInfoRead { get; }

    public delegate* unmanaged<nint, nint*> MultiGetHandles { get; }

    public delegate* unmanaged<nint, int> MultiCleanup { get; }

    public delegate* unmanaged<nint> ShareInit { get; }

    public delegate* unmanaged<nint, int, nint, int> ShareSetOption { get; }

    public delegate* unmanaged<nint, int> ShareCleanup { get; }

    public delegate* unmanaged<nint, void> Free { get; }

    public void Dispose() => NativeLibrary.Free(_library);

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);
}
