namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamPerlOneLiner"/>: each <c>%PERL -e</c> one-liner form the vendored cases
/// use, written here as the expander leaves it, does what Perl would, and any other line is refused.
/// </summary>
[TestClass]
public sealed class UpstreamPerlOneLinerTests
{
    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"perl-one-liner-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    [DataRow(960898200L, 0)]
    [DataRow(-922349651L, 0)]
    [DataRow(1234567890L, 1)]
    public void Run_StatModificationTime_ExitsZeroOnlyWhenTheTimeMatches(long seconds, int expectedExitCode)
    {
        string file = $"{folder}/1443";
        File.WriteAllText(file, "x");
        File.SetLastWriteTimeUtc(file, DateTimeOffset.FromUnixTimeSeconds(seconds == 1234567890L ? 960898200L : seconds).UtcDateTime);

        var result = UpstreamPerlOneLiner.Run($"-e 'exit((stat(\"{file}\"))[9] != {seconds})'", "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(expectedExitCode, ""), result);
    }

    [TestMethod]
    public void Run_StatModificationTimeOfAMissingFile_ComparesZero()
    {
        Assert.AreEqual(1, UpstreamPerlOneLiner.Run($"-e 'exit((stat(\"{folder}/missing\"))[9] != 960898200)'", "linux")!.ExitCode);
        Assert.AreEqual(0, UpstreamPerlOneLiner.Run($"-e 'exit((stat(\"{folder}/missing\"))[9] != 0)'", "linux")!.ExitCode);
    }

    [TestMethod]
    [DataRow("127.0.0.1", "")]
    [DataRow("127.0.0.2", "Test requires default test client host address")]
    public void Run_PrintIfNotEqual_PrintsOnlyWhenTheValuesDiffer(string clientIp, string expectedOutput)
    {
        var result = UpstreamPerlOneLiner.Run(
            $"-e \"print 'Test requires default test client host address' if('{clientIp}' ne '127.0.0.1');\"", "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, expectedOutput), result);
    }

    [TestMethod]
    [DataRow("MSWin32", "Test requires a Unix system")]
    [DataRow("msys", "Test requires a Unix system")]
    [DataRow("linux", "")]
    [DataRow("darwin", "")]
    public void Run_PrintIfOperatingSystem_PrintsOnlyOnTheNamedSystems(string operatingSystemName, string expectedOutput)
    {
        var result = UpstreamPerlOneLiner.Run(
            "-e \"print 'Test requires a Unix system' if($^O eq 'MSWin32' || $^O eq 'cygwin' || $^O eq 'dos' || $^O eq 'msys');\"",
            operatingSystemName);

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, expectedOutput), result);
    }

