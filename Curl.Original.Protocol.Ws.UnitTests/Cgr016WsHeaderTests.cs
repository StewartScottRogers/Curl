using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// Pins CGR-016 (BL-3860): an <c>-H</c> value holding the U+DC80-U+DCFF characters that stand for
/// argument bytes UTF-8 could not read sends those bytes, as curl 8.18.0 does.
/// </summary>
[TestClass]
public sealed class Cgr016WsHeaderTests
{
    [TestMethod]
    public void Format_HeaderWithEscapedBytes_SendsThoseBytes()
    {
        var options = new HttpRequestOptions { Headers = ["X-A: \uDCFF\uDC80é𐃿\uDD00"], CommandLineTextEncoding = Encoding.UTF8 };

        byte[] head = WsUpgradeRequestFormatter.Format(CurlUrl.Parse("ws://h/"), options, "GET", "k", null);

        byte[] expected = [.. "X-A: "u8, 0xFF, 0x80, 0xC3, 0xA9, 0xF0, 0x90, 0x83, 0xBF, 0xEF, 0xBF, 0xBD, (byte)'\r', (byte)'\n'];
        Assert.AreNotEqual(-1, head.AsSpan().IndexOf(expected));
    }
}
