using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="IShareHandle"/> whose every call succeeds and shares nothing.</summary>
internal sealed class FakeShareHandle : IShareHandle
{
    public CurlShareCode Share(CurlLockData data) => CurlShareCode.Ok;

    public CurlShareCode Unshare(CurlLockData data) => CurlShareCode.Ok;

    public CurlShareCode SetLockCallback(ShareLockCallback? callback) => CurlShareCode.Ok;

    public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback) => CurlShareCode.Ok;

    public CurlShareCode Cleanup() => CurlShareCode.Ok;
}
