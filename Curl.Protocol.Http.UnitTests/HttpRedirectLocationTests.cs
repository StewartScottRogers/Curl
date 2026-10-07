using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRedirectLocation" /> to the <c>%{redirect_url}</c> curl 8.21.0 printed
/// for each <c>Location</c> value, measured against a request to
/// <c>http://127.0.0.1:PORT/a/b/c?q=1#base</c> (BL-179 Notes).
/// </summary>
[TestClass]
public sealed class HttpRedirectLocationTests
{
    private const string RequestUrl = "http://example.com:8080/a/b/c?q=1#base";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("/next?a=b#frag", "http://example.com:8080/next?a=b#frag", DisplayName = "absolute path")]
    [DataRow("next", "http://example.com:8080/a/b/next", DisplayName = "relative path")]
    [DataRow("../../up", "http://example.com:8080/up", DisplayName = "relative path climbing")]
    [DataRow("/x/../../y", "http://example.com:8080/y", DisplayName = "climbing past the root")]
    [DataRow(".", "http://example.com:8080/a/b/", DisplayName = "dot")]
    [DataRow("?only", "http://example.com:8080/a/b/c?only", DisplayName = "query only")]
    [DataRow("#f", "http://example.com:8080/a/b/c?q=1#f", DisplayName = "fragment only")]
    [DataRow("/a b", "http://example.com:8080/a%20b", DisplayName = "space encoded")]
    [DataRow("/%7e/x", "http://example.com:8080/%7E/x", DisplayName = "escape uppercased")]
    [DataRow("/a%2fb?c=%3d#%3d", "http://example.com:8080/a%2Fb?c=%3D#%3D", DisplayName = "escapes uppercased everywhere")]
    [DataRow("//other.example/x", "http://other.example/x", DisplayName = "scheme-relative")]
    [DataRow("//h", "http://h/", DisplayName = "scheme-relative without a path")]
    [DataRow("//h:8080", "http://h:8080/", DisplayName = "scheme-relative with a port")]
    [DataRow("http://abs.example:81/y z", "http://abs.example:81/y z", DisplayName = "absolute keeps its space")]
    [DataRow("http://h/p?x#y z", "http://h/p?x#y z", DisplayName = "absolute keeps a space in its fragment")]
    [DataRow("http://h/%7e", "http://h/%7e", DisplayName = "absolute keeps its escapes")]
    [DataRow("https://h/%zz", "https://h/%zz", DisplayName = "absolute keeps a bad escape")]
    [DataRow("HTTP://UP.EXAMPLE/Q", "http://UP.EXAMPLE/Q", DisplayName = "absolute scheme lowercased")]
    [DataRow("HTTPS://H:443", "https://H:443/", DisplayName = "absolute without a path")]
    [DataRow("http://h/a/../b/./c", "http://h/b/c", DisplayName = "absolute dot segments removed")]
    [DataRow("http://h:80/x", "http://h:80/x", DisplayName = "absolute keeps a default port")]
    [DataRow("http://u:p@h/x", "http://u:p@h/x", DisplayName = "absolute with credentials")]
    [DataRow("http://[bad", "http://[bad", DisplayName = "absolute that does not parse")]
    [DataRow("mailto:x@y", "mailto:x@y", DisplayName = "mailto")]
    [DataRow("foo:bar", "foo:bar", DisplayName = "unknown scheme")]
    public void Resolve_MeasuredLocation_GivesWhatCurlReports(string location, string expected) =>
        Assert.AreEqual(expected, Resolved(expected, RequestUrl, location));

    [TestMethod]
    [DataRow("/ä", "http://example.com:8080/%C3%A4", DisplayName = "non-ASCII as UTF-8")]
    [DataRow("/😀", "http://example.com:8080/%F0%9F%98%80", DisplayName = "surrogate pair as one character")]
    [DataRow("/a%", "http://example.com:8080/a%", DisplayName = "percent at the end")]
    [DataRow("/a%4", "http://example.com:8080/a%4", DisplayName = "percent with one digit")]
    [DataRow("/a%4g", "http://example.com:8080/a%4g", DisplayName = "percent with a non-hex digit")]
    [DataRow("/a%g4", "http://example.com:8080/a%g4", DisplayName = "percent with a non-hex first digit")]
    [DataRow("1abc:x", "http://example.com:8080/a/b/1abc:x", DisplayName = "scheme must start with a letter")]
    [DataRow("a b:c", "http://example.com:8080/a/b/a%20b:c", DisplayName = "scheme cannot hold a space")]
    [DataRow(":x", "http://example.com:8080/a/b/:x", DisplayName = "empty scheme")]
    [DataRow("/..", "http://example.com:8080/", DisplayName = "climbing from the root")]
    [DataRow("/a/./b", "http://example.com:8080/a/b", DisplayName = "inner dot")]
    [DataRow("http://h?q", "http://h/?q", DisplayName = "absolute with a query and no path")]
    [DataRow("urn:x?y#z", "urn:x?y#z", DisplayName = "absolute without an authority")]
    public void Resolve_OtherLocation_FollowsRfc3986(string location, string expected) =>
        Assert.AreEqual(expected, Resolved(expected, RequestUrl, location));

    [TestMethod]
    public void Resolve_RequestUrlWithCredentials_KeepsThem() =>
        Assert.AreEqual("http://u:p@example.com/x", Resolved("http://u:p@example.com/x", "http://u:p@example.com/a", "/x"));

    [TestMethod]
    [DataRow(300)]
    [DataRow(302)]
    [DataRow(304)]
    [DataRow(399)]
    public void Find_3xxWithLocation_Resolves(int status) =>
        Assert.AreEqual("http://example.com:8080/next", Found("http://example.com:8080/next", Head(status, ("Location", "/next"))));

    [TestMethod]
    [DataRow(200)]
    [DataRow(299)]
    [DataRow(400)]
    public void Find_Not3xx_IsNull(int status) =>
        Assert.IsNull(Found(null, Head(status, ("Location", "/next"))));

    [TestMethod]
    public void Find_NoLocation_IsNull() =>
        Assert.IsNull(Found(null, Head(302, ("Content-Length", "0"))));

    [TestMethod]
    public void Find_EmptyLocationThenAnother_TakesTheFirstWithAValue() =>
        Assert.AreEqual(
            "http://example.com:8080/second",
            Found("http://example.com:8080/second", Head(302, ("location", string.Empty), ("LOCATION", "/second"), ("Location", "/third"))));

    private string Resolved(string expected, string requestUrl, string location)
    {
        Diagnostics.Arrange("request URL, Location", $"{requestUrl}, {location}");
        string actual = HttpRedirectLocation.Resolve(CurlUrl.Parse(requestUrl), location);
        Diagnostics.Act("redirect URL", actual);
        Diagnostics.Assert("redirect URL", expected, actual);
        return actual;
    }

    private string? Found(string? expected, HttpResponseHead head)
    {
        Diagnostics.Arrange("request URL, status, headers", $"{RequestUrl}, {head.StatusLine.StatusCode}, {string.Join("; ", head.Headers.Select(header => $"{header.Name}: {header.Value}"))}");
        string? actual = HttpRedirectLocation.Find(CurlUrl.Parse(RequestUrl), head);
        Diagnostics.Act("redirect URL", actual ?? "(null)");
        Diagnostics.Assert("redirect URL", expected ?? "(null)", actual ?? "(null)");
        return actual;
    }

    private static HttpResponseHead Head(int status, params (string Name, string Value)[] headers) =>
        new(
            new HttpStatusLine(new Version(1, 1), status, "Reason"),
            [.. headers.Select(header => new HttpResponseHeader(header.Name, header.Value))],
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty);
}
