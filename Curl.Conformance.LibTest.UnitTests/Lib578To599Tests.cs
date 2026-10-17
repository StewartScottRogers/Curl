using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Info;
using Curl.Api.Options.Authentication;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestArgumentFileTests
{
    [TestMethod]
    public void TryWrite_ReplacesOrAppendsAndFailsWithoutAPath()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Assert.IsTrue(LibTestArgumentFile.TryWrite(path, "a\n", append: false));
            Assert.IsTrue(LibTestArgumentFile.TryWrite(path, "b\n", append: true));
            Assert.AreEqual("a\nb\n", File.ReadAllText(path));
            Assert.IsTrue(LibTestArgumentFile.TryWrite(path, "c\n", append: false));
            Assert.AreEqual("c\n", File.ReadAllText(path));
            Assert.IsFalse(LibTestArgumentFile.TryWrite(null, "x", append: false));
            Assert.ThrowsExactly<ArgumentNullException>(() => LibTestArgumentFile.TryWrite(path, null!, append: false));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

[TestClass]
public sealed class Lib578To599Tests
{
    private static readonly CurlSocket First = new(5);

    [TestMethod]
    public async Task EveryEasyPort_OnACleanedUpLibrary_FailsMajorBad()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 578, "http://x/");

        await Lib578.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib579.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib589.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib590.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib598.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib599.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(6, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
    }

    [TestMethod]
    public async Task EveryMacroPort_OnACleanedUpLibrary_FailsEasyOrMultiInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 583, "sftp://x/", "u:", "pub", "key");
        var clock = new LibTestClock(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        using var source = new MemoryStream();

        await new Lib582Run(run.Context, TimeProvider.System, clock, source).TestAsync(run.Steps, library, CancellationToken.None);
        await Lib583.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib591.TestAsync(run.Context, run.Steps, library, clock, source, 5, CancellationToken.None);
        await Lib597.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);

        Assert.AreEqual(3, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(1, run.Error.ToString().Split("curl_multi_init() failed").Length - 1);
    }

    [TestMethod]
    public async Task MultiPorts_MultiInitFailsAfterTheEasyHandle_StopThere()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(new FakeCurlEngine(), 591, "ftp://x/");
        var clock = new LibTestClock(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        using var source = new MemoryStream();

        foreach (Func<CurlLibrary, ValueTask> test in new Func<CurlLibrary, ValueTask>[]
        {
            l => new Lib582Run(run.Context, TimeProvider.System, clock, source).TestAsync(run.Steps, l, CancellationToken.None),
            l => Lib591.TestAsync(run.Context, run.Steps, l, clock, source, 5, CancellationToken.None),
            l => Lib597.TestAsync(run.Context, run.Steps, l, clock, CancellationToken.None),
        })
        {
            holder.Library = CurlGlobal.Init(PortRun.CleaningUpOnEasy(holder, 1));
            await test(holder.Library);
        }

        Assert.AreEqual(3, run.Error.ToString().Split("curl_multi_init() failed").Length - 1);
    }

    [TestMethod]
    public async Task PerformWithMultiTimeoutUntilDoneAsync_WaitsTheMultiTimeoutAndStopsOnAnyFailure()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var waits = new FakeCurlEngine();
        waits.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        waits.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        waits.Multi.TimeoutResult = (CurlMultiCode.Ok, long.MaxValue);
        var timeoutFails = Running(1);
        timeoutFails.Multi.TimeoutResult = (CurlMultiCode.BadHandle, 0);
        var selectFails = Running(1);
        selectFails.Multi.WaitResult = (CurlMultiCode.OutOfMemory, 0);
        var timesOut = Running(1);
        timesOut.Multi.Waiting = () => clock.Advance(TimeSpan.FromMinutes(5));
        var performFails = Running(0);
        performFails.Multi.PerformResult = (CurlMultiCode.OutOfMemory, 0);

        foreach (var (engine, done, code) in new[]
        {
            (waits, true, CurlCode.Ok),
            (timeoutFails, false, LibTestErrorCodes.BadTimeout),
            (selectFails, false, LibTestErrorCodes.Select),
            (timesOut, false, LibTestErrorCodes.RunsForever),
            (performFails, false, LibTestErrorCodes.Multi),
        })
        {
            var steps = new LibTestSteps(new StringWriter());
            await using CurlLibrary library = CurlGlobal.Init(engine);
            await using CurlMulti multi = library.MultiInit();
            var libTestClock = new LibTestClock(clock);
            libTestClock.StartTestTiming();

            Assert.AreEqual(done, await steps.PerformWithMultiTimeoutUntilDoneAsync(multi, libTestClock, 100, CancellationToken.None));
            Assert.AreEqual(code, steps.Result);
        }

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new LibTestSteps(new StringWriter()).PerformWithMultiTimeoutUntilDoneAsync(null!, new LibTestClock(clock), 1, CancellationToken.None));
    }

    private static FakeCurlEngine Running(int running)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, running);
        return engine;
    }

    [TestMethod]
    public async Task Lib578_PostsTheDataAndReportsTheUploadCounts()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new FakeCurlEngine { Performing = o => { o.Callbacks.Progress!(0, 0, 25, 25); return CurlCode.Ok; } };
            var run = new PortRun(engine, 578, "http://x/578", path);

            LibTestResult result = await new Lib578().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("PASSED, UL data matched data size\n", await File.ReadAllTextAsync(path));
            Assert.AreEqual(0, Lib578.Progress(path, 25, 3));
            Assert.AreEqual("Progress callback called with UL 3.000000 out of 25.000000\n", await File.ReadAllTextAsync(path));
            Assert.AreEqual("lib578", new Lib578().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Lib579_ReadsTheFourPostsAndReportsStartAndEnd()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var read = new List<string>();
            var engine = new FakeCurlEngine { Performing = o => Upload(o.Callbacks, read) };
            var run = new PortRun(engine, 579, "http://x/579", path);

            LibTestResult result = await new Lib579().RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            CollectionAssert.AreEqual(new[] { "one", "two", "three", "and a final longer crap: four", string.Empty }, read);
            Assert.AreEqual("Progress: start UL 0/0\nProgress: end UL 10/10\nProgress: start UL 0/10\nProgress: end UL 0/10\n", await File.ReadAllTextAsync(path));
            Assert.AreEqual("lib579", new Lib579().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Lib579_ReadIntoAnEmptyBuffer_GivesNothing()
    {
        int counter = 0;

        Assert.AreEqual(0, Lib579.Read([], ref counter));
        Assert.AreEqual(0, counter);
    }

    [TestMethod]
    public void Lib579Progress_FileDoesNotOpen_WritesToStderr()
    {
        var error = new StringWriter();
        var progress = new Lib579Progress(null, error);

        Assert.AreEqual(0, progress.Callback(0, 0, 4, 2));
        progress.FinalReport();

        Assert.AreEqual("Progress: start UL 2/4\nProgress: start UL, cannot open \nProgress: end UL 2/4\nProgress: end UL, cannot open \n", error.ToString());
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib579Progress(null, null!));
    }

    [TestMethod]
    public async Task Lib582_UploadsThroughSocketActionsUntilDone()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "abc");
        try
        {
            var engine = new FakeCurlEngine();
            FakeMultiTransfer multi = engine.Multi;
            int waits = 0;
            int writes = 0;
            multi.Waiting = () => _ = ++waits switch { 1 => Timer(multi, 0), 3 => Timer(multi, -1), _ => 0 };
            multi.SocketActing = (socket, events) => socket == CurlMultiSockets.Timeout
                ? (CurlMultiCode.Ok, Socket(multi, First, CurlPoll.In | CurlPoll.Out) + Timer(multi, 5) + 1)
                : events == CurlSocketSelect.In ? (CurlMultiCode.BadSocket, 1)
                : ++writes == 3 ? Finish(multi, Socket(multi, First, CurlPoll.Remove), CurlCode.Ok) : (CurlMultiCode.Ok, 1);
            var run = new PortRun(engine, 582, "sftp://x/582", path, "u:", "pub", "key");

            LibTestResult result = await new Lib582(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

            string error = run.Error.ToString();
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(4, waits);
            StringAssert.StartsWith(error, "Set to upload 3 bytes\nAdd socket fd 5 for read\nAdd socket fd 5 for write\ncurl error on read (");
            Assert.Contains("Remove socket fd 5\n", error);
            Assert.AreEqual("lib582", new Lib582(TimeProvider.System).ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Lib582_FailedTransferOrUnexpectedMessage_ErrorUploadingFile()
    {
        foreach (CurlMultiMessageType type in new[] { CurlMultiMessageType.Done, (CurlMultiMessageType)7 })
        {
            var engine = new FakeCurlEngine();
            engine.Multi.DoneMessageType = type;
            engine.Multi.Waiting = () => engine.Multi.DoneResult = CurlCode.CouldntConnect;
            var (lib582, run) = Create582(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

            await lib582.TestAsync(run.Steps, CurlGlobal.Init(engine), CancellationToken.None);

            Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
            Assert.Contains("Error uploading file.\n", run.Error.ToString());
            Assert.AreEqual(type != CurlMultiMessageType.Done, run.Error.ToString().Contains("Got an unexpected message from curl: 7\n", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task Lib582_AddSelectOrTimeoutFailing_StopsTheLoop()
    {
        var addFails = new FakeCurlEngine();
        addFails.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var selectFails = new FakeCurlEngine();
        selectFails.Multi.WaitResult = (CurlMultiCode.OutOfMemory, 0);
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var timesOut = new FakeCurlEngine();
        timesOut.Multi.Waiting = () => clock.Advance(TimeSpan.FromMinutes(5));

        foreach (var (engine, code) in new[] { (addFails, LibTestErrorCodes.Multi), (selectFails, LibTestErrorCodes.Select), (timesOut, LibTestErrorCodes.RunsForever) })
        {
            var (lib582, run) = Create582(engine, clock);

            await lib582.TestAsync(run.Steps, CurlGlobal.Init(engine), CancellationToken.None);

            Assert.AreEqual(code, run.Steps.Result);
        }
    }

    [TestMethod]
    public async Task Lib582_TooFewArgumentsOrNoFile_FailsBeforeTheLibrary()
    {
        var tooFew = new PortRun(new FakeCurlEngine(), 582, "sftp://x/", "f", "u:");
        var noFile = new PortRun(new FakeCurlEngine(), 582, "sftp://x/", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "u:", "pub");
        var lib582 = new Lib582(TimeProvider.System);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await lib582.RunAsync(tooFew.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.FileOpen, (await lib582.RunAsync(noFile.Context, CancellationToken.None)).ExitCode);
        StringAssert.StartsWith(noFile.Error.ToString(), "fopen() failed with error ");
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => lib582.RunAsync(null!, CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib582(null!));
    }

    [TestMethod]
    public void Lib582Run_NullArguments_Throw()
    {
        var run = new PortRun(new FakeCurlEngine(), 582);
        var clock = new LibTestClock(TimeProvider.System);
        using var source = new MemoryStream();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib582Run(null!, TimeProvider.System, clock, source));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib582Run(run.Context, null!, clock, source));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib582Run(run.Context, TimeProvider.System, null!, source));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib582Run(run.Context, TimeProvider.System, clock, null!));
        Assert.IsEmpty(new Lib582Run(run.Context, TimeProvider.System, clock, source).ReadSockets);
        Assert.IsEmpty(new Lib582Run(run.Context, TimeProvider.System, clock, source).WriteSockets);
    }

    [TestMethod]
    [DataRow(0, 0, 0, "curl_multi_remove_handle() succeeded\n")]
    [DataRow(0, (int)CurlMultiCode.BadEasyHandle, (int)LibTestErrorCodes.Multi, "curl_multi_remove_handle() failed, with code 2\n")]
    [DataRow((int)CurlMultiCode.OutOfMemory, 0, (int)LibTestErrorCodes.Multi, "curl_multi_perform() failed, with code 3")]
    public async Task Lib583_RemovesTheHandleAfterOnePerform(int performCode, int removeCode, int exitCode, string lastLine)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = ((CurlMultiCode)performCode, 1);
        engine.Multi.RemoveResult = (CurlMultiCode)removeCode;
        var run = new PortRun(engine, 583, "sftp://x/583", "u:", "pub", "key");

        LibTestResult result = await new Lib583().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.Contains(lastLine, run.Error.ToString());
        Assert.AreEqual("lib583", new Lib583().ProgramName);
    }

    [TestMethod]
    public async Task Lib583_TooFewArgumentsAddFailingOrEasyInitFailing_Stops()
    {
        var tooFew = new PortRun(new FakeCurlEngine(), 583, "sftp://x/", "u:");
        var addFails = new FakeCurlEngine();
        addFails.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var addRun = new PortRun(addFails, 583, "sftp://x/583", "u:", "pub", "key");
        var holder = new PortRun.LibraryHolder();
        var cleaning = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        holder.Library = CurlGlobal.Init(cleaning);
        var cleaningRun = new PortRun(cleaning, 583, "sftp://x/583", "u:", "pub", "key");

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await new Lib583().RunAsync(tooFew.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.Multi, (await new Lib583().RunAsync(addRun.Context, CancellationToken.None)).ExitCode);
        Assert.DoesNotContain("curl_multi_perform()", addRun.Error.ToString());
        await Lib583.TestAsync(cleaningRun.Context, cleaningRun.Steps, holder.Library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, cleaningRun.Steps.Result);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Lib583().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(589, 0, 1, 0)]
    [DataRow(584, 0, 2, 0)]
    [DataRow(584, (int)CurlCode.RecvError, 1, (int)CurlCode.RecvError)]
    public async Task Lib589_PostsMimeOnlyFor584ThenWithItCleared(int testNumber, int firstResult, int performs, int exitCode)
    {
        var mime = new FakeMimeBuilder();
        var posted = new List<int?>();
        var engine = new FakeCurlEngine
        {
            CreatingMime = () => mime,
            Performing = o => { posted.Add(o.Http.MimePost?.Parts.Count); return posted.Count == 1 ? (CurlCode)firstResult : CurlCode.Ok; },
        };
        var run = new PortRun(engine, testNumber, "http://x/589");

        LibTestResult result = await new Lib589().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.HasCount(performs, posted);
        Assert.AreEqual(testNumber == 584 && performs == 1 ? 1 : null, posted[^1]);
        Assert.AreEqual(testNumber == 584 ? "fake" : null, mime.Parts.SingleOrDefault()?.Name);
        Assert.AreEqual("lib589", new Lib589().ProgramName);
    }

    [TestMethod]
    [DataRow((long)HttpAuthenticationMethods.Ntlm, 0, "")]
    [DataRow((long)HttpAuthenticationMethods.Basic, 0, "CURLINFO_PROXYAUTH_USED did not say NTLM\n")]
    [DataRow((long)HttpAuthenticationMethods.Ntlm, (int)CurlCode.CouldntConnect, "")]
    public async Task Lib590_SaysWhenTheProxyAuthUsedIsNotNtlm(long used, int performResult, string output)
    {
        string? proxy = null;
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { ProxyAuthUsed = used },
            Performing = o => { proxy = o.Proxy.Proxy; return (CurlCode)performResult; },
        };
        var run = new PortRun(engine, 590, "http://x/590", "http://127.0.0.1:8990");

        LibTestResult result = await new Lib590().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(performResult, result.ExitCode);
        Assert.AreEqual(output, run.OutputText);
        Assert.AreEqual("http://127.0.0.1:8990", proxy);
        Assert.AreEqual("lib590", new Lib590().ProgramName);
    }

    [TestMethod]
    [DataRow(null, false, 0L)]
    [DataRow("abc", false, 0L)]
    [DataRow("123456", false, 0L)]
    [DataRow("70000", false, 0L)]
    [DataRow("5", true, 5L)]
    [DataRow("65535x", true, 65535L)]
    public void Lib591_TryParseAcceptTimeout_TakesDecimalDigitsUpTo65535(string? text, bool parsed, long seconds)
    {
        bool result = Lib591.TryParseAcceptTimeout(text, out long value);

        Assert.AreEqual(parsed, result);
        if (parsed)
        {
            Assert.AreEqual(seconds, value);
        }
    }

    [TestMethod]
    public async Task Lib591_UploadsAndAnswersTheTransfersResult()
    {
        string path = Path.GetTempFileName();
        try
        {
            var engine = new FakeCurlEngine();
            engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
            engine.Multi.Waiting = () => engine.Multi.DoneResult = CurlCode.FtpAcceptFailed;
            var run = new PortRun(engine, 591, "ftp://x/591", "2", path);

            LibTestResult result = await new Lib591(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual((int)CurlCode.FtpAcceptFailed, result.ExitCode);
            Assert.AreEqual(2000, engine.Multi.Added!.Options.Network.AcceptTimeout.TotalMilliseconds);
            Assert.AreEqual("lib591", new Lib591(TimeProvider.System).ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Lib591_BadTimeoutNoFileOrFailingLoop_AnswersItsCode()
    {
        var badTimeout = new PortRun(new FakeCurlEngine(), 591, "ftp://x/", "x");
        var noFile = new PortRun(new FakeCurlEngine(), 591, "ftp://x/", "2", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var noFileArgument = new PortRun(new FakeCurlEngine(), 591, "ftp://x/", "2");
        var lib591 = new Lib591(TimeProvider.System);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, (await lib591.RunAsync(badTimeout.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.FileOpen, (await lib591.RunAsync(noFile.Context, CancellationToken.None)).ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.FileOpen, (await lib591.RunAsync(noFileArgument.Context, CancellationToken.None)).ExitCode);
        StringAssert.StartsWith(noFile.Error.ToString(), "fopen() failed with error ");
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => lib591.RunAsync(null!, CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib591(null!));
    }

    [TestMethod]
    [DataRow(0, (int)CurlCode.Ok, 0, 0)]
    [DataRow(0, (int)CurlCode.LoginDenied, 0, (int)CurlCode.LoginDenied)]
    [DataRow(0, -1, 0, 0)]
    [DataRow((int)CurlMultiCode.OutOfMemory, (int)CurlCode.Ok, 0, (int)LibTestErrorCodes.Multi)]
    [DataRow(0, (int)CurlCode.Ok, (int)CurlMultiCode.BadEasyHandle, (int)LibTestErrorCodes.Multi)]
    public async Task Lib597_ConnectsOnlyAndAnswersTheTransfersResult(int performCode, int doneResult, int removeCode, int exitCode)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = ((CurlMultiCode)performCode, 0);
        engine.Multi.DoneResult = doneResult < 0 ? null : (CurlCode)doneResult;
        engine.Multi.RemoveResult = (CurlMultiCode)removeCode;
        var run = new PortRun(engine, 597, "ftp://x/");

        LibTestResult result = await new Lib597(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual("lib597", new Lib597(TimeProvider.System).ProgramName);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Lib597(TimeProvider.System).RunAsync(null!, CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib597(null!));
    }

    [TestMethod]
    [DataRow(0, 0, 2, "")]
    [DataRow(1, (int)CurlCode.RecvError, 1, "retrieve 1 failed\n")]
    [DataRow(2, (int)CurlCode.RecvError, 2, "retrieve 2 failed\n")]
    public async Task Lib598_ResetsBetweenTwoRetrieves(int failing, int exitCode, int performs, string error)
    {
        var referers = new List<string?>();
        var engine = new FakeCurlEngine
        {
            Performing = o => { referers.Add(o.Http.Referer); return referers.Count == failing ? CurlCode.RecvError : CurlCode.Ok; },
        };
        var run = new PortRun(engine, 598, "http://x/598");

        LibTestResult result = await new Lib598().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.HasCount(performs, referers);
        Assert.AreEqual("http://example.com/the-moo", referers[0]);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.AreEqual("lib598", new Lib598().ProgramName);
    }

    [TestMethod]
    public async Task Lib599_WritesTheContentLengthOnceDone()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var engine = new FakeCurlEngine
            {
                Info = new TransferInfo { ContentLengthDownload = 1234 },
                Performing = o => o.Callbacks.Progress!(10, 5, 0, 0) == 0 ? CurlCode.Ok : CurlCode.AbortedByCallback,
            };
            var failing = new FakeCurlEngine { PerformResult = CurlCode.AbortedByCallback };
            var run = new PortRun(engine, 599, "http://x/599", path);
            var failingRun = new PortRun(failing, 599, "http://x/599", path + "x");

            LibTestResult result = await new Lib599().RunAsync(run.Context, CancellationToken.None);
            LibTestResult failed = await new Lib599().RunAsync(failingRun.Context, CancellationToken.None);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("CL 1234\n", await File.ReadAllTextAsync(path));
            Assert.AreEqual((int)CurlCode.AbortedByCallback, failed.ExitCode);
            Assert.IsFalse(File.Exists(path + "x"));
            Assert.AreEqual("lib599", new Lib599().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Lib599_Progress_AbortsOnlyPastAKnownTotal()
    {
        var run = new PortRun(new FakeCurlEngine(), 599);

        Assert.AreEqual(0, Lib599.Progress(run.Context, 0, 50));
        Assert.AreEqual(0, Lib599.Progress(run.Context, 50, 50));
        Assert.AreEqual(-1, Lib599.Progress(run.Context, 50, 51));
        Assert.AreEqual("50 > 51 !!\n", run.OutputText);
    }

    private static (Lib582Run Run, PortRun Port) Create582(FakeCurlEngine engine, TimeProvider clock)
    {
        var port = new PortRun(engine, 582, "sftp://x/582", "f", "u:", "pub", "key");
        var libTestClock = new LibTestClock(clock);
        libTestClock.StartTestTiming();
        return (new Lib582Run(port.Context, clock, libTestClock, new MemoryStream()), port);
    }

    private static int Timer(FakeMultiTransfer multi, long milliseconds) => multi.Options.Timer!(milliseconds);

    private static int Socket(FakeMultiTransfer multi, CurlSocket socket, CurlPoll what) => multi.Options.Socket!(multi.Added!, socket, what, null);

    private static (CurlMultiCode Code, int RunningTransfers) Finish(FakeMultiTransfer multi, int callbacks, CurlCode result)
    {
        multi.DoneResult = result;
        return (CurlMultiCode.Ok, callbacks);
    }

    private static CurlCode Upload(Curl.Api.Callbacks.EasyCallbackOptions callbacks, List<string> read)
    {
        callbacks.Progress!(0, 0, 0, 0);
        byte[] buffer = new byte[64];
        long count;
        do
        {
            count = callbacks.Read!(buffer);
            read.Add(System.Text.Encoding.ASCII.GetString(buffer, 0, (int)count));
        }
        while (count > 0);

        callbacks.Progress!(0, 0, 10, 10);
        callbacks.Progress!(0, 0, 10, 0);
        callbacks.Progress!(0, 0, 10, 0);
        return CurlCode.Ok;
    }
}
