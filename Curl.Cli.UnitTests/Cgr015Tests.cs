using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-015 part 2 (BL-3844): curl 8.22.0's Windows build gets its arguments in the ANSI code
/// page and sends a <c>-d</c>, <c>--json</c>, <c>--data-urlencode</c> or <c>--url-query</c> value's
/// bytes unchanged, so <c>-d é</c> posts E9 under Windows-1252; off Windows the value goes out as
/// UTF-8. The Windows parse reads the encoding from <see cref="CommandLineOptions.ConfigFileWireTextEncoding"/>,
/// which is Windows-1252 on the measuring machine and on every host that names no ANSI code page.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private const string Url = "http://127.0.0.1/a";

    [TestMethod]
    [DataRow("-d")]
    [DataRow("--data")]
    [DataRow("--data-ascii")]
    [DataRow("--data-binary")]
    [DataRow("--data-raw")]
    [DataRow("--json")]
    public void Parse_DataOfEAcuteOnWindows_PostsTheAnsiCodePageByte(string option)
    {
        CommandLineOptions options = Parse([option, "é", Url], true);

        CollectionAssert.AreEqual(AnsiBytes(options, "é"), options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataOfEuroSignOnWindows_PostsTheAnsiCodePageByte()
    {
        CommandLineOptions options = Parse(["-d", "€", Url], true);

        CollectionAssert.AreEqual(AnsiBytes(options, "€"), options.PostData!.Value.ToArray());
    }

    [TestMethod]
    [DataRow("é", "%E9")]
    [DataRow("x=é", "x=%E9")]
    public void Parse_DataUrlencodeOfEAcuteOnWindows_PostsTheAnsiCodePageByteEscaped(string value, string expected)
    {
        CommandLineOptions options = Parse(["--data-urlencode", value, Url], true);

        Assert.AreEqual(expected, Encoding.ASCII.GetString(options.PostData!.Value.Span));
    }

    [TestMethod]
    public void Parse_UrlQueryOfEAcuteOnWindows_AddsTheAnsiCodePageByteEscaped()
    {
        CommandLineOptions options = Parse(["--url-query", "q=é", Url], true);

        Assert.AreEqual("q=%E9", options.UrlQuery);
    }

    [TestMethod]
    public void Parse_DataOfEAcuteOffWindows_PostsUtf8()
    {
        CommandLineOptions options = Parse(["-d", "é", Url], false);

        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9 }, options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataUrlencodeAndUrlQueryOfEAcuteOffWindows_EscapeUtf8()
    {
        CommandLineOptions options = Parse(["--data-urlencode", "x=é", "--url-query", "q=é", Url], false);

        Assert.AreEqual("x=%C3%A9", Encoding.ASCII.GetString(options.PostData!.Value.Span));
        Assert.AreEqual("q=%C3%A9", options.UrlQuery);
    }

    private static byte[] AnsiBytes(CommandLineOptions options, string text)
    {
        byte[] expected = options.ConfigFileWireTextEncoding!.GetBytes(text);
        Assert.HasCount(1, expected);
        return expected;
    }

    private static CommandLineOptions Parse(string[] arguments, bool isWindows)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
