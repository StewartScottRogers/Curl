using Curl.Api.Codes;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlUrlCodeTests
{
    // CURLUE_* in curl 8.22.0's include/curl/urlapi.h.
    private static readonly (CurlUrlCode Code, int Number)[] UpstreamCodes =
    [
        (CurlUrlCode.Ok, 0), (CurlUrlCode.BadHandle, 1), (CurlUrlCode.BadPartPointer, 2),
        (CurlUrlCode.MalformedInput, 3), (CurlUrlCode.BadPortNumber, 4), (CurlUrlCode.UnsupportedScheme, 5),
        (CurlUrlCode.UrlDecode, 6), (CurlUrlCode.OutOfMemory, 7), (CurlUrlCode.UserNotAllowed, 8),
        (CurlUrlCode.UnknownPart, 9), (CurlUrlCode.NoScheme, 10), (CurlUrlCode.NoUser, 11),
        (CurlUrlCode.NoPassword, 12), (CurlUrlCode.NoOptions, 13), (CurlUrlCode.NoHost, 14),
        (CurlUrlCode.NoPort, 15), (CurlUrlCode.NoQuery, 16), (CurlUrlCode.NoFragment, 17),
        (CurlUrlCode.NoZoneId, 18), (CurlUrlCode.BadFileUrl, 19), (CurlUrlCode.BadFragment, 20),
        (CurlUrlCode.BadHostname, 21), (CurlUrlCode.BadIPv6, 22), (CurlUrlCode.BadLogin, 23),
        (CurlUrlCode.BadPassword, 24), (CurlUrlCode.BadPath, 25), (CurlUrlCode.BadQuery, 26),
        (CurlUrlCode.BadScheme, 27), (CurlUrlCode.BadSlashes, 28), (CurlUrlCode.BadUser, 29),
        (CurlUrlCode.LacksIdn, 30), (CurlUrlCode.TooLarge, 31), (CurlUrlCode.Backslash, 32),
    ];

    [TestMethod]
    public void Members_UpstreamTable_MatchExactlyWithUpstreamNumbers() => UpstreamCodeTable.AssertMatches(UpstreamCodes);
}
