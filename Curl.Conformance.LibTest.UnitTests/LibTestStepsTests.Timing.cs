namespace Curl.Conformance.LibTest.UnitTests;

public sealed partial class LibTestStepsTests
{
    [TestMethod]
    public void AbortOnTestTimeout_AtTheHangTimeout_GoesOn()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);
        clock.StartTestTiming();
        time.Advance(TimeSpan.FromMilliseconds(LibTestClock.TestHangTimeoutMilliseconds));
        var error = new StringWriter();
        var steps = new LibTestSteps(error) { Result = LibTestErrorCodes.Failure };

        bool goesOn = steps.AbortOnTestTimeout(clock);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void AbortOnTestTimeout_PastTheHangTimeout_FailsWithTestErrRunsForever()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);
        clock.StartTestTiming();
        time.Advance(TimeSpan.FromMilliseconds(60001));
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.AbortOnTestTimeout(clock, file: "lib1500.c", line: 60);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.RunsForever, steps.Result);
        Assert.AreEqual(24, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib1500.c:60 ABORTING TEST, since it seems that it would have run forever (60001 ms > 60000 ms)\n", error.ToString());
    }

    [TestMethod]
    public void AbortOnTestTimeout_PastACustomTimeout_StillNamesTheHangTimeout()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);
        clock.StartTestTiming();
        time.Advance(TimeSpan.FromMilliseconds(5001));
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.AbortOnTestTimeout(clock, 5000, "lib1501.c", 70);

        Assert.IsFalse(goesOn);
        Assert.AreEqual("lib1501.c:70 ABORTING TEST, since it seems that it would have run forever (5001 ms > 60000 ms)\n", error.ToString());
    }

    [TestMethod]
    public void AbortOnTestTimeout_RefusesANullClock()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.AbortOnTestTimeout(null!));
    }
}
