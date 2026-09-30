using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsNegotiateInfoLines" />: which requests curl picks Negotiate for, and which
/// head lines and challenges offer it (BL-955).
/// </summary>
[TestClass]
public sealed class WsNegotiateInfoLinesTests
{
    [TestMethod]
    [DataRow("Negotiate", true)]
    [DataRow("negotiate YIIB", true)]
    [DataRow("NegotiateX", false)]
    [DataRow("Negotiat", false)]
    [DataRow("Basic realm=\"x\"", false)]
    public void OffersNegotiate_Challenge_ReadsTheFirstToken(string challenge, bool expected) =>
        Assert.AreEqual(expected, WsNegotiateInfoLines.OffersNegotiate(challenge));

    [TestMethod]
    [DataRow("WWW-Authenticate: Negotiate\r\n", true)]
    [DataRow("www-authenticate:negotiate\r\n", true)]
    [DataRow("X-WWW-Authenticate: Negotiate\r\n", false)]
    [DataRow("WWW-Authenticate: NTLM\r\n", false)]
    public void IsNegotiateChallenge_HeadLine_ReadsNameAndScheme(string line, bool expected) =>
        Assert.AreEqual(expected, WsNegotiateInfoLines.IsNegotiateChallenge(Encoding.Latin1.GetBytes(line)));

    [TestMethod]
    public void ChallengesOf_Head_GivesEveryWwwAuthenticateValueTrimmedInOrder()
    {
        byte[] head = Encoding.Latin1.GetBytes("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nX-A: 1\r\nwww-authenticate:  Negotiate \r\n\r\n");

        CollectionAssert.AreEqual(new[] { "Basic realm=\"x\"", "Negotiate" }, WsNegotiateInfoLines.ChallengesOf(head));
    }

    [TestMethod]
    [DataRow("Negotiate YIIB", HttpAuthSchemes.Basic, true)]
    [DataRow("Basic dTpw", HttpAuthSchemes.Negotiate, false)]
    [DataRow(null, HttpAuthSchemes.Negotiate, true)]
    [DataRow(null, HttpAuthSchemes.Any, false)]
    public void PicksNegotiate_Request_DecidesAsCurl(string? authorization, HttpAuthSchemes schemes, bool expected)
    {
        var request = new HttpAuthRequest("GET", CurlUrl.Parse("ws://h/"), "/", null, null, schemes, IsProxy: false);

        Assert.AreEqual(expected, WsNegotiateInfoLines.PicksNegotiate(request, authorization));
    }
}
