using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="IShareHandle"/> that keeps its lock callbacks, answers <see cref="LockResult"/> and <see cref="CleanupResult"/>, and shares nothing.</summary>
internal sealed class FakeShareHandle : IShareHandle
{
    public CurlShareCode LockResult { get; init; } = CurlShareCode.Ok;

    public CurlShareCode CleanupResult { get; init; } = CurlShareCode.Ok;

    public ShareLockCallback? Lock { get; private set; }

    public ShareUnlockCallback? Unlock { get; private set; }

    public CurlShareCode ShareResult { get; init; } = CurlShareCode.Ok;

    public CurlShareCode Share(CurlLockData data) => ShareResult;

    public CurlShareCode Unshare(CurlLockData data) => CurlShareCode.Ok;

    public CurlShareCode SetLockCallback(ShareLockCallback? callback)
    {
        Lock = callback;
        return LockResult;
    }

    public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback)
    {
        Unlock = callback;
        return CurlShareCode.Ok;
    }

    public CurlShareCode Cleanup() => CleanupResult;
}
