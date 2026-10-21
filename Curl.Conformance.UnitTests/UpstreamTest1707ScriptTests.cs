using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

[TestClass]
public sealed class UpstreamTest1707ScriptTests
{
    private const string HelpAll =
        "Usage: curl [options...] <url>\n" +
        "     --form-escape                    Escape form fields using backslash\n" +
        " -F, --form <name=content>            Specify multipart MIME data\n" +
        " -k, --insecure                       Allow insecure server connections\n" +
        "     --no-clobber                     Do not overwrite files that already exist\n";

    private const string Manual =
        "    -F, --form <name=content>\n" +
        "\t    (HTTP SMTP IMAP) For the HTTP protocol family.\n" +
        "\n" +
        "    -k, --insecure\n" +
        "\t    (TLS SFTP SCP) By default.\n" +
        "\n" +
        "    --no-clobber\n" +
        "\t    When used in conjunction.\n";

    [TestMethod]
    [DataRow("-F", "-F, --form")]
    [DataRow("--insecure", "-k, --insecure")]
    [DataRow("--no-clobber", "--no-clobber")]
    [DataRow("--absent", "--absent")]
    public void FullOptionName_FromHelpAll_IsTheManualHeadingsNames(string option, string expected) =>
        Assert.AreEqual(expected, UpstreamTest1707Script.FullOptionName(option, HelpAll));

    [TestMethod]
    public void ManualSection_ForAnOption_RunsFromItsHeadingToTheNext()
    {
        List<string> section = UpstreamTest1707Script.ManualSection(Lines(Manual), "-k, --insecure");

        CollectionAssert.AreEqual(new[] { "    -k, --insecure\n", "\t    (TLS SFTP SCP) By default.\n", "\n" }, section);
    }

    [TestMethod]
    public void ManualSection_ForAnOptionNotInTheManual_IsEmpty() =>
        Assert.IsEmpty(UpstreamTest1707Script.ManualSection(Lines(Manual), "--absent"));

    [TestMethod]
    public void Compare_LinesThatDifferOnlyInLineEnds_ReportNothing()
    {
        StringBuilder report = new();

        int differences = UpstreamTest1707Script.Compare(["    --no-clobber\r\n", "x\r\n"], ["    --no-clobber\n", "x\n"], "--no-clobber", "curl.txt", report);

        Assert.AreEqual(0, differences);
        Assert.AreEqual("", report.ToString());
    }

    [TestMethod]
    public void Compare_DifferentLineCounts_ReportsBothCounts()
    {
        StringBuilder report = new();

        int differences = UpstreamTest1707Script.Compare(["a\n"], ["a\n", "b\n"], "-F", "../docs/cmdline-opts/curl.txt", report);

        Assert.AreEqual(1, differences);
        Assert.AreEqual("curl -h -F is 1 lines, ../docs/cmdline-opts/curl.txt says 2 lines\n", report.ToString());
    }

    [TestMethod]
    public void Compare_LinesOfOneLengthThatDiffer_ReportsTheLineAndEachColumnAsPrintDoes()
    {
        StringBuilder report = new();

        int differences = UpstreamTest1707Script.Compare(["same\n", "abc\n"], ["same\n", "abd\n"], "-F", "curl.txt", report);

        Assert.AreEqual(1, differences);
        Assert.AreEqual("Line 1\n-h   : abc (3 bytes)\nfile : abd (3 bytes)\n-h   col %d: %02x\n299file col %d: %02x\n2100", report.ToString());
    }

    [TestMethod]
    public void Compare_LinesOfDifferentLengths_ReportsTheLineAlone()
    {
        StringBuilder report = new();

        int differences = UpstreamTest1707Script.Compare(["ab\n", "x\n"], ["abc\n", "y\n"], "-F", "curl.txt", report);

        Assert.AreEqual(2, differences);
        StringAssert.StartsWith(report.ToString(), "Line 0\n-h   : ab (2 bytes)\nfile : abc (3 bytes)\nLine 1\n");
    }

