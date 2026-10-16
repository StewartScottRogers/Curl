using Curl.Api.Codes;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlHeaderCodeTests
{
    // CURLHE_* in curl 8.22.0's include/curl/header.h.
    private static readonly (CurlHeaderCode Code, int Number)[] UpstreamCodes =
    [
        (CurlHeaderCode.Ok, 0), (CurlHeaderCode.BadIndex, 1), (CurlHeaderCode.Missing, 2),
        (CurlHeaderCode.NoHeaders, 3), (CurlHeaderCode.NoRequest, 4), (CurlHeaderCode.OutOfMemory, 5),
        (CurlHeaderCode.BadArgument, 6), (CurlHeaderCode.NotBuiltIn, 7),
    ];

    [TestMethod]
    public void Members_UpstreamTable_MatchExactlyWithUpstreamNumbers() => UpstreamCodeTable.AssertMatches(UpstreamCodes);
}
