using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-054 (BL-3583): curl 8.22.0 sends the origin-form path's <c>%XX</c> escapes in upper
/// case and leaves the query, and a forward proxy's absolute form, as typed (measured:
/// <c>http://h/a%2fb%c3%bc?x=%2f</c> goes out as <c>GET /a%2Fb%C3%BC?x=%2f</c>, and through
/// <c>-x</c> as <c>GET http://example.com/a%2fb</c>).
/// </summary>
[TestClass]
public sealed class Cgr054Tests
{
    [TestMethod]
    public void Format_LowerCaseEscapesInThePath_SendsThemUpperCasedAndTheQueryAsTyped()
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://127.0.0.1:20583/a%2fb%c3%bc?x=%2f"), null);

        Assert.StartsWith("GET /a%2Fb%C3%BC?x=%2f HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_ForwardProxy_KeepsTheAbsoluteFormAsTyped()
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.com/a%2fb"), null, forwardProxy: true);

        Assert.StartsWith("GET http://example.com/a%2fb HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void OriginTarget_PathAboveTilde_EncodesItThenKeepsTheQuery()
    {
        Assert.AreEqual("/%C3%A4%2F?q=%2f", HttpUrlText.OriginTarget(CurlUrl.Parse("http://h/ä%2f?q=%2f")));
    }
}
