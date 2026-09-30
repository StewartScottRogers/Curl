using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpNegotiateInfoLines" />: when curl 8.21.0 writes
/// <c>Server auth using Negotiate</c>, and which header of a 401 the authenticator's lines go
/// before (measured, BL-843 Notes).
/// </summary>
[TestClass]
public sealed class HttpNegotiateInfoLinesTests
{
    [TestMethod]
    [DataRow("Negotiate YII=", false, HttpAuthSchemes.Any, true, DisplayName = "Negotiate value sent")]
    [DataRow("Basic dTpw", false, HttpAuthSchemes.Negotiate, false, DisplayName = "Another scheme's value")]
    [DataRow("", true, HttpAuthSchemes.Any, true, DisplayName = "Sent again without a header, --anyauth")]
    [DataRow(null, false, HttpAuthSchemes.Negotiate, true, DisplayName = "First request, --negotiate alone, no token")]
    [DataRow(null, false, HttpAuthSchemes.Any, false, DisplayName = "First request, --anyauth")]
    [DataRow(null, true, HttpAuthSchemes.Negotiate, false, DisplayName = "No value in answer to a challenge")]
    public void PicksNegotiate_Request_DecidesAsCurl(string? authorization, bool answersChallenge, HttpAuthSchemes allowed, bool expected)
    {
        Assert.AreEqual(expected, HttpNegotiateInfoLines.PicksNegotiate(Request(allowed), authorization, answersChallenge));
    }

    [TestMethod]
    [DataRow(401, HttpAuthSchemes.Negotiate, "WWW-Authenticate", "Negotiate", true, DisplayName = "Bare Negotiate")]
    [DataRow(401, HttpAuthSchemes.Any, "www-authenticate", "negotiate YII=", true, DisplayName = "Any case, with a token")]
    [DataRow(407, HttpAuthSchemes.Negotiate, "WWW-Authenticate", "Negotiate", false, DisplayName = "Not a 401")]
    [DataRow(401, HttpAuthSchemes.Basic, "WWW-Authenticate", "Negotiate", false, DisplayName = "Negotiate not allowed")]
    [DataRow(401, HttpAuthSchemes.Negotiate, "Proxy-Authenticate", "Negotiate", false, DisplayName = "Another header")]
    [DataRow(401, HttpAuthSchemes.Negotiate, "WWW-Authenticate", "Basic realm=\"r\"", false, DisplayName = "Another scheme")]
    [DataRow(401, HttpAuthSchemes.Negotiate, "WWW-Authenticate", "NegotiateX", false, DisplayName = "A longer scheme name")]
    public void IsNegotiateChallenge_Header_DecidesWhetherTheLinesGoBeforeIt(int statusCode, HttpAuthSchemes allowed, string name, string value, bool expected)
    {
        HttpStatusLine statusLine = HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X");

        Assert.AreEqual(expected, HttpNegotiateInfoLines.IsNegotiateChallenge(Request(allowed), statusLine, new HttpResponseHeader(name, value)));
    }

    private static HttpAuthRequest Request(HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", null, null, allowed, IsProxy: false);
}
