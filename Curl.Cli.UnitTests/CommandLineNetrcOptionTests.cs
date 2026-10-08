using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-n</c> / <c>--netrc</c>, <c>--netrc-optional</c> and <c>--netrc-file</c>.
/// Measured with the local curl 8.21.0 (Schannel) on 2026-09-28, directly and through
/// <c>Record-CurlExchange.ps1</c> against <c>http://127.0.0.1:45504/</c>; the bytes are in BL-504's Notes.
/// The <c>--netrc-file</c> existence check runs against a fake, never the disk.
/// </summary>
[TestClass]
public sealed class CommandLineNetrcOptionTests
{
    private const string Url = "http://127.0.0.1:45504/";

    private static readonly Func<string, bool> EveryPathExists = _ => true;

    private static readonly Func<string, bool> NoPathExists = _ => false;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoNetrcOptions_IgnoresNetrc()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NetrcRequested", false, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcRequested);
        Assert.IsFalse(result.Options.NetrcRequested);
        Diagnostics.Assert("NetrcOptionalRequested", false, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcOptionalRequested);
        Assert.IsFalse(result.Options.NetrcOptionalRequested);
        Diagnostics.Assert("NetrcFile", null, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcFile);
        Assert.IsNull(result.Options.NetrcFile);
        Diagnostics.Assert("NetrcUse", NetrcUse.Ignored, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcUse);
        Assert.AreEqual(NetrcUse.Ignored, result.Options.NetrcUse);
    }

    [TestMethod]
    [DataRow(new[] { "-n" }, true, false, null, NetrcUse.Required)]
    [DataRow(new[] { "--netrc" }, true, false, null, NetrcUse.Required)]
    [DataRow(new[] { "-n", "-n" }, true, false, null, NetrcUse.Required)]
    [DataRow(new[] { "--no-netrc" }, false, false, null, NetrcUse.Ignored)]
    [DataRow(new[] { "-n", "--no-netrc" }, false, false, null, NetrcUse.Ignored)]
    [DataRow(new[] { "--netrc-optional" }, false, true, null, NetrcUse.Optional)]
    [DataRow(new[] { "--no-netrc-optional" }, false, false, null, NetrcUse.Ignored)]
    [DataRow(new[] { "-n", "--netrc-optional" }, true, true, null, NetrcUse.Optional)]
    [DataRow(new[] { "--netrc-optional", "-n" }, true, true, null, NetrcUse.Optional)]
    [DataRow(new[] { "-n", "--no-netrc", "--netrc-optional" }, false, true, null, NetrcUse.Optional)]
    [DataRow(new[] { "--netrc-optional", "--no-netrc-optional", "-n" }, true, false, null, NetrcUse.Required)]
    [DataRow(new[] { "--netrc-file", "f" }, false, false, "f", NetrcUse.Required)]
    [DataRow(new[] { "--netrc-file=f" }, false, false, "f", NetrcUse.Required)]
    [DataRow(new[] { "--netrc-file", "f", "--netrc-optional" }, false, true, "f", NetrcUse.Optional)]
    [DataRow(new[] { "--netrc-optional", "--netrc-file", "f" }, false, true, "f", NetrcUse.Optional)]
    [DataRow(new[] { "--netrc-file", "f", "-n" }, true, false, "f", NetrcUse.Required)]
    [DataRow(new[] { "-n", "--netrc-file", "f" }, true, false, "f", NetrcUse.Required)]
    [DataRow(new[] { "--netrc-file", "f", "--no-netrc" }, false, false, "f", NetrcUse.Required)]
    [DataRow(new[] { "--netrc-file", "f", "--netrc-file", "g" }, false, false, "g", NetrcUse.Required)]
    public void Parse_NetrcOptions_RecordWhetherAndWhichNetrcIsRead(string[] arguments, bool requested, bool optionalRequested, string? file, NetrcUse use)
    {
        CommandLineParseResult result = Parse([.. arguments, Url], EveryPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NetrcRequested", requested, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcRequested);
        Assert.AreEqual(requested, result.Options.NetrcRequested);
        Diagnostics.Assert("NetrcOptionalRequested", optionalRequested, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcOptionalRequested);
        Assert.AreEqual(optionalRequested, result.Options.NetrcOptionalRequested);
        Diagnostics.Assert("NetrcFile", file, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcFile);
        Assert.AreEqual(file, result.Options.NetrcFile);
        Diagnostics.Assert("NetrcUse", use, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcUse);
        Assert.AreEqual(use, result.Options.NetrcUse);
    }

    [TestMethod]
    public void Parse_NetrcFileThatExists_RecordsTheFileItChecked()
    {
        string? checkedPath = null;

        CommandLineParseResult result = Parse(
            ["--netrc-file", "my.netrc", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NetrcFile", "my.netrc", CommandLineParseDiagnostics.Peek(result.Options)?.NetrcFile);
        Assert.AreEqual("my.netrc", result.Options.NetrcFile);
        Diagnostics.Assert("checked path", "my.netrc", checkedPath);
        Assert.AreEqual("my.netrc", checkedPath);
    }

    [TestMethod]
    [DataRow(new[] { "--netrc-file", "f" }, "--netrc-file", "f")]
    [DataRow(new[] { "--netrc-file", "" }, "--netrc-file", "")]
    [DataRow(new[] { "--netrc-file=nope" }, "--netrc-file=nope", "nope")]
    [DataRow(new[] { "--netrc-file", "f", "--netrc-optional" }, "--netrc-file", "f")]
    [DataRow(new[] { "--netrc-optional", "--netrc-file", "f" }, "--netrc-file", "f")]
    [DataRow(new[] { "--netrc-file", "f", "-n" }, "--netrc-file", "f")]
    [DataRow(new[] { "-n", "--netrc-file", "f" }, "--netrc-file", "f")]
    public void Parse_NetrcFileThatDoesNotExist_RefusesWithThreeLines(string[] arguments, string spelledOption, string file)
    {
        CommandLineParseResult result = Parse([.. arguments, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --netrc-file does not exist",
            $"curl: option {spelledOption}: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentThenMissingNetrcFile_HidesTheFileLine()
    {
        CommandLineParseResult result = Parse(["-s", "--netrc-file", "nope", Url], NoPathExists);

        AssertRefused(result, "curl: option --netrc-file: is badly used here");
    }

    [TestMethod]
    public void Parse_NetrcFileGivenFlagLikeFileThatDoesNotExist_RefusesAfterFileNameWarning()
    {
        CommandLineParseResult result = Parse(["--netrc-file", "-s", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '-s' provided to --netrc-file does not exist",
            "curl: option --netrc-file: is badly used here");
        Diagnostics.Assert("warning lines", "[\"Warning: The filename argument '-s' looks like a flag.\"]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoNetrcFile_RefusesTheNoPrefix()
    {
        CommandLineParseResult result = Parse(["--no-netrc-file", "f", Url], EveryPathExists);

        AssertRefused(result, "curl: option --no-netrc-file: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_NetrcOptionalWithValue_IgnoresTheValue()
    {
        CommandLineParseResult result = Parse(["--netrc-optional=x", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("NetrcUse", NetrcUse.Optional, CommandLineParseDiagnostics.Peek(result.Options)?.NetrcUse);
        Assert.AreEqual(NetrcUse.Optional, result.Options.NetrcUse);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, pathExists);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach(expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine)),
            CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
