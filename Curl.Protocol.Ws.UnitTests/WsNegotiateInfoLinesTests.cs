using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsNegotiateInfoLines" />: which requests curl picks Negotiate for, and which
/// head lines and challenges offer it (BL-955).
/// </summary>
[TestClass]
public sealed class WsNegotiateInfoLinesTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("Negotiate", true)]
    [DataRow("negotiate YIIB", true)]
    [DataRow("NegotiateX", false)]
    [DataRow("Negotiat", false)]
    [DataRow("Basic realm=\"x\"", false)]
    public void OffersNegotiate_Challenge_ReadsTheFirstToken(string challenge, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("challenge", challenge);

        bool actual = WsNegotiateInfoLines.OffersNegotiate(challenge);

        diagnostics.Act("offers Negotiate", actual);
        diagnostics.Assert("offers Negotiate", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("WWW-Authenticate: Negotiate\r\n", true)]
    [DataRow("www-authenticate:negotiate\r\n", true)]
    [DataRow("X-WWW-Authenticate: Negotiate\r\n", false)]
    [DataRow("WWW-Authenticate: NTLM\r\n", false)]
    public void IsNegotiateChallenge_HeadLine_ReadsNameAndScheme(string line, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] bytes = Encoding.Latin1.GetBytes(line);
        diagnostics.Bytes("head line", bytes);
        diagnostics.Arrange("head line", line);

        bool actual = WsNegotiateInfoLines.IsNegotiateChallenge(bytes);

        diagnostics.Act("is Negotiate challenge", actual);
        diagnostics.Assert("is Negotiate challenge", expected, actual);
        Assert.AreEqual(expected, WsNegotiateInfoLines.IsNegotiateChallenge(Encoding.Latin1.GetBytes(line)));
    }

    [TestMethod]
    public void ChallengesOf_Head_GivesEveryWwwAuthenticateValueTrimmedInOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] head = Encoding.Latin1.GetBytes("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nX-A: 1\r\nwww-authenticate:  Negotiate \r\n\r\n");
        diagnostics.Bytes("response head", head);
        diagnostics.Arrange("response head length", head.Length);

        var challenges = WsNegotiateInfoLines.ChallengesOf(head);

        diagnostics.Act("challenges", string.Join(" | ", challenges));
        diagnostics.Assert("challenges", "Basic realm=\"x\" | Negotiate", string.Join(" | ", challenges));
        CollectionAssert.AreEqual(new[] { "Basic realm=\"x\"", "Negotiate" }, WsNegotiateInfoLines.ChallengesOf(head));
    }

    [TestMethod]
    [DataRow("Negotiate YIIB", HttpAuthSchemes.Basic, true)]
    [DataRow("Basic dTpw", HttpAuthSchemes.Negotiate, false)]
    [DataRow(null, HttpAuthSchemes.Negotiate, true)]
    [DataRow(null, HttpAuthSchemes.Any, false)]
    public void PicksNegotiate_Request_DecidesAsCurl(string? authorization, HttpAuthSchemes schemes, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var request = new HttpAuthRequest("GET", CurlUrl.Parse("ws://h/"), "/", null, null, schemes, IsProxy: false);
        diagnostics.Arrange("authorization", authorization);
        diagnostics.Arrange("schemes", schemes);

        bool actual = WsNegotiateInfoLines.PicksNegotiate(request, authorization);

        diagnostics.Act("picks Negotiate", actual);
        diagnostics.Assert("picks Negotiate", expected, actual);
        Assert.AreEqual(expected, WsNegotiateInfoLines.PicksNegotiate(request, authorization));
    }
}
