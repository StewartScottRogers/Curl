namespace Curl.Cli;

/// <summary>
/// Pins CGR-016 part 1 (BL-3849): curl 8.18.0 on Linux sends its arguments' bytes unchanged, so
/// <c>curl -d $'\xff' URL</c> posts the one byte FF (<c>Content-Length: 1</c>) and
/// <c>--data-urlencode $'\xff'</c> posts <c>%FF</c>. The console reads argv's raw bytes and hands
/// each invalid byte over as <see cref="InvalidUtf8ByteEscape"/>'s surrogate; the parse sends it back.
/// </summary>
[TestClass]
public sealed class Cgr016DataTests
{
    private const string Url = "http://127.0.0.1/a";

    private static readonly string EscapedFF = InvalidUtf8ByteEscape.Decode([0xFF]);

    [TestMethod]
    [DataRow("-d")]
    [DataRow("--data-binary")]
    [DataRow("--data-raw")]
    [DataRow("--json")]
    public void Parse_DataOfInvalidByteOffWindows_PostsTheByte(string option)
    {
        CommandLineOptions options = Parse([option, EscapedFF, Url]);

        CollectionAssert.AreEqual(new byte[] { 0xFF }, options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataUrlencodeOfInvalidByteOffWindows_PostsTheByteEscaped()
    {
        CommandLineOptions options = Parse(["--data-urlencode", EscapedFF, Url]);

        CollectionAssert.AreEqual("%FF"u8.ToArray(), options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_UrlQueryOfInvalidByteOffWindows_AddsTheByteEscaped()
    {
        CommandLineOptions options = Parse(["--url-query", "q=" + EscapedFF, Url]);

        Assert.AreEqual("q=%FF", options.UrlQuery);
    }

    [TestMethod]
    public void Parse_DataOfValidUtf8OffWindows_PostsUtf8()
    {
        CommandLineOptions options = Parse(["-d", "é", Url]);

        CollectionAssert.AreEqual("é"u8.ToArray(), options.PostData!.Value.ToArray());
    }

    private static CommandLineOptions Parse(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), false);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
