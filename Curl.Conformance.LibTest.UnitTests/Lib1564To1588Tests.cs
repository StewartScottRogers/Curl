using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1564To1588Tests
{
    private static readonly TimeSpan PastHangTimeout = TimeSpan.FromSeconds(61);

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1564, "http://x/", "1", "2");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1567.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib1564.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1565.TestAsync(run.Context, run.Steps, library, clock, 0, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        await Lib1569.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib1571.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1576.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1582.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1588.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(2, run.Error.ToString().Split("curl_multi_init() failed").Length - 1);
        Assert.AreEqual(5, run.Error.ToString().Split("curl_easy_init() failed\n").Length - 1);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public void Constructors_NullTimeProvider_Throw()
    {
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new Lib1564(null!)).ParamName);
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new Lib1565(null!)).ParamName);
    }

    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports = [new Lib1564(TimeProvider.System), new Lib1565(TimeProvider.System), new Lib1567(), new Lib1568(), new Lib1569(), new Lib1571(), new Lib1576(), new Lib1582(), new Lib1587(), new Lib1588()];

        CollectionAssert.AreEqual(
            new[] { "lib1564", "lib1565", "lib1567", "lib1568", "lib1569", "lib1571", "lib1576", "lib1582", "lib1587", "lib1588" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task Lib1564_PollsWaitingUnlessWokenUp_Succeeds()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        int polls = 0;
        var engine = new FakeCurlEngine();
        engine.Multi.Polling = () => time.Advance(TimeSpan.FromMilliseconds(Lib1564.Rounds[polls++].EndsEarly ? 0 : Lib1564.PollTimeoutMilliseconds));
        var run = new PortRun(engine, 1564);

        LibTestResult result = await new Lib1564(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Lib1564.Rounds.Count, polls);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(0L, "lib1564.c:51 curl_multi_poll returned too early\n")]
    [DataRow(1000L, "lib1564.c:68 curl_multi_poll returned too late\n")]
    public async Task Lib1564_OnAPollOnTheWrongSide_WritesItAndEndsMajorBad(long eachPollMilliseconds, string expected)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.Polling = () => time.Advance(TimeSpan.FromMilliseconds(eachPollMilliseconds));
        var run = new PortRun(engine, 1564);

        LibTestResult result = await new Lib1564(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual(expected, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1564_OnAFailingPoll_StopsWithMulti()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PollResult = (CurlMultiCode.BadHandle, 0);
        var run = new PortRun(engine, 1564);

        LibTestResult result = await new Lib1564(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1564_PastTheHangTimeout_Aborts()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.Polling = () => time.Advance(PastHangTimeout);
        var run = new PortRun(engine, 1564);

        LibTestResult result = await new Lib1564(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.RunsForever, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1565_AddsEveryHandleTheOtherThreadMakes_UntilAllFinish()
    {
        int created = 0;
        var engine = new FakeCurlEngine { CreatingEasy = () => Interlocked.Increment(ref created) };
        engine.Multi.EachAddedDoneResult = CurlCode.Ok;
        var run = new PortRun(engine, 1565, "http://x/1565");

        LibTestResult result = await new Lib1565(TimeProvider.System, 0).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Lib1565.ConnectionCount, created);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops()
    {
        int created = 0;
        var engine = new FakeCurlEngine { CreatingEasy = () => Interlocked.Increment(ref created) };
        engine.Multi.EachAddedDoneResult = CurlCode.CouldntConnect;
        var run = new PortRun(engine, 1565, "http://x/1565");

        LibTestResult result = await new Lib1565(new FirstTimerAtOnceTimeProvider(), 1000).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(2, created);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task Lib1565_OnAFailingMultiStep_EndsWithMulti(int failing)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.EachAddedDoneResult = CurlCode.Ok;
        engine.Multi.PerformResult = failing == 1 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 0);
        engine.Multi.PollResult = failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 0);
        engine.Multi.AddResult = failing == 3 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.RemoveResult = failing == 4 ? CurlMultiCode.BadHandle : CurlMultiCode.Ok;
        var run = new PortRun(engine, 1565, "http://x/1565");

        LibTestResult result = await new Lib1565(TimeProvider.System, 0).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1565_OnAFailingWakeup_AnswersTheOtherThreadsCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.EachAddedDoneResult = CurlCode.Ok;
        engine.Multi.WakeupResult = CurlMultiCode.BadHandle;
        var run = new PortRun(engine, 1565, "http://x/1565");

        LibTestResult result = await new Lib1565(TimeProvider.System, 0).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "curl_multi_wakeup() failed");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Lib1565_PastTheHangTimeout_Aborts(bool inThePerform)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.Performing = () => time.Advance(inThePerform ? PastHangTimeout : TimeSpan.Zero);
        engine.Multi.Polling = () => time.Advance(PastHangTimeout);
        var run = new PortRun(engine, 1565, "http://x/1565");

        LibTestResult result = await new Lib1565(time, 0).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.RunsForever, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1567_PerformsTheCurluTwiceWithTheLineBetween()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1567, "http://x/1567");

        LibTestResult result = await new Lib1567().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(2, urls);
        Assert.AreEqual(Lib1567.DoItAgainLine, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1567_OnAFailingFirstPerform_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } };
        var run = new PortRun(engine, 1567, "http://x/1567");

        LibTestResult result = await new Lib1567().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    [DataRow("8990", true, 8990L)]
    [DataRow("65535x", true, 65535L)]
    [DataRow("65536", false, 0L)]
    [DataRow("99999999999999999999", false, 0L)]
    [DataRow("x", false, 0L)]
    [DataRow(null, false, 0L)]
    public void Lib1568_TryParsePort_ReadsLeadingDigitsUpTo65535(string? text, bool expected, long expectedPort)
    {
        bool parsed = Lib1568.TryParsePort(text, out long port);

        Assert.AreEqual(expected, parsed);
        Assert.AreEqual(expected ? expectedPort : 0L, parsed ? port : 0L);
    }

    [TestMethod]
    public async Task Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode()
    {
        string? userAgent = null;
        var engine = new FakeCurlEngine { Performing = o => { userAgent = o.Http.UserAgent; return CurlCode.LoginDenied; } };
        var run = new PortRun(engine, 1568, "http://127.0.0.1/1568", "8990");

        LibTestResult result = await new Lib1568().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("lib1568", userAgent);
    }

    [TestMethod]
    [DataRow("http://x/1568", "port")]
    [DataRow("http://x/1568", null)]
    public async Task Lib1568_WithNoPortArgument_EndsMajorBad(string url, string? port)
    {
        var run = new PortRun(new FakeCurlEngine(), 1568, port is null ? [url] : [url, port]);

        LibTestResult result = await new Lib1568().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1568_WithNoEngine_EndsMajorBad()
    {
        var run = new PortRun(null, 1568, "http://x/1568", "80");

        LibTestResult result = await new Lib1568().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1569_PerformsTheUrlThenTheSecondArgument()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1569, "ftp://x/1569;type=A", "ftp://x/1569");

        LibTestResult result = await new Lib1569().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "ftp://x/1569;type=A", "ftp://x/1569" }, urls);
    }

    [TestMethod]
    public async Task Lib1569_OnAFailingFirstPerform_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.FtpCouldntRetrFile; } };
        var run = new PortRun(engine, 1569, "ftp://x/1569;type=A", "ftp://x/1569");

        LibTestResult result = await new Lib1569().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.FtpCouldntRetrFile, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    [DataRow(1571, true, false, FollowLocation.ObeyCode)]
    [DataRow(1572, true, false, FollowLocation.ObeyCode)]
    [DataRow(1573, false, false, FollowLocation.ObeyCode)]
    [DataRow(1574, false, false, FollowLocation.FirstOnly)]
    [DataRow(1575, true, false, FollowLocation.FirstOnly)]
    [DataRow(1581, true, true, FollowLocation.ObeyCode)]
    public void Lib1571_Options_FollowTheCase(int testNumber, bool posts, bool keepsPostAfter301, FollowLocation follow)
    {
        var run = new PortRun(new FakeCurlEngine(), testNumber, "http://x/");

        var options = Lib1571.Options(run.Context).ToDictionary(o => o.Option, o => o.Value);

        Assert.AreEqual(posts, options.ContainsKey(CurlOption.Postfields));
        Assert.AreEqual(keepsPostAfter301, options.ContainsKey(CurlOption.Postredir));
        Assert.AreEqual("IGLOO", options[CurlOption.Customrequest]);
        Assert.AreEqual((long)follow, options[CurlOption.Followlocation]);
    }

    [TestMethod]
    public async Task Lib1571_PerformsWithTheCustomMethodAndAnswersItsCode()
    {
        string? method = null;
        var engine = new FakeCurlEngine { Performing = o => { method = o.Transfer.CustomRequest; return CurlCode.TooManyRedirects; } };
        var run = new PortRun(engine, 1571, "http://x/1571");

        LibTestResult result = await new Lib1571().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual("IGLOO", method);
    }

    [TestMethod]
    [DataRow(1576, FollowLocation.ObeyCode)]
    [DataRow(1578, FollowLocation.FirstOnly)]
    [DataRow(1580, FollowLocation.FirstOnly)]
    public async Task Lib1576_UploadsWithTheCustomMethodAndAnswersItsCode(int testNumber, FollowLocation follow)
    {
        ReadCallback? read = null;
        FollowLocation? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { read = o.Callbacks.Read; seen = o.Http.FollowLocation; return CurlCode.SendError; } };
        var run = new PortRun(engine, testNumber, "http://x/1576");

        LibTestResult result = await new Lib1576().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.IsNotNull(read);
        Assert.AreEqual(follow, seen);
    }

    [TestMethod]
    public void Lib1576_Read_CopiesTheBodyOnEveryCallUnlessTheBufferIsTooSmall()
    {
        byte[] buffer = new byte[100];
        byte[] small = new byte[10];

        long first = Lib1576.Read(buffer);
        long second = Lib1576.Read(buffer);
        long tooSmall = Lib1576.Read(small);

        Assert.AreEqual(Lib1576.Body.Length, first);
        Assert.AreEqual(Lib1576.Body.Length, second);
        Assert.AreEqual(Lib1576.Body.Length, tooSmall);
        CollectionAssert.AreEqual(Lib1576.Body.ToArray(), buffer[..Lib1576.Body.Length]);
        CollectionAssert.AreEqual(new byte[10], small);
    }

    [TestMethod]
    [DataRow(SeekOrigin.Begin, SeekResult.Ok)]
    [DataRow(SeekOrigin.Current, SeekResult.Fail)]
    [DataRow(SeekOrigin.End, SeekResult.Fail)]
    public void Lib1576_Seek_WorksOnlyFromTheStart(SeekOrigin origin, SeekResult expected)
    {
        Assert.AreEqual(expected, Lib1576.Seek(5, origin));
    }

    [TestMethod]
    public async Task Lib1582_PerformsWithNegotiateAndAnswersItsCode()
    {
        var engine = new FakeCurlEngine { PerformResult = CurlCode.LoginDenied };
        var run = new PortRun(engine, 1582, "https://x/");

        LibTestResult result = await new Lib1582().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1587_WithoutOpenSsl_SucceedsAtOnce()
    {
        var run = new PortRun(null, 1587, "https://x/");

        LibTestResult result = await new Lib1587().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1588_RequestsThroughEachProxyWithTheLineBetween()
    {
        var proxies = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { proxies.Add(o.Proxy.Proxy); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1588, "http://test.remote.example.com/path/1588", "127.0.0.1", "8990", "silly:person");

        LibTestResult result = await new Lib1588().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "firstproxy:8990", "secondproxy:8990" }, proxies);
        Assert.AreEqual(Lib1588.AgainLine, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1588_OnAFailingFirstRequest_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntResolveProxy; } };
        var run = new PortRun(engine, 1588, "http://x/1588", "127.0.0.1", "8990", "silly:person");

        LibTestResult result = await new Lib1588().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task Lib1588_WithNoPortArgument_NamesTheProxyPortNil()
    {
        var proxies = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { proxies.Add(o.Proxy.Proxy); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1588, "http://x/1588", "127.0.0.1");

        LibTestResult result = await new Lib1588().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "firstproxy:(nil)", "secondproxy:(nil)" }, proxies);
    }

    [TestMethod]
    public async Task Lib1588_WithTooFewArguments_EndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 1588, "http://x/1588");

        LibTestResult result = await new Lib1588().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    /// <summary>
    /// The system clock, except that the first timer fires at once: lib1565's second thread makes its first handle
    /// straight away and waits the whole second for the next, which the main thread's failure, recorded within it even
    /// on a busy runner, stops (BL-2678: a 50 ms wait let a slow Linux runner hand over a third handle).
    /// </summary>
    private sealed class FirstTimerAtOnceTimeProvider : TimeProvider
    {
        private int timers;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, Interlocked.Increment(ref timers) == 1 ? TimeSpan.Zero : dueTime, period);
    }
}
