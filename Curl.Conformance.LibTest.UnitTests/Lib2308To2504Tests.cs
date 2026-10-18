using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Libcurl.Global;
using Libcurl.Misc.Version;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib2308To2504Tests
{
    private static readonly CurlWaitSocket[] TwoSockets =
    [
        new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, 0),
        new CurlWaitSocket(new CurlSocket(8), CurlWaitEvents.PollIn, 0),
    ];

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib2308(), new Lib2309(), new Lib2397(), new Lib2402(TimeProvider.System), new Lib2404(TimeProvider.System),
            new Lib2405(), new Lib2412(TimeProvider.System), new Lib2414(), new Lib2502(TimeProvider.System), new Lib2504(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib2308", "lib2309", "lib2397", "lib2402", "lib2404", "lib2405", "lib2412", "lib2414", "lib2502", "lib2504" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public void Constructors_WithoutATimeProvider_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2402(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2412(null!));
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 2405, "http://x/");

        await Lib2308.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib2405.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await new Lib2402(TimeProvider.System).TestAsync(run.Context, run.Steps, library, new LibTestClock(TimeProvider.System), null, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib2309.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib2397.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2504.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2412.TestAsync(run.Context, run.Steps, library, TimeProvider.System);
        await Lib2414.TestAsync(run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);

        StringAssert.Contains(run.Error.ToString(), "curl_multi_init() failed\n");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib2308_WritesTheWriteErrorAndSucceeds()
    {
        long written = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { written = o.Callbacks.Write!("x"u8); return CurlCode.WriteError; } }, 2308, "http://x/2308");

        LibTestResult result = await new Lib2308().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(WriteCallbackResult.Error, written);
        Assert.AreEqual("Returned 23, should be 23.\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib2309_PerformsADuplicateWithTheNetrcAndProxy()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 2309, "http://github.com", "netrc2309", "http://127.0.0.1:8990/");

        LibTestResult result = await new Lib2309().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Returned 0, should be 23.\n", run.OutputText);
        Assert.AreEqual("http://127.0.0.1:8990/", seen!.Proxy.Proxy);
        Assert.AreEqual(NetrcUsage.Required, seen.Authentication.Netrc);
        Assert.AreEqual("netrc2309", seen.Authentication.NetrcFile);
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    public async Task Lib2397_SetsTheRefererItReadsBack()
    {
        var seen = new List<EasyOptions>();
        var run = new PortRun(new FakeCurlEngine { Info = new TransferInfo { Referer = "http://x/2397" }, Performing = o => { seen.Add(o); return CurlCode.Ok; } }, 2397, "http://x/2397");

        LibTestResult result = await new Lib2397().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(seen[0].Http.AutoReferer);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(CurlCode.Ok, "CURLINFO_REFERER was not populated\n")]
    [DataRow(CurlCode.CouldntConnect, "transfer failed: Could not connect to server\nCURLINFO_REFERER was not populated\n")]
    public async Task Lib2397_WithoutAReferer_SaysSo(CurlCode code, string error)
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = code }, 2397, "http://x/2397");

        LibTestResult result = await new Lib2397().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)code, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2504_SendsTheHostHeaderOnlyFirstAndWritesTheCookies()
    {
        var seen = new List<EasyOptions>();
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Informing = () => new TransferInfo { CookieList = performs == 1 ? ["a"] : ["a", "b"] },
            Performing = o => { seen.Add(o); return ++performs == 1 ? CurlCode.Ok : CurlCode.RecvError; },
        };
        var run = new PortRun(engine, 2504, "http://x/2504");

        LibTestResult result = await new Lib2504().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual("req1=0\n== after request 1 ==\na\nreq2=56\n== after request 2 ==\na\nb\n", run.OutputText);
        CollectionAssert.AreEqual(new[] { Lib2504.HostHeader }, seen[0].Http.HttpHeaders.ToArray());
        Assert.IsEmpty(seen[1].Http.HttpHeaders);
    }

    [TestMethod]
    public async Task LibPipeWaitHandles_TransferEachHandleInTurnWithTheirOptions()
    {
        foreach (var (port, version, weight) in new (LibPipeWaitHandles, HttpVersion, int)[]
        {
            (new Lib2402(TimeProvider.System), HttpVersion.Http2, 16),
            (new Lib2404(TimeProvider.System), HttpVersion.Http2, 131),
            (new Lib2502(TimeProvider.System), HttpVersion.Http3Only, 16),
        })
        {
            var engine = new FakeCurlEngine();
            engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
            var run = new PortRun(engine, 2402, "-", "127.0.0.1", "9000", "ca.pem");

            LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("localhost:9000:127.0.0.1\n", run.OutputText);
            Assert.AreEqual("Start at URL 0\n", run.Error.ToString());
            EasyOptions last = engine.Multi.Added!.Options;
            Assert.AreEqual($"https://localhost:9000/path/{port.ProgramName[3..]}0004", last.Network.Url);
            Assert.AreEqual(version, last.Http.HttpVersion);
            Assert.AreEqual(weight, last.Http.StreamWeight);
            Assert.IsTrue(last.Http.PipeWait);
            CollectionAssert.AreEqual(new[] { "localhost:9000:127.0.0.1" }, last.Network.Resolve.ToArray());
        }
    }

    [TestMethod]
    public async Task LibPipeWaitHandles_WhenAStepFails_StopsThere()
    {
        var addFails = new FakeCurlEngine();
        addFails.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var performFails = new FakeCurlEngine();
        performFails.Multi.PerformResult = (CurlMultiCode.BadHandle, 0);
        foreach (FakeCurlEngine engine in new[] { addFails, performFails })
        {
            var run = new PortRun(engine, 2402, "-", "127.0.0.1", "9000");

            LibTestResult result = await new Lib2402(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        }
    }

    [TestMethod]
    public async Task LibPipeWaitHandles_WhenTheSecondHandleCannotBeMade_StopsBeforeAnyTransfer()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 2), 2402, "-", "127.0.0.1", "9000");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await new Lib2402(TimeProvider.System).TestAsync(run.Context, run.Steps, holder.Library, new LibTestClock(TimeProvider.System), null, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.DoesNotContain("Start at URL 0", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2405_Case2405_HoldsTwoDescriptorsOverHttp1()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.Performing = () => engine.Multi.WaitSockets = TwoSockets;
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 2));
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, run.Error.ToString());
        Assert.AreEqual(HttpVersion.Http11, engine.Multi.Added!.Options.Http.HttpVersion);
    }

    [TestMethod]
    public async Task Lib2405_Case2407_RunsHttp2ThenMultiplexed()
    {
        var engine = new FakeCurlEngine();
        int performs = 0;
        engine.Multi.Performing = () => engine.Multi.WaitSockets = ++performs == 1 ? TwoSockets : TwoSockets[..1];
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 2));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 0));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 2407, "http://x/2407");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, run.Error.ToString());
        Assert.IsTrue(engine.Multi.Added!.Options.Http.PipeWait);
    }

    [TestMethod]
    public async Task Lib2405_WhenTheCountIsNotTheExpectedOne_Fails()
    {
        var run = new PortRun(new FakeCurlEngine(), 2407, "http://x/2407");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual("Max number of waitfds: 0 not as expected: 2\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2405_WhenATransferFails_WritesItsCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.DoneResult = CurlCode.RecvError;
        engine.Multi.DoneMessageType = CurlMultiMessageType.Done;
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual("test failed with code: 56\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2405_IgnoresAMessageThatIsNotDone()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.DoneResult = CurlCode.RecvError;
        engine.Multi.DoneMessageType = CurlMultiMessageType.None;
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
    }

    [TestMethod]
    [DataRow(0, "curl_multi_waitfds() failed, code 1.\n")]
    [DataRow(2, "curl_multi_waitfds(), empty, returned non-zero count of waitfds: 2.\n")]
    public async Task Lib2405_WhenTheEmptyHandleCheckFails_StopsThere(int sockets, string error)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSockets = TwoSockets[..sockets];
        engine.Multi.WaitSocketsResult = sockets == 0 ? CurlMultiCode.BadHandle : CurlMultiCode.Ok;
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2405_WhenTheAddedHandleGivesTwoDescriptors_Fails()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.Adding = () => engine.Multi.WaitSockets = TwoSockets;
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual("curl_multi_waitfds() returned > 1 count of waitfds: 2.\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2405_WhenTheAddedHandlesWaitFdsFails_Fails()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.Adding = () => engine.Multi.WaitSocketsResult = CurlMultiCode.BadHandle;
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual("curl_multi_waitfds() failed, code 1.\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2405_WhenTheEmptyTestsHandleCannotBeMade_Stops()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        var run = new PortRun(engine, 2405, "http://x/2405");
        holder.Library = CurlGlobal.Init(engine);

        await Lib2405.TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib2405_WhenTheEmptyHandleCannotBeAddedTo_Stops()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 2405, "http://x/2405");

        LibTestResult result = await new Lib2405().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Lib2405Run_WhenAHandleCannotBeMade_ReturnsNone(int handlesMade)
    {
        var holder = new PortRun.LibraryHolder();
        int created = 0;
        var engine = new FakeCurlEngine
        {
            CreatingEasy = () => PortRun.CleanUpWhen(holder, ++created == handlesMade),
            CreatingMulti = () => PortRun.CleanUpWhen(holder, ++created == handlesMade),
        };
        var run = new PortRun(engine, 2405, "http://x/2405");
        holder.Library = CurlGlobal.Init(engine);
        if (handlesMade == 0)
        {
            await holder.Library.DisposeAsync();
        }

        int most = await Lib2405Run.RunAsync(run.Context, run.Steps, holder.Library, Lib2405.UseHttp1, CancellationToken.None);

        Assert.AreEqual(0, most);
        Assert.AreNotEqual(CurlCode.Ok, run.Steps.Result);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Lib2405Run_WhenAnAddFails_ReturnsNone(bool firstFails)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResults.Enqueue(firstFails ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok);
        engine.Multi.AddResults.Enqueue(CurlMultiCode.BadEasyHandle);
        await using CurlLibrary library = CurlGlobal.Init(engine);
        var run = new PortRun(engine, 2405, "http://x/2405");

        int most = await Lib2405Run.RunAsync(run.Context, run.Steps, library, Lib2405.UseHttp1, CancellationToken.None);

        Assert.AreEqual(0, most);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib2405Run_LoopAsync_GoesRoundWhileNoDescriptorIsGiven()
    {
        var engine = new FakeCurlEngine();
        int performs = 0;
        engine.Multi.Performing = () => engine.Multi.WaitSockets = ++performs < 2 ? [] : TwoSockets;
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using var multi = library.MultiInit();
        await using var poll = library.MultiInit();
        var steps = new LibTestSteps(TextWriter.Null);

        int most = await Lib2405Run.LoopAsync(TextWriter.Null, steps, multi, poll, CancellationToken.None);

        Assert.AreEqual(2, most);
        Assert.AreEqual(3, performs);
    }

    [TestMethod]
    public async Task Lib2405Run_LoopAsync_WhenAWaitOrPollFails_Fails()
    {
        foreach (var (fault, message) in new (Action<FakeMultiTransfer>, string)[]
        {
            (multi => multi.WaitSocketsResult = CurlMultiCode.BadHandle, "curl_multi_waitfds() failed, code 1.\n"),
            (multi => multi.PollResult = (CurlMultiCode.BadHandle, 0), "curl_multi_poll() failed, code 1.\n"),
            (multi => multi.WaitSockets = [.. TwoSockets, .. TwoSockets, .. TwoSockets, .. TwoSockets, .. TwoSockets, .. TwoSockets], "curl_multi_waitfds() failed, code 3.\n"),
        })
        {
            var engine = new FakeCurlEngine();
            engine.Multi.WaitSockets = TwoSockets;
            engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
            fault(engine.Multi);
            await using CurlLibrary library = CurlGlobal.Init(engine);
            await using var multi = library.MultiInit();
            var steps = new LibTestSteps(TextWriter.Null);
            var error = new StringWriter();

            _ = await Lib2405Run.LoopAsync(error, steps, multi, multi, CancellationToken.None);

            Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
            Assert.AreEqual(message, error.ToString());
        }
    }

    [TestMethod]
    public async Task Lib2405Run_LoopAsync_WhenTheCountChecksFail_Stops()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSockets = TwoSockets;
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        int asks = 0;
        engine.Multi.WaitSocketsAnswering = () => ++asks == 2 ? CurlMultiCode.BadHandle : null;
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using var multi = library.MultiInit();
        var steps = new LibTestSteps(TextWriter.Null);

        int most = await Lib2405Run.LoopAsync(TextWriter.Null, steps, multi, multi, CancellationToken.None);

        Assert.AreEqual(0, most);
        Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
    }

    [TestMethod]
    [DataRow(1, CurlMultiCode.BadHandle, 2, "curl_multi_waitfds() failed, code 1.\n")]
    [DataRow(1, CurlMultiCode.Ok, 1, "curl_multi_waitfds() should return at least the number of fds needed (1 vs. 2)\n")]
    [DataRow(2, CurlMultiCode.Ok, 2, "curl_multi_waitfds() return code 0 instead of CURLM_OUT_OF_MEMORY.\n")]
    [DataRow(2, CurlMultiCode.OutOfMemory, 1, "curl_multi_waitfds() should return the amount of fds needed if enough is not passed in (1 vs. 2).\n")]
    public async Task Lib2405_CountChecksPass_WhenACallAnswersWrong_Fails(int failingAsk, CurlMultiCode code, int count, string message)
    {
        int asks = 0;
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSockets = TwoSockets;
        engine.Multi.WaitSocketsAnswering = () =>
        {
            if (++asks != failingAsk)
            {
                return null;
            }

            engine.Multi.WaitSockets = TwoSockets[..count];
            return code;
        };
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using var multi = library.MultiInit();
        var steps = new LibTestSteps(TextWriter.Null);
        var error = new StringWriter();

        bool passed = Lib2405.CountChecksPass(error, steps, multi, 2);

        Assert.IsFalse(passed);
        Assert.AreEqual(message, error.ToString());
        Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
    }

    [TestMethod]
    public async Task Lib2405_CountChecksPass_WithOneDescriptor_TakesTheEmptySpanAsTooShort()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSockets = TwoSockets[..1];
        await using CurlLibrary library = CurlGlobal.Init(engine);
        await using var multi = library.MultiInit();

        bool passed = Lib2405.CountChecksPass(TextWriter.Null, new LibTestSteps(TextWriter.Null), multi, 1);

        Assert.IsTrue(passed);
    }

    [TestMethod]
    [DataRow(new[] { "asyn-rr" }, CurlVersionFeatures.None, null, true)]
    [DataRow(new string[0], CurlVersionFeatures.AsynchDns, null, true)]
    [DataRow(new string[0], CurlVersionFeatures.AsynchDns, "1.34.5", false)]
    [DataRow(new string[0], CurlVersionFeatures.None, null, false)]
    public void Lib2405_UsesThreadedResolver_ReadsTheFeatures(string[] names, CurlVersionFeatures features, string? ares, bool expected)
    {
        var version = new CurlVersionInfoData { Version = "8.22.0", VersionNumber = 0, Host = "x", Features = features, Protocols = [], FeatureNames = names, Ares = ares };

        Assert.AreEqual(expected, Lib2405.UsesThreadedResolver(version));
        Assert.AreEqual(expected ? 1 : 0, Lib2405.WakeupDescriptorCount(version));
    }

    [TestMethod]
    public async Task Lib2412_WithNoDescriptor_SaysSo()
    {
        var run = new PortRun(new FakeCurlEngine(), 2412, "http://x/2412");

        LibTestResult result = await new Lib2412(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("There are no file descriptors to wait for\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2412_WithADescriptor_Fails()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitSockets = TwoSockets[..1];
        var run = new PortRun(engine, 2412, "http://x/2412");

        LibTestResult result = await new Lib2412(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual("libcurl supplied a file descriptor to wait for (maxFd=7).  Waiting now ...\n", run.Error.ToString());
    }

    [TestMethod]
    [DataRow(true, "curl_multi_add_handle() failed: 2\n")]
    [DataRow(false, "curl_multi_fdset() failed: 1\n")]
    public async Task Lib2412_WhenAMultiCallFails_EndsMajorBad(bool addFails, string error)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = addFails ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.WaitSocketsResult = CurlMultiCode.BadHandle;
        var run = new PortRun(engine, 2412, "http://x/2412");

        LibTestResult result = await new Lib2412(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2412_WhenTheEasyHandleCannotBeMade_EndsMajorBad()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        var run = new PortRun(engine, 2412);
        holder.Library = CurlGlobal.Init(engine);

        await Lib2412.TestAsync(run.Context, run.Steps, holder.Library, TimeProvider.System);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual("curl_easy_init() failed\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib2414_WakesPerformsAndPolls()
    {
        var engine = new FakeCurlEngine();
        bool polled = false;
        engine.Multi.Polling = () => polled = true;
        var run = new PortRun(engine, 2414);

        LibTestResult result = await new Lib2414().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(polled);
    }
}
