using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="UrlEncodedContent.Escape"/> against curl 8.21.0's <c>--data-urlencode</c>, measured
/// with the local curl 8.21.0 on 2026-09-26: <c>=a b&amp;c=d</c> sends <c>a+b%26c%3Dd</c>,
/// <c>~-_.*!</c> sends <c>~-_.%2A%21</c> and a CR LF in a file sends <c>%0D%0A</c>.
/// </summary>
[TestClass]
public sealed class UrlEncodedContentTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Escape_UnreservedBytes_KeepsThem()
    {
        byte[] content = "AZaz09-._~"u8.ToArray();
        ArrangeContent(content);

        string escaped = UrlEncodedContent.Escape(content);

        Diagnostics.Act("escaped", "\"" + escaped + "\"");
        Diagnostics.Diff("escaped", "AZaz09-._~", escaped);
        Assert.AreEqual("AZaz09-._~", escaped);
    }

    [TestMethod]
    public void Escape_Space_WritesPlus()
    {
        byte[] content = "a b"u8.ToArray();
        ArrangeContent(content);

        string escaped = UrlEncodedContent.Escape(content);

        Diagnostics.Act("escaped", "\"" + escaped + "\"");
        Diagnostics.Diff("escaped", "a+b", escaped);
        Assert.AreEqual("a+b", escaped);
    }

    [TestMethod]
    public void Escape_ReservedAndControlBytes_WritesUpperCaseHex()
    {
        byte[] content = [(byte)'&', (byte)'=', (byte)'*', (byte)'+', 0x0D, 0x0A, 0x00, 0xE9, 0xFF];
        ArrangeContent(content);

        string escaped = UrlEncodedContent.Escape(content);

        Diagnostics.Act("escaped", "\"" + escaped + "\"");
        Diagnostics.Diff("escaped", "%26%3D%2A%2B%0D%0A%00%E9%FF", escaped);
        Assert.AreEqual("%26%3D%2A%2B%0D%0A%00%E9%FF", escaped);
    }

    [TestMethod]
    public void Escape_Empty_IsEmpty()
    {
        ArrangeContent([]);

        string escaped = UrlEncodedContent.Escape([]);

        Diagnostics.Act("escaped", "\"" + escaped + "\"");
        Diagnostics.Diff("escaped", string.Empty, escaped);
        Assert.AreEqual(string.Empty, escaped);
    }

    private void ArrangeContent(byte[] content)
    {
        Diagnostics.Arrange("content length", content.Length);
        Diagnostics.Bytes("content", content);
    }
}
