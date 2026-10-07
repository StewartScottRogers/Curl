using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the runner runs transfers at once under <c>-Z</c> (ADR-0127): at most <c>--parallel-max</c>
/// running, each transfer run once, its body and then its error line and <c>-w</c> text written in
/// completion order, the exit code the first failure's in completion order, and under
/// <c>--fail-early</c> the running transfers aborted with exit 42 and the queued ones ended with the
/// first failure's code, both reported in command-line order after it. Each case is one of the ADR's
/// rows measured on curl 8.21.0 (Schannel), with <see cref="HeldTransferHandler" /> ending the transfers
/// in the measured order instead of real delays. Each of the ADR's URLs is on a host of its own, so
/// <see cref="ParallelHostQueue" /> holds none back; the BL-520 cases put several on one host.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerParallelTests
{
    private const string A = "http://a.h/a";

    private const string B = "http://b.h/b";

    private const string C = "http://c.h/c";

    private const string D = "http://d.h/d";

    private const string F = "http://f.h/f";

    private const string M = "http://m.h/m";

    private const string R = "http://r.h/r";

    private const string MissingFile = "Could not open file /nonexistent/missing.txt";

    private const string CouldNotConnect = "Failed to connect to 127.0.0.1:1 after 2016 ms: Could not connect to server";

    private static readonly string NewLine = Environment.NewLine;

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly TextWaitingStream standardOutput = new();

    private readonly TextWaitingStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_ParallelMaxTwo_RunsAtMostTwoAtOnceAndEachOnce()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--parallel-max", "2", "-s", A, B, C, D]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b"));
        Diagnostics.Assert("started while two run", 2, http.Started.ToArray().Length);
        Assert.HasCount(2, http.Started);
        http.Finish("/b", "b");
        await http.WhenStartedAsync("/c");
        http.Finish("/a", "a");
        await http.WhenStartedAsync("/d");
        http.Finish("/d", "d");
        http.Finish("/c", "c");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("most running at once", 2, http.MostRunningAtOnce);
        Diagnostics.Assert("started paths (sorted)", "/a,/b,/c,/d", SortedStarted(http));
        Diagnostics.Assert("stdout length", 4, standardOutput.Text.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(2, http.MostRunningAtOnce);
        CollectionAssert.AreEquivalent(new[] { "/a", "/b", "/c", "/d" }, http.Started.ToArray());
        Assert.HasCount(4, standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_NoParallelMax_StartsEveryTransferAtOnce()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-s", A, B, C]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b"), http.WhenStartedAsync("/c"));
        http.Finish("/a", "a");
        http.Finish("/b", "b");
        http.Finish("/c", "c");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("most running at once", 3, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(3, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_NextGroups_RunTheirTransfersAtOnce()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-s", A, "--next", B]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b"));
        http.Finish("/b", "b");
        await standardOutput.WhenEndsWithAsync("b");
        http.Finish("/a", "a");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "ba", Lf(standardOutput.Text));
        Diagnostics.Assert("most running at once", 2, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ba", standardOutput.Text);
        Assert.AreEqual(2, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_WriteOut_FollowsEachBodyInCompletionOrder()
    {
        // ADR-0127 row 3: -Z -s -w '%{urlnum} %{url} %{exitcode}\n' A F C, F ending first.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-s", "-w", "%{urlnum} %{url} %{exitcode}\\n", A, F, C]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/f"), http.WhenStartedAsync("/c"));
        http.Finish("/f", "from-file\n");
        await standardOutput.WhenEndsWithAsync("1 http://f.h/f 0\n");
        http.Finish("/a", "from-conn1\n");
        await standardOutput.WhenEndsWithAsync("0 http://a.h/a 0\n");
        http.Finish("/c", "from-conn2\n");

        int exitCode = await run;
        string expectedStandardOutput = "from-file\n1 http://f.h/f 0\nfrom-conn1\n0 http://a.h/a 0\nfrom-conn2\n2 http://c.h/c 0\n";
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", expectedStandardOutput, Lf(standardOutput.Text));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardOutput, standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_ParallelMaxOne_WritesInCommandLineOrder()
    {
        // ADR-0127 row 4: -Z --parallel-max 1 -s -w '%{urlnum}\n' A F C.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--parallel-max", "1", "-s", "-w", "%{urlnum}\\n", A, F, C]);
        await http.WhenStartedAsync("/a");
        http.Finish("/a", "from-conn1\n");
        await http.WhenStartedAsync("/f");
        http.Finish("/f", "from-file\n");
        await http.WhenStartedAsync("/c");
        http.Finish("/c", "from-conn2\n");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "from-conn1\n0\nfrom-file\n1\nfrom-conn2\n2\n", Lf(standardOutput.Text));
        Diagnostics.Assert("most running at once", 1, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("from-conn1\n0\nfrom-file\n1\nfrom-conn2\n2\n", standardOutput.Text);
        Assert.AreEqual(1, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_FailureEndingFirst_WritesItsLinesFirstAndExitsWithItsCode()
    {
        // ADR-0127 row 5: -Z -sS -w '%{urlnum} %{exitcode}\n' A M C, M failing first.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-sS", "-w", "%{urlnum} %{exitcode}\\n", A, M, C]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/m"), http.WhenStartedAsync("/c"));
        http.Fail("/m", CurlExitCode.FileCouldntReadFile, MissingFile);
        await standardOutput.WhenEndsWithAsync("1 37\n");
        http.Finish("/a", "from-conn1\n");
        await standardOutput.WhenEndsWithAsync("0 0\n");
        http.Finish("/c", "from-conn2\n");

        int exitCode = await run;
        string expectedStandardError = $"curl: (37) {MissingFile}{NewLine}";
        Diagnostics.Assert("exit code", 37, exitCode);
        Diagnostics.Diff("stdout", "1 37\nfrom-conn1\n0 0\nfrom-conn2\n2 0\n", Lf(standardOutput.Text));
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(standardError.Text));
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("1 37\nfrom-conn1\n0 0\nfrom-conn2\n2 0\n", standardOutput.Text);
        Assert.AreEqual(expectedStandardError, standardError.Text);
    }

    [TestMethod]
    public async Task RunAsync_TwoFailures_ExitsWithTheFirstToEnd()
    {
        // ADR-0127 row 7: -Z -sS -f R A, A's 404 ending before R's refused connect; exit 22.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-sS", "-f", R, A]);
        await Task.WhenAll(http.WhenStartedAsync("/r"), http.WhenStartedAsync("/a"));
        http.Fail("/a", CurlExitCode.HttpReturnedError, "The requested URL returned error: 404");
        await standardError.WhenEndsWithAsync("404" + NewLine);
        http.Fail("/r", CurlExitCode.CouldntConnect, CouldNotConnect);

        int exitCode = await run;
        string expectedStandardError = $"curl: (22) The requested URL returned error: 404{NewLine}curl: (7) {CouldNotConnect}{NewLine}";
        Diagnostics.Assert("exit code", 22, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(standardError.Text));
        Assert.AreEqual(22, exitCode);
        Assert.AreEqual(expectedStandardError, standardError.Text);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyWithEveryTransferRunning_AbortsTheOthersAndReportsThemInOrder()
    {
        // ADR-0127 row 11: -Z --fail-early -sS -w '%{urlnum} %{exitcode}\n' A M C.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--fail-early", "-sS", "-w", "%{urlnum} %{exitcode}\\n", A, M, C]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/m"), http.WhenStartedAsync("/c"));
        http.Fail("/m", CurlExitCode.FileCouldntReadFile, MissingFile);

        int exitCode = await run;
        string aborted = "curl: (42) Transfer aborted due to critical error in another transfer" + NewLine;
        string expectedStandardError = $"curl: (37) {MissingFile}{NewLine}{aborted}{aborted}";
        Diagnostics.Assert("exit code", 37, exitCode);
        Diagnostics.Diff("stdout", "1 37\n0 42\n2 42\n", Lf(standardOutput.Text));
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(standardError.Text));
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("1 37\n0 42\n2 42\n", standardOutput.Text);
        Assert.AreEqual(expectedStandardError, standardError.Text);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyWithATransferQueued_ReportsItWithTheFirstFailuresCode()
    {
        // ADR-0127 row 12: -Z --parallel-max 2 --fail-early -sS -w '%{urlnum} %{exitcode}\n' A M C.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--parallel-max", "2", "--fail-early", "-sS", "-w", "%{urlnum} %{exitcode}\\n", A, M, C]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/m"));
        http.Fail("/m", CurlExitCode.FileCouldntReadFile, MissingFile);

        int exitCode = await run;
        string expectedStandardError = $"curl: (37) {MissingFile}{NewLine}"
            + $"curl: (42) Transfer aborted due to critical error in another transfer{NewLine}"
            + $"curl: (37) Could not read a file:// file{NewLine}";
        Diagnostics.Assert("exit code", 37, exitCode);
        Diagnostics.Diff("stdout", "1 37\n0 42\n2 37\n", Lf(standardOutput.Text));
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(standardError.Text));
        Diagnostics.Assert("started paths (sorted)", "/a,/m", SortedStarted(http));
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("1 37\n0 42\n2 37\n", standardOutput.Text);
        Assert.AreEqual(expectedStandardError, standardError.Text);
        CollectionAssert.AreEquivalent(new[] { "/a", "/m" }, http.Started.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyWithParallelMaxOne_ReportsEveryQueuedTransferWithTheFirstFailuresCode()
    {
        // ADR-0127 row 13: -Z --parallel-max 1 --fail-early -sS -w '%{urlnum} %{exitcode}\n' R A C.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--parallel-max", "1", "--fail-early", "-sS", "-w", "%{urlnum} %{exitcode}\\n", R, A, C]);
        await http.WhenStartedAsync("/r");
        http.Fail("/r", CurlExitCode.CouldntConnect, CouldNotConnect);

        int exitCode = await run;
        string skipped = "curl: (7) Could not connect to server" + NewLine;
        string expectedStandardError = $"curl: (7) {CouldNotConnect}{NewLine}{skipped}{skipped}";
        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Diff("stdout", "0 7\n1 7\n2 7\n", Lf(standardOutput.Text));
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(standardError.Text));
        Diagnostics.Assert("started paths", "/r", string.Join(',', http.Started.ToArray()));
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("0 7\n1 7\n2 7\n", standardOutput.Text);
        Assert.AreEqual(expectedStandardError, standardError.Text);
        CollectionAssert.AreEqual(new[] { "/r" }, http.Started.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyAndATransferEndingAfterTheFailure_ReportsItAsAborted()
    {
        HeldTransferHandler http = new(observesCancellation: false);

        Task<int> run = RunAsync(http, ["-Z", "--fail-early", "-sS", "-w", "%{urlnum} %{exitcode}\\n", A, M]);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/m"));
        http.Fail("/m", CurlExitCode.FileCouldntReadFile, MissingFile);
        await standardOutput.WhenEndsWithAsync("1 37\n");
        http.Finish("/a", string.Empty);

        int exitCode = await run;
        Diagnostics.Assert("exit code", 37, exitCode);
        Diagnostics.Diff("stdout", "1 37\n0 42\n", Lf(standardOutput.Text));
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("1 37\n0 42\n", standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_CancelledWithoutFailEarly_Throws()
    {
        RecordingProtocolHandler http = new("http", _ => throw new OperationCanceledException());
        Diagnostics.Arrange("handler", "throws OperationCanceledException on every request");

        OperationCanceledException thrown;
        using (Diagnostics.Phase("run"))
        {
            thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => RunAsync(http, ["-Z", "-s", A]));
        }

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown type", nameof(OperationCanceledException), thrown.GetType().Name);
        Assert.IsNotNull(thrown);
    }

    [TestMethod]
    public async Task RunAsync_BadGlobAfterARunningTransfer_WaitsForItAndExitsWithTheGlobsCode()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-s", A, "http://h/[1-"]);
        await http.WhenStartedAsync("/a");
        http.Finish("/a", "a");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 3, exitCode);
        Diagnostics.Diff("stdout", "a", Lf(standardOutput.Text));
        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("a", standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_RequestMethodsConflictInALaterGroup_WaitsForTheRunningTransferThenRefuses()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-s", A, "--next", "-I", "-d", "x", B]);
        await http.WhenStartedAsync("/a");
        http.Finish("/a", "a");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Diff("stdout", "a", Lf(standardOutput.Text));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("a", standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_UploadFileMissing_StartsNoFurtherTransfer()
    {
        HeldTransferHandler http = new();
        fileSystem.UnreadablePaths.Add("nosuch");
        Diagnostics.Arrange("unreadable path", "nosuch");

        int exitCode = await RunAsync(http, ["-Z", "--parallel-max", "1", "-s", "-T", "nosuch", A, B]);

        Diagnostics.Assert("exit code", 26, exitCode);
        Diagnostics.Assert("started count", 0, http.Started.ToArray().Length);
        Assert.AreEqual(26, exitCode);
        Assert.IsEmpty(http.Started);
    }

    [TestMethod]
    public async Task RunAsync_OneHostWithoutParallelImmediate_StartsTheOthersOnceTheFirstHasEnded()
    {
        // BL-520: -Z -s http://127.0.0.1/a /b /c: /b and /c connect once /a has ended.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "-s", "http://h/1", "http://h/2", "http://h/3"]);
        await http.WhenStartedAsync("/1");
        Diagnostics.Assert("started while the first runs", 1, http.Started.ToArray().Length);
        Assert.HasCount(1, http.Started);
        http.Finish("/1", "1");
        await Task.WhenAll(http.WhenStartedAsync("/2"), http.WhenStartedAsync("/3"));
        http.Finish("/2", "2");
        http.Finish("/3", "3");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("most running at once", 2, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(2, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_OneHostWithParallelImmediate_StartsEveryTransferAtOnce()
    {
        // BL-520: -Z --parallel-immediate -s http://127.0.0.1/a /b /c: three connections at once.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--parallel-immediate", "-s", "http://h/1", "http://h/2", "http://h/3"]);
        await Task.WhenAll(http.WhenStartedAsync("/1"), http.WhenStartedAsync("/2"), http.WhenStartedAsync("/3"));
        http.Finish("/1", "1");
        http.Finish("/2", "2");
        http.Finish("/3", "3");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("most running at once", 3, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(3, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_ParallelMaxHostOne_RunsOneAtATimeToTheHostAndOtherHostsAlongside()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(http, ["-Z", "--parallel-immediate", "--parallel-max-host", "1", "-s", "http://h/1", "http://h/2", "http://o/3"]);
        await Task.WhenAll(http.WhenStartedAsync("/1"), http.WhenStartedAsync("/3"));
        Diagnostics.Assert("started while two run", 2, http.Started.ToArray().Length);
        Assert.HasCount(2, http.Started);
        http.Finish("/1", "1");
        await http.WhenStartedAsync("/2");
        http.Finish("/2", "2");
        http.Finish("/3", "3");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("most running at once", 2, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(2, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_TransferWaitingForItsHost_KeepsItsParallelMaxSlot()
    {
        // BL-520: -Z --parallel-max 2 --parallel-max-host 1 --parallel-immediate A B C, C on another
        // host: C starts only once A has ended, B holding the second slot while it waits.
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(
            http,
            ["-Z", "--parallel-max", "2", "--parallel-max-host", "1", "--parallel-immediate", "-s", "http://h/1", "http://h/2", "http://o/3"]);
        await http.WhenStartedAsync("/1");
        Diagnostics.Assert("started while the first runs", 1, http.Started.ToArray().Length);
        Assert.HasCount(1, http.Started);
        http.Finish("/1", "1");
        await Task.WhenAll(http.WhenStartedAsync("/2"), http.WhenStartedAsync("/3"));
        http.Finish("/2", "2");
        http.Finish("/3", "3");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("most running at once", 2, http.MostRunningAtOnce);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(2, http.MostRunningAtOnce);
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyWithATransferWaitingForItsHost_ReportsItAsAborted()
    {
        HeldTransferHandler http = new();

        Task<int> run = RunAsync(
            http,
            ["-Z", "--fail-early", "-s", "-w", "%{urlnum} %{exitcode}\\n", "http://h/1", "http://h/2", "http://o/3"]);
        await Task.WhenAll(http.WhenStartedAsync("/1"), http.WhenStartedAsync("/3"));
        http.Fail("/3", CurlExitCode.CouldntConnect, CouldNotConnect);

        int exitCode = await run;
        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Diff("stdout", "2 7\n0 42\n1 42\n", Lf(standardOutput.Text));
        Diagnostics.Assert("started paths (sorted)", "/1,/3", SortedStarted(http));
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("2 7\n0 42\n1 42\n", standardOutput.Text);
        CollectionAssert.AreEquivalent(new[] { "/1", "/3" }, http.Started.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_WithoutParallel_RunsOneTransferAtATime()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync(http, ["-s", A, B]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "/a/b", Lf(standardOutput.Text));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/a/b", standardOutput.Text);
    }

    [TestMethod]
    public async Task RunAsync_VerboseWithProgressMeter_WritesAnotherTransfersLineWhileOneReportedDoneIsHeld()
    {
        // BL-773: a transfer reported done is not holding the -v lines of the transfers running beside it.
        HeldTransferHandler http = new(reportsDoneWhileHeld: true);

        Task<int> run = RunAsync(http, ["-Z", "-v", A, B], standardOutput, writesProgressMeter: true);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b"));
        http.Finish("/b", "b");
        await standardOutput.WhenEndsWithAsync("b");
        string errorWhileAIsHeld = standardError.Text;
        http.Finish("/a", "a");

        int exitCode = await run;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr while A is held has Ending /b", true, errorWhileAIsHeld.Contains("* Ending /b\r\n", StringComparison.Ordinal));
        Diagnostics.Assert("stderr while A is held has Ending /a", false, errorWhileAIsHeld.Contains("* Ending /a", StringComparison.Ordinal));
        Diagnostics.Assert("final stderr has Ending /a", true, standardError.Text.Contains("* Ending /a\r\n", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.Contains("* Ending /b\r\n", errorWhileAIsHeld);
        Assert.DoesNotContain("* Ending /a", errorWhileAIsHeld);
        Assert.Contains("* Ending /a\r\n", standardError.Text);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputFailsForOneTransfersWrite_EndsOnlyThatTransferWithExit23()
    {
        // BL-773: A's body write fails; B, running beside it, still writes its body and -w text and succeeds.
        HeldTransferHandler http = new();
        TextRefusingStream refusingStandardOutput = new(standardOutput, "bad");
        Diagnostics.Arrange("refused standard output text", "bad");

        Task<int> run = RunAsync(http, ["-Z", "-w", "%{urlnum} %{exitcode}\\n", A, B], refusingStandardOutput);
        await Task.WhenAll(http.WhenStartedAsync("/a"), http.WhenStartedAsync("/b"));
        http.Finish("/a", "bad");
        await standardError.WhenEndsWithAsync("curl: Failed writing body" + NewLine);
        http.Finish("/b", "good");

        int exitCode = await run;
        string expectedStandardError = "curl: Failed writing body" + NewLine;
        Diagnostics.Assert("exit code", 23, exitCode);
        Diagnostics.Diff("stdout", "good1 0\n", Lf(standardOutput.Text));
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(standardError.Text));
        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("good1 0\n", standardOutput.Text);
        Assert.AreEqual(expectedStandardError, standardError.Text);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string SortedStarted(HeldTransferHandler http) =>
        string.Join(',', http.Started.ToArray().OrderBy(path => path, StringComparer.Ordinal));

    /// <summary>Runs <paramref name="arguments" /> as on Windows through <paramref name="handler" /> alone.</summary>
    private Task<int> RunAsync(IProtocolHandler handler, string[] arguments) =>
        RunAsync(handler, arguments, standardOutput);

    /// <summary>
    /// Runs <paramref name="arguments" /> as on Windows through <paramref name="handler" /> alone, writing
    /// to <paramref name="runStandardOutput" /> and drawing the progress meter when <paramref name="writesProgressMeter" />.
    /// </summary>
    private async Task<int> RunAsync(IProtocolHandler handler, string[] arguments, Stream runStandardOutput, bool writesProgressMeter = false)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    fileSystem,
                    fileSystem,
                    runStandardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    writesProgressMeter: writesProgressMeter)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Lf(standardOutput.Text));
        Diagnostics.Act("stderr", Lf(standardError.Text));
        return exitCode;
    }
}
