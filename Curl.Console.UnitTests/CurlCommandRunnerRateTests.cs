using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--rate</c> spacing serial transfer starts (BL-650), on a clock whose every wait passes at once
/// so no millisecond is real. Measured with the local curl 8.21.0 (Schannel) on 2026-09-29 through
/// <c>Record-CurlExchange.ps1 -Connections 3</c> (BL-650 Notes): <c>--rate 2/s</c> over three URLs ran
/// 1318 ms and <c>-v</c> printed <c>Note: Transfer took 46 ms, waits 454ms as set by --rate</c> before the
/// second and third; <c>--rate 1/3s</c> ran 6124 ms; with <c>-Z</c> 145 ms; a failed transfer is waited
/// after too; and <c>--rate</c> in a later <c>--next</c> group spaces every transfer.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRateTests
{
    private const string Note = "Note: Transfer took 46 ms, waits 454ms as set by --rate";

    private readonly MemoryStream standardError = new();

    private readonly WaitRecordingTimeProvider clock = new();

    private readonly List<long> starts = [];

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_TwoPerSecond_StartsEachTransferHalfASecondAfterTheLast()
    {
        int exitCode = await RunAsync(Taking(46), "-s", "--rate", "2/s", "dict://h/a", "dict://h/b", "dict://h/c");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new long[] { 0, 500, 1000 }, starts);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(454), TimeSpan.FromMilliseconds(454) }, clock.Waits.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_OnePerThreeSeconds_StartsEachTransferThreeSecondsAfterTheLast()
    {
        await RunAsync(Taking(46), "-s", "--rate", "1/3s", "dict://h/a", "dict://h/b", "dict://h/c");

        CollectionAssert.AreEqual(new long[] { 0, 3000, 6000 }, starts);
    }

    [TestMethod]
    public async Task RunAsync_TransferSlowerThanTheRate_StartsTheNextAtOnce()
    {
        await RunAsync(Taking(600), "-s", "--rate", "2/s", "dict://h/a", "dict://h/b");

        CollectionAssert.AreEqual(new long[] { 0, 600 }, starts);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_TransferTakingExactlyTheInterval_StartsTheNextAtOnce()
    {
        await RunAsync(Taking(500), "-s", "--rate", "2/s", "dict://h/a", "dict://h/b");

        CollectionAssert.AreEqual(new long[] { 0, 500 }, starts);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_NoRate_NeverWaits()
    {
        await RunAsync(Taking(46), "-s", "dict://h/a", "dict://h/b");

        CollectionAssert.AreEqual(new long[] { 0, 46 }, starts);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_Verbose_NotesEachWait()
    {
        await RunAsync(Taking(46), "-v", "--rate", "2/s", "dict://h/a", "dict://h/b", "dict://h/c");

        string[] notes = StandardErrorText.Split(Environment.NewLine).Where(line => line.StartsWith("Note: ", StringComparison.Ordinal)).ToArray();
        CollectionAssert.AreEqual(new[] { Note, Note }, notes);
    }

    [TestMethod]
    public async Task RunAsync_SilentVerbose_StillNotesEachWait()
    {
        await RunAsync(Taking(46), "-s", "-v", "--rate", "2/s", "dict://h/a", "dict://h/b");

        StringAssert.Contains(StandardErrorText, Note + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_NotVerbose_WaitsWithoutANote()
    {
        await RunAsync(Taking(46), "--rate", "2/s", "dict://h/a", "dict://h/b");

        Assert.DoesNotContain("Note: ", StandardErrorText);
        Assert.HasCount(1, clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransfer_IsWaitedAfterToo()
    {
        RecordingProtocolHandler failing = new("dict", _ =>
        {
            starts.Add(clock.GetTimestamp());
            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect to server"));
        });

        int exitCode = await RunAsync(failing, "-s", "--rate", "2/s", "dict://h/a", "dict://h/b");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(new long[] { 0, 500 }, starts);
    }

    [TestMethod]
    public async Task RunAsync_RateInALaterGroup_SpacesEveryTransfer()
    {
        await RunAsync(Taking(0), "-s", "dict://h/a", "--next", "--rate", "2/s", "dict://h/b", "dict://h/c");

        CollectionAssert.AreEqual(new long[] { 0, 500, 1000 }, starts);
    }

    [TestMethod]
    public async Task RunAsync_Parallel_IgnoresTheRate()
    {
        await RunAsync(Taking(0), "-s", "-Z", "--rate", "1/3s", "dict://h/a", "dict://h/b", "dict://h/c");

        CollectionAssert.AreEqual(new long[] { 0, 0, 0 }, starts);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_WaitLongerThanOneDelayAllows_WaitsInPieces()
    {
        await RunAsync(Taking(0), "-s", "--rate", "1/25d", "dict://h/a", "dict://h/b");

        CollectionAssert.AreEqual(new long[] { 0, 2_160_000_000 }, starts);
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMilliseconds(int.MaxValue), TimeSpan.FromMilliseconds(2_160_000_000 - int.MaxValue) },
            clock.Waits.ToArray());
    }

    /// <summary>A dict handler that records when each transfer starts and takes <paramref name="milliseconds" />.</summary>
    private RecordingProtocolHandler Taking(long milliseconds) =>
        new("dict", _ =>
        {
            starts.Add(clock.GetTimestamp());
            clock.Advance(milliseconds);
            return ValueTask.FromResult(TransferResult.Success(0));
        });

    private Task<int> RunAsync(RecordingProtocolHandler handler, params string[] arguments)
    {
        InMemoryFileSystem files = new();
        TransferDispatch dispatch = new(new ProtocolDispatcher([handler]));
        return new CurlCommandRunner(
                _ => dispatch,
                files,
                files,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                timeProvider: clock)
            .RunAsync(arguments);
    }

    /// <summary>
    /// A clock in milliseconds whose every timer fires at once, moving the clock on by its due time and
    /// recording it, however long it is.
    /// </summary>
    private sealed class WaitRecordingTimeProvider : TimeProvider
    {
        private readonly List<TimeSpan> waits = [];

        private long elapsedMilliseconds;

        public IReadOnlyList<TimeSpan> Waits => waits;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => elapsedMilliseconds;

        public void Advance(long milliseconds) => elapsedMilliseconds += milliseconds;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            waits.Add(dueTime);
            elapsedMilliseconds += (long)dueTime.TotalMilliseconds;
            callback(state);
            return new InertTimer();
        }

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