    [TestMethod]
    [DataRow("127.0.0.1", 0, "")]
    [DataRow("192.168.1.5", 1, "Test only works for HOSTIPs ending with .0.0.1")]
    public void Run_HostIpPrecheck_PrintsAndExitsOneWhenTheAddressDoesNotMatch(string hostIp, int expectedExitCode, string expectedOutput)
    {
        var result = UpstreamPerlOneLiner.Run(
            $"-e 'if(\"{hostIp}\" !~ /\\.0\\.0\\.1$/) {{print \"Test only works for HOSTIPs ending with .0.0.1\"; exit(1)}}'", "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(expectedExitCode, expectedOutput), result);
    }

    [TestMethod]
    [DataRow("Usage: curl [options...]\n --version  Show version\nother\n", 0)]
    [DataRow("Usage: curl [options...]\nother\n", 1)]
    public void Run_GrepLineCount_ExitsZeroOnlyWhenTheCountMatches(string stdout, int expectedExitCode)
    {
        string file = $"{folder}/stdout1027";
        File.WriteAllText(file, stdout);

        var result = UpstreamPerlOneLiner.Run(
            "-e 'open(IN,$ARGV[0]); my $lines=grep(/(Usage: curl )|(--version\\s*Show version)/, <IN>); exit ($lines != 2); # Let this file pass an XML syntax check: </IN>' "
            + file,
            "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(expectedExitCode, ""), result);
    }

    [TestMethod]
    public void Run_GrepLineCountOfAMissingFile_CountsNoLines()
    {
        var result = UpstreamPerlOneLiner.Run($"-e 'open(IN,$ARGV[0]); my $lines=grep(/AUTHORS/, <IN>); exit ($lines != 0);' {folder}/missing", "linux");

        Assert.AreEqual(0, result!.ExitCode);
    }

    [TestMethod]
    public void Run_PrintfLoopRedirected_WritesTheTextThatManyTimes()
    {
        string file = $"{folder}/cmd1291";

        var result = UpstreamPerlOneLiner.Run(
            $"-e 'for(1 .. 3) {{ printf(\"upload-file={folder}/upload-this\\nurl=htttttp://non-existing-host.haxx.se/upload/1291\\n\", $_);}}' > {file};",
            "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), result);
        string once = $"upload-file={folder}/upload-this\nurl=htttttp://non-existing-host.haxx.se/upload/1291\n";
        Assert.AreEqual(once + once + once, File.ReadAllText(file));
    }

    [TestMethod]
    public void Run_WriteLoop_WritesEveryNumberedFile()
    {
        var result = UpstreamPerlOneLiner.Run(WriteLoop($"{folder}/exist1683"), "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), result);
        Assert.AreEqual("to stay the same", File.ReadAllText($"{folder}/exist1683.1"));
        Assert.AreEqual("to stay the same", File.ReadAllText($"{folder}/exist1683.100"));
    }

    [TestMethod]
    public void Run_WriteLoopIntoAMissingFolder_DiesWithEnoent()
    {
        var result = UpstreamPerlOneLiner.Run(WriteLoop($"{folder}/missing/exist1683"), "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(2, ""), result);
    }

    [TestMethod]
    public void Run_VerifyLoop_ExitsZeroWhenEveryFileHoldsTheText()
    {
        UpstreamPerlOneLiner.Run(WriteLoop($"{folder}/exist1683"), "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamPerlOneLiner.Run(VerifyLoop($"{folder}/exist1683"), "linux"));
    }

    [TestMethod]
    public void Run_VerifyLoop_DiesWhenAFileIsMissingOrChanged()
    {
        UpstreamPerlOneLiner.Run(WriteLoop($"{folder}/exist1683"), "linux");
        File.WriteAllText($"{folder}/exist1683.50", "overwritten");

        Assert.AreEqual(255, UpstreamPerlOneLiner.Run(VerifyLoop($"{folder}/exist1683"), "linux")!.ExitCode);

        File.Delete($"{folder}/exist1683.50");

        Assert.AreEqual(2, UpstreamPerlOneLiner.Run(VerifyLoop($"{folder}/exist1683"), "linux")!.ExitCode);
    }

    [TestMethod]
    [DataRow("-e \"if('[::1]' ne '[::1]') {print 'Test requires default test client host address';} else {exec 'resolve --ipv6 ip6-localhost'; print 'Cannot run precheck resolve';}\"")]
    [DataRow("/tests/libtest/test610.pl gone /log/file")]
    [DataRow("-e 'print \"hello\"'")]
    public void Run_AnyOtherLine_IsNotInterpreted(string arguments)
    {
        Assert.IsNull(UpstreamPerlOneLiner.Run(arguments, "linux"));
    }

    [TestMethod]
    public void Run_NullArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamPerlOneLiner.Run(null!, "linux"));
    }

    private static string WriteLoop(string prefix) =>
        $"-e 'for my $i ((1..100)) {{ my $filename = \"{prefix}.$i\"; open(FH, \">\", $filename) or die $!; print FH \"to stay the same\" ; close(FH) }}'";

    private static string VerifyLoop(string prefix) =>
        $"-e 'for my $i ((1..100)) {{ my $filename = \"{prefix}.$i\"; open(FH, \"<\", $filename) or die $!; (<FH> eq \"to stay the same\" and <FH> eq \"\") or die \"incorrect $filename\" ; close(FH) }}'";

    [TestMethod]
    [DataRow("perl -e \"print 'x' if('a' ne 'b');\"", true)]
    [DataRow("perl -e 'exec \"something\"'", false)]
    [DataRow("-e \"print 'x' if('a' ne 'b');\"", false)]
    public void Interprets_TellsAnInterpretedPerlLineFromAnyOther(string line, bool expected)
    {
        Assert.AreEqual(expected, UpstreamPerlOneLiner.Interprets(line));
    }

    [TestMethod]
    public void RunLine_PerlLine_RunsTheOneLiner()
    {
        UpstreamPerlOneLinerResult? result = UpstreamPerlOneLiner.RunLine("perl -e \"print 'Test requires a Unix system' if($^O eq 'MSWin32');\"", "MSWin32");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, "Test requires a Unix system"), result);
    }

    [TestMethod]
    public void RunLine_LineThatDoesNotStartWithThePerlProgram_IsNotRun()
    {
        Assert.IsNull(UpstreamPerlOneLiner.RunLine("sh -e \"print 'x' if('a' ne 'b');\"", "linux"));
    }
}
