using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpAuthProblemLines" />: how many <c>authentication problem, ignoring.</c>
/// lines curl writes before a challenge header refusing the Basic or Bearer value it sent
/// (measured, BL-1040 Notes).
/// </summary>
[TestClass]
public sealed class HttpAuthProblemLinesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(401, false, "Basic dTpw", "WWW-Authenticate", "Basic realm=\"x\"", 1, DisplayName = "Basic refused")]
    [DataRow(401, false, "Bearer tok", "www-authenticate", "bearer, Basic x,\tBEARER\ty", 2, DisplayName = "Bearer, any case, after blanks")]
    [DataRow(401, false, "Basic dTpw", "WWW-Authenticate", "Basic", 1, DisplayName = "A bare scheme")]
    [DataRow(407, true, "Basic dTpw", "Proxy-Authenticate", "Basic x, Basic y", 2, DisplayName = "A proxy's 407")]
    [DataRow(401, false, null, "WWW-Authenticate", "Basic x", 0, DisplayName = "No scheme picked")]
    [DataRow(401, false, "Digest username=\"u\"", "WWW-Authenticate", "Basic x", 0, DisplayName = "Digest picked")]
    [DataRow(403, false, "Basic dTpw", "WWW-Authenticate", "Basic x", 0, DisplayName = "Not a 401")]
    [DataRow(401, true, "Basic dTpw", "Proxy-Authenticate", "Basic x", 0, DisplayName = "A proxy's value on a 401")]
    [DataRow(401, false, "Basic dTpw", "Proxy-Authenticate", "Basic x", 0, DisplayName = "Another header")]
    [DataRow(401, false, "Basic dTpw", "WWW-Authenticate", "Basicx, Digest y", 0, DisplayName = "No challenge offers the scheme")]
    public void LinesBefore_Header_GivesOneLineForEachChallengeOfferingThePickedScheme(int statusCode, bool isProxy, string? authorization, string name, string value, int expected)
    {
        HttpStatusLine statusLine = HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X");
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", null, null, HttpAuthSchemes.Basic, isProxy);

        Diagnostics.Arrange("status, proxy, authorization", $"{statusCode}, {isProxy}, {authorization ?? "none"}");
        Diagnostics.Arrange("header", $"{name}: {value}");

        List<string> lines = [.. new HttpAuthProblemLines().LinesBefore(request, authorization, statusLine, new HttpResponseHeader(name, value))];

        Diagnostics.Act("lines", string.Join(" | ", lines));
        Diagnostics.Assert("line count", expected, lines.Count);
        Assert.HasCount(expected, lines);
        Assert.IsTrue(lines.All(line => line == $"{authorization!.Split(' ')[0]} authentication problem, ignoring."));
    }

    private const string DigestProblem = "Digest authentication problem, ignoring.";

    private const string DigestDuplicate = "Ignoring duplicate digest auth header.";

    [TestMethod]
    [DataRow(401, false, "Digest username=\"u\"", "WWW-Authenticate", "Digest realm=\"r\", nonce=\"b\", qop=\"auth\"", new[] { DigestProblem }, DisplayName = "Digest refused")]
    [DataRow(401, false, "Digest username=\"u\"", "WWW-Authenticate", "Digest realm=\"r\", nonce=\"b\", Digest realm=\"s\", nonce=\"c\"", new[] { DigestProblem, DigestDuplicate }, DisplayName = "Two Digest challenges")]
    [DataRow(401, false, "Digest username=\"u\"", "WWW-Authenticate", "Basic realm=\"x\", digest realm=\"r\", nonce=\"b\"", new[] { DigestProblem }, DisplayName = "After a Basic challenge, any case")]
    [DataRow(401, false, "Digest username=\"u\"", "WWW-Authenticate", "Digest realm=\"r\", nonce=\"b\", stale=TRUE", new string[0], DisplayName = "Stale")]
    [DataRow(401, false, "Digest username=\"u\"", "WWW-Authenticate", "Digest realm=\"r\", nonce=\"b\", stale=true, Digest realm=\"s\"", new[] { DigestDuplicate }, DisplayName = "Stale, then a duplicate")]
    [DataRow(401, false, null, "WWW-Authenticate", "Digest realm=\"r\", nonce=\"a\", Digest realm=\"s\"", new[] { DigestDuplicate }, DisplayName = "No scheme picked")]
    [DataRow(401, false, "Basic dTpw", "WWW-Authenticate", "Digest realm=\"r\", nonce=\"a\"", new string[0], DisplayName = "Basic sent")]
    [DataRow(407, true, "Digest username=\"u\"", "Proxy-Authenticate", "Digest realm=\"r\", nonce=\"b\", Digest realm=\"s\"", new[] { DigestProblem, DigestDuplicate }, DisplayName = "A proxy's 407")]
    [DataRow(200, false, "Digest username=\"u\"", "WWW-Authenticate", "Digest realm=\"r\", nonce=\"b\"", new string[0], DisplayName = "Not a 401")]
    public void LinesBefore_DigestHeader_GivesTheProblemLineForTheFirstAndTheDuplicateLineForEachLater(
        int statusCode, bool isProxy, string? authorization, string name, string value, string[] expected)
    {
        HttpStatusLine statusLine = HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X");
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", null, null, HttpAuthSchemes.Digest, isProxy);

        Diagnostics.Arrange("status, proxy, authorization", $"{statusCode}, {isProxy}, {authorization ?? "none"}");
        Diagnostics.Arrange("header", $"{name}: {value}");

        List<string> lines = [.. new HttpAuthProblemLines().LinesBefore(request, authorization, statusLine, new HttpResponseHeader(name, value))];

        Diagnostics.Act("lines", string.Join(" | ", lines));
        Diagnostics.Assert("lines", string.Join(" | ", expected), string.Join(" | ", lines));
        CollectionAssert.AreEqual(expected, lines);
    }

    /// <summary>Measured: a second Digest header of the same head is a duplicate, whatever the first said.</summary>
    [TestMethod]
    public void LinesBefore_SecondDigestHeaderOfTheHead_GivesTheDuplicateLine()
    {
        HttpStatusLine statusLine = HttpStatusLine.Parse("HTTP/1.1 401 X");
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", null, null, HttpAuthSchemes.Digest, false);
        HttpAuthProblemLines head = new();

        List<string> first = [.. head.LinesBefore(request, "Digest username=\"u\"", statusLine, new HttpResponseHeader("WWW-Authenticate", "Digest realm=\"r\", nonce=\"b\""))];
        List<string> second = [.. head.LinesBefore(request, "Digest username=\"u\"", statusLine, new HttpResponseHeader("WWW-Authenticate", "Digest realm=\"s\", nonce=\"c\""))];

        Diagnostics.Arrange("headers", "two Digest WWW-Authenticate headers in one 401 head");
        Diagnostics.Act("lines for each header", $"{string.Join(" | ", first)}; {string.Join(" | ", second)}");
        Diagnostics.Assert("lines for the second header", DigestDuplicate, string.Join(" | ", second));
        CollectionAssert.AreEqual(new[] { DigestProblem }, first);
        CollectionAssert.AreEqual(new[] { DigestDuplicate }, second);
    }
}
