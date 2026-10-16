using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;

namespace Libcurl.UnitTests.Share;

/// <summary>
/// An <see cref="IShareHandle"/> that records every call, answers <see cref="Answer"/> to each, and lets a
/// test call the lock callbacks it was given as the engine would.
/// </summary>
internal sealed class FakeShareHandle : IShareHandle
{
    public CurlShareCode Answer { get; set; } = CurlShareCode.Ok;

    public List<string> Calls { get; } = [];

    public ShareLockCallback? LockCallback { get; private set; }

    public ShareUnlockCallback? UnlockCallback { get; private set; }

    public CurlShareCode Share(CurlLockData data) => Record($"Share {data}");

    public CurlShareCode Unshare(CurlLockData data) => Record($"Unshare {data}");

    public CurlShareCode SetLockCallback(ShareLockCallback? callback)
    {
        LockCallback = callback;
        return Record("SetLockCallback");
    }

    public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback)
    {
        UnlockCallback = callback;
        return Record("SetUnlockCallback");
    }

    public CurlShareCode Cleanup() => Record("Cleanup");

    private CurlShareCode Record(string call)
    {
        Calls.Add(call);
        return Answer;
    }
}
