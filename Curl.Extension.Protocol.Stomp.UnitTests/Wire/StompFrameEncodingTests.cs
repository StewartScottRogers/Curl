using System.Text;

namespace Curl.Extension.Protocol.Stomp.Wire;

/// <summary>Checks frame writing and header escaping against STOMP 1.2, "STOMP Frames" and "Value Encoding".</summary>
[TestClass]
public sealed class StompFrameEncodingTests
{
    [TestMethod]
    public void Encode_SendFrame_WritesCommandHeadersBlankLineBodyAndNul()
    {
        StompFrame frame = new("SEND", [new("destination", "/queue/a"), new("content-length", "2")], "hi"u8.ToArray());

        byte[] bytes = StompFrameEncoding.Encode(frame);

        Assert.AreEqual("SEND\ndestination:/queue/a\ncontent-length:2\n\nhi\0", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public void Encode_SendFrame_EscapesBackslashCrLfAndColonInHeaders()
    {
        StompFrame frame = new("SEND", [new("a:b", "c\\d\re\nf")], []);

        string text = Encoding.UTF8.GetString(StompFrameEncoding.Encode(frame));

        Assert.AreEqual("SEND\na\\cb:c\\\\d\\re\\nf\n\n\0", text);
    }

    [TestMethod]
    public void Encode_ConnectFrame_LeavesHeadersUnescaped()
    {
        StompFrame frame = new("CONNECT", [new("host", "[::1]")], []);

        string text = Encoding.UTF8.GetString(StompFrameEncoding.Encode(frame));

        Assert.AreEqual("CONNECT\nhost:[::1]\n\n\0", text);
    }

    [TestMethod]
    public void Unescape_EveryDefinedSequence_GivesItsOctet() =>
        Assert.AreEqual("a\r\n:\\b", StompFrameEncoding.Unescape("a\\r\\n\\c\\\\b"));

    [TestMethod]
    [DataRow("a\\tb")]
    [DataRow("ab\\")]
    public void Unescape_UndefinedOrUnfinishedSequence_IsAFatalError(string text) =>
        Assert.ThrowsExactly<FormatException>(() => StompFrameEncoding.Unescape(text));

    [TestMethod]
    public void ParseHeader_Escaped_SplitsAtTheFirstColonAndUnescapes() =>
        Assert.AreEqual(new StompHeader("a:b", "c:d"), StompFrameEncoding.ParseHeader("a\\cb:c:d", escaped: true));

    [TestMethod]
    public void ParseHeader_Unescaped_KeepsBackslashes() =>
        Assert.AreEqual(new StompHeader("version", "1\\2"), StompFrameEncoding.ParseHeader("version:1\\2", escaped: false));

    [TestMethod]
    public void ParseHeader_NoColon_IsAFormatError() =>
        Assert.ThrowsExactly<FormatException>(() => StompFrameEncoding.ParseHeader("nocolon", escaped: true));

    [TestMethod]
    public void HeaderValue_RepeatedHeader_GivesTheFirstEntry()
    {
        StompFrame frame = new("MESSAGE", [new("foo", "World"), new("foo", "Hello")], []);

        Assert.AreEqual("World", frame.HeaderValue("foo"));
        Assert.IsNull(frame.HeaderValue("bar"));
    }
}
