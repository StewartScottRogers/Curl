using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Proxy;
using Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1525To1534Tests
{
    private static readonly TimeSpan PastHangTimeout = TimeSpan.FromSeconds(61);

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtEasyInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1525, "http://x/", "127.0.0.1:8990");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1531.TestAsync(run.Context, run.Steps, library, clock, CancellationToken.None);
        await Lib1532.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1534.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await LibProxyTunnelUpload.Unified.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1528.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1529.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1530.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1533.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(8, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
    }

    [TestMethod]
    [DataRow("lib1525", "User-Agent: Http Agent", "User-Agent: Http Agent", HeaderOption.Unified)]
    [DataRow("lib1526", "User-Agent: Http Agent", "User-Agent: Proxy Agent|Expect:", HeaderOption.Separate)]
    [DataRow("lib1527", "User-Agent: Http Agent|Expect: 100-continue", "", HeaderOption.Unified)]
    public async Task ProxyTunnelUploads_TunnelTheUploadWithTheirHeaderLists(string programName, string serverHeaders, string proxyHeaders, HeaderOption headerOption)
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } };
        var run = new PortRun(engine, 1525, "http://x/1525", "127.0.0.1:8990");
        ILibTest program = LibTestPrograms.Ported.Find(programName)!;

        LibTestResult result = await program.RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(programName, program.ProgramName);
        Assert.AreEqual("http://x/1525", seen!.Network.Url);
        Assert.AreEqual("127.0.0.1:8990", seen.Proxy.Proxy);
        Assert.IsTrue(seen.Proxy.HttpProxyTunnel);
        Assert.AreEqual(headerOption, seen.Proxy.HeaderOption);
        Assert.AreEqual(serverHeaders, string.Join('|', seen.Http.HttpHeaders));
        Assert.AreEqual(proxyHeaders, string.Join('|', seen.Proxy.ProxyHeaders));
        Assert.IsTrue(seen.Transfer.Upload);
        Assert.AreEqual(13L, seen.Transfer.InFileSize);
        Assert.IsNotNull(seen.Callbacks.Read);
    }

    [TestMethod]
    public void HelloCloudReader_CopiesTheDataIntoABufferThatHoldsIt()
    {
        byte[] buffer = new byte[20];

        long count = LibProxyTunnelUpload.HelloCloudReader.Read(buffer);

        Assert.AreEqual(13L, count);
        Assert.AreEqual("Hello Cloud!\n", Encoding.ASCII.GetString(buffer, 0, 13));
    }

    [TestMethod]
    public void HelloCloudReader_IntoASmallerBuffer_CopiesNothingYetAnswersTheLength()
    {
        byte[] buffer = new byte[4];

        long count = LibProxyTunnelUpload.HelloCloudReader.Read(buffer);

        Assert.AreEqual(13L, count);
        CollectionAssert.AreEqual(new byte[4], buffer);
    }

    [TestMethod]
    public async Task Lib1528_SendsSeparateServerAndProxyHeadersThroughTheProxy()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.CouldntConnect; } };
        var run = new PortRun(engine, 1528, "http://x/1528", "127.0.0.1:8990");

        LibTestResult result = await new Lib1528().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("lib1528", new Lib1528().ProgramName);
        Assert.AreEqual("User-Agent: Http Agent", string.Join('|', seen!.Http.HttpHeaders));
        Assert.AreEqual("Proxy-User-Agent: Http Agent2", string.Join('|', seen.Proxy.ProxyHeaders));
        Assert.AreEqual(HeaderOption.Separate, seen.Proxy.HeaderOption);
        Assert.AreEqual("127.0.0.1:8990", seen.Proxy.Proxy);
    }

    [TestMethod]
    public async Task Lib1529_AppendsTheInjectedRequestToTheUrl()
    {
        string? url = null;
        var engine = new FakeCurlEngine { Performing = o => { url = o.Network.Url; return CurlCode.UrlMalformat; } };
        var run = new PortRun(engine, 1529, "http://the.old.moo:8990/1529", "127.0.0.1:8991");

        LibTestResult result = await new Lib1529().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("lib1529", new Lib1529().ProgramName);
        Assert.AreEqual("http://the.old.moo:8990/1529 HTTP/1.1\r\nGET http://1529.com/1529", url);
    }

    [TestMethod]
    public async Task Lib1530_SetsAnOpenSocketCallbackThatWritesAndGivesNoSocket()
    {
        string? url = null;
        CurlSocket? socket = null;
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                url = o.Network.Url;
                socket = o.Callbacks.OpenSocket!(CurlSocketType.IpConnection, new CurlSocketAddress(2, 1, 6, ReadOnlyMemory<byte>.Empty));
                return CurlCode.CouldntConnect;
            },
        };
        var run = new PortRun(engine, 1530);

        LibTestResult result = await new Lib1530().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("lib1530", new Lib1530().ProgramName);
        Assert.AreEqual(Lib1530.Url, url);
        Assert.AreEqual(CurlSocket.Bad, socket);
        Assert.AreEqual("opensocket() returns CURL_SOCKET_BAD\n", run.Error.ToString());
    }

    [TestMethod]
    public void Lib1531_WithoutATimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib1531(null!));

    [TestMethod]
    [DataRow(-1L, 1000)]
    [DataRow(0L, 0)]
    [DataRow(1999L, 1999)]
    [DataRow(2000L, 1000)]
    public void Lib1531SelectTimeout_IsTheMultiTimeoutUnderTwoSecondsElseOne(long multiTimeout, int expected) =>
        Assert.AreEqual(expected, Lib1531.SelectTimeoutMilliseconds(multiTimeout));

    [TestMethod]
    public async Task Lib1531_PostsTheBinaryDataAndWritesTheTransferStatus()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 0));
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, 5);
        int waits = 0;
        engine.Multi.Waiting = () => waits++;
        engine.Multi.DoneResult = CurlCode.Ok;
        var run = new PortRun(engine, 1531, "127.0.0.1:8990/1531");

        LibTestResult result = await new Lib1531(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);
        engine.Multi.WaitSockets = [new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, 0)];
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.DoneResult = CurlCode.PartialFile;
        LibTestResult second = await new Lib1531(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, second.ExitCode);
        Assert.AreEqual(1, waits);
        Assert.AreEqual("lib1531", new Lib1531(TimeProvider.System).ProgramName);
        Assert.AreEqual("HTTP transfer completed with status 0\nHTTP transfer completed with status 18\n", run.OutputText);
        EasyOptions options = engine.Multi.Added!.Options;
        Assert.AreEqual(8L, options.Http.PostFieldSize);
        CollectionAssert.AreEqual(Lib1531.PostData, options.Http.PostFields!.Value.ToArray());
    }

    [TestMethod]
    public async Task Lib1531_OnAFailingFdSet_WritesItAndReadsTheMessages()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitSocketsResult = CurlMultiCode.OutOfMemory;
        var run = new PortRun(engine, 1531, "127.0.0.1:8990/1531");

        LibTestResult result = await new Lib1531(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual($"curl_multi_fdset() failed, code {(int)CurlMultiCode.OutOfMemory}.\n", run.Error.ToString());
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Lib1531_PastTheHangTimeout_EndsWithoutReadingTheMessage(bool atFirstPerform)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        int performs = 0;
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitSockets = [new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, 0)];
        engine.Multi.Performing = () => time.Advance(atFirstPerform || performs++ == 1 ? PastHangTimeout : TimeSpan.Zero);
        engine.Multi.DoneResult = CurlCode.Ok;
        var run = new PortRun(engine, 1531, "127.0.0.1:8990/1531");

        LibTestResult result = await new Lib1531(time).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.RunsForever, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib1531_WhenTheLibraryGoesAtTheEasyHandle_StopsAtMultiInit()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), 1531, "http://x/");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib1531.TestAsync(run.Context, run.Steps, holder.Library, new LibTestClock(TimeProvider.System), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib1532_WithA200ThatTheResetZeroes_Succeeds()
    {
        var run = new PortRun(Answering(info => info with { ResponseCode = 200 }, info => info), 1532, "http://x/1532");

        LibTestResult result = await new Lib1532().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1532", new Lib1532().ProgramName);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(404L, 0L, "unexpected response code 404\n")]
    [DataRow(200L, 200L, "curl_easy_reset failed to zero the response code\npossible regression of github bug 1017\n")]
    public async Task Lib1532_WithAWrongCode_WritesItAndReturnsHttpReturnedError(long first, long afterReset, string message)
    {
        var run = new PortRun(Answering(info => info with { ResponseCode = first }, info => info with { ResponseCode = afterReset }), 1532, "http://x/1532");

        LibTestResult result = await new Lib1532().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.HttpReturnedError, result.ExitCode);
        StringAssert.EndsWith(run.Error.ToString(), message);
        StringAssert.StartsWith(run.Error.ToString(), "Lib1532.cs:");
    }

    [TestMethod]
    public async Task Lib1532_OnAFailingTransfer_ReportsItAndStops()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1532, "http://x/1532");

        LibTestResult result = await new Lib1532().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "curl_easy_perform() failed with code 7");
    }

    [TestMethod]
    public async Task Lib1533_FourRequestsWithTheExpectedConnections_SucceedsWithItsCode()
    {
        var seen = new List<bool>();
        var run = new PortRun(Connecting(seen, 1, 1, 1, 0), 1533, "http://x/1533");

        LibTestResult result = await new Lib1533().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Success, result.ExitCode);
        Assert.AreEqual("lib1533", new Lib1533().ProgramName);
        CollectionAssert.AreEqual(new[] { false, false, true, true }, seen);
        Assert.AreEqual(
            "First request without CURLOPT_KEEP_SENDING_ON_ERROR: expected: 1 connections; actual: 1 connections\n"
            + "Second request without CURLOPT_KEEP_SENDING_ON_ERROR: expected: 1 connections; actual: 1 connections\n"
            + "First request with CURLOPT_KEEP_SENDING_ON_ERROR: expected: 1 connections; actual: 1 connections\n"
            + "Second request with CURLOPT_KEEP_SENDING_ON_ERROR: expected: 0 connections; actual: 0 connections\n",
            run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1533_AWrongConnectionCount_StopsWithFailure()
    {
        var seen = new List<bool>();
        var run = new PortRun(Connecting(seen, 1, 0), 1533, "http://x/1533");

        LibTestResult result = await new Lib1533().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual(2, seen.Count);
    }

    [TestMethod]
    public async Task Lib1533_AFailingTransfer_WritesItAndStopsWithMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.RecvError }, 1533, "http://x/1533");

        LibTestResult result = await new Lib1533().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("curl_easy_perform() failed with 56\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1533PausedPoster_PausesUntilTheResponseThenSendsTheBodyAndUnpausesOnce()
    {
        var engine = new FakeCurlEngine();
        CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit()!;
        var poster = new Lib1533.PausedPoster(easy);
        byte[] buffer = new byte[2];

        long paused = poster.Read(buffer);
        long firstWrite = poster.Write("ab"u8);
        _ = poster.Write("c"u8);
        long first = poster.Read(buffer);
        string firstBytes = Encoding.ASCII.GetString(buffer);
        long second = poster.Read(buffer);
        long end = poster.Read(buffer);
        poster.Reset();
        long pausedAgain = poster.Read(buffer);

        Assert.AreEqual(ReadCallbackResult.Pause, paused);
        Assert.AreEqual(2L, firstWrite);
        Assert.AreEqual(2L, first);
        Assert.AreEqual("aa", firstBytes);
        Assert.AreEqual(1L, second);
        Assert.AreEqual(0L, end);
        Assert.AreEqual(ReadCallbackResult.Pause, pausedAgain);
        CollectionAssert.AreEqual(new[] { CurlPause.Continue }, engine.Pauses);
        await library.DisposeAsync();
    }

    [TestMethod]
    public async Task Lib1534_WithTheExpectedFiletimes_Succeeds()
    {
        bool filetimeSet = false;
        var run = new PortRun(Filetimes(o => filetimeSet = o.Transfer.FileTime, -1, 30, -1, -1), 1534, "http://x/1534");

        LibTestResult result = await new Lib1534().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1534", new Lib1534().ProgramName);
        Assert.IsTrue(filetimeSet);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(new long[] { 5 }, (int)CurlCode.FailedInit, "filetime init failed; expected -1 but is 5\n")]
    [DataRow(new long[] { -1, 7 }, (int)CurlCode.HttpReturnedError, "filetime of http resource is incorrect; expected 30 but is 7\n")]
    [DataRow(new long[] { -1, 30, 30 }, (int)CurlCode.FailedInit, "filetime init failed; expected -1 but is 30\n")]
    [DataRow(new long[] { -1, 30, -1, 30 }, (int)CurlCode.FailedInit, "filetime init failed; expected -1 but is 30\n")]
    public async Task Lib1534_WithAWrongFiletime_WritesItAndReturnsItsCode(long[] filetimes, int expectedCode, string message)
    {
        var run = new PortRun(Filetimes(_ => { }, filetimes), 1534, "http://x/1534");

        LibTestResult result = await new Lib1534().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expectedCode, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "Lib1534.cs:");
        StringAssert.EndsWith(run.Error.ToString(), message);
    }

    [TestMethod]
    public async Task Lib1534_OnAFailingTransfer_ReportsItAndStops()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1534, "http://x/1534");

        LibTestResult result = await new Lib1534().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "curl_easy_perform() failed with code 7");
    }

    // An engine whose first info read gives afterPerform's answer and every later one afterReset's.
    private static FakeCurlEngine Answering(Func<TransferInfo, TransferInfo> afterPerform, Func<TransferInfo, TransferInfo> afterReset)
    {
        int reads = 0;
        return new FakeCurlEngine { Informing = () => reads++ == 0 ? afterPerform(new TransferInfo()) : afterReset(new TransferInfo()) };
    }

    // An engine whose transfers record whether keep-sending-on-error was set and report the next connection count.
    private static FakeCurlEngine Connecting(List<bool> seen, params long[] connections)
    {
        int performs = 0;
        return new FakeCurlEngine
        {
            Performing = o => { seen.Add(o.Behaviour.KeepSendingOnError); performs++; return CurlCode.Ok; },
            Informing = () => new TransferInfo { NumConnects = connections[performs - 1] },
        };
    }

    // An engine whose info reads give the filetimes in turn, the last one again once they run out.
    private static FakeCurlEngine Filetimes(Action<EasyOptions> performing, params long[] filetimes)
    {
        int reads = 0;
        return new FakeCurlEngine
        {
            Performing = o => { performing(o); return CurlCode.Ok; },
            Informing = () => new TransferInfo { FileTime = filetimes[Math.Min(reads++, filetimes.Length - 1)] },
        };
    }
}
