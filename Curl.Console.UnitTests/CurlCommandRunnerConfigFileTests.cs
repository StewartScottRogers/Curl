using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the runner applies the default config file (<c>.curlrc</c>) and <c>-K</c> files, and
/// when it prints curl's <c>Note: Read config file from</c> line, against curl 8.21.0 (mingw,
/// Schannel) measured on 2026-09-27 (BL-243) with <c>CURL_HOME</c> naming a directory holding a
/// <c>.curlrc</c> of <c>-H "X-From-Curlrc: yes"</c>, and a <c>-K</c> file of
/// <c>header = "X-From-K: yes"</c>; and where the note falls around a refused command line,
/// measured the same way on 2026-09-27 (BL-352).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerConfigFileTests
{
    private const string Url = "http://127.0.0.1:18243/a";

    /// <summary>The directory the measurement's <c>CURL_HOME</c> named.</summary>
    private const string FakeHome = @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl243";

    private const string CurlrcPath = FakeHome + @"\.curlrc";

    private const string ConfigFilePath = FakeHome + @"\k.txt";

    private static readonly string NewLine = Environment.NewLine;

    /// <summary>
    /// curl 8.21.0's standard error for <c>curl -v http://127.0.0.1:18245/a</c> up to its first
    /// verbose line, at its default 79 columns.
    /// </summary>
    private static readonly string MeasuredNote =
        @"Note: Read config file from 'C:\Users\Stewart " + NewLine
        + @"Note: Rogers\AppData\Local\Temp\bl243\.curlrc'" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly InMemoryDataFileReader configFiles = new();
    private readonly RecordingProtocolHandler http =
        new("http", _ => ValueTask.FromResult(TransferResult.Success(0)));

    public CurlCommandRunnerConfigFileTests()
    {
        configFiles.Files[CurlrcPath] = Encoding.ASCII.GetBytes("-H \"X-From-Curlrc: yes\"\n");
        configFiles.Files[ConfigFilePath] = Encoding.ASCII.GetBytes("header = \"X-From-K: yes\"\n");
    }

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_CurlrcInHome_AddsItsHeaderToTheTransfer()
    {
        int exitCode = await RunAsync([Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("sent headers", "X-From-Curlrc: yes", string.Join(" | ", SentHeaders()));
        Diagnostics.Assert("stderr", string.Empty, Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "X-From-Curlrc: yes" }, SentHeaders());
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CurlrcAndConfigFile_SendsTheCurlrcHeaderThenTheConfigFileHeader()
    {
        int exitCode = await RunAsync([Url, "-K", ConfigFilePath]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("sent headers", "X-From-Curlrc: yes | X-From-K: yes", string.Join(" | ", SentHeaders()));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "X-From-Curlrc: yes", "X-From-K: yes" }, SentHeaders());
    }

    [TestMethod]
    [DataRow("-q")]
    [DataRow("--disable")]
    public async Task RunAsync_FirstArgumentDisablesTheCurlrc_SendsNoCurlrcHeader(string disable)
    {
        int exitCode = await RunAsync([disable, Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("sent header count", 0, SentHeaders().Length);
        Diagnostics.Assert("curlrc was read", false, configFiles.PathsRead.Contains(CurlrcPath));
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(SentHeaders());
        CollectionAssert.DoesNotContain(configFiles.PathsRead, CurlrcPath);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--trace-ascii=t.txt")]
    public async Task RunAsync_CurlrcReadWithTraceOn_PrintsTheMeasuredNoteFirst(string trace)
    {
        int exitCode = await RunAsync([trace, Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", Visible(MeasuredNote), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(MeasuredNote, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CurlrcReadWithSilentVerbose_StillPrintsTheNote()
    {
        int exitCode = await RunAsync(["-s", "-v", Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", Visible(MeasuredNote), Visible(StandardErrorText));
        Assert.AreEqual(MeasuredNote, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseOverridingTrace_PrintsTheWarningThenTheNote()
    {
        int exitCode = await RunAsync(["--trace-ascii", "t.txt", "-v", Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "stderr",
            Visible("Warning: -v, --verbose overrides an earlier trace option" + NewLine + MeasuredNote),
            Visible(StandardErrorText));
        Assert.AreEqual(
            "Warning: -v, --verbose overrides an earlier trace option" + NewLine + MeasuredNote,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CurlrcReadWithVerboseAndVersion_PrintsTheNoteWithoutTransferring()
    {
        int exitCode = await RunAsync(["-v", "-V"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", Visible(MeasuredNote), Visible(StandardErrorText));
        Diagnostics.Assert("stdout is not empty", true, standardOutput.Length > 0);
        Diagnostics.Assert("transfers dispatched", 0, http.Contexts.Count);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(MeasuredNote, StandardErrorText);
        Assert.IsGreaterThan(0L, standardOutput.Length);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    [DataRow(new[] { Url })]
    [DataRow(new[] { "-v", "--no-verbose", Url })]
    public async Task RunAsync_CurlrcReadWithoutTrace_PrintsNoNote(string[] arguments)
    {
        int exitCode = await RunAsync(arguments);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", string.Empty, Visible(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithNoCurlrc_PrintsNoNote()
    {
        configFiles.Files.Remove(CurlrcPath);

        int exitCode = await RunAsync(["-v", Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", string.Empty, Visible(StandardErrorText));
        Diagnostics.Assert("sent header count", 0, SentHeaders().Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.IsEmpty(SentHeaders());
    }

    [TestMethod]
    public async Task RunAsync_NoDefaultConfigFileSearchGiven_ReadsNoCurlrc()
    {
        string[] arguments = ["-v", Url];
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("default config file search", "none given");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    configFileReader: configFiles)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Visible(StandardErrorText));
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("config paths read", 0, configFiles.PathsRead.Count);
        Diagnostics.Assert("stderr", string.Empty, Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(configFiles.PathsRead);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseThenUnknownOption_PrintsTheRefusalThenTheNote()
    {
        int exitCode = await RunAsync(["-v", "--bogus", Url]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert(
            "stderr",
            Visible(
                "curl: option --bogus: is unknown" + NewLine
                + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine
                + MeasuredNote),
            Visible(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine
            + MeasuredNote,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_UnknownOptionThenVerbose_PrintsTheRefusalWithoutTheNote()
    {
        int exitCode = await RunAsync(["--bogus", "-v", Url]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert(
            "stderr",
            Visible(
                "curl: option --bogus: is unknown" + NewLine
                + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine),
            Visible(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: option --bogus: is unknown" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithNoUrl_PrintsTheNoteThenTheRefusal()
    {
        int exitCode = await RunAsync(["-v"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert(
            "stderr",
            Visible(
                MeasuredNote
                + "curl: (2) no URL specified" + NewLine
                + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine),
            Visible(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            MeasuredNote
            + "curl: (2) no URL specified" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithFormAndData_PrintsTheNoteThenTheWarning()
    {
        int exitCode = await RunAsync(["-v", "-F", "a=b", "-d", "x", Url]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert(
            "stderr",
            Visible(
                MeasuredNote
                + "Warning: You can only select one HTTP request method! You asked for both POST " + NewLine
                + "Warning: (-d, --data) and multipart formpost (-F, --form)." + NewLine),
            Visible(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            MeasuredNote
            + "Warning: You can only select one HTTP request method! You asked for both POST " + NewLine
            + "Warning: (-d, --data) and multipart formpost (-F, --form)." + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_VerboseThenUnreadableConfigFile_PrintsTheRefusalThenTheNote()
    {
        int exitCode = await RunAsync(["-v", "-K", FakeHome + @"
x", Url]);

        Diagnostics.Assert("exit code", 26, exitCode);
        Diagnostics.Assert(
            "stderr ends with the refusal then the note",
            true,
            StandardErrorText.EndsWith(
                "curl: option -K: error encountered when reading a file" + NewLine
                + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine
                + MeasuredNote,
                StringComparison.Ordinal));
        Diagnostics.Assert("stderr tail", Visible(MeasuredNote), Visible(StandardErrorText[Math.Max(0, StandardErrorText.Length - MeasuredNote.Length)..]));
        Assert.AreEqual(26, exitCode);
        Assert.EndsWith(
            "curl: option -K: error encountered when reading a file" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine
            + MeasuredNote,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EmptyCommandLineWithVerboseCurlrc_PrintsTheTryHelpLineAlone()
    {
        configFiles.Files[CurlrcPath] = Encoding.ASCII.GetBytes("verbose\n");

        int exitCode = await RunAsync([]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert(
            "stderr",
            Visible("curl: try 'curl --help' or 'curl --manual' for more information" + NewLine),
            Visible(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    /// <summary>The text with each platform newline spelled out, so what a test prints does not depend on the OS.</summary>
    private static string Visible(string text) =>
        text.Replace("\r\n", "<NewLine>", StringComparison.Ordinal).Replace("\n", "<NewLine>", StringComparison.Ordinal);

    private string[] SentHeaders() => [.. http.Contexts.Single().Http!.Headers];

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange(
            "curlrc content",
            configFiles.Files.TryGetValue(CurlrcPath, out byte[]? curlrc) ? Visible(Encoding.ASCII.GetString(curlrc)) : "<absent>");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    configFileReader: configFiles,
                    defaultConfigFileSearch: new DefaultConfigFileSearch(
                        name => name == "CURL_HOME" ? FakeHome : null,
                        isWindows: true,
                        executableDirectory: null,
                        accountHomeDirectory: null))
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr", Visible(StandardErrorText));
        return exitCode;
    }
}
