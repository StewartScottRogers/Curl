using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NoNetrcOptions_IgnoresNetrc()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.NetrcRequested);
        Assert.IsFalse(result.Options.NetrcOptionalRequested);
        Assert.IsNull(result.Options.NetrcFile);
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
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url], EveryPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(requested, result.Options.NetrcRequested);
        Assert.AreEqual(optionalRequested, result.Options.NetrcOptionalRequested);
        Assert.AreEqual(file, result.Options.NetrcFile);
        Assert.AreEqual(use, result.Options.NetrcUse);
    }

    [TestMethod]
    public void Parse_NetrcFileThatExists_RecordsTheFileItChecked()
    {
        string? checkedPath = null;

        CommandLineParseResult result = CommandLineParser.Parse(
            ["--netrc-file", "my.netrc", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("my.netrc", result.Options.NetrcFile);
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
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --netrc-file does not exist",
            $"curl: option {spelledOption}: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentThenMissingNetrcFile_HidesTheFileLine()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--netrc-file", "nope", Url], NoPathExists);

        AssertRefused(result, "curl: option --netrc-file: is badly used here");
    }

    [TestMethod]
    public void Parse_NetrcFileGivenFlagLikeFileThatDoesNotExist_RefusesAfterFileNameWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--netrc-file", "-s", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '-s' provided to --netrc-file does not exist",
            "curl: option --netrc-file: is badly used here");
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoNetrcFile_RefusesTheNoPrefix()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-netrc-file", "f", Url], EveryPathExists);

        AssertRefused(result, "curl: option --no-netrc-file: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_NetrcOptionalWithValue_IgnoresTheValue()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--netrc-optional=x", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(NetrcUse.Optional, result.Options.NetrcUse);
    }

    private static void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
