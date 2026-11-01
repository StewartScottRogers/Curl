using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-275: over HTTP/2 a <c>-H 'X-Ws:   spaced   '</c> value loses its leading blanks but
/// keeps its trailing ones (<c>x-ws: spaced   </c>, HEADERS length 56), as curl's nghttp2 build does.
/// </summary>
[TestClass]
public sealed class Cgr275Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_HeaderValueWithTrailingBlanks_KeepsTheTrailingBlanks()
    {
        string head = "GET /x HTTP/1.1\r\nHost: h\r\nX-Ws:   spaced   \r\n\r\n";
        Diagnostics.Arrange("head", "X-Ws:   spaced   ");

        var fields = Http2RequestHeaders.Of(System.Text.Encoding.Latin1.GetBytes(head), "http");

        string value = fields.Single(field => field.Name == "x-ws").Value;
        Diagnostics.Assert("x-ws value", "spaced   ", value);
        Assert.AreEqual("spaced   ", value);
    }
}
