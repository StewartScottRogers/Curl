using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest.UnitTests;

public sealed partial class LibTestStepsTests
{
    [TestMethod]
    public async Task MultiInit_OnALiveLibrary_GoesOnWithAHandleAndKeepsResult()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var error = new StringWriter();
        var steps = new LibTestSteps(error) { Result = LibTestErrorCodes.Failure };

        bool goesOn = steps.MultiInit(library, out CurlMulti? multi);

        Assert.IsTrue(goesOn);
        Assert.IsNotNull(multi);
        Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
        await multi.DisposeAsync();
    }

    [TestMethod]
    public async Task MultiInit_OnACleanedUpLibrary_FailsWithTestErrMulti()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await library.DisposeAsync();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiInit(library, out CurlMulti? multi, "lib1500.c", 30);

        Assert.IsFalse(goesOn);
        Assert.IsNull(multi);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual(32, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib1500.c:30 curl_multi_init() failed\n", error.ToString());
    }

    [TestMethod]
    public void MultiInit_RefusesANullLibrary()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiInit(null!, out _));
    }

    [TestMethod]
    public async Task MultiSetOption_ThatSucceeds_GoesOnAndSetsTheOption()
    {
        var engine = new FakeCurlEngine();
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiSetOption(multi, CurlMultiOption.MaxConnects, 3L);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(3L, engine.Multi.Options.MaxConnects);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task MultiSetOption_ThatFails_FailsWithTestErrMultiAndReportsIt()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiSetOption(multi, (CurlMultiOption)99999, 1L, "lib1500.c", 31);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.StartsWith("lib1500.c:31 curl_multi_setopt() failed, with code ", error.ToString());
    }

    [TestMethod]
    public async Task MultiAddHandle_ThatSucceeds_GoesOn()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlMulti multi = library.MultiInit();
        await using CurlEasy easy = library.EasyInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiAddHandle(multi, easy);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task MultiAddHandle_OfNoHandle_FailsWithTestErrMultiAndReportsIt()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiAddHandle(multi, null, "lib1500.c", 32);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual("lib1500.c:32 curl_multi_add_handle() failed, with code 2 (Invalid easy handle)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiRemoveHandle_ThatSucceeds_GoesOn()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlMulti multi = library.MultiInit();
        await using CurlEasy easy = library.EasyInit();
        multi.AddHandle(easy);
        var steps = new LibTestSteps(new StringWriter());

        bool goesOn = steps.MultiRemoveHandle(multi, easy);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task MultiRemoveHandle_ThatFails_FailsWithTestErrMultiAndReportsIt()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.RemoveResult = CurlMultiCode.InternalError;
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        await using CurlEasy easy = library.EasyInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiRemoveHandle(multi, easy, "lib1500.c", 33);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual("lib1500.c:33 curl_multi_remove_handle() failed, with code 4 (Internal error)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiPerformAsync_ThatSucceeds_GoesOnWithTheRunningCount()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 2);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());

        var (goesOn, running) = await steps.MultiPerformAsync(multi, CancellationToken.None);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(2, running);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task MultiPerformAsync_ThatFails_FailsWithTestErrMultiAndReportsIt()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.BadHandle, 0);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        var (goesOn, _) = await steps.MultiPerformAsync(multi, CancellationToken.None, "lib1500.c", 34);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual("lib1500.c:34 curl_multi_perform() failed, with code 1 (Invalid multi handle)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiPerformAsync_WithANegativeRunningCount_FailsWithTestErrNumHandles()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, -1);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        var (goesOn, running) = await steps.MultiPerformAsync(multi, CancellationToken.None, "lib1500.c", 35);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(-1, running);
        Assert.AreEqual(LibTestErrorCodes.NumHandles, steps.Result);
        Assert.AreEqual(34, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib1500.c:35 curl_multi_perform() succeeded, but returned invalid running_handles value (-1)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiFdSet_ThatSucceeds_GoesOnWithTheSocketsAndTheHighest()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSockets = [new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, 0)];
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var read = new HashSet<CurlSocket>();
        var steps = new LibTestSteps(new StringWriter());

        bool goesOn = steps.MultiFdSet(multi, read, null, null, out nint maxSocket);

        Assert.IsTrue(goesOn);
        Assert.AreEqual((nint)7, maxSocket);
        Assert.Contains(new CurlSocket(7), read);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task MultiFdSet_ThatFails_FailsWithTestErrMultiAndReportsIt()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSocketsResult = CurlMultiCode.InternalError;
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiFdSet(multi, null, null, null, out _, "lib1500.c", 36);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual("lib1500.c:36 curl_multi_fdset() failed, with code 4 (Internal error)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiTimeout_ThatSucceeds_GoesOnWithTheTimeout()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, 150);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());

        bool goesOn = steps.MultiTimeout(multi, out long timeout);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(150L, timeout);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task MultiTimeout_ThatFails_FailsWithTestErrBadTimeoutAndReportsIt()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.TimeoutResult = (CurlMultiCode.BadHandle, 0);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiTimeout(multi, out _, "lib1500.c", 37);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.BadTimeout, steps.Result);
        Assert.AreEqual("lib1500.c:37 curl_multi_timeout() failed, with code 1 (Invalid multi handle)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiTimeout_BelowMinusOne_FailsWithTestErrBadTimeout()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, -2);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.MultiTimeout(multi, out long timeout, "lib1500.c", 38);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(-2L, timeout);
        Assert.AreEqual(LibTestErrorCodes.BadTimeout, steps.Result);
        Assert.AreEqual(57, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib1500.c:38 curl_multi_timeout() succeeded, but returned invalid timeout value (-2)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiPollAsync_ThatSucceeds_GoesOnWithTheReadyCount()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PollResult = (CurlMultiCode.Ok, 1);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());

        var (goesOn, ready) = await steps.MultiPollAsync(multi, Memory<CurlWaitSocket>.Empty, 1000, CancellationToken.None);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(1, ready);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task MultiPollAsync_ThatFails_FailsWithTestErrMultiAndReportsIt()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        var (goesOn, _) = await steps.MultiPollAsync(multi, Memory<CurlWaitSocket>.Empty, -1, CancellationToken.None, "lib1500.c", 39);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual("lib1500.c:39 curl_multi_poll() failed, with code 10 (A libcurl function was given a bad argument)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiPollAsync_WithANegativeReadyCount_FailsWithTestErrNumHandles()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PollResult = (CurlMultiCode.Ok, -1);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        var (goesOn, _) = await steps.MultiPollAsync(multi, Memory<CurlWaitSocket>.Empty, 1000, CancellationToken.None, "lib1500.c", 40);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.NumHandles, steps.Result);
        Assert.AreEqual("lib1500.c:40 curl_multi_poll() succeeded, but returned invalid numfds value (-1)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiWaitAsync_ThatSucceeds_GoesOnWithTheReadyCount()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitResult = (CurlMultiCode.Ok, 3);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());

        var (goesOn, ready) = await steps.MultiWaitAsync(multi, Memory<CurlWaitSocket>.Empty, 1000, CancellationToken.None);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(3, ready);
    }

    [TestMethod]
    public async Task MultiWaitAsync_WithANegativeReadyCount_FailsWithTestErrNumHandles()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitResult = (CurlMultiCode.Ok, -2);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        var (goesOn, _) = await steps.MultiWaitAsync(multi, Memory<CurlWaitSocket>.Empty, 1000, CancellationToken.None, "lib1500.c", 41);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(LibTestErrorCodes.NumHandles, steps.Result);
        Assert.AreEqual("lib1500.c:41 curl_multi_wait() succeeded, but returned invalid numfds value (-2)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiWakeup_ThatSucceeds_AnswersTrue()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlMulti multi = library.MultiInit();
        var steps = new LibTestSteps(new StringWriter());

        bool succeeded = steps.MultiWakeup(multi);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task MultiWakeup_ThatFails_FailsWithTestErrMultiAndReportsIt()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WakeupResult = CurlMultiCode.InternalError;
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool succeeded = steps.MultiWakeup(multi, "lib1500.c", 42);

        Assert.IsFalse(succeeded);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
        Assert.AreEqual("lib1500.c:42 curl_multi_wakeup() failed, with code 4 (Internal error)\n", error.ToString());
    }

    [TestMethod]
    public async Task MultiSteps_RefuseANullMultiHandle()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiSetOption(null!, CurlMultiOption.MaxConnects, 1L));
        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiAddHandle(null!, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiRemoveHandle(null!, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiFdSet(null!, null, null, null, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiTimeout(null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => steps.MultiWakeup(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await steps.MultiPerformAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await steps.MultiPollAsync(null!, Memory<CurlWaitSocket>.Empty, 0, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await steps.MultiWaitAsync(null!, Memory<CurlWaitSocket>.Empty, 0, CancellationToken.None));
    }
}
