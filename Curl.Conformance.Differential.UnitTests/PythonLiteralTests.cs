using System.Text;

namespace Curl.Conformance.Differential.UnitTests;

[TestClass]
public sealed class PythonLiteralTests
{
    [TestMethod]
    public void FormatBytes_ControlAndHighBytes_EscapesAsPythonRepr()
    {
        var literal = PythonLiteral.FormatBytes([(byte)'a', (byte)'\\', (byte)'\t', (byte)'\n', (byte)'\r', 0x01, 0x7f, 0xe9]);

        Assert.AreEqual(@"b'a\\\t\n\r\x01\x7f\xe9'", literal);
    }

    [TestMethod]
    public void FormatBytes_SingleQuoteOnly_UsesDoubleQuotes()
    {
        Assert.AreEqual("b\"it's\"", PythonLiteral.FormatBytes(Encoding.ASCII.GetBytes("it's")));
    }

    [TestMethod]
    public void FormatBytes_BothQuotes_EscapesTheSingleQuote()
    {
        Assert.AreEqual(@"b'\'""'", PythonLiteral.FormatBytes(Encoding.ASCII.GetBytes("'\"")));
    }

    [TestMethod]
    public void FormatString_PrintableLatin1_StaysLiteralButControlsEscape()
    {
        Assert.AreEqual(@"'sub\\é\xa0\xad\x1f'", PythonLiteral.FormatString("sub\\é ­\u001f"));
    }

    [TestMethod]
    public void FormatString_SingleQuoteOnly_UsesDoubleQuotes()
    {
        Assert.AreEqual("\"a'b\"", PythonLiteral.FormatString("a'b"));
    }
}