    [TestMethod]
    public async Task RunAsync_HelpTextMatchingTheManual_PrintsNothingAndExitsZero()
    {
        (int exitCode, string output) = await RunScriptAsync("--insecure", "    -k, --insecure\r\n\t    (TLS SFTP SCP) By default.\r\n\r\n");

        Assert.AreEqual(0, exitCode, output);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public async Task RunAsync_HelpTextDifferingFromTheManual_ReportsTheLineAndExitsOne()
    {
        (int exitCode, string output) = await RunScriptAsync("--no-clobber", "    --no-clobber\n\t    When used together.");

        Assert.AreEqual(1, exitCode);
        StringAssert.StartsWith(output, "Line 1\n");
    }

    [TestMethod]
    public void RunAsync_NullArguments_Throw()
    {
        UpstreamTest1707Script script = new("tree");

        Assert.ThrowsExactly<ArgumentNullException>(() => script.RunAsync(null!, _ => Task.FromResult(0)).GetAwaiter().GetResult());
        Assert.ThrowsExactly<ArgumentNullException>(() => script.RunAsync(Invocation([]), null!).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void PublicMembers_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1707Script.FullOptionName(null!, HelpAll));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1707Script.ManualSection(null!, "-F"));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1707Script.Compare(null!, [], "-F", "x", new StringBuilder()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1707Script.Compare([], null!, "-F", "x", new StringBuilder()));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1707Script.Compare([], [], "-F", "x", null!));
    }

    // Upstream's cases against Curl's own -h and the vendored curl.txt (real curl 8.22.0's manual), as the ratchet runs them.
    [TestMethod]
    [DataRow(1707)]
    [DataRow(1708)]
    [DataRow(1710)]
    public async Task UpstreamCase_RunThroughTheInterpretation_Passes(int testNumber)
    {
        string upstreamTestData = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(upstreamTestData, $"test{testNumber}.curltest"));
        UpstreamTest1707Script script = new(upstreamTestData);
        UpstreamCaseRunner runner = new(
            UpstreamConformanceTests.RunCurlAsync,
            UpstreamCurlPlatform.Windows,
            TimeProvider.System,
            TimeSpan.FromSeconds(60),
            perlCommandScripts: new Dictionary<string, Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>> { [UpstreamTest1707Script.ScriptName] = script.RunAsync });
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test{testNumber}-{Guid.NewGuid():N}"));
        try
        {
            UpstreamCaseOutcome outcome = await runner.RunAsync(testNumber, testFile, logDirectory.FullName, sourceDirectory: upstreamTestData);

            Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        }
        finally
        {
            logDirectory.Delete(recursive: true);
        }
    }

    // Runs the script for the option over a temporary source tree whose docs/curl.txt is the test manual, with a stub
    // curl printing the help text for -h <option> and HelpAll for -h all.
    private static async Task<(int ExitCode, string Output)> RunScriptAsync(string option, string helpText)
    {
        DirectoryInfo tree = Directory.CreateTempSubdirectory("curl1707-");
        try
        {
            Directory.CreateDirectory(Path.Combine(tree.FullName, "docs"));
            await File.WriteAllTextAsync(Path.Combine(tree.FullName, "docs", "curl.txt"), Manual);
            using MemoryStream output = new();
            int exitCode = await new UpstreamTest1707Script(tree.FullName).RunAsync(
                Invocation([UpstreamCaseRunner.CurlProgram, option, "../docs/curl.txt"], output),
                async curl =>
                {
                    await curl.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(curl.Arguments[1] == "all" ? HelpAll : helpText));
                    return 0;
                });
            return (exitCode, Encoding.Latin1.GetString(output.ToArray()));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static UpstreamCurlInvocation Invocation(IReadOnlyList<string> arguments, Stream? output = null) =>
        new(arguments, output ?? Stream.Null, Stream.Null, Stream.Null, new RefusingConnector(), new RefusingDatagramConnector());

    private static List<string> Lines(string text) =>
        [.. text.Split('\n').SkipLast(1).Select(line => line + "\n")];

    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused("no server"));
    }

    private sealed class RefusingDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "no server"));
    }
}
