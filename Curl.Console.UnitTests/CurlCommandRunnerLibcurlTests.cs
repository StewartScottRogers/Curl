using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--libcurl</c> through the runner against curl 8.21.0 (mingw, Schannel) measured on 2026-10-01
/// (BL-652 Notes): the transfers still run, the source follows their output on standard output for
/// <c>-</c>, a file gets it in CR LF on Windows and LF elsewhere, and a file that cannot be opened gets
/// curl's warning unless <c>-s</c> was given.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerLibcurlTests
{
    private const string SourceFile = "out.c";

    private const string Url = "http://localhost:47652/";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.Latin1.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_LibcurlToStandardOutput_WritesTheSourceAfterTheTransfersBody()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", "-", Url]);

        string expectedStandardOutput = "hello" + ExpectedSource();
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", Lf(expectedStandardOutput), Lf(StandardOutputText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardOutput, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlToAFileOnWindows_WritesItInCrLfAndPerformsTheTransfer()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", SourceFile, Url]);

        string expectedSource = ExpectedSource().Replace("\n", "\r\n", StringComparison.Ordinal);
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "hello", StandardOutputText);
        Diagnostics.Assert("out.c CR LF count", CountCrLf(expectedSource), CountCrLf(WrittenSource()));
        Diagnostics.Diff("out.c", Lf(expectedSource), Lf(WrittenSource()));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.AreEqual(expectedSource, WrittenSource());
    }

    [TestMethod]
    public async Task RunAsync_LibcurlToAFileOffWindows_WritesItInLineFeeds()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", SourceFile, Url], runsOnWindows: false);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("out.c CR LF count", 0, CountCrLf(WrittenSource()));
        Diagnostics.Diff("out.c", ExpectedSource(), WrittenSource());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(ExpectedSource(), WrittenSource());
    }

    [TestMethod]
    public async Task RunAsync_FileThatCannotBeOpened_WarnsAsCurlDoes()
    {
        files.UnwritablePaths.Add(SourceFile);
        Diagnostics.Arrange("unwritable path", SourceFile);

        int exitCode = await RunAsync(["--no-progress-meter", "--libcurl", SourceFile, Url]);

        string expectedWarning = "Warning: Failed to open out.c to write libcurl code" + Environment.NewLine;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains the warning", true, StandardErrorText.Contains(expectedWarning, StringComparison.Ordinal));
        Diagnostics.Assert("out.c written", false, files.Written.ContainsKey(SourceFile));
        Assert.AreEqual(0, exitCode);
        Assert.Contains(expectedWarning, StandardErrorText);
        Assert.IsFalse(files.Written.ContainsKey(SourceFile));
    }

    [TestMethod]
    public async Task RunAsync_FileThatCannotBeOpenedUnderSilent_SaysNothing()
    {
        files.UnwritablePaths.Add(SourceFile);
        Diagnostics.Arrange("unwritable path", SourceFile);

        int exitCode = await RunAsync(["-s", "--libcurl", SourceFile, Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithoutLibcurl_WritesNoSource()
    {
        int exitCode = await RunAsync(["-s", Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "hello", StandardOutputText);
        Diagnostics.Assert("files written", 0, files.Written.Count);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.IsEmpty(files.Written);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithAnUploadFile_WritesItsUrlUploadAndSize()
    {
        files.ExistingContent["up.txt"] = Encoding.ASCII.GetBytes("abcde");
        Diagnostics.Arrange("existing up.txt", "abcde");

        await RunAsync(["-s", "--libcurl", "-", "-T", "up.txt", Url]);

        string expectedUploadLines =
            "  curl_easy_setopt(curl, CURLOPT_URL, \"http://localhost:47652/up.txt\");\n  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n  curl_easy_setopt(curl, CURLOPT_UPLOAD, 1L);\n";
        string expectedSizeLines = "  curl_easy_setopt(curl, CURLOPT_INFILESIZE_LARGE, (curl_off_t)5);\n\n  /* Here is a list";
        Diagnostics.Assert("stdout contains the URL and upload lines", true, StandardOutputText.Contains(expectedUploadLines, StringComparison.Ordinal));
        Diagnostics.Assert("stdout contains the size line", true, StandardOutputText.Contains(expectedSizeLines, StringComparison.Ordinal));
        Assert.Contains(
            expectedUploadLines,
            StandardOutputText);
        Assert.Contains(expectedSizeLines, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithAnUploadFileThatCannotBeOpened_WritesNoSize()
    {
        files.UnreadablePaths.Add("up.txt");
        Diagnostics.Arrange("unreadable path", "up.txt");

        await RunAsync(["-s", "--libcurl", "-", "-T", "up.txt", Url]);

        string expectedUploadLine = "  curl_easy_setopt(curl, CURLOPT_UPLOAD, 1L);\n";
        Diagnostics.Assert("stdout contains the upload line", true, StandardOutputText.Contains(expectedUploadLine, StringComparison.Ordinal));
        Diagnostics.Assert("stdout contains INFILESIZE", false, StandardOutputText.Contains("INFILESIZE", StringComparison.Ordinal));
        Assert.Contains(expectedUploadLine, StandardOutputText);
        Assert.DoesNotContain("INFILESIZE", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlWithEtagCompare_AddsTheFilesHeader()
    {
        files.ExistingContent["etag.txt"] = Encoding.ASCII.GetBytes("\"x\"\r\nsecond\n");
        Diagnostics.Arrange("existing etag.txt", "\"x\"\\r\\nsecond\\n");

        await RunAsync(["-s", "--libcurl", "-", "--etag-compare", "etag.txt", Url]);

        string expectedHeaderLine = "  slist1 = curl_slist_append(slist1, \"If-None-Match: \\\"x\\\"second\");\n";
        Diagnostics.Assert("stdout contains the If-None-Match line", true, StandardOutputText.Contains(expectedHeaderLine, StringComparison.Ordinal));
        Assert.Contains(expectedHeaderLine, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlResumingTheOutputFile_WritesItsSize()
    {
        files.ExistingContent["out.bin"] = Encoding.ASCII.GetBytes("1234567");
        Diagnostics.Arrange("existing out.bin", "1234567");

        await RunAsync(["-s", "--libcurl", "-", "-C", "-", "-o", "out.bin", Url]);

        string expectedResumeLine = "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)7);\n";
        Diagnostics.Assert("stdout contains the resume line", true, StandardOutputText.Contains(expectedResumeLine, StringComparison.Ordinal));
        Assert.Contains(expectedResumeLine, StandardOutputText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static int CountCrLf(string text) => text.Split("\r\n").Length - 1;

    private static string ExpectedSource()
    {
        CommandLineOptions options = CommandLineParser.Parse(["-s", Url]).Options!;
        return LibcurlSourceCode.Generate([(options, Url)]);
    }

    private string WrittenSource() => Encoding.UTF8.GetString(files.Written[SourceFile].ToArray());

    private async Task<int> RunAsync(string[] arguments, bool runsOnWindows = true)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        Diagnostics.Arrange("server reply", "HTTP/1.1 200 OK, Content-Length: 5, body hello");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    options => CreateTransferDispatch(),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: runsOnWindows)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("files written", string.Join(", ", files.Written.Keys));
        return exitCode;
    }

    private static TransferDispatch CreateTransferDispatch() =>
        new(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
            new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver())));
}
