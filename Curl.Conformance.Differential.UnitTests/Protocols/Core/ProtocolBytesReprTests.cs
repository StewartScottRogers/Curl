using System.Text;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Core;

[TestClass]
public sealed class ProtocolBytesReprTests
{
    [TestMethod]
    [DataRow("", "b''")]
    [DataRow("abc /x\r\n", "b'abc /x\\r\\n'")]
    [DataRow("a\tb\\c", "b'a\\tb\\\\c'")]
    [DataRow("it's", "b\"it's\"")]
    [DataRow("it's \"x\"", "b'it\\'s \"x\"'")]
    [DataRow("say \"hi\"", "b'say \"hi\"'")]
    public void Format_Text_MatchesPythonRepr(string text, string expected) =>
        Assert.AreEqual(expected, ProtocolBytesRepr.Format(Encoding.ASCII.GetBytes(text)));

    [TestMethod]
    public void Format_NonPrintableBytes_AreHexEscaped() =>
        Assert.AreEqual("b'\\xff\\xfd\\x18\\x00\\x7f'", ProtocolBytesRepr.Format([0xff, 0xfd, 0x18, 0x00, 0x7f]));
}
