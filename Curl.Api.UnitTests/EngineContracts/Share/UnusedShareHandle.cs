using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;

namespace Curl.Api.UnitTests.EngineContracts.Share;

/// <summary>A share handle that only stands in as a value; calling any member fails the test.</summary>
internal sealed class UnusedShareHandle : IShareHandle
{
    public CurlShareCode Share(CurlLockData data) => throw new NotSupportedException();

    public CurlShareCode Unshare(CurlLockData data) => throw new NotSupportedException();

    public CurlShareCode SetLockCallback(ShareLockCallback? callback) => throw new NotSupportedException();

    public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback) => throw new NotSupportedException();

    public CurlShareCode Cleanup() => throw new NotSupportedException();
}
