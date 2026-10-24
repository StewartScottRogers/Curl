using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Easy;

[TestClass]
public sealed class LibcurlEasyLineWriterTests
{
    private static readonly LibcurlEasyHeaderFields Date = new("Date"u8.ToArray(), "x"u8.ToArray(), 1, 0, 4);

    [TestMethod]
    public void Round_WritesTheTestersLineStart() =>
        Assert.AreEqual(
            "0 rc=56 err=41 set=[set84=1,set3=43] body=6F6B hdr=",
            LibcurlEasyLineWriter.Round(0, 56, "A"u8, [LibcurlEasyLineWriter.SetFailure(84, 1), LibcurlEasyLineWriter.SetFailure(3, 43)], "ok"u8, []));

    [TestMethod]
    public void Info_WritesTheIdInHex() => Assert.AreEqual(" |200002=0:200", LibcurlEasyLineWriter.Info(0x200002, 0, "200"));

    [TestMethod]
    public void HeaderQuery_Found_WritesTheFields() =>
        Assert.AreEqual(" |Hdate/0/4/-1=0:44617465:78:1:0:4", LibcurlEasyLineWriter.HeaderQuery(new("date", 0, 4, -1), 0, Date));

    [TestMethod]
    public void HeaderQuery_NotFound_WritesNull() =>
        Assert.AreEqual(" |H/0/1/0=2:null", LibcurlEasyLineWriter.HeaderQuery(new(null, 0, 1, 0), 2, null));

    [TestMethod]
    public void HeaderWalk_WritesEachHeader() =>
        Assert.AreEqual(" |W4/0=[44617465=78/1/0/4,44617465=78/1/0/4]", LibcurlEasyLineWriter.HeaderWalk(new(4, 0), [Date, Date]));
}
