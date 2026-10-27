using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-050 (BL-3579): curl 8.22.0 reads a config file in <c>fgets</c> chunks of up to 127 bytes
/// and keeps each only up to its first NUL, so a NUL drops the rest of its chunk, LF included, and the
/// line runs on into the next one; a chunk starting with a NUL stops the file silently. Each
/// expectation was measured with the local curl 8.22.0 on 2026-10-26 with <c>curl -sS -K k.cfg</c>.
/// </summary>
[TestClass]
public sealed class Cgr050Tests
{
    private const string Url = "http://example.com/";

    [TestMethod]
    public void Parse_NulBeforeTheEndOfTheFile_DropsEverythingAfterTheNul()
    {
        CommandLineParseResult result = Parse([], "url = http://127.0.0.1/k70\0junk");

        CollectionAssert.AreEqual(new[] { "http://127.0.0.1/k70" }, result.Options!.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NulInsideAQuotedHeader_JoinsTheNextLineToTheHeaderAndLeavesNoUrl()
    {
        CommandLineParseResult result = Parse([], "header = \"X-A: a\0b\"\nurl = http://127.0.0.1/nb01\n");

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Assert.AreEqual("curl: (2) no URL specified", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_NulBeforeTheLineFeed_IgnoresTheNextLineAfterTheClosingQuote()
    {
        CommandLineParseResult result = Parse([], "url = \"http://127.0.0.1/nb04\"\0\nheader = X-Next:1\n");

        CollectionAssert.AreEqual(new[] { "http://127.0.0.1/nb04" }, result.Options!.Urls.ToArray());
        Assert.IsEmpty(result.Options.Headers);
    }

    [TestMethod]
    [DataRow("header = \"X-A: 1\"\nbog\0us\nheader = \"X-B: 2\"\n")]
    [DataRow("header = \"X-A: 1\"\r\nbog\0us\r\nheader = \"X-B: 2\"\r\n")]
    public void Parse_NulInsideAnOption_JoinsTheNextLineToTheOption(string contents)
    {
        CommandLineParseResult result = Parse([Url], contents);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Assert.AreEqual("curl: k.cfg:2 config file option 'bogheader' is unknown", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_NulStartingALine_StopsTheFileSilently()
    {
        CommandLineParseResult result = Parse([Url], "write-out = \"A\"\n\0\nwrite-out = \"C\"\nbogus\n");

        Assert.AreEqual("A", result.Options!.WriteOut);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_Utf16LittleEndianFile_IsIgnoredSilently()
    {
        byte[] contents = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("header = \"X-A: 1\"\n")];

        CommandLineParseResult result = Parse([Url], contents);

        Assert.IsEmpty(result.Options!.Headers);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_NulInsideALongParameter_KeepsTheLaterReadChunks()
    {
        CommandLineParseResult result = Parse([Url], "write-out = \"B\0" + new string('x', 300) + "Z\"\n");

        Assert.AreEqual("B" + new string('x', 188) + "Z", result.Options!.WriteOut);
    }

    [TestMethod]
    public void Parse_NulStartingTheSecondChunkOfALine_StopsTheFile()
    {
        CommandLineParseResult result = Parse([Url], "write-out = \"A\"\nwrite-out = \"" + new string('x', 114) + "\0Z\"\n");

        Assert.AreEqual("A", result.Options!.WriteOut);
    }

    [TestMethod]
    public void Parse_CarriageReturnsNotBeforeALineFeed_AreKept()
    {
        CommandLineParseResult result = Parse([Url], "write-out = \"A\rB\r");

        Assert.AreEqual("A\rB\r", result.Options!.WriteOut);
    }

    private static CommandLineParseResult Parse(string[] arguments, string contents) =>
        Parse(arguments, Encoding.Latin1.GetBytes(contents));

    private static CommandLineParseResult Parse(string[] arguments, byte[] contents)
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.cfg"] = contents;
        return CommandLineParser.Parse(["-K", "k.cfg", .. arguments], _ => true, new EmptyPasswordPrompt(), reader, false);
    }
}
