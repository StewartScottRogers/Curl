using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class ProxyTransferModeUrlTests
{
    private static readonly ProxyEndpoint HttpProxy = new(ProxyKind.Http, "proxy", 3128, null);

    [TestMethod]
    [DataRow("ftp://www.example.com/moo/549", false, "/moo/549;type=i", DisplayName = "Binary (upstream test549)")]
    [DataRow("ftp://www.example.com/moo/550", true, "/moo/550;type=a", DisplayName = "ASCII (upstream test550)")]
    [DataRow("ftp://h", false, "/;type=i", DisplayName = "No path")]
    [DataRow("ftp.example.com/f", false, "/f;type=i", DisplayName = "Scheme guessed from the host")]
    [DataRow("ftp.example.com", true, "/;type=a", DisplayName = "Scheme guessed, no path")]
    [DataRow("ftp://h/f?q=1#frag", false, "/f;type=i", DisplayName = "Before the query and fragment")]
    [DataRow("ftp://h/f;type=x", false, "/f;type=x;type=i", DisplayName = "A type curl does not know")]
    [DataRow("ftp://h/f;type=ii", false, "/f;type=ii;type=i", DisplayName = "A type of two letters")]
    public void Of_FtpForwardedWithTheModeSet_PutsTheTypeOnThePath(string url, bool ascii, string expectedPath)
    {
        CurlUrl typed = ProxyTransferModeUrl.Of(CurlUrl.Parse(url), HttpProxy, tunnels: false, proxyTransferMode: true, ascii, pathAsIs: false);

        Assert.AreEqual(expectedPath, typed.AbsolutePath);
    }

    [TestMethod]
    public void Of_ForwardedWithAQuery_KeepsTheQueryAndHost()
    {
        CurlUrl typed = ProxyTransferModeUrl.Of(CurlUrl.Parse("ftp://h:2121/f?q=1"), HttpProxy, false, true, false, false);

        Assert.AreEqual("q=1", typed.Query);
        Assert.AreEqual("h", typed.Host);
        Assert.AreEqual(2121, typed.Port);
    }

    [TestMethod]
    [DataRow("ftp://h/f;type=a", DisplayName = "a")]
    [DataRow("ftp://h/f;type=D", DisplayName = "D")]
    [DataRow("ftp://h/f;TYPE=i", DisplayName = "i after an upper-case marker")]
    public void Of_PathAlreadyNamesItsType_LeavesTheUrl(string url)
    {
        CurlUrl original = CurlUrl.Parse(url);

        Assert.AreSame(original, ProxyTransferModeUrl.Of(original, HttpProxy, false, true, false, false));
    }

    [TestMethod]
    [DataRow("ftp://h/f", ProxyKind.Http, false, false, DisplayName = "Mode not set")]
    [DataRow("ftp://h/f", ProxyKind.Http, true, true, DisplayName = "Tunnelled")]
    [DataRow("http://h/f", ProxyKind.Http, false, true, DisplayName = "Not ftp")]
    [DataRow("ftp://h/f", ProxyKind.Socks5, false, true, DisplayName = "SOCKS proxy")]
    [DataRow("ftp://h/f", ProxyKind.Https, false, true, DisplayName = "HTTPS proxy")]
    public void Of_NotForwardedWithTheMode_LeavesTheUrl(string url, ProxyKind kind, bool tunnels, bool mode)
    {
        CurlUrl original = CurlUrl.Parse(url);

        Assert.AreSame(original, ProxyTransferModeUrl.Of(original, new ProxyEndpoint(kind, "proxy", 1080, null), tunnels, mode, false, false));
    }

    [TestMethod]
    public void Of_NoProxy_LeavesTheUrl()
    {
        CurlUrl original = CurlUrl.Parse("ftp://h/f");

        Assert.AreSame(original, ProxyTransferModeUrl.Of(original, null, false, true, false, false));
    }

    [TestMethod]
    public void Of_Http10Proxy_PutsTheTypeOnThePath()
    {
        CurlUrl typed = ProxyTransferModeUrl.Of(CurlUrl.Parse("ftp://h/f"), new ProxyEndpoint(ProxyKind.Http10, "proxy", 3128, null), false, true, true, false);

        Assert.AreEqual("/f;type=a", typed.AbsolutePath);
    }

    [TestMethod]
    [DataRow(false, "/b;type=i")]
    [DataRow(true, "/a/../b;type=i")]
    public void Of_DotSegments_NormalizesThePathUnlessPathAsIs(bool pathAsIs, string expectedPath)
    {
        CurlUrl typed = ProxyTransferModeUrl.Of(CurlUrl.Parse("ftp://h/a/../b", pathAsIs), HttpProxy, false, true, false, pathAsIs);

        Assert.AreEqual(expectedPath, typed.AbsolutePath);
    }
}
