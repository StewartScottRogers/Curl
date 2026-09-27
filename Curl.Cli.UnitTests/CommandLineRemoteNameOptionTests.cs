using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-O</c>/<c>--remote-name</c>, <c>--remote-name-all</c>, <c>-J</c>/<c>--remote-header-name</c>,
/// <c>--output-dir</c> and <c>--create-dirs</c>. Measured with the local curl 8.21.0 on 2026-09-26
/// (<c>curl &lt;arguments&gt; --bogus</c> for what parses, and <c>file:///C:/Windows/win.ini</c> for
/// where bodies go): <c>-o x -O u1 u2</c> writes <c>x</c> and <c>win.ini</c>; <c>u1 --remote-name-all u2</c>
/// saves only <c>u2</c>; <c>--remote-name-all -o x u</c> writes only <c>x</c>;
/// <c>--remote-name-all --no-remote-name u</c> writes to standard output; <c>-O -O u</c> and
/// <c>--remote-name-all --no-remote-name --no-remote-name u</c> warn <c>Warning: Got more output options
/// than URLs</c> after the transfer; <c>--no-remote-name --no-remote-name u</c> and
/// <c>-o x --no-remote-name u</c> do not. <c>--output-dir ''</c> exits 2 with
/// <c>curl: option --output-dir: blank argument where content is expected</c>; <c>--output-dir -x</c>
/// does not warn. <c>--no-remote-name</c>, <c>--no-remote-name-all</c>, <c>--no-remote-header-name</c>
/// and <c>--no-create-dirs</c> (each also with <c>=x</c>) are accepted; <c>--no-output-dir</c> and
/// <c>--no-output-dir=x</c> exit 2 as not reversible.
/// </summary>
[TestClass]
public sealed class CommandLineRemoteNameOptionTests
{
    private const string Url = "http://127.0.0.1:1/a";

