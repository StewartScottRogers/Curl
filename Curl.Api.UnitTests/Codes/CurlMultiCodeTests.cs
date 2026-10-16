using Curl.Api.Codes;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlMultiCodeTests
{
    // CURLM_* in curl 8.22.0's include/curl/multi.h.
    private static readonly (CurlMultiCode Code, int Number)[] UpstreamCodes =
    [
        (CurlMultiCode.CallMultiPerform, -1), (CurlMultiCode.Ok, 0), (CurlMultiCode.BadHandle, 1),
        (CurlMultiCode.BadEasyHandle, 2), (CurlMultiCode.OutOfMemory, 3), (CurlMultiCode.InternalError, 4),
        (CurlMultiCode.BadSocket, 5), (CurlMultiCode.UnknownOption, 6), (CurlMultiCode.AddedAlready, 7),
        (CurlMultiCode.RecursiveApiCall, 8), (CurlMultiCode.WakeupFailure, 9), (CurlMultiCode.BadFunctionArgument, 10),
        (CurlMultiCode.AbortedByCallback, 11), (CurlMultiCode.UnrecoverablePoll, 12),
    ];

    [TestMethod]
    public void Members_UpstreamTable_MatchExactlyWithUpstreamNumbers() => UpstreamCodeTable.AssertMatches(UpstreamCodes);
}
