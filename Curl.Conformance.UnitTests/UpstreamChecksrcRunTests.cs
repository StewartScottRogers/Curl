using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamChecksrcRun"/> and <see cref="UpstreamChecksrcScript"/>: checksrc.pl's options, usage text,
/// <c>checksrc.skip</c>, <c>.checksrc</c>, <c>!checksrc!</c> instructions, <c>checkwarn</c>, the end-of-file checks and
/// the summary, with no per-line check plugged in yet (BL-3416).
/// </summary>
[TestClass]
public sealed class UpstreamChecksrcRunTests
{
    private string root = "";

    [TestInitialize]
    public void CreateRoot()
    {
        root = Path.Combine(Path.GetTempPath(), "checksrc-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, true);

    [TestMethod]
    public void Check_NoFile_WritesTheUsageAndExitsZero()
    {
        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check(["-bmagicbad"]);

        Assert.AreEqual(0, result.ExitCode);
        Assert.StartsWith("checksrc.pl [option] <file1> [file2] ...\n Options:\n", result.Output);
        Assert.Contains(" COPYRIGHTYEAR     : copyright year incorrect[*]\n", result.Output);
        Assert.Contains(" TABS              : TAB characters not allowed\n", result.Output);
        Assert.Contains(" [*] = disabled by default\n\nDetects and bans use of these functions:\n", result.Output);
        Assert.Contains(" magicbad          \n", result.Output);
        Assert.AreEqual("", result.Error);
    }

    [TestMethod]
    public void Check_HelpAfterAnOption_WritesTheUsage()
    {
        Assert.StartsWith("checksrc.pl [option]", UpstreamChecksrcScript.Check(["-v", "--help", "x.c"]).Output);
        Assert.StartsWith("checksrc.pl [option]", UpstreamChecksrcScript.Check(["-h"]).Output);
    }

    [TestMethod]
    public void Check_AFileWithNoCopyright_ReportsTheErrorAfterTheDirectory()
    {
        Write("code.c", "int a;\r\nint b;\n");

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check([$"-D{root}", "code.c"]);

        Assert.AreEqual(
            $"{root}/code.c:1:1: error: Missing copyright statement (COPYRIGHT)\n \n ^\nchecksrc: 1 errors and 0 warnings\n",
            result.Output);
        Assert.AreEqual(5, result.ExitCode);
    }

    [TestMethod]
    public void Check_AnAbsolutePathAfterDotSlash_IsOpenedAsItIs()
    {
        Write("code.c", "int a;");

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check(["-v", $"./{root}/code.c"]);

        Assert.StartsWith($"Checking file: ./{root}/code.c\n./{root}/code.c:1:1: error:", result.Output);
    }

    [TestMethod]
    public void Check_TheDirectoryDotAndAPathWithASlash_ReadsTheFilesFolderChecksrc()
    {
        Write("code.c", "int a;\n");
        Write(".checksrc", "# comment\n\nbadline\n");

        Assert.ThrowsExactly<InvalidDataException>(() => UpstreamChecksrcScript.Check([$"{root}/code.c"]));
    }

    [TestMethod]
    public void Check_SkippedFileVerbose_WritesAnEmptySummary()
    {
        Write("code.c", "int a;\n");

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check([$"-D{root}", "-Wcode.c", "-v", "code.c", "code.c"]);

        Assert.AreEqual("checksrc: 0 errors and 0 warnings\n", result.Output);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public void Check_AnAcceptedRule_IsSuppressedAndCounted()
    {
        Write("code.c", "int a;\n");

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check([$"-D{root}", "-ACOPYRIGHT", "-v", "code.c"]);

        Assert.EndsWith("checksrc: 0 errors and 0 warnings\nchecksrc: 1 errors and 0 warnings suppressed\n", result.Output);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public void Check_AnUnknownAcceptedRule_StopsTheScript()
    {
        Write("code.c", "int a;\n");

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check([$"-D{root}", "-AFOO", "code.c"]);

        Assert.AreEqual("'FOO' is not a warning to accept!\n", result.Output);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public void Check_ASkipListLine_SuppressesTheProblemOnIt()
    {
        Write("code.c", "int a;\n");
        Write("checksrc.skip", "foo\n\nbar");

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check([$"-D{root}", "code.c"]);

        Assert.AreEqual("", result.Output);
    }

    [TestMethod]
    public void Run_TheLocalChecksrc_EnablesDisablesBansAndAllows()
    {
        Write("code.c", "int a;\n");
        Write(".checksrc", "  # comment\n\nenable STDERR\nenable BOGUS\ndisable COPYRIGHT\ndisable BOGUS\nbanfunc foo\nallowfunc malloc\n");
        UpstreamChecksrcRun run = new([$"-D{root}", "-i3", "-m100", "-bbar", "-abar", "code.c"]);

        UpstreamChecksrcScript.Result result = run.Run();

        Assert.AreEqual("invalid warning specified in .checksrc: \"BOGUS\"\ninvalid warning specified in .checksrc: \"BOGUS\"\n", result.Error);
        Assert.AreEqual("", result.Output);
        Assert.IsTrue(run.IsEnabled("STDERR"));
        Assert.IsFalse(run.IsEnabled("COPYRIGHTYEAR"));
        Assert.IsTrue(run.IsBanned("foo"));
        Assert.IsFalse(run.IsBanned("malloc"));
        Assert.IsFalse(run.IsBanned("bar"));
        Assert.IsFalse(run.IsBanned("nothing"));
        Assert.Contains("bar", run.KnownFunctions);
        Assert.AreEqual(3, run.Indent);
        Assert.AreEqual(100, run.MaxColumn);
    }

    [TestMethod]
    public void Check_Instructions_DisableEnableAndReportBadOnes()
    {
        Write("code.c", string.Join('\n',
            "/* !checksrc! disable TABS 2 */",
            "/* !checksrc! disable TABS */",
            "/* !checksrc! disable LONGLINE 0 */",
            "// !checksrc! frob",
            "/* !checksrc! enable TABS */",
            "/* !checksrc! disable FOO all */",
            "/* !checksrc! disable BAR abc */",
            "/* !checksrc! disable COPYRIGHT */",
            "#line 50 \"other.c\"",
            "/* !checksrc! disable BADCOMMAND */",
            "/* !checksrc! frob */",
            ""));

        UpstreamChecksrcScript.Result result = UpstreamChecksrcScript.Check([$"-D{root}", "code.c"]);

        string file = $"{root}/code.c";
        Assert.AreEqual(
            $"{file}:2:1: warning: TABS already disabled from line 1 (BADCOMMAND)\n /* !checksrc! disable TABS */\n ^\n" +
            $"{file}:3:1: warning: Disable zero not supported, did you mean to enable? (BADCOMMAND)\n /* !checksrc! disable LONGLINE 0 */\n ^\n" +
            $"{file}:4:1: warning: Illegal !checksrc! command (BADCOMMAND)\n // !checksrc! frob\n ^\n" +
            $"{file}:4:1: warning: // comment (CPPCOMMENTS)\n // !checksrc! frob\n ^\n" +
            $"{file}:5:16: warning: No warning was inhibited! (UNUSEDIGNORE)\n /* !checksrc! enable TABS */\n" + new string(' ', 16) + "^\n" +
            "other.c:6:15: warning: Unused ignore: FOO (UNUSEDIGNORE)\n /* !checksrc! disable FOO all */\n" + new string(' ', 15) + "^\n" +
            "other.c:7:15: warning: Unused ignore: BAR (UNUSEDIGNORE)\n /* !checksrc! disable BAR abc */\n" + new string(' ', 15) + "^\n" +
            "checksrc: 0 errors and 7 warnings\nchecksrc: 1 errors and 1 warnings suppressed\n",
            result.Output);
    }

    [TestMethod]
    public void Warn_PastColumn79_PrintsNoCaret()
    {
        UpstreamChecksrcRun run = new([]);

        run.Warn("LONGLINE", 3, 79, "f.c", "text", "Longer than 79 columns");

        Assert.StartsWith("f.c:3:80: warning: Longer than 79 columns (LONGLINE)\n text\nchecksrc.pl", run.Run().Output);
    }

    [TestMethod]
    public void CheckEndOfFile_AnOpenComment_ReportsIt()
    {
        UpstreamChecksrcRun run = new([]);
        UpstreamChecksrcRun.FileScan scan = new("f.c") { InComment = true };
        scan.Copyright.Add(new UpstreamChecksrcRun.CopyrightYear(2026, 1, 3, " * Copyright 2026"));

        run.CheckEndOfFile(scan);

        Assert.StartsWith("f.c:1:1: error: Missing closing comment (OPENCOMMENT)\n \n ^\n", run.Run().Output);
    }

    [TestMethod]
    public void FileScan_LinesTrackThePreviousCodeAndPreprocessorLines()
    {
        UpstreamChecksrcRun.FileScan scan = new("f.c");
        scan.StartLine("#include <a.h>");
        scan.Preprocessor = true;
        scan.Column = 4;
        UpstreamChecksrcRun.CheckLineSecondHalf(scan);
        scan.EndLine();
        scan.StartLine("int a;");
        scan.Preprocessor = false;
        scan.EndLine();
        scan.Text = "x";
        scan.PreviousFreeIndent = "  ";
        scan.PreviousFreeVariable = "p";
        scan.Includes["a.h"] = "#include <a.h>";

        Assert.AreEqual(3, scan.Line);
        Assert.AreEqual(0, scan.Column);
        Assert.AreEqual("#include <a.h>", scan.PreviousPreprocessorLine);
        Assert.AreEqual("int a;", scan.PreviousLine);
        Assert.AreEqual("int a;", scan.Original);
        Assert.AreEqual("x", scan.Text);
        Assert.IsFalse(scan.PreviousPreprocessor);
        Assert.AreEqual("  p", scan.PreviousFreeIndent + scan.PreviousFreeVariable);
        Assert.HasCount(1, scan.Includes);
        Assert.AreEqual("f.c", scan.File);
    }

    [TestMethod]
    public async Task RunAsync_WritesTheResultToStandardOutput()
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamChecksrcScript.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.StartsWith("checksrc.pl [option]", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamChecksrcScript.Check(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamChecksrcScript.RunAsync(null!, _ => Task.FromResult(0)));
    }

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(root, name), text, Encoding.Latin1);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
