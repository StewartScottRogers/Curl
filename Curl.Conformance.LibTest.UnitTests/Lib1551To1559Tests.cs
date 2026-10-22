using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Info;
using Curl.Api.Options;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1551To1559Tests
{
    private static readonly TimeSpan PastHangTimeout = TimeSpan.FromSeconds(61);

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1551, "http://x/");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1551.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1554.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib1552.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1553.TestAsync(run.Context, run.Steps, library, clock, TimeProvider.System, CancellationToken.None);
        await Lib1557.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        await Lib1555.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1556.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1558.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1559.TestAsync(run.Context, run.Steps, library);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.AreEqual(4, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(3, run.Error.ToString().Split("curl_multi_init() failed").Length - 1);
        StringAssert.Contains(run.Error.ToString(), "curl_share_init() failed\n");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task MultiPorts_WhenTheLibraryGoesAtTheMultiHandle_StopAtEasyInit()
    {
        foreach (int testNumber in new[] { 1552, 1553, 1557 })
        {
            var holder = new PortRun.LibraryHolder();
            var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
            var run = new PortRun(engine, testNumber, "http://x/");
            holder.Library = CurlGlobal.Init(engine);
            var clock = new LibTestClock(TimeProvider.System);

            await (testNumber switch
            {
                1552 => Lib1552.TestAsync(run.Context, run.Steps, holder.Library, clock, CancellationToken.None),
                1553 => Lib1553.TestAsync(run.Context, run.Steps, holder.Library, clock, TimeProvider.System, CancellationToken.None),
                _ => Lib1557.TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None),
            });

            Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result, $"lib for test {testNumber}");
        }
    }

    [TestMethod]
    public void Constructors_NullTimeProvider_Throw()
    {
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new Lib1552(null!)).ParamName);
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new Lib1553(null!)).ParamName);
    }

    [TestMethod]
    public async Task Lib1551_PerformsTwiceWithTheLineBetween()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1551, "http://x/1551");

        LibTestResult result = await new Lib1551().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1551", new Lib1551().ProgramName);
        CollectionAssert.AreEqual(new[] { "http://x/1551", "http://x/1551" }, urls);
        Assert.AreEqual(Lib1551.DoItAgainLine, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1551_OnAFailingFirstPerform_StopsWithItsCode()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } };
        var run = new PortRun(engine, 1551, "http://x/1551");

        LibTestResult result = await new Lib1551().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(1, performs);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(true, (int)CurlCode.LoginDenied)]
    [DataRow(false, 0)]
    public async Task Lib1552_AnswersTheTransfersOwnCode(bool hasMessage, int expected)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.DoneResult = hasMessage ? CurlCode.LoginDenied : null;
        var run = new PortRun(engine, 1552, "imap://x/1552");

        LibTestResult result = await new Lib1552(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);
        EasyOptions seen = ((IEasyTransfer)engine.Multi.Added!).Options;

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual("lib1552", new Lib1552(TimeProvider.System).ProgramName);
        Assert.AreEqual("imap://x/1552", seen.Network.Url);
    }

    [TestMethod]
    public async Task Lib1552_WhileTransfersRun_WaitsAtMostThreeTimes()
    {
        int waits = 0;
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () => waits++;
        var run = new PortRun(engine, 1552, "imap://x/1552");

        LibTestResult result = await new Lib1552(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Lib1552.WaitTurns, waits);
    }

    [TestMethod]
    public async Task Lib1552_OnAFailingWait_WritesItAndEndsMajorBad()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        var run = new PortRun(engine, 1552, "imap://x/1552");

        LibTestResult result = await new Lib1552(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual($"curl_multi_wait() returned {(int)CurlMultiCode.BadHandle}\n", run.OutputText);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Lib1552_OnAFailingStep_StopsWithItsCode(int failing)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = failing == 1 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.PerformResult = failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () => time.Advance(PastHangTimeout);
        var run = new PortRun(engine, 1552, "imap://x/");

        LibTestResult result = await new Lib1552(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(failing == 3 ? (int)LibTestErrorCodes.RunsForever : (int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1553_PostsTheMimeWithTracingAndWaitsOnce()
    {
        int waits = 0;
        var mime = new FakeMimeBuilder();
        var engine = new FakeCurlEngine { CreatingMime = () => mime };
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () => waits++;
        var run = new PortRun(engine, 1553, "imap://non-existing-host.haxx.se/1553");

        LibTestResult result = await new Lib1553(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);
        EasyOptions seen = ((IEasyTransfer)engine.Multi.Added!).Options;

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1553", new Lib1553(TimeProvider.System).ProgramName);
        Assert.AreEqual(Lib1553.WaitTurns, waits);
        Assert.IsNotNull(seen.Callbacks.TransferInfo);
        Assert.IsNotNull(seen.Callbacks.Debug);
        Assert.AreEqual(1, seen.Callbacks.TransferInfo!(0, 0, 0, 0));
        Assert.AreEqual(Lib1553.TransferInfoFailLine, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1553_OnAFailingAdd_StopsWithItsCode()
    {
        var engine = new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder() };
        engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 1553, "imap://x/1553");

        LibTestResult result = await new Lib1553(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1553_TracesSentDataAsTextWithTheTimeAndNoHex()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 16, 14, 5, 6, TimeSpan.Zero));
        time.Advance(TimeSpan.FromMicroseconds(123456));
        var engine = new FakeCurlEngine { CreatingMime = () => new FakeMimeBuilder() };
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        var run = new PortRun(engine, 1553, "imap://x/1553");
        await new Lib1553(time).RunAsync(run.Context, CancellationToken.None);
        EasyOptions seen = ((IEasyTransfer)engine.Multi.Added!).Options;

        _ = seen.Callbacks.Debug!(CurlInfoType.DataOut, "ok"u8);

        Assert.AreEqual("14:05:06.123456 => Send data, 2 bytes (0x2)\n0000: ok\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1554_SharesTheConnectionsAndWritesEachLock()
    {
        FakeCurlEngine engine = null!;
        int performs = 0;
        engine = new FakeCurlEngine
        {
            Performing = _ =>
            {
                performs++;
                engine.LastShare!.Lock!(null, CurlLockData.Connect, CurlLockAccess.Single);
                engine.LastShare.Unlock!(null, CurlLockData.Connect);
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1554, "http://x/1554");

        LibTestResult result = await new Lib1554().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1554", new Lib1554().ProgramName);
        Assert.AreEqual(Lib1554.Transfers, performs);
        Assert.AreEqual(string.Concat(Enumerable.Repeat("-] Mutex lock CONNECT\n[- Mutex unlock CONNECT\n", 3)), run.OutputText);
    }

    [TestMethod]
    public async Task Lib1554_OnAFailingPerform_WritesItAndStops()
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } };
        var run = new PortRun(engine, 1554, "http://x/1554");

        LibTestResult result = await new Lib1554().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(1, performs);
        StringAssert.StartsWith(run.Error.ToString(), "curl_easy_perform() failed: ");
    }

    [TestMethod]
    public async Task Lib1554_WhenTheLibraryGoesAtTheShare_SkipsEveryTransfer()
    {
        var holder = new PortRun.LibraryHolder();
        int performs = 0;
        var engine = new FakeCurlEngine { CreatingShare = () => PortRun.CleanUpWhen(holder, true), Performing = _ => { performs++; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1554, "http://x/");
        holder.Library = CurlGlobal.Init(engine);

        await Lib1554.TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        Assert.AreEqual(0, performs);
    }

    [TestMethod]
    public async Task Lib1555_ProgressCallback_WritesTheRecvAndSendAnswersAndAborts()
    {
        var connection = new FakeConnectOnlyConnection();
        connection.Receives.Enqueue((CurlCode.RecursiveApiCall, string.Empty));
        connection.Sends.Enqueue((CurlCode.RecursiveApiCall, 0));
        int? progress = null;
        var engine = new FakeCurlEngine
        {
            ConnectOnly = connection,
            Performing = o =>
            {
                progress = o.Callbacks.Progress!(0, 0, 0, 0);
                return CurlCode.AbortedByCallback;
            },
        };
        var run = new PortRun(engine, 1555, "http://x/1555");

        LibTestResult result = await new Lib1555().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual("lib1555", new Lib1555().ProgramName);
        Assert.AreEqual(1, progress);
        Assert.AreEqual("curl_easy_recv returned 93\ncurl_easy_send returned 93\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1556_WritesTheLongestHeaderAfterTheTransfer()
    {
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                _ = o.Callbacks.Header!("Long-Header: 12345\r\n"u8);
                _ = o.Callbacks.Header!("\r\n"u8);
                _ = o.Callbacks.Write!("-foo-\n"u8);
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1556, "http://x/1556");

        LibTestResult result = await new Lib1556().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1556", new Lib1556().ProgramName);
        Assert.AreEqual("-foo-\nMax = 20\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1556_OnAFailingPerform_WritesItAndEndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1556, "http://x/1556");

        LibTestResult result = await new Lib1556().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), $"lib1556.c:{Lib1556.PerformFailedLine} curl_easy_perform() failed, with code 7 (");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1557_AddsBothRemovesBothAndSucceeds()
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, 1557, "hostname.invalid");

        LibTestResult result = await new Lib1557().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1557", new Lib1557().ProgramName);
        Assert.AreEqual(1L, engine.Multi.Options.MaxHostConnections);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task Lib1557_OnAFailingStep_StopsWithItsCode(int failing)
    {
        FakeCurlEngine engine = null!;
        int created = 0;
        engine = new FakeCurlEngine
        {
            CreatingEasy = () => engine.Multi.AddResult = (failing, ++created) is (1, 1) or (2, 2) ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok,
        };
        engine.Multi.PerformResult = failing == 3 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 1);
        engine.Multi.RemoveResult = failing == 4 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        var run = new PortRun(engine, 1557, "hostname.invalid");

        LibTestResult result = await new Lib1557().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        Assert.AreEqual(failing == 1 ? 1 : 2, created);
    }

    [TestMethod]
    public async Task Lib1557_WhenTheLibraryGoesAtTheFirstEasyHandle_StopsAtTheSecond()
    {
        var holder = new PortRun.LibraryHolder();
        FakeCurlEngine engine = PortRun.CleaningUpOnEasy(holder, 1);
        var run = new PortRun(engine, 1557, "hostname.invalid");
        holder.Library = CurlGlobal.Init(engine);

        await Lib1557.TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib1558_WritesTheBodyAndTheProtocolInHex()
    {
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { Protocol = 0x400 },
            Performing = o => { _ = o.Callbacks.Write!("hello\n"u8); return CurlCode.Ok; },
        };
        var run = new PortRun(engine, 1558, "file:///dir/data1558");

        LibTestResult result = await new Lib1558().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1558", new Lib1558().ProgramName);
        Assert.AreEqual("hello\nProtocol: 400\n", run.OutputText);
    }

    [TestMethod]
    public async Task Lib1558_OnAFailingPerform_WritesItAndReturnsItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.ReadError }, 1558, "file:///dir/data1558");

        LibTestResult result = await new Lib1558().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.ReadError, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "curl_easy_perform() returned 26 (");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1559_WritesEachAnswerToTheExcessiveString()
    {
        var engine = new FakeCurlEngine { UrlSetResult = CurlUrlCode.MalformedInput };
        var run = new PortRun(engine, 1559);

        LibTestResult result = await new Lib1559().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1559", new Lib1559().ProgramName);
        string[] lines = run.OutputText.Split('\n');
        StringAssert.StartsWith(lines[0], "CURLOPT_URL 10000000 bytes URL == ");
        Assert.AreEqual("CURLOPT_POSTFIELDS 10000000 bytes data == 0", lines[1]);
        Assert.AreEqual("CURLUPART_URL 10000000 bytes URL == 3 (Malformed input to a URL function)", lines[2]);
        Assert.AreEqual("CURLUPART_SCHEME 10000000 bytes scheme == 3 (Malformed input to a URL function)", lines[3]);
        Assert.AreEqual("CURLUPART_USER 10000000 bytes user == 3 (Malformed input to a URL function)", lines[4]);
    }
}
