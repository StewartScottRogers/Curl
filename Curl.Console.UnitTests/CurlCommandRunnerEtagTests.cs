using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--etag-save</c> and <c>--etag-compare</c> through the runner, against curl 8.21.0 measured on
/// Windows on 2026-09-29 with <c>Record-CurlExchange.ps1</c> (BL-619 Notes): what the save file holds after
/// each kind of response, the <c>If-None-Match</c> line the compare file becomes and where it goes, and
/// what a missing compare file and an uncreatable save file print.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerEtagTests
{
    private const string Url = "http://127.0.0.1:18619/x";

    private const string Ok = "HTTP/1.1 200 OK";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_EtagSaveWithAnEtag_SavesTheValueAndALineFeed()
    {
        int exitCode = await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering(Ok, "Content-Length: 2", "ETag: \"abc123\""));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("e.txt", "\"abc123\"\n", WrittenText("e.txt"));
        Diagnostics.Assert("stdout", "hi", StandardOutputText);
        Diagnostics.Assert("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("\"abc123\"\n", WrittenText("e.txt"));
        Assert.AreEqual("hi", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveWithoutAnEtagAndNoFile_CreatesItEmpty()
    {
        int exitCode = await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering(Ok, "Content-Length: 2"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("e.txt", string.Empty, WrittenText("e.txt"));
        Diagnostics.Assert("write modes", nameof(FileWriteMode.Append), string.Join(",", files.WriteModes));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, WrittenText("e.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Append }, files.WriteModes);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK", "")]
    [DataRow("HTTP/1.1 304 Not Modified", "")]
    [DataRow("HTTP/1.1 404 Not Found", "ETag: \"e404\"")]
    [DataRow("HTTP/1.1 500 Oops", "ETag: \"e500\"")]
    [DataRow("HTTP/1.1 400 Bad", "ETag: \"e400\"")]
    [DataRow("HTTP/1.1 200 OK", "ETag:   ")]
    public async Task RunAsync_EtagSaveWithNoEtagToSave_KeepsTheFileAsItWas(string statusLine, string etagLine)
    {
        files.ExistingContent["e.txt"] = Encoding.ASCII.GetBytes("OLD");
        Diagnostics.Arrange("existing e.txt", "OLD");

        int exitCode = await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering(statusLine, etagLine));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("e.txt", "OLD", WrittenText("e.txt"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("OLD", WrittenText("e.txt"));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 201 Created", "\"e201\"")]
    [DataRow("HTTP/1.1 302 Found", "\"e302\"")]
    [DataRow("HTTP/1.1 304 Not Modified", "\"e304\"")]
    [DataRow("HTTP/1.1 399 Odd", "\"e399\"")]
    [DataRow("HTTP/2 200", "W/\"h2\"")]
    public async Task RunAsync_EtagSaveWithAnEtagOfA2xxOr3xxResponse_ReplacesTheFile(string statusLine, string etag)
    {
        files.ExistingContent["e.txt"] = Encoding.ASCII.GetBytes("OLD");
        Diagnostics.Arrange("existing e.txt", "OLD");

        await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering(statusLine, "ETag: " + etag));

        Diagnostics.Diff("e.txt", etag + "\n", WrittenText("e.txt"));
        Assert.AreEqual(etag + "\n", WrittenText("e.txt"));
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveWithAnEtagOfAnInterimResponse_KeepsTheFile()
    {
        files.ExistingContent["e.txt"] = Encoding.ASCII.GetBytes("OLD");
        Diagnostics.Arrange("existing e.txt", "OLD");

        await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering("HTTP/1.1 100 Continue", "ETag: \"c100\"", string.Empty, Ok, "Content-Length: 2"));

        Diagnostics.Assert("e.txt", "OLD", WrittenText("e.txt"));
        Assert.AreEqual("OLD", WrittenText("e.txt"));
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveWithTwoEtagLines_TrimsThemAndKeepsTheLast()
    {
        files.ExistingContent["e.txt"] = Encoding.ASCII.GetBytes("oldoldoldoldold");
        Diagnostics.Arrange("existing e.txt", "oldoldoldoldold");

        await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering(Ok, "etag:  \t W/\"weak1\"  \t", "ETag: \"2\""));

        Diagnostics.Assert("e.txt", "\"2\"\n", WrittenText("e.txt"));
        Diagnostics.Assert(
            "write modes",
            string.Join(",", FileWriteMode.Append, FileWriteMode.Truncate, FileWriteMode.Truncate),
            string.Join(",", files.WriteModes));
        Assert.AreEqual("\"2\"\n", WrittenText("e.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Append, FileWriteMode.Truncate, FileWriteMode.Truncate }, files.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveWithAnEtagOnlyOnTheRedirect_KeepsTheRedirectsEtag()
    {
        await RunAsync(
            ["-s", "-L", "--etag-save", "e.txt", Url],
            Answering("HTTP/1.1 302 Found", "Location: /y", "ETag: \"hop1\"", string.Empty, Ok, "Content-Length: 2"));

        Diagnostics.Assert("e.txt", "\"hop1\"\n", WrittenText("e.txt"));
        Assert.AreEqual("\"hop1\"\n", WrittenText("e.txt"));
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveToStandardOutput_WritesTheEtagBeforeTheBody()
    {
        int exitCode = await RunAsync(["-s", "--etag-save", "-", Url], Answering(Ok, "ETag: \"new1\""));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stdout", "\"new1\"\nhi", StandardOutputText);
        Diagnostics.Assert("files written", 0, files.WriteModes.Count);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("\"new1\"\nhi", StandardOutputText);
        Assert.IsEmpty(files.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveToStandardOutputWithDumpHeaderToStandardOutput_WritesTheHeaderLineFirst()
    {
        await RunAsync(["-s", "-D", "-", "--etag-save", "-", Url], Answering(Ok, "ETag: \"e1\"", "Content-Length: 2"));

        Diagnostics.Diff("stdout", "HTTP/1.1 200 OK\r\nETag: \"e1\"\r\n\"e1\"\nContent-Length: 2\r\n\r\nhi", StandardOutputText);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nETag: \"e1\"\r\n\"e1\"\nContent-Length: 2\r\n\r\nhi", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveToStandardOutputWithShowHeaders_WritesTheEtagBeforeItsHeaderLine()
    {
        await RunAsync(["-s", "-i", "--etag-save", "-", Url], Answering(Ok, "ETag: \"e1\"", "Content-Length: 2"));

        Diagnostics.Diff("stdout", "HTTP/1.1 200 OK\r\n\"e1\"\nETag: \"e1\"\r\nContent-Length: 2\r\n\r\nhi", StandardOutputText);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n\"e1\"\nETag: \"e1\"\r\nContent-Length: 2\r\n\r\nhi", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveWithCreateDirs_CreatesTheFilesDirectory()
    {
        await RunAsync(["-s", "--create-dirs", "--etag-save", "cd/sub/e.txt", Url], Answering(Ok, "ETag: \"cd\""));

        Diagnostics.Assert("created directories", "cd,cd/sub", string.Join(",", files.CreatedDirectories));
        Diagnostics.Assert("cd/sub/e.txt", "\"cd\"\n", WrittenText("cd/sub/e.txt"));
        CollectionAssert.AreEqual(new[] { "cd", "cd/sub" }, files.CreatedDirectories);
        Assert.AreEqual("\"cd\"\n", WrittenText("cd/sub/e.txt"));
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveWithAnUncreatableDirectory_ReportsItAndExits23()
    {
        files.UncreatableDirectories.Add("cd");
        Diagnostics.Arrange("uncreatable directory", "cd");
        RecordingProtocolHandler http = Answering(Ok);

        int exitCode = await RunAsync(["--create-dirs", "--etag-save", "cd/e.txt", Url], http);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Assert(
            "stderr",
            "curl: Error creating directory cd\ncurl: (23) Failed writing received data to disk/application\n",
            StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Diagnostics.Assert("transfers started", 0, http.Contexts.Count);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "curl: Error creating directory cd" + NewLine + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveFileCannotBeCreated_WarnsSkipsTheTransferAndExits26()
    {
        files.UnwritablePaths.Add("nodir/x.txt");
        Diagnostics.Arrange("unwritable path", "nodir/x.txt");
        RecordingProtocolHandler http = Answering(Ok);

        int exitCode = await RunAsync(["-w", "[%{http_code}]", "--etag-save", "nodir/x.txt", Url], http);

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert(
            "stderr",
            "Warning: Failed creating file for saving etags: \"nodir/x.txt\". Skip this \nWarning: transfer\ncurl: no transfer performed\n",
            StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Diagnostics.Assert("stdout", string.Empty, StandardOutputText);
        Diagnostics.Assert("transfers started", 0, http.Contexts.Count);
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual(
            "Warning: Failed creating file for saving etags: \"nodir/x.txt\". Skip this " + NewLine
            + "Warning: transfer" + NewLine
            + "curl: no transfer performed" + NewLine,
            StandardErrorText);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    [DataRow("-s", "")]
    [DataRow("-sS", "curl: no transfer performed")]
    public async Task RunAsync_EtagSaveFileCannotBeCreatedWhileSilent_HidesTheWarning(string silent, string expectedLine)
    {
        files.UnwritablePaths.Add("nodir/x.txt");
        Diagnostics.Arrange("unwritable path", "nodir/x.txt");

        int exitCode = await RunAsync([silent, "--etag-save", "nodir/x.txt", Url], Answering(Ok));

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert(
            "stderr",
            expectedLine.Length == 0 ? string.Empty : expectedLine + "\n",
            StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual(expectedLine.Length == 0 ? string.Empty : expectedLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveFileCannotBeCreatedBeforeAGroupThatRuns_ExitsWithThatGroupsCode()
    {
        files.UnwritablePaths.Add("nodir/x.txt");
        Diagnostics.Arrange("unwritable path", "nodir/x.txt");

        int exitCode = await RunAsync(
            ["-s", "-w", "[%{http_code}]", "--etag-save", "nodir/x.txt", Url, "--next", "-w", "[%{http_code}]", Url],
            Answering(Ok));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stdout", "hi[200]", StandardOutputText);
        Diagnostics.Assert("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hi[200]", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveFileCannotBeCreatedInAParallelRun_ExitsWith26()
    {
        files.UnwritablePaths.Add("nodir/x.txt");
        Diagnostics.Arrange("unwritable path", "nodir/x.txt");
        RecordingProtocolHandler http = Answering(Ok);

        int exitCode = await RunAsync(["-sS", "-Z", "--etag-save", "nodir/x.txt", Url], http);

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert(
            "stderr",
            "curl: no transfer performed\n",
            StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Diagnostics.Assert("transfers started", 0, http.Contexts.Count);
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual("curl: no transfer performed" + NewLine, StandardErrorText);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_EtagSaveFileCannotBeReopenedForAnEtag_FailsTheHeaderWrite()
    {
        files.UntruncatablePaths.Add("e.txt");
        Diagnostics.Arrange("untruncatable path", "e.txt");

        int exitCode = await RunAsync(["-s", "--etag-save", "e.txt", Url], Answering(Ok, "ETag: \"e1\""));

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_EtagCompare_SendsTheFileAsIfNoneMatchAfterEveryOtherHeader()
    {
        files.ExistingContent["c.txt"] = Encoding.ASCII.GetBytes("\"abc123\"\n");
        Diagnostics.Arrange("existing c.txt", "\"abc123\"\n");
        RecordingProtocolHandler http = Answering(Ok);

        int exitCode = await RunAsync(["-s", "-H", "X-A: 1", "--etag-compare", "c.txt", "--json", "{}", "-H", "X-B: 2", Url], http);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "headers sent",
            "X-A: 1|X-B: 2|Content-Type: application/json|Accept: application/json|If-None-Match: \"abc123\"",
            string.Join("|", HeadersSent(http, 0)));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[] { "X-A: 1", "X-B: 2", "Content-Type: application/json", "Accept: application/json", "If-None-Match: \"abc123\"" },
            HeadersSent(http, 0));
    }

    [TestMethod]
    [DataRow("", "If-None-Match: \"\"")]
    [DataRow("\r\n", "If-None-Match: \"\"")]
    [DataRow("\"l1\"\r\n\"l2\"\n  x  \n", "If-None-Match: \"l1\"\"l2\"  x  ")]
    public async Task RunAsync_EtagCompare_DropsEveryLineEnd(string content, string expectedHeader)
    {
        files.ExistingContent["c.txt"] = Encoding.ASCII.GetBytes(content);
        Diagnostics.Arrange("existing c.txt", content);
        RecordingProtocolHandler http = Answering(Ok);

        await RunAsync(["-s", "--etag-compare", "c.txt", Url], http);

        Diagnostics.Assert("headers sent", expectedHeader, string.Join("|", HeadersSent(http, 0)));
        CollectionAssert.AreEqual(new[] { expectedHeader }, HeadersSent(http, 0));
    }

    [TestMethod]
    public async Task RunAsync_EtagCompareFileMissing_WarnsAndSendsAnEmptyEtag()
    {
        files.UnreadablePaths.Add("nothere.txt");
        Diagnostics.Arrange("unreadable path", "nothere.txt");
        RecordingProtocolHandler http = Answering(Ok);

        int exitCode = await RunAsync(["--etag-compare", "nothere.txt", Url], http);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "stderr",
            "Warning: Failed to open nothere.txt: No such file or directory\n",
            StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Diagnostics.Assert("headers sent", "If-None-Match: \"\"", string.Join("|", HeadersSent(http, 0)));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("Warning: Failed to open nothere.txt: No such file or directory" + NewLine, StandardErrorText);
        CollectionAssert.AreEqual(new[] { "If-None-Match: \"\"" }, HeadersSent(http, 0));
    }

    [TestMethod]
    public async Task RunAsync_EtagCompareFileMissingUnderSilent_PrintsNothing()
    {
        files.UnreadablePaths.Add("nothere.txt");
        Diagnostics.Arrange("unreadable path", "nothere.txt");

        await RunAsync(["-s", "--etag-compare", "nothere.txt", Url], Answering(Ok));

        Diagnostics.Assert("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EtagCompareOverAGlob_AddsTheLineAgainForEachTransfer()
    {
        files.ExistingContent["c.txt"] = Encoding.ASCII.GetBytes("\"abc123\"\n");
        Diagnostics.Arrange("existing c.txt", "\"abc123\"\n");
        RecordingProtocolHandler http = Answering(Ok);

        await RunAsync(["-s", "--etag-compare", "c.txt", "http://127.0.0.1:18619/{a,b}"], http);

        Diagnostics.Assert("transfers started", 2, http.Contexts.Count);
        Diagnostics.Assert("headers sent, first transfer", "If-None-Match: \"abc123\"", string.Join("|", HeadersSent(http, 0)));
        Diagnostics.Assert(
            "headers sent, second transfer",
            "If-None-Match: \"abc123\"|If-None-Match: \"abc123\"",
            string.Join("|", HeadersSent(http, 1)));
        Assert.HasCount(2, http.Contexts);
        CollectionAssert.AreEqual(new[] { "If-None-Match: \"abc123\"" }, HeadersSent(http, 0));
        CollectionAssert.AreEqual(new[] { "If-None-Match: \"abc123\"", "If-None-Match: \"abc123\"" }, HeadersSent(http, 1));
    }

    [TestMethod]
    public async Task RunAsync_EtagCompareAndSaveNamingOneFile_SendsTheOldEtagAndSavesTheNewOne()
    {
        files.ExistingContent["both.txt"] = Encoding.ASCII.GetBytes("\"abc123\"\n");
        Diagnostics.Arrange("existing both.txt", "\"abc123\"\n");
        RecordingProtocolHandler http = Answering(Ok, "ETag: \"new1\"");

        await RunAsync(["-s", "--etag-compare", "both.txt", "--etag-save", "both.txt", Url], http);

        Diagnostics.Assert("headers sent", "If-None-Match: \"abc123\"", string.Join("|", HeadersSent(http, 0)));
        Diagnostics.Assert("both.txt", "\"new1\"\n", WrittenText("both.txt"));
        CollectionAssert.AreEqual(new[] { "If-None-Match: \"abc123\"" }, HeadersSent(http, 0));
        Assert.AreEqual("\"new1\"\n", WrittenText("both.txt"));
    }

    [TestMethod]
    public async Task RunAsync_EtagCompareAndSaveNamingOneFileWithA304_KeepsTheFile()
    {
        files.ExistingContent["both.txt"] = Encoding.ASCII.GetBytes("\"abc123\"\n");
        Diagnostics.Arrange("existing both.txt", "\"abc123\"\n");

        await RunAsync(["-s", "--etag-compare", "both.txt", "--etag-save", "both.txt", Url], Answering("HTTP/1.1 304 Not Modified"));

        Diagnostics.Assert("both.txt", "\"abc123\"\n", WrittenText("both.txt"));
        Assert.AreEqual("\"abc123\"\n", WrittenText("both.txt"));
    }

    [TestMethod]
    public async Task RunAsync_EtagCompareToAnOutputFile_SendsTheLine()
    {
        files.ExistingContent["c.txt"] = Encoding.ASCII.GetBytes("\"abc123\"\n");
        Diagnostics.Arrange("existing c.txt", "\"abc123\"\n");
        RecordingProtocolHandler http = Answering(Ok);

        await RunAsync(["-s", "--etag-compare", "c.txt", "-o", "body.txt", Url], http);

        Diagnostics.Assert("headers sent", "If-None-Match: \"abc123\"", string.Join("|", HeadersSent(http, 0)));
        Diagnostics.Assert("body.txt", "hi", WrittenText("body.txt"));
        CollectionAssert.AreEqual(new[] { "If-None-Match: \"abc123\"" }, HeadersSent(http, 0));
        Assert.AreEqual("hi", WrittenText("body.txt"));
    }

    /// <summary>
    /// An <c>http</c> handler that writes <paramref name="headerLines" />, each ended with CR LF and then a blank
    /// line, to the header output when there is one, then <c>hi</c> to the body output; a header write that fails
    /// ends the transfer with exit 23, as the HTTP handler does.
    /// </summary>
    private static RecordingProtocolHandler Answering(params string[] headerLines) =>
        new("http", async context =>
        {
            try
            {
                if (context.HeaderOutput is { } headerOutput)
                {
                    foreach (string line in headerLines)
                    {
                        await headerOutput.WriteAsync(Encoding.ASCII.GetBytes(line + "\r\n"), context.CancellationToken);
                    }

                    await headerOutput.WriteAsync("\r\n"u8.ToArray(), context.CancellationToken);
                }
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, "Failed writing received data to disk/application");
            }

            await context.Output.WriteAsync("hi"u8.ToArray(), context.CancellationToken);
            return TransferResult.Success(2) with { Report = new TransferReport { ResponseCode = 200 } };
        });

    private static string[] HeadersSent(RecordingProtocolHandler http, int transfer) =>
        [.. http.Contexts[transfer].Http!.Headers];

    private string WrittenText(string path) => Encoding.ASCII.GetString(files.Written[path].ToArray());

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([handler])),
            files,
            files,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            outputPaths: files);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }
}
