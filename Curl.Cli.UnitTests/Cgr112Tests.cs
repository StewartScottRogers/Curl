using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-112 (BL-3641): curl 8.22.0's Windows build reads standard input in text mode for
/// <c>-d @-</c>, <c>--data-ascii @-</c>, <c>--json @-</c>, <c>--variable v@-</c>, <c>-H @-</c> and
/// <c>-w @-</c>, so a Ctrl-Z (0x1A) ends it and CR LF reads as LF; <c>--data-binary @-</c> and
/// <c>--data-urlencode @-</c> read it in binary, and the Linux build reads every byte. Measured on
/// Windows with the local curl 8.22.0 on 2026-10-26.
/// </summary>
[TestClass]
public sealed class Cgr112Tests
{
    private const string Url = "http://127.0.0.1/";

    private const string CtrlZThenCrLf = "ab\u001Acd\r\nef";

    [TestMethod]
    [DataRow("-d")]
    [DataRow("--data-ascii")]
    [DataRow("--json")]
    public void Parse_CtrlZOnStandardInputOnWindows_EndsTheBody(string option)
    {
        CommandLineOptions options = Accept([option, "@-", Url], CtrlZThenCrLf, isWindows: true);

        Assert.AreEqual("ab", PostText(options));
    }

    [TestMethod]
    public void Parse_JsonCrLfOnStandardInputOnWindows_SendsLineFeeds()
    {
        CommandLineOptions options = Accept(["--json", "@-", Url], "{\r\n\"a\":1\r\n}", isWindows: true);

        Assert.AreEqual("{\n\"a\":1\n}", PostText(options));
    }

    [TestMethod]
    public void Parse_JsonOnStandardInputOffWindows_SendsEveryByte()
    {
        CommandLineOptions options = Accept(["--json", "@-", Url], CtrlZThenCrLf, isWindows: false);

        Assert.AreEqual(CtrlZThenCrLf, PostText(options));
    }

    [TestMethod]
    public void Parse_DataBinaryOnStandardInputOnWindows_SendsEveryByte()
    {
        CommandLineOptions options = Accept(["--data-binary", "@-", Url], CtrlZThenCrLf, isWindows: true);

        Assert.AreEqual(CtrlZThenCrLf, PostText(options));
    }

    [TestMethod]
    public void Parse_DataUrlencodeOnStandardInputOnWindows_EscapesEveryByte()
    {
        CommandLineOptions options = Accept(["--data-urlencode", "@-", Url], CtrlZThenCrLf, isWindows: true);

        Assert.AreEqual("ab%1Acd%0D%0Aef", PostText(options));
    }

    [TestMethod]
    public void Parse_VariableOnStandardInputOnWindows_ReadsTextMode()
    {
        CommandLineOptions options = Accept(["--variable", "v@-", "--expand-data", "{{v}}", Url], "a\r\nb\rc\u001Ad", isWindows: true);

        Assert.AreEqual("a\nb\rc", PostText(options));
    }

    [TestMethod]
    public void Parse_HeaderLinesAfterCtrlZOnStandardInputOnWindows_AreNotRead()
    {
        CommandLineOptions options = Accept(["-H", "@-", Url], "X-A: 1\r\nX-B: 2\u001A\r\nX-C: 3", isWindows: true);

        CollectionAssert.AreEqual(new[] { "X-A: 1", "X-B: 2" }, options.Headers.ToArray());
    }

    [TestMethod]
    public void Parse_WriteOutOnStandardInputOnWindows_EndsAtCtrlZ()
    {
        CommandLineOptions options = Accept(["-w", "@-", Url], CtrlZThenCrLf + "\n", isWindows: true);

        Assert.AreEqual("ab", options.WriteOut);
    }

    [TestMethod]
    public void Parse_JsonLoneCrsOnStandardInputOnWindows_KeepsThem()
    {
        CommandLineOptions options = Accept(["--json", "@-", Url], "\ra\r\n\r", isWindows: true);

        Assert.AreEqual("\ra\n\r", PostText(options));
    }

    private static CommandLineOptions Accept(string[] arguments, string standardInput, bool isWindows)
    {
        RecordingDataFileReader reader = new() { StandardInput = Encoding.Latin1.GetBytes(standardInput) };
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), reader, isWindows);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }

    private static string PostText(CommandLineOptions options) => Encoding.Latin1.GetString(options.PostData!.Value.Span);
}
