using System.Runtime.InteropServices;
using Curl.Conformance.Differential.Libcurl.Multi;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMulti;

/// <summary>Runs a multi or share case's calls through the reference libcurl 8.22.0, as the tester's <c>multitest.py</c> did through <c>ctypes</c> (ADR-0793).</summary>
internal sealed unsafe class ReferenceLibcurlMultiTarget(ReferenceLibcurlMultiLibrary curl) : ILibcurlMultiTarget
{
    private const int ResponseCodeInfo = 0x200002;
    private const int NumConnectsInfo = 0x20001A;

    private readonly nint _multi = curl.MultiInit();
    private readonly nint _share = curl.ShareInit();
    private readonly List<nint> _easies = [];
    private readonly List<MemoryStream> _bodies = [];
    private readonly List<GCHandle> _handles = [];
    private readonly List<nint> _kept = [];
    private readonly HashSet<int> _freed = [];
    private bool _multiCleaned;
    private bool _shareCleaned;

    public IReadOnlyList<int> AddEasy(IReadOnlyList<LibcurlMultiValue> options)
    {
        var easy = curl.EasyInit();
        var body = new MemoryStream();
        var handle = GCHandle.Alloc(body);
        _easies.Add(easy);
        _bodies.Add(body);
        _handles.Add(handle);
        curl.EasySetOption(easy, 10001, GCHandle.ToIntPtr(handle));
        curl.EasySetOption(easy, 20011, (nint)(delegate* unmanaged<byte*, nuint, nuint, nint, nuint>)&Collect);
        return [.. options.Select(option => curl.EasySetOption(easy, option.Id, option.Kind == "share" ? _share : Value(option)))];
    }

    public int AddHandle(int? easy) => curl.MultiAddHandle(_multi, easy is { } index ? _easies[index] : 0);

    public int RemoveHandle(int? easy) => curl.MultiRemoveHandle(_multi, easy is { } index ? _easies[index] : 0);

    public int SetMultiOption(LibcurlMultiValue setting) => curl.MultiSetOption(_multi, setting.Id, Value(setting));

    public ValueTask<(int Code, int Running)> PerformAsync()
    {
        int running = 0;
        var code = curl.MultiPerform(_multi, &running);
        return ValueTask.FromResult((code, running));
    }

    public ValueTask<(int Code, int Ready)> PollAsync(int timeoutMilliseconds)
    {
        int ready = 0;
        var code = curl.MultiPoll(_multi, 0, 0, timeoutMilliseconds, &ready);
        return ValueTask.FromResult((code, ready));
    }

    public ValueTask<(int Code, int Ready)> WaitAsync(int timeoutMilliseconds)
    {
        int ready = 0;
        var code = curl.MultiWait(_multi, 0, 0, timeoutMilliseconds, &ready);
        return ValueTask.FromResult((code, ready));
    }

    public IReadOnlyList<LibcurlMultiMessage> ReadMessages()
    {
        // struct CURLMsg { CURLMSG msg; CURL *easy_handle; union { void *whatever; CURLcode result; } data; }
        var messages = new List<LibcurlMultiMessage>();
        int queue = 0;
        nint message;
        while ((message = curl.MultiInfoRead(_multi, &queue)) != 0)
        {
            var fields = (nint*)message;
            messages.Add(new(*(int*)fields, _easies.IndexOf(fields[1]), *(int*)(fields + 2), queue));
        }

        return messages;
    }

    public (byte[] Body, long ResponseCode) Body(int easy) => (_bodies[easy].ToArray(), Info(_easies[easy], ResponseCodeInfo));

    public int HandleCount()
    {
        var handles = curl.MultiGetHandles(_multi);
        var count = 0;
        while (handles[count] != 0)
        {
            count++;
        }

        curl.Free((nint)handles);
        return count;
    }

    public void CleanupEasy(int easy)
    {
        _freed.Add(easy);
        curl.EasyCleanup(_easies[easy]);
    }

    public ValueTask CleanupMultiAsync()
    {
        _multiCleaned = true;
        curl.MultiCleanup(_multi);
        return ValueTask.CompletedTask;
    }

    public int SetShareOption(LibcurlMultiValue setting) => curl.ShareSetOption(_share, setting.Id, setting.Kind == "null" ? 0 : Value(setting));

    public (int Code, long ResponseCode, long Connects) PerformEasy(int easy)
    {
        var code = curl.EasyPerform(_easies[easy]);
        return (code, Info(_easies[easy], ResponseCodeInfo), Info(_easies[easy], NumConnectsInfo));
    }

    public int CleanupShare()
    {
        var code = curl.ShareCleanup(_share);
        _shareCleaned |= code == 0;
        return code;
    }

    public ValueTask DisposeAsync()
    {
        if (!_multiCleaned)
        {
            curl.MultiCleanup(_multi);
        }

        for (var index = 0; index < _easies.Count; index++)
        {
            if (!_freed.Contains(index))
            {
                curl.EasyCleanup(_easies[index]);
            }
        }

        if (!_shareCleaned)
        {
            curl.ShareCleanup(_share);
        }

        _handles.ForEach(handle => handle.Free());
        _kept.ForEach(Marshal.FreeCoTaskMem);
        return ValueTask.CompletedTask;
    }

    private nint Value(LibcurlMultiValue value)
    {
        if (value.Kind == "l")
        {
            return (nint)value.Number;
        }

        var text = Marshal.StringToCoTaskMemUTF8(value.Text);
        _kept.Add(text);
        return text;
    }

    private long Info(nint easy, int id)
    {
        long slot = 0;
        curl.EasyGetInfo(easy, id, (nint)(&slot));
        return OperatingSystem.IsWindows() ? (int)slot : slot;
    }

    [UnmanagedCallersOnly]
    private static nuint Collect(byte* data, nuint size, nuint count, nint sink)
    {
        var length = size * count;
        ((MemoryStream)GCHandle.FromIntPtr(sink).Target!).Write(new ReadOnlySpan<byte>(data, (int)length));
        return length;
    }
}
