using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-D</c> / <c>--dump-header</c> as curl 8.21.0 parses it, measured against the local
/// curl 8.21.0 on 2026-09-26: the file name is kept as given, <c>-</c> included; the last value
/// wins; an empty value is refused as blank; a value that looks like a flag is accepted with
/// curl's filename warning; a missing value is refused as requiring a parameter; and
/// <c>--no-dump-header</c> is refused as not reversible.
/// </summary>
[TestClass]
public sealed class CommandLineDumpHeaderTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoDumpHeader_LeavesDumpHeaderFileNull()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("dump header file", null, result.Options?.DumpHeaderFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.DumpHeaderFile);
    }

    [TestMethod]
    public void Parse_ShortDumpHeader_RecordsFileVerbatim()
    {
        CommandLineParseResult result = Parse(["-D", "hd.txt", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("dump header file", "hd.txt", result.Options?.DumpHeaderFile);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("hd.txt", result.Options.DumpHeaderFile);
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_LongDumpHeaderDash_RecordsDashForStandardOutputWithoutWarning()
    {
        CommandLineParseResult result = Parse(["--dump-header", "-", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("dump header file", "-", result.Options?.DumpHeaderFile);
        Diagnostics.Assert("warning line count", 0, result.WarningLines.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-", result.Options.DumpHeaderFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-D - -D hd.txt", "hd.txt")]
    [DataRow("-D hd.txt --dump-header -", "-")]
    public void Parse_DumpHeaderGivenTwice_LastValueWins(string dumpHeaderArguments, string expectedFile)
    {
        CommandLineParseResult result = Parse([.. dumpHeaderArguments.Split(' '), Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("dump header file", expectedFile, result.Options?.DumpHeaderFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedFile, result.Options.DumpHeaderFile);
    }

    [TestMethod]
    public void Parse_DumpHeaderGivenFlagLikeValue_AcceptsWithFileNameWarning()
    {
        CommandLineParseResult result = Parse(["-D", "-x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("dump header file", "-x", result.Options?.DumpHeaderFile);
        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach(["Warning: The filename argument '-x' looks like a flag."]),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.DumpHeaderFile);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-D")]
    [DataRow("--dump-header")]
    public void Parse_DumpHeaderEmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "", Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("-D")]
    [DataRow("--dump-header")]
    public void Parse_DumpHeaderAsLastArgument_RefusesAsRequiringParameter(string spelledOption)
    {
        CommandLineParseResult result = Parse([Url, spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    public void Parse_NoDumpHeader_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = Parse(["--no-dump-header", Url]);

        AssertRefused(result, "curl: option --no-dump-header: the given option cannot be reversed with a --no- prefix");
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([expectedFirstLine, CommandLineRefusal.TryHelpLine]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
