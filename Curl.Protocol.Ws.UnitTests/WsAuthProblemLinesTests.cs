using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsAuthProblemLines" />: when curl 8.21.0 reports a Basic or Bearer problem
/// for a refused upgrade, and before which head lines (BL-953 Notes).
/// </summary>
[TestClass]
public sealed class WsAuthProblemLinesTests
{
    [TestMethod]
    [DataRow(401, HttpAuthSchemes.Basic, true, false, "Basic")]
    [DataRow(401, HttpAuthSchemes.Bearer, false, true, "Bearer")]
    [DataRow(401, HttpAuthSchemes.Basic, false, true, "Basic")]
    [DataRow(403, HttpAuthSchemes.Basic, true, false, null)]
    [DataRow(401, HttpAuthSchemes.Basic, false, false, null)]
    [DataRow(401, HttpAuthSchemes.Any, true, false, null)]
    [DataRow(401, HttpAuthSchemes.Digest, true, false, null)]
    public void ProblemScheme_Request_PicksAsCurl(int statusCode, HttpAuthSchemes schemes, bool hasUser, bool hasToken, string? expected)
    {
        var request = new HttpAuthRequest("GET", CurlUrl.Parse("ws://h/"), "/", hasUser ? new NetworkCredential("u", "p") : null, hasToken ? "tok" : null, schemes, IsProxy: false);

        Assert.AreEqual(expected, WsAuthProblemLines.ProblemScheme(request, statusCode));
    }

    [TestMethod]
    [DataRow("WWW-Authenticate: Basic realm=\"x\"\r\n", "Basic", 1)]
    [DataRow("www-authenticate: basic\r\n", "Basic", 1)]
    [DataRow("WWW-Authenticate: Digest realm=\"x\", Basic realm=\"y\"\r\n", "Basic", 1)]
    [DataRow("WWW-Authenticate: Basic, Basic\r\n", "Basic", 2)]
    [DataRow("WWW-Authenticate: Basicx\r\n", "Basic", 0)]
    [DataRow("WWW-Authenticate: Digest realm=\"x\"\r\n", "Basic", 0)]
    [DataRow("X-Basic: Basic\r\n", "Basic", 0)]
    [DataRow("WWW-Authenticate: Bearer\r\n", "Bearer", 1)]
    public void LinesBefore_HeadLine_WritesOneLinePerOfferingChallenge(string line, string scheme, int expected)
    {
        string[] lines = [.. WsAuthProblemLines.LinesBefore(Encoding.Latin1.GetBytes(line), scheme)];

        Assert.HasCount(expected, lines);
        Assert.IsTrue(lines.All(text => text == scheme + " authentication problem, ignoring."));
    }
}
