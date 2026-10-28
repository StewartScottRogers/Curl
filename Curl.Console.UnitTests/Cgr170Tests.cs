using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins CGR-170 (BL-3699) in <see cref="CurlCommandRunner" />: under <c>-Z</c> curl 8.22.0 sets up
/// <c>--parallel-max</c> plus one transfers before it performs any, and a setup failure among them (a URL
/// that is not a well-formed glob, a <c>-T</c> file that cannot be opened) abandons that whole first batch:
/// nothing is sent, and each transfer already set up is reported with the failure's code. A URL that is not
/// a well-formed glob past the first batch is taken as written and the run carries on; a later <c>-T</c>
/// file that cannot be opened reports exit 26 for its transfer and starts nothing more. Measured on curl
/// 8.22.0 (BL-3699 Notes).
/// </summary>
[TestClass]
public sealed class Cgr170Tests
{
    private const string BadGlobLines =
        "curl: (3) bad range specification in position 9:\nhttp://[::1\n        ^\n";

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    [TestMethod]
    public async Task RunAsync_BadGlobInTheFirstBatch_ReportsEveryTransferSetUpWithExit3AndSendsNothing()
    {
        int exitCode = await RunAsync(
            ["-Z", "--parallel-immediate", "-sS", "-w", "%{urlnum} %{exitcode} %{url}\\n", "http://h/ok1", "http://h/ok2", "http://[::1"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("0 3 http://h/ok1\n1 3 http://h/ok2\n", StandardOutput);
        Assert.AreEqual(
            BadGlobLines + "curl: (3) URL using bad/illegal format or missing URL\ncurl: (3) URL using bad/illegal format or missing URL\n",
            StandardError);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing()
    {
        fileSystem.UnreadablePaths.Add("nope.txt");

        int exitCode = await RunAsync(
            ["-Z", "--parallel-immediate", "-sS", "-w", "%{urlnum} %{exitcode} %{http_code}\\n", "-T", "u1.txt", "http://h/a", "-T", "nope.txt", "http://h/b"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("0 26 000\n1 26 000\n", StandardOutput);
        Assert.AreEqual(
            "curl: cannot open 'nope.txt'\ncurl: try 'curl --help' or 'curl --manual' for more information\n"
                + "curl: (26) Failed to open/read local data from file/application\n"
                + "curl: (26) Failed to open/read local data from file/application\n",
            StandardError);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_BadGlobInTheFirstBatchOfParallelMax1_AbandonsTheTransferBeforeIt()
    {
        int exitCode = await RunAsync(
            ["-Z", "--parallel-max", "1", "-sS", "-w", "%{urlnum} %{exitcode}\\n", "http://h/ok1", "http://[::1", "http://h/ok2"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("0 3\n", StandardOutput);
        Assert.AreEqual(BadGlobLines + "curl: (3) URL using bad/illegal format or missing URL\n", StandardError);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_BadGlobPastTheFirstBatch_TakesTheUrlAsWrittenAndCarriesOn()
    {
        int exitCode = await RunAsync(
            ["-Z", "--parallel-max", "1", "-sS", "-o", "a", "-o", "b", "-o", "c", "-o", "d",
                "-w", "%{urlnum} %{exitcode} %{url}\\n", "http://h/ok1", "http://h/ok2", "http://[::1", "http://h/ok4"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("0 0 http://h/ok1\n1 0 http://h/ok2\n2 3 http://[::1\n3 0 http://h/ok4\n", StandardOutput);
        Assert.AreEqual(BadGlobLines + "curl: (3) URL rejected: Bad IPv6 address\n", StandardError);
        Assert.HasCount(3, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore()
    {
        http = RecordingProtocolHandler.Failing("http", CurlExitCode.CouldntConnect, "Failed to connect");
        fileSystem.UnreadablePaths.Add("nope.txt");

        int exitCode = await RunAsync(
            ["-Z", "--parallel-max", "1", "-sS", "-w", "%{urlnum} %{exitcode}\\n",
                "-T", "u1.txt", "http://h/1", "-T", "u1.txt", "http://h/2", "-T", "nope.txt", "http://h/3", "-T", "u1.txt", "http://h/4"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("0 7\n1 7\n2 26\n", StandardOutput);
        Assert.HasCount(2, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_UploadRequestMethodConflictPastTheFirstBatch_RunsTheFirstBatchThenRefuses()
    {
        int exitCode = await RunAsync(
            ["-Z", "--parallel-max", "1", "-sS", "-o", "a", "-o", "b", "http://h/ok1", "http://h/ok2", "--next", "-I", "-T", "u1.txt", "http://h/ok3"]);

        Assert.AreEqual(2, exitCode);
        Assert.HasCount(2, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FewerUrlsThanTheFirstBatch_StartsThemAll()
    {
        int exitCode = await RunAsync(["-Z", "-sS", "-o", "a", "-o", "b", "http://h/ok1", "http://h/ok2"]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(2, http.Contexts);
    }

    private string StandardError => Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    private string StandardOutput => Encoding.ASCII.GetString(standardOutput.ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
            _ => new TransferDispatch(new ProtocolDispatcher([http])),
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            outputPaths: fileSystem,
            configFileReader: new InMemoryDataFileReader(),
            readEnvironmentVariable: _ => null).RunAsync(arguments);
}
