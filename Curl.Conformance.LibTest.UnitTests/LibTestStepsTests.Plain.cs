using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

public sealed partial class LibTestStepsTests
{
    [TestMethod]
    public async Task GlobalInitOrMajorBad_WithAndWithoutAnEngine_GoesOnOrFailsMajorBad()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool withEngine = steps.GlobalInitOrMajorBad(new FakeCurlEngine(), CurlGlobalFlags.All, out CurlLibrary? library);
        bool withoutEngine = steps.GlobalInitOrMajorBad(null, CurlGlobalFlags.All, out CurlLibrary? none);

        Assert.IsTrue(withEngine);
        Assert.IsFalse(withoutEngine);
        Assert.IsNull(none);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, steps.Result);
        Assert.AreEqual("curl_global_init() failed\n", error.ToString());
        await library!.DisposeAsync();
    }

    [TestMethod]
    public async Task EasyInitOrMajorBad_OnLiveAndCleanedUpLibraries_GoesOnOrFailsMajorBad()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);
        await using CurlLibrary live = CurlGlobal.Init(new FakeCurlEngine());

        bool onLive = steps.EasyInitOrMajorBad(live, out CurlEasy? easy);
        bool onCleaned = steps.EasyInitOrMajorBad(await PortRun.CleanedUpLibraryAsync(), out CurlEasy? none);

        Assert.IsTrue(onLive);
        Assert.IsFalse(onCleaned);
        Assert.IsNull(none);
        Assert.AreEqual("curl_easy_init() failed\n", error.ToString());
        await easy!.DisposeAsync();
    }

    [TestMethod]
    public async Task MultiInitOrMajorBad_OnLiveAndCleanedUpLibraries_GoesOnOrFailsMajorBad()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);
        await using CurlLibrary live = CurlGlobal.Init(new FakeCurlEngine());

        bool onLive = steps.MultiInitOrMajorBad(live, out var multi);
        bool onCleaned = steps.MultiInitOrMajorBad(await PortRun.CleanedUpLibraryAsync(), out var none);

        Assert.IsTrue(onLive);
        Assert.IsFalse(onCleaned);
        Assert.IsNull(none);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, steps.Result);
        Assert.AreEqual("curl_multi_init() failed\n", error.ToString());
        await multi!.DisposeAsync();
    }

    [TestMethod]
    public async Task EasySetOptions_StopsAtTheFirstRefusedOption()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();

        bool allSet = steps.EasySetOptions(easy, [(CurlOption.Verbose, 1L), (CurlOption.Header, 1L)]);
        bool oneRefused = steps.EasySetOptions(easy, [(UnknownOption, 1L), (CurlOption.Header, 1L)], "lib500.c", 7);

        Assert.IsTrue(allSet);
        Assert.IsFalse(oneRefused);
        Assert.AreEqual(CurlCode.UnknownOption, steps.Result);
        Assert.StartsWith("lib500.c:7 curl_easy_setopt() failed", error.ToString());
    }

    [TestMethod]
    public async Task SetOptionsAndPerformAsync_PerformsOnlyWhenEveryOptionIsSet()
    {
        var steps = new LibTestSteps(new StringWriter());
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect });
        await using CurlEasy easy = library.EasyInit();

        bool refused = await steps.SetOptionsAndPerformAsync(easy, [(UnknownOption, 1L)], CancellationToken.None);
        CurlCode afterRefusal = steps.Result;
        bool performed = await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Verbose, 1L)], CancellationToken.None);

        Assert.IsFalse(refused);
        Assert.AreEqual(CurlCode.UnknownOption, afterRefusal);
        Assert.IsFalse(performed);
        Assert.AreEqual(CurlCode.CouldntConnect, steps.Result);
    }

    [TestMethod]
    public async Task SetOptionsAndAddHandle_AddsOnlyWhenEveryOptionIsSet()
    {
        var steps = new LibTestSteps(new StringWriter());
        var engine = new FakeCurlEngine();
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit();
        await using CurlMulti multi = library.MultiInit();

        bool refused = steps.SetOptionsAndAddHandle(easy, [(UnknownOption, 1L)], multi);
        bool added = steps.SetOptionsAndAddHandle(easy, [(CurlOption.Verbose, 1L)], multi);

        Assert.IsFalse(refused);
        Assert.IsTrue(added);
        Assert.ThrowsExactly<ArgumentNullException>(() => steps.SetOptionsAndAddHandle(easy, [], null!));
    }

    [TestMethod]
    public async Task SelectTestAsync_FdSetFailure_WaitFailure_AndSuccess()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);
        var engine = new FakeCurlEngine();
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();

        bool waited = await steps.SelectTestAsync(multi, 100, CancellationToken.None);
        engine.Multi.WaitResult = (CurlMultiCode.InternalError, 0);
        bool waitFailed = await steps.SelectTestAsync(multi, 100, CancellationToken.None, "lib502.c", 9);
        CurlCode afterWait = steps.Result;
        engine.Multi.WaitSocketsResult = CurlMultiCode.InternalError;
        bool fdSetFailed = await steps.SelectTestAsync(multi, 100, CancellationToken.None);

        Assert.IsTrue(waited);
        Assert.IsFalse(waitFailed);
        Assert.AreEqual(LibTestErrorCodes.Select, afterWait);
        Assert.Contains("lib502.c:9 select() failed, with code 4", error.ToString());
        Assert.IsFalse(fdSetFailed);
        Assert.AreEqual(LibTestErrorCodes.Multi, steps.Result);
    }

    [TestMethod]
    public async Task PerformUntilDoneAsync_LoopsUntilNothingRuns()
    {
        var steps = new LibTestSteps(new StringWriter());
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();
        var clock = new LibTestClock(new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        bool done = await steps.PerformUntilDoneAsync(multi, clock, 100, CancellationToken.None);

        Assert.IsTrue(done);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task PerformUntilDoneAsync_StopsOnAFailedPerformSelectOrTimeout()
    {
        var steps = new LibTestSteps(new StringWriter());
        var engine = new FakeCurlEngine();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new LibTestClock(time);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlMulti multi = library.MultiInit();

        engine.Multi.PerformResults.Enqueue((CurlMultiCode.InternalError, 0));
        bool performFailed = await steps.PerformUntilDoneAsync(multi, clock, 100, CancellationToken.None);
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitResult = (CurlMultiCode.InternalError, 0);
        bool selectFailed = await steps.PerformUntilDoneAsync(multi, clock, 100, CancellationToken.None);
        engine.Multi.WaitResult = (CurlMultiCode.Ok, 0);
        engine.Multi.Waiting = () => time.Advance(TimeSpan.FromMinutes(2));
        bool timedOut = await steps.PerformUntilDoneAsync(multi, clock, 100, CancellationToken.None);
        CurlCode afterTimeout = steps.Result;
        (bool goesOn, int running) = await steps.PerformAndCheckTimeoutAsync(multi, clock, CancellationToken.None);

        Assert.IsFalse(performFailed);
        Assert.IsFalse(selectFailed);
        Assert.IsFalse(timedOut);
        Assert.AreEqual(LibTestErrorCodes.RunsForever, afterTimeout);
        Assert.IsFalse(goesOn);
        Assert.AreEqual(1, running);
    }
}
