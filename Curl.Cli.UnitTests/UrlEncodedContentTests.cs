namespace Curl.Cli;

/// <summary>
/// Pins <see cref="UrlEncodedContent.Escape"/> against curl 8.21.0's <c>--data-urlencode</c>, measured
/// with the local curl 8.21.0 on 2026-09-26: <c>=a b&amp;c=d</c> sends <c>a+b%26c%3Dd</c>,
/// <c>~-_.*!</c> sends <c>~-_.%2A%21</c> and a CR LF in a file sends <c>%0D%0A</c>.
/// </summary>
[TestClass]
public sealed class UrlEncodedContentTests
{
    [TestMethod]
    public void Escape_UnreservedBytes_KeepsThem()
    {
        string escaped = UrlEncodedContent.Escape("AZaz09-._~"u8);

        Assert.AreEqual("AZaz09-._~", escaped);
    }

    [TestMethod]
    public void Escape_Space_WritesPlus()
    {
        string escaped = UrlEncodedContent.Escape("a b"u8);

        Assert.AreEqual("a+b", escaped);
    }

    [TestMethod]
    public void Escape_ReservedAndControlBytes_WritesUpperCaseHex()
    {
        string escaped = UrlEncodedContent.Escape([(byte)'&', (byte)'=', (byte)'*', (byte)'+', 0x0D, 0x0A, 0x00, 0xE9, 0xFF]);

        Assert.AreEqual("%26%3D%2A%2B%0D%0A%00%E9%FF", escaped);
    }

    [TestMethod]
    public void Escape_Empty_IsEmpty()
    {
        string escaped = UrlEncodedContent.Escape([]);

        Assert.AreEqual(string.Empty, escaped);
    }
}
