using Curl.Conformance.Differential.Libcurl.Multi;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Multi;

[TestClass]
public sealed class LibcurlMultiReplayerTests
{
    private readonly RecordingMultiTarget _target = new();

    private LibcurlMultiReplayer Replayer => new(_target, _target.Clock);

    private static IReadOnlyList<LibcurlMultiStep> Steps(string json) => LibcurlMultiCaseReader.ReadShare(json);

    [TestMethod]
    public async Task ReplayMultiAsync_HandleSteps_WritesTheTestersTokens()
    {
        _target.Running.Enqueue(1);
        var line = await Replayer.ReplayMultiAsync(4, Steps("""
            [["easy", [[10002, "s", "http://x/"]]], ["easy", []], ["add", 0], ["add_null"], ["remove", 1], ["remove_null"],
             ["setopt", 7, "l", 1], ["setopt", 99999, "l", 1], ["perform"], ["run"], ["info"], ["handles"], ["unknown"]]
            """));
        Assert.AreEqual("4 add=0 addnull=2 rm=0 rmnull=2 set7=0 set99999=6 perf=0/1 run=0/0 info=[1:0:7:q1,1:1:0:q0] nh=3", line);
    }

    [TestMethod]
    public async Task ReplayMultiAsync_CleanedUpEasy_IsWrittenAsFreedWithoutACall()
    {
        var line = await Replayer.ReplayMultiAsync(0, Steps("""
            [["easy", []], ["easy", []], ["cleanup_easy", 0], ["cleanup_easy", 0], ["add", 0], ["remove", 0], ["bodies"], ["cleanup_multi"]]
            """));
        Assert.AreEqual("0 add=freed rm=freed b0=freed b1=6F6B/200 cleaned", line);
        CollectionAssert.AreEqual(new[] { "easy0", "easy0", "cleanup0", "cleanup_multi" }, _target.Calls);
    }

    [TestMethod]
    public async Task ReplayMultiAsync_TimedSteps_WriteTheirElapsedBuckets()
    {
        _target.DefaultRunning = 1;
        var line = await Replayer.ReplayMultiAsync(1, Steps("""[["pollms", 600], ["waitms", 400], ["runfor", 1000]]"""));
        Assert.AreEqual("1 poll=0/0/3 wait=0/1/2 runfor=1/4/ok", line);
    }

    [TestMethod]
    public async Task ReplayMultiAsync_RunforThatFinishes_StopsEarly()
    {
        var line = await Replayer.ReplayMultiAsync(1, Steps("""[["runfor", 1000]]"""));
        Assert.AreEqual("1 runfor=0/0/ok", line);
    }

    [TestMethod]
    public async Task ReplayMultiAsync_RunforPerformingOverTwoHundredTimes_WritesSpin()
    {
        _target.DefaultRunning = 1;
        var line = await Replayer.ReplayMultiAsync(1, Steps("""[["runfor", 30000]]"""));
        Assert.AreEqual("1 runfor=1/120/SPIN", line);
    }

    [TestMethod]
    public async Task ReplayMultiAsync_RunThatNeverFinishes_StopsAfterTheSpinLimit()
    {
        _target.DefaultRunning = 1;
        Assert.AreEqual("2 run=0/1", await Replayer.ReplayMultiAsync(2, Steps("""[["run"]]""")));
    }

    [TestMethod]
    public async Task ReplayMultiAsync_StepThatThrows_EndsTheLineWithExc()
    {
        Assert.AreEqual("3 EXC NullReferenceException", await Replayer.ReplayMultiAsync(3, [new("setopt", [], 0, null)]));
    }

    [TestMethod]
    public async Task ReplayShareAsync_ShareSteps_WriteTheTestersTokens()
    {
        _target.SetCodes = [0, 48];
        var line = await Replayer.ReplayShareAsync(Steps("""
            [["sset", 1, 2], ["ssetnull", 5], ["easy", [[10002, "s", "u"], [99, "l", 1]]], ["perform", 0], ["cleanup_easy", 0], ["perform", 0], ["cleanup_share"]]
            """));
        Assert.AreEqual("sset1=0 ssetnull5=3 eset99=48 perf0=0/200/nc1 ce perf0=freed cs=0", line);
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibcurlMultiReplayer(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibcurlMultiReplayer(_target, null!));
    }

    [TestMethod]
    public async Task ReplayShareAsync_NullSteps_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Replayer.ReplayShareAsync(null!));
}
