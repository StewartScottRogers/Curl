using Curl.Api.Codes;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlShareCodeTests
{
    // CURLSHE_* in curl 8.22.0's include/curl/curl.h.
    private static readonly (CurlShareCode Code, int Number)[] UpstreamCodes =
    [
        (CurlShareCode.Ok, 0), (CurlShareCode.BadOption, 1), (CurlShareCode.InUse, 2),
        (CurlShareCode.Invalid, 3), (CurlShareCode.NoMem, 4), (CurlShareCode.NotBuiltIn, 5),
    ];

    [TestMethod]
    public void Members_UpstreamTable_MatchExactlyWithUpstreamNumbers() => UpstreamCodeTable.AssertMatches(UpstreamCodes);
}