    private const string OtherUrl = "http://127.0.0.1:1/b";

    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    [TestMethod]
    public void Parse_NoOutputOption_SendsTheBodyToStandardOutput()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual(Url, output.Url);
        Assert.IsNull(output.FileName);
        Assert.IsFalse(output.UsesRemoteName);
        Assert.IsFalse(result.Options.RemoteNameAll);
        Assert.IsFalse(result.Options.RemoteHeaderName);
        Assert.IsNull(result.Options.OutputDirectory);
        Assert.IsFalse(result.Options.CreateDirectories);
    }

    [TestMethod]
    [DataRow("-O")]
    [DataRow("--remote-name")]
    public void Parse_RemoteName_UsesTheRemoteNameForItsUrl(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual(Url, output.Url);
        Assert.IsTrue(output.UsesRemoteName);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_RemoteNameAfterItsUrl_StillPairsWithIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "-O"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UrlOutputs.Single().UsesRemoteName);
    }

    [TestMethod]
    public void Parse_OutputThenRemoteName_PairsEachWithItsUrlInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "x", "-O", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Options.UrlOutputs);
        Assert.AreEqual(Url, result.Options.UrlOutputs[0].Url);
        Assert.AreEqual("x", result.Options.UrlOutputs[0].FileName);
        Assert.IsFalse(result.Options.UrlOutputs[0].UsesRemoteName);
        Assert.AreEqual(OtherUrl, result.Options.UrlOutputs[1].Url);
        Assert.IsNull(result.Options.UrlOutputs[1].FileName);
        Assert.IsTrue(result.Options.UrlOutputs[1].UsesRemoteName);
        CollectionAssert.AreEqual(new[] { "x" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_RemoteNameThenOutput_PairsTheOutputFileWithTheSecondUrl()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-O", "-o", "x", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { null, "x" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_OneRemoteNameForTwoUrls_SendsTheSecondToStandardOutput()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-O", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UrlOutputs[0].UsesRemoteName);
        Assert.IsFalse(result.Options.UrlOutputs[1].UsesRemoteName);
    }

    [TestMethod]
    public void Parse_TwoRemoteNamesForOneUrl_WarnsAfterTheTransfers()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-O", "-O", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.UrlOutputs[1].Url);
        CollectionAssert.AreEqual(new[] { CommandLineWarning.MoreOutputOptionsThanUrls }, result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    public void Parse_OutputThenRemoteNameForOneUrl_WarnsAfterTheTransfers()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "a", "-O", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { CommandLineWarning.MoreOutputOptionsThanUrls }, result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    public void Parse_SilentWithTwoRemoteNamesForOneUrl_DoesNotWarn()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "-O", "-O", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_RemoteNameAll_UsesTheRemoteNameForEveryUrl()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RemoteNameAll);
        Assert.IsTrue(result.Options.UrlOutputs.All(output => output.UsesRemoteName));
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_RemoteNameAllBetweenUrls_AppliesOnlyToTheUrlsAfterIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--remote-name-all", OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.UrlOutputs[0].UsesRemoteName);
        Assert.IsTrue(result.Options.UrlOutputs[1].UsesRemoteName);
    }

    [TestMethod]
    public void Parse_RemoteNameAllWithOutput_KeepsTheOutputFileName()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", Url, "-o", "x"]);

        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual("x", output.FileName);
        Assert.IsTrue(output.UsesRemoteName);
    }

    [TestMethod]
    public void Parse_RemoteNameAllThenNoRemoteName_SendsTheUrlToStandardOutput()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", "--no-remote-name", Url]);

        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual(Url, output.Url);
        Assert.IsFalse(output.UsesRemoteName);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_RemoteNameAllThenTwoNoRemoteNamesForOneUrl_WarnsAfterTheTransfers()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", "--no-remote-name", "--no-remote-name", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { CommandLineWarning.MoreOutputOptionsThanUrls }, result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    public void Parse_TwoNoRemoteNamesForOneUrl_DropsThemWithoutWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-remote-name", "--no-remote-name", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(1, result.Options.UrlOutputs);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_OutputThenNoRemoteNameForOneUrl_DropsItWithoutWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "x", "--no-remote-name", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("x", result.Options.UrlOutputs.Single().FileName);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_NoRemoteNameAfterItsUrl_PairsWithIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", Url, "--no-remote-name"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.UrlOutputs.Single().UsesRemoteName);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    [DataRow("--no-remote-name")]
    [DataRow("--no-remote-name=x")]
    public void Parse_NoRemoteName_IsAccepted(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-O", Url, spelledOption, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UrlOutputs[0].UsesRemoteName);
        Assert.IsFalse(result.Options.UrlOutputs[1].UsesRemoteName);
    }

    [TestMethod]
    [DataRow("--no-remote-name-all")]
    [DataRow("--no-remote-name-all=x")]
    public void Parse_NoRemoteNameAll_TurnsRemoteNameAllOff(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoteNameAll);
        Assert.IsFalse(result.Options.UrlOutputs.Single().UsesRemoteName);
    }

    [TestMethod]
    [DataRow("-J")]
    [DataRow("--remote-header-name")]
    public void Parse_RemoteHeaderName_AsksForTheHeaderName(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RemoteHeaderName);
    }

    [TestMethod]
    [DataRow("--no-remote-header-name")]
    [DataRow("--no-remote-header-name=x")]
    public void Parse_NoRemoteHeaderName_TurnsItOff(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-J", spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RemoteHeaderName);
    }

    [TestMethod]
    public void Parse_CreateDirs_AsksForMissingDirectories()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--create-dirs", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.CreateDirectories);
    }

    [TestMethod]
    [DataRow("--no-create-dirs")]
    [DataRow("--no-create-dirs=x")]
    public void Parse_NoCreateDirs_TurnsItOff(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--create-dirs", spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.CreateDirectories);
    }

    [TestMethod]
    [DataRow("d")]
    [DataRow("-x")]
    public void Parse_OutputDir_KeepsTheLastDirectoryWithoutWarning(string directory)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--output-dir", "first", "--output-dir", directory, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(directory, result.Options.OutputDirectory);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(new[] { "--output-dir", "" }, "--output-dir")]
    [DataRow(new[] { "--output-dir=" }, "--output-dir=")]
    public void Parse_BlankOutputDir_IsRefused(string[] arguments, string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--no-output-dir")]
    [DataRow("--no-output-dir=x")]
    public void Parse_NoOutputDir_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: {CannotBeReversed}");
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
