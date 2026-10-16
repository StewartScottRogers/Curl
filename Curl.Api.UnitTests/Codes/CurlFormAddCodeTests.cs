using Curl.Api.Codes;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlFormAddCodeTests
{
    // CURL_FORMADD_* in curl 8.22.0's include/curl/curl.h.
    private static readonly (CurlFormAddCode Code, int Number)[] UpstreamCodes =
    [
        (CurlFormAddCode.Ok, 0), (CurlFormAddCode.Memory, 1), (CurlFormAddCode.OptionTwice, 2),
        (CurlFormAddCode.Null, 3), (CurlFormAddCode.UnknownOption, 4), (CurlFormAddCode.Incomplete, 5),
        (CurlFormAddCode.IllegalArray, 6), (CurlFormAddCode.Disabled, 7),
    ];

    [TestMethod]
    public void Members_UpstreamTable_MatchExactlyWithUpstreamNumbers() => UpstreamCodeTable.AssertMatches(UpstreamCodes);
}
