namespace Curl.Cli;

/// <summary>
/// Pins <c>-N</c> / <c>--no-buffer</c> / <c>--buffer</c> as curl 8.21.0 reads them, measured with the
/// local curl 8.21.0 on 2026-09-28: every spelling below is accepted, <c>-N</c> means
/// <c>--no-buffer</c>, and the last one wins (BL-491).
/// </summary>
[TestClass]
public sealed class CommandLineNoBufferTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoSpelling_Buffers()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.NoBuffer);
    }

    [TestMethod]
    [DataRow("-N")]
    [DataRow("--no-buffer")]
    [DataRow("--no-buffer=x")]
    public void Parse_NoBufferSpelling_TurnsBufferingOff(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.NoBuffer);
    }

    [TestMethod]
    public void Parse_Buffer_Buffers()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--buffer", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.NoBuffer);
    }

    [TestMethod]
    public void Parse_SilentAndNoBufferBundle_SetsBoth()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sN", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.NoBuffer);
    }

    [TestMethod]
    [DataRow("-N", "--buffer", false)]
    [DataRow("--buffer", "-N", true)]
    [DataRow("--no-buffer", "--buffer", false)]
    [DataRow("--buffer", "--no-buffer", true)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool noBuffer)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(noBuffer, result.Options.NoBuffer);
    }

    [TestMethod]
    public void Parse_NoNoBuffer_IsUnknown()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-no-buffer", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --no-no-buffer: is unknown", result.Refusal.StandardErrorLines[0]);
    }
}
