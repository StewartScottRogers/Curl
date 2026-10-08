using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsAuthProblemLines" />: when curl 8.21.0 reports a Basic or Bearer problem
/// for a refused upgrade, and before which head lines (BL-953 Notes).
/// </summary>
[TestClass]
public sealed class WsAuthProblemLinesTests
{
    public TestContext TestContext { get; set; } = null!;

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
        var diagnostics = TestDiagnostics.For(TestContext);
        var request = new HttpAuthRequest("GET", CurlUrl.Parse("ws://h/"), "/", hasUser ? new NetworkCredential("u", "p") : null, hasToken ? "tok" : null, schemes, IsProxy: false);
        diagnostics.Arrange("status code", statusCode);
        diagnostics.Arrange("allowed schemes", schemes);
        diagnostics.Arrange("has user", hasUser);
        diagnostics.Arrange("has bearer token", hasToken);

        string? actual = WsAuthProblemLines.ProblemScheme(request, statusCode);

        diagnostics.Act("problem scheme", actual ?? "(null)");
        diagnostics.Assert("problem scheme", expected ?? "(null)", actual ?? "(null)");
        Assert.AreEqual(expected, actual);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] head = Encoding.Latin1.GetBytes(line);
        diagnostics.Arrange("scheme", scheme);
        diagnostics.Bytes("head line", head);

        string[] lines = [.. WsAuthProblemLines.LinesBefore(head, scheme)];

        diagnostics.Act("lines written", lines.Length);
        diagnostics.Assert("line count", expected, lines.Length);
        Assert.HasCount(expected, lines);
        diagnostics.Assert("every line is the problem line", true, lines.All(text => text == scheme + " authentication problem, ignoring."));
        Assert.IsTrue(lines.All(text => text == scheme + " authentication problem, ignoring."));
    }
}
