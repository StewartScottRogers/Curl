namespace Curl.Cli;

/// <summary>
/// Pins CGR-016 part 2 (BL-3850): curl 8.18.0 on Linux sends <c>-H $'X-A: \xff'</c>, <c>-b $'a=\xff'</c>
/// and <c>-X $'G\xffT'</c> with the byte FF unchanged. A <c>-X</c> method goes out one character per byte, so
/// the parse gives each <see cref="InvalidUtf8ByteEscape"/> surrogate as its byte's character; a <c>-H</c>
/// value keeps the surrogate, which the request head gives as its byte, and so does a <c>-b</c> cookie
/// string, which the console's cookie string encoding gives as its byte (BL-3545).
/// </summary>
[TestClass]
public sealed class Cgr016HeaderCookieTests
{
    private const string Url = "http://127.0.0.1/a";

    private static readonly string EscapedFF = InvalidUtf8ByteEscape.Decode([0xFF]);

    [TestMethod]
    public void Parse_CookieOfInvalidByte_KeepsTheEscapeForTheCookieStringEncoding()
    {
        CommandLineOptions options = Parse(["-b", "a=" + EscapedFF, Url]);

        Assert.AreEqual("a=\uDCFF", options.Cookies[0].Value);
    }

    [TestMethod]
    public void Parse_MethodOfInvalidByte_SendsTheByte()
    {
        CommandLineOptions options = Parse(["-X", "G" + EscapedFF + "T", Url]);

        Assert.AreEqual("GÿT", options.RequestMethod);
    }

    [TestMethod]
    public void Parse_HeaderOfInvalidByte_KeepsTheEscapeForTheRequestHead()
    {
        CommandLineOptions options = Parse(["-H", "X-A: " + EscapedFF, Url]);

        Assert.AreEqual("X-A: \uDCFF", options.Headers[0]);
    }

    [TestMethod]
    [DataRow("a=é")]
    [DataRow("a=\U0001F600")]
    public void Parse_CookieOfValidText_IsUnchanged(string cookie)
    {
        CommandLineOptions options = Parse(["-b", cookie, Url]);

        Assert.AreEqual(cookie, options.Cookies[0].Value);
    }

    [TestMethod]
    public void Parse_CookieWithPairEndingInDcSurrogate_KeepsThePair()
    {
        string cookie = "a=􏳿";

        CommandLineOptions options = Parse(["-b", cookie, Url]);

        Assert.AreEqual(cookie, options.Cookies[0].Value);
    }

    [TestMethod]
    public void Parse_CookieStartingWithInvalidByte_KeepsTheEscape()
    {
        CommandLineOptions options = Parse(["-b", EscapedFF + "=1", Url]);

        Assert.AreEqual("\uDCFF=1", options.Cookies[0].Value);
    }

    private static CommandLineOptions Parse(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), false);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
