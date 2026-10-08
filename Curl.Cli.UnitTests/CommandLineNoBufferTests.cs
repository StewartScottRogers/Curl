using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoSpelling_Buffers()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NoBuffer", false, CommandLineParseDiagnostics.Peek(result.Options)?.NoBuffer);
        Assert.IsFalse(result.Options.NoBuffer);
    }

    [TestMethod]
    [DataRow("-N")]
    [DataRow("--no-buffer")]
    [DataRow("--no-buffer=x")]
    public void Parse_NoBufferSpelling_TurnsBufferingOff(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NoBuffer", true, CommandLineParseDiagnostics.Peek(result.Options)?.NoBuffer);
        Assert.IsTrue(result.Options.NoBuffer);
    }

    [TestMethod]
    public void Parse_Buffer_Buffers()
    {
        CommandLineParseResult result = Parse(["--buffer", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NoBuffer", false, CommandLineParseDiagnostics.Peek(result.Options)?.NoBuffer);
        Assert.IsFalse(result.Options.NoBuffer);
    }

    [TestMethod]
    public void Parse_SilentAndNoBufferBundle_SetsBoth()
    {
        CommandLineParseResult result = Parse(["-sN", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Silent", true, CommandLineParseDiagnostics.Peek(result.Options)?.Silent);
        Assert.IsTrue(result.Options.Silent);
        Diagnostics.Assert("NoBuffer", true, CommandLineParseDiagnostics.Peek(result.Options)?.NoBuffer);
        Assert.IsTrue(result.Options.NoBuffer);
    }

    [TestMethod]
    [DataRow("-N", "--buffer", false)]
    [DataRow("--buffer", "-N", true)]
    [DataRow("--no-buffer", "--buffer", false)]
    [DataRow("--buffer", "--no-buffer", true)]
    public void Parse_TwoSpellings_LastOneWins(string first, string second, bool noBuffer)
    {
        CommandLineParseResult result = Parse([first, second, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NoBuffer", noBuffer, CommandLineParseDiagnostics.Peek(result.Options)?.NoBuffer);
        Assert.AreEqual(noBuffer, result.Options.NoBuffer);
    }

    [TestMethod]
    public void Parse_NoNoBuffer_IsUnknown()
    {
        CommandLineParseResult result = Parse(["--no-no-buffer", Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Diagnostics.Assert("first stderr line", "curl: option --no-no-buffer: is unknown", CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines.FirstOrDefault());
        Assert.AreEqual("curl: option --no-no-buffer: is unknown", result.Refusal.StandardErrorLines[0]);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
