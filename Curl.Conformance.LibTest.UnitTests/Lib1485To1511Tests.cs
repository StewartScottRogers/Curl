using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1485To1511Tests
{
    private static readonly TimeSpan PastHangTimeout = TimeSpan.FromSeconds(61);

    [TestMethod]
    public async Task EveryEasyFirstPort_OnACleanedUpLibrary_StopsAtEasyInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1485, "http://x/", "127.0.0.1", "8990");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1485.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1501.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1502.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1507.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1509.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1510.TestAsync(run.Context, run.Steps, library, clock, null, CancellationToken.None);
        await Lib1511.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.AreEqual(7, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task EveryMultiFirstPort_OnACleanedUpLibrary_StopsAtMultiInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1500, "http://x/", "127.0.0.1", "8990");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1500.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1506.TestAsync(run.Context, run.Steps, library, clock, null, CancellationToken.None);
        await Lib1508.TestAsync(run.Steps, library);

        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        Assert.AreEqual(3, run.Error.ToString().Split("curl_multi_init() failed").Length - 1);
    }

    [TestMethod]
    public async Task EasyThenMultiPorts_WhenTheLibraryGoesAtTheEasyHandle_StopAtMultiInit()
    {
        foreach (int testNumber in new[] { 1501, 1502, 1503, 1507 })
        {
            var holder = new PortRun.LibraryHolder();
            var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), testNumber, "http://x/");
            holder.Library = CurlGlobal.Init(run.Context.Engine!);
            var clock = new LibTestClock(TimeProvider.System);

            await (testNumber switch
            {
                1501 => Lib1501.TestAsync(run.Context, run.Steps, holder.Library, clock, CancellationToken.None),
                1507 => Lib1507.TestAsync(run.Context, run.Steps, holder.Library, clock, CancellationToken.None),
                _ => Lib1502.TestAsync(run.Context, run.Steps, holder.Library, clock, CancellationToken.None),
            });

            Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result, $"lib for test {testNumber}");
        }
    }

    [TestMethod]
    public async Task MultiThenEasyPorts_WhenTheLibraryGoesAtTheMultiHandle_StopAtEasyInit()
    {
        foreach (int testNumber in new[] { 1500, 1506 })
        {
            var holder = new PortRun.LibraryHolder();
            var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
            var run = new PortRun(engine, testNumber, "http://x/");
            holder.Library = CurlGlobal.Init(engine);
            var clock = new LibTestClock(TimeProvider.System);

            await (testNumber == 1500
                ? Lib1500.TestAsync(run.Context, run.Steps, holder.Library, clock, CancellationToken.None)
                : Lib1506.TestAsync(run.Context, run.Steps, holder.Library, clock, null, CancellationToken.None));

            Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result, $"lib for test {testNumber}");
        }
    }

    [TestMethod]
    public void TimedPorts_WithoutATimeProvider_Throw()
    {
        Action[] constructors =
        [
            () => _ = new Lib1500(null!),
            () => _ = new Lib1501(null!),
            () => _ = new Lib1502(null!),
            () => _ = new Lib1506(null!),
            () => _ = new Lib1507(null!),
            () => _ = new Lib1510(null!),
        ];

        foreach (Action construct in constructors)
        {
            Assert.ThrowsExactly<ArgumentNullException>(construct);
        }
    }

    [TestMethod]
    public async Task Lib1485_CopiesTheTransferToStdout()
    {
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { ResponseCode = 200, ContentLengthDownload = 2 },
            Performing = o =>
            {
                _ = o.Callbacks.Header!("HTTP/1.1 200 OK\r\n"u8);
                _ = o.Callbacks.Header!("\r\n"u8);
                _ = o.Callbacks.Write!("ok"u8);
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1485, "http://x/1485");

        LibTestResult result = await new Lib1485().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\nok", run.OutputText);
        Assert.AreEqual("header_callback, get status: 200, 0\nheader_callback, info Content-Length: 2, 0\n", run.Error.ToString());
        Assert.AreEqual("lib1485", new Lib1485().ProgramName);
    }

    [TestMethod]
    [DataRow(99L, 7L, Lib1485.TransferStatus.WriteError, "header_callback, get status: 99, 0\nheader_callback, invalid status: 99, 0\n")]
    [DataRow(1000L, 7L, Lib1485.TransferStatus.WriteError, "header_callback, get status: 1000, 0\nheader_callback, invalid status: 1000, 0\n")]
    [DataRow(100L, -1L, 2L, "header_callback, get status: 100, 0\n")]
    [DataRow(300L, -1L, 2L, "header_callback, get status: 300, 0\n")]
    [DataRow(204L, 0L, 2L, "header_callback, get status: 204, 0\nheader_callback, info Content-Length: 0, 0\n")]
    [DataRow(200L, -1L, Lib1485.TransferStatus.WriteError, "header_callback, get status: 200, 0\nheader_callback, info Content-Length: -1, 0\nheader_callback, expected known Content-Length, got: -1\n")]
    public async Task Lib1485TransferStatus_AtTheEndOfAResponse_ChecksStatusAndLength(long status, long length, long expected, string expectedError)
    {
        var engine = new FakeCurlEngine { Info = new TransferInfo { ResponseCode = status, ContentLengthDownload = length } };
        CurlLibrary library = CurlGlobal.Init(engine);
        CurlEasy easy = library.EasyInit();
        var run = new PortRun(engine, 1485);
        var transfer = new Lib1485.TransferStatus(easy, run.Context);

        long answered = transfer.Header("\r\n"u8);
        long line = transfer.Header("X: y\r\n"u8);
        long written = transfer.Write("abc"u8);

        Assert.AreEqual(expected, answered);
        Assert.AreEqual(6L, line);
        Assert.AreEqual(3L, written);
        Assert.AreEqual(3L, transfer.OutputLength);
        Assert.AreEqual(2, transfer.HeaderLines);
        Assert.AreEqual(expected == Lib1485.TransferStatus.WriteError && length != -1 ? 0L : status, transfer.HttpStatus);
        Assert.AreEqual(expectedError, run.Error.ToString());
        Assert.AreEqual("\r\nX: y\r\nabc", run.OutputText);
        await library.DisposeAsync();
    }

    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(false, (int)CurlCode.Obsolete44)]
    public async Task Lib1500_AfterTheLoop_AnswersTheTransfersCodeOrFailure(bool hasMessage, int expected)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.DoneResult = hasMessage ? CurlCode.Ok : null;
        var run = new PortRun(engine, 1500, "http://x/");

        LibTestResult result = await new Lib1500(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual("lib1500", new Lib1500(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task Lib1500_OnAFailingWait_WritesItOnStdoutAndEndsMajorBad()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        var run = new PortRun(engine, 1500, "http://x/");

        LibTestResult result = await new Lib1500(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("curl_multi_wait() returned 1\n", run.OutputText);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task Lib1500_OnAFailingStep_StopsWithItsCode(int failing)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = failing == 1 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.PerformResult = failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () => time.Advance(PastHangTimeout);
        var run = new PortRun(engine, 1500, "http://x/");

        LibTestResult result = await new Lib1500(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(failing == 3 ? (int)LibTestErrorCodes.RunsForever : (int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1501_PingsAndPongsAroundEachPerformInTheLoop()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 1501, "ftp://x/1501/");

        LibTestResult result = await new Lib1501(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("ping\npong = 0\nping\npong = 0\n", run.Error.ToString());
        Assert.AreEqual("lib1501", new Lib1501(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task Lib1501_WhenAPerformBlocksPastTheLimit_EndsTooLarge()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        int performs = 0;
        engine.Multi.Performing = () => time.Advance(TimeSpan.FromMilliseconds(performs++ == 0 ? 0 : 501));
        var run = new PortRun(engine, 1501, "ftp://x/1501/");

        LibTestResult result = await new Lib1501(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.TooLarge, result.ExitCode);
        Assert.AreEqual("ping\npong = 501\n", run.Error.ToString());
    }

    [TestMethod]
    [DataRow(1, (int)CurlCode.Obsolete32)]
    [DataRow(2, (int)CurlCode.Obsolete32)]
    [DataRow(3, (int)CurlCode.Obsolete24)]
    [DataRow(4, (int)CurlCode.Obsolete40)]
    [DataRow(5, (int)CurlCode.Obsolete24)]
    [DataRow(6, (int)CurlCode.Obsolete32)]
    [DataRow(7, (int)CurlCode.Obsolete24)]
    public async Task Lib1501_OnAFailingStep_StopsWithItsCode(int failing, int expected)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = failing == 1 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.PerformResults.Enqueue(failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 1));
        engine.Multi.PerformResult = failing == 6 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 1);
        engine.Multi.WaitResult = failing == 4 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 0);
        int performs = 0;
        engine.Multi.Performing = () => time.Advance(failing == 3 || (failing == 7 && performs++ == 1) ? TimeSpan.FromSeconds(31) : TimeSpan.Zero);
        engine.Multi.Waiting = () => time.Advance(failing == 5 ? TimeSpan.FromSeconds(31) : TimeSpan.Zero);
        var run = new PortRun(engine, 1501, "ftp://x/1501/");

        LibTestResult result = await new Lib1501(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
    }

    [TestMethod]
    [DataRow(1502)]
    [DataRow(1503)]
    [DataRow(1504)]
    [DataRow(1505)]
    public async Task Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder(int testNumber)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, testNumber, "http://google.com:8990/1502", "8990", "127.0.0.1");

        LibTestResult result = await new Lib1502(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("http://google.com:8990/1502", engine.Multi.Added!.Options.Network.Url);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual("lib1502", new Lib1502(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    [DataRow(1503)]
    [DataRow(1505)]
    public async Task Lib1502_OnARefusedAdd_RemovesAndCleansUp(int testNumber)
    {
        var engine = new FakeCurlEngine { Multi = { AddResult = CurlMultiCode.BadEasyHandle } };
        var run = new PortRun(engine, testNumber, "http://google.com:8990/1502", "8990", "127.0.0.1");

        LibTestResult result = await new Lib1502(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        Assert.AreEqual(1, engine.Multi.Removals, "the refused handle is removed from the multi handle");
        Assert.AreEqual(2, engine.DisposedEasyCount, "the original and the duplicated easy handle are both cleaned up");
    }

    [TestMethod]
    public async Task Lib1506_WritesTheEntriesAndRunsEachHandleInTurn()
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, 1506, "-", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1506(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(
            "server1.example.com:8990:127.0.0.1\nserver2.example.com:8990:127.0.0.1\nserver3.example.com:8990:127.0.0.1\nserver4.example.com:8990:127.0.0.1\n",
            run.OutputText);
        Assert.AreEqual("Start at URL 0\n", run.Error.ToString());
        Assert.AreEqual("http://server4.example.com:8990/path/15060004", engine.Multi.Added!.Options.Network.Url);
        Assert.AreEqual(3L, engine.Multi.Options.MaxConnects);
        Assert.AreEqual("lib1506", new Lib1506(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Lib1506_OnAFailingMultiCall_StopsWithMulti(int failing)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = failing == 1 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        engine.Multi.PerformResult = failing == 2 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 0);
        var run = new PortRun(engine, 1506, "-", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1506(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib1507_SetsTheMailAndAnAbortingReadThenRunsUntilDone()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 1507, "smtp://x/1507");

        LibTestResult result = await new Lib1507(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(ReadCallbackResult.Abort, engine.Multi.Added!.Options.Callbacks.Read!(new byte[4]));
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual("lib1507", new Lib1507(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task Lib1507_WhenWaitsFailPastTheLimit_SkipsThePerformsThenAborts()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        int performs = 0;
        engine.Multi.Performing = () => performs++;
        engine.Multi.Waiting = () => time.Advance(TimeSpan.FromSeconds(30));
        var run = new PortRun(engine, 1507, "smtp://x/1507");

        LibTestResult result = await new Lib1507(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(1, performs);
        Assert.AreEqual("ABORTING TEST, since it seems that it would have run forever.\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1507_OnARefusedAdd_StopsWithMulti()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 1507, "smtp://x/1507");

        LibTestResult result = await new Lib1507(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
    }

    [TestMethod]
    [DataRow(-1L, 1000)]
    [DataRow(0L, 0)]
    [DataRow(1999L, 1999)]
    [DataRow(2000L, 1000)]
    public void Lib1507SelectTimeout_CapsAtOneSecondFromTwoSeconds(long multiTimeout, int expected)
    {
        int timeout = Lib1507.SelectTimeout(multiTimeout);

        Assert.AreEqual(expected, timeout);
    }

    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(false, (int)CurlCode.FailedInit)]
    public async Task Lib1508_WritesWeAreDoneWhetherOrNotTheInitSucceeded(bool hasEngine, int expected)
    {
        var run = new PortRun(hasEngine ? new FakeCurlEngine() : null, 1508);

        LibTestResult result = await new Lib1508().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual("We are done\n", run.OutputText);
        Assert.AreEqual("lib1508", new Lib1508().ProgramName);
    }

    [TestMethod]
    public async Task Lib1509_WritesTheBodyAndBothHeaderLengths()
    {
        var engine = new FakeCurlEngine
        {
            Info = new TransferInfo { HeaderSize = 11 },
            Performing = o =>
            {
                _ = o.Callbacks.Header!("X: y\r\n"u8);
                _ = o.Callbacks.Header!("\r\n"u8);
                _ = o.Callbacks.Write!("body"u8);
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 1509, "http://the.old.moo.1509:8990/1509", "127.0.0.1:8991");

        LibTestResult result = await new Lib1509().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("bodyheader length is ........: 11\nheader length should be..: 8\n", run.OutputText);
        Assert.AreEqual("lib1509", new Lib1509().ProgramName);
    }

    [TestMethod]
    public async Task Lib1509_OnAFailingTransfer_WritesItAndEndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1509, "http://x/", "p:1");

        LibTestResult result = await new Lib1509().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        StringAssert.EndsWith(run.Error.ToString(), $" curl_easy_perform() failed, with code 7 ({Libcurl.Misc.ErrorText.CurlErrorText.Easy(CurlCode.CouldntConnect)})\n");
        StringAssert.StartsWith(run.Error.ToString(), "Lib1509.cs:");
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1510_PerformsEveryUrlOnTheOneHandle()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine { Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1510, "-", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1510(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "http://server1.example.com:8990/path/15100001", "http://server2.example.com:8990/path/15100002", "http://server3.example.com:8990/path/15100003", "http://server4.example.com:8990/path/15100004" },
            urls);
        StringAssert.StartsWith(run.OutputText, "server1.example.com:8990:127.0.0.1\n");
        Assert.AreEqual("lib1510", new Lib1510(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    [DataRow(false, (int)CurlCode.CouldntConnect)]
    [DataRow(true, (int)CurlCode.Obsolete24)]
    public async Task Lib1510_StopsAtAFailingTransferOrTheHangTimeout(bool passTheTimeout, int expected)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Performing = _ =>
            {
                performs++;
                time.Advance(passTheTimeout ? PastHangTimeout : TimeSpan.Zero);
                return passTheTimeout ? CurlCode.Ok : CurlCode.CouldntConnect;
            },
        };
        var run = new PortRun(engine, 1510, "-", "127.0.0.1", "8990");

        LibTestResult result = await new Lib1510(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    [DataRow(1L, 0L, (int)CurlCode.Obsolete41)]
    [DataRow(0L, 0L, (int)CurlCode.Obsolete44)]
    [DataRow(1L, 1L, (int)CurlCode.Obsolete44)]
    public async Task Lib1511_ChecksConditionUnmetAfterEachTransfer(long firstUnmet, long secondUnmet, int expected)
    {
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Performing = _ => { performs++; return CurlCode.Ok; },
            Informing = () => new TransferInfo { ConditionUnmet = performs == 1 ? firstUnmet : secondUnmet },
        };
        var run = new PortRun(engine, 1511, "http://x/1511");

        LibTestResult result = await new Lib1511().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
        Assert.AreEqual("lib1511", new Lib1511().ProgramName);
    }

    [TestMethod]
    public async Task Lib1511_OnAFailingTransfer_AnswersItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1511, "http://x/1511");

        LibTestResult result = await new Lib1511().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public void LibTestServerResolves_WritesEachEntryAndBuildsTheUrls()
    {
        var run = new PortRun(new FakeCurlEngine(), 1510, "-", "10.0.0.1", "80");

        var list = LibTestServerResolves.AppendAndWrite(run.Context, 2);
        var none = LibTestServerResolves.AppendAndWrite(run.Context, 0);
        string url = LibTestServerResolves.Url(run.Context, 12);

        CollectionAssert.AreEqual(new[] { "server1.example.com:80:10.0.0.1", "server2.example.com:80:10.0.0.1" }, list!.ToArray());
        Assert.IsNull(none);
        Assert.AreEqual("server1.example.com:80:10.0.0.1\nserver2.example.com:80:10.0.0.1\n", Encoding.UTF8.GetString(run.Output.ToArray()));
        Assert.AreEqual("http://server12.example.com:80/path/15100012", url);
    }
}
