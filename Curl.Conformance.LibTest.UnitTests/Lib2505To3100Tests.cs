using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib2505To3100Tests
{
    [TestMethod]
    public void ProgramNames_AreTheUpstreamNames()
    {
        ILibTest[] ports =
        [
            new Lib2505(), new Lib2506(), new Lib2700(TimeProvider.System), new Lib3010(), new Lib3025(), new Lib3026(),
            new Lib3027(), new Lib3033(TimeProvider.System), new Lib3034(), new Lib3100(),
        ];

        CollectionAssert.AreEqual(
            new[] { "lib2505", "lib2506", "lib2700", "lib3010", "lib3025", "lib3026", "lib3027", "lib3033", "lib3034", "lib3100" },
            ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task Constructors_WithoutTheirDependencies_Throw()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit()!;
        var context = new LibTestContext(2700, []);

        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2700(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib3033(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib3026(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2700Frames(null!, easy));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib2700Frames(context, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib2700.ReportFailure(null!, "curl_ws_recv", CurlCode.Ok));
    }

    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 2505, "http://x/");

        await Lib2700.TestAsync(run.Context, run.Steps, library, TimeProvider.System, CancellationToken.None);
        await Lib3034.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib3033.TestAsync(run.Context, run.Steps, library, TimeProvider.System, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib3027.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        await Lib3010.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        run.Steps.Result = CurlCode.Ok;
        await Lib2505.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib2506.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib3025.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib3100.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);

        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib3033_WhenTheLibraryGoesAtTheMultiHandle_StopsAtEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        var run = new PortRun(engine, 3033, "http://x/");
        holder.Library = CurlGlobal.Init(engine);

        await Lib3033.TestAsync(run.Context, run.Steps, holder.Library, TimeProvider.System, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib2505_FollowsThenDoesNotAndWritesEachCode()
    {
        var seen = new List<EasyOptions>();
        var engine = new FakeCurlEngine { Performing = o => { seen.Add(o); return seen.Count == 1 ? CurlCode.Ok : CurlCode.RecvError; } };
        var run = new PortRun(engine, 2505, "http://x/");

        LibTestResult result = await new Lib2505().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        Assert.AreEqual("req1=0\nreq2=56\n", run.OutputText);
        Assert.IsTrue(seen[0].Http.AutoReferer);
        Assert.AreNotEqual(seen[0].Http.FollowLocation, seen[1].Http.FollowLocation);
    }

    [TestMethod]
    public async Task Lib2506_SetsTheProxyNetrcAndUnrestrictedAuthentication()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.SendError; } };
        var run = new PortRun(engine, 2506, "http://proxy/", "http://site.example/", "netrc2506");

        LibTestResult result = await new Lib2506().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.AreEqual("http://proxy/", seen!.Proxy.Proxy);
        Assert.AreEqual(NetrcUsage.Optional, seen.Authentication.Netrc);
        Assert.AreEqual("netrc2506", seen.Authentication.NetrcFile);
        Assert.IsTrue(seen.Authentication.UnrestrictedAuthentication);
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.Text, "txt fin")]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, "txt ---")]
    [DataRow(CurlWebSocketFlags.Binary, "bin fin")]
    [DataRow(CurlWebSocketFlags.Binary | CurlWebSocketFlags.Continuation, "bin ---")]
    [DataRow(CurlWebSocketFlags.Ping, "ping")]
    [DataRow(CurlWebSocketFlags.Pong, "pong")]
    [DataRow(CurlWebSocketFlags.Close, "close")]
    [DataRow(CurlWebSocketFlags.None, "")]
    public void Lib2700_DescribeFlags_NamesTheFrameKind(CurlWebSocketFlags flags, string expected)
    {
        string described = Lib2700.DescribeFlags(flags);

        Assert.AreEqual(expected, described);
    }

    [TestMethod]
    public async Task Lib2700_EchoesEachFrameUntilAClose_RetryingWhatAnswersAgain()
    {
        var socket = new FakeEasyWebSocket { SendLimit = 2 };
        socket.FrameReceives.Enqueue((CurlCode.Again, default, string.Empty));
        socket.FrameReceives.Enqueue((CurlCode.Ok, new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 3, 3), string.Empty));
        socket.FrameReceives.Enqueue((CurlCode.Again, default, string.Empty));
        socket.FrameReceives.Enqueue((CurlCode.Ok, new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 3), "txt"));
        socket.FrameReceives.Enqueue((CurlCode.Ok, new CurlWebSocketFrame(0, CurlWebSocketFlags.Close, 0, 0, 0), string.Empty));
        socket.SendResults.Enqueue(CurlCode.Again);
        socket.SendResults.Enqueue(CurlCode.Ok);
        socket.SendResults.Enqueue(CurlCode.Again);
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket, Performing = o => { seen = o; return CurlCode.Ok; } }, 2700, "ws://x/2700");

        LibTestResult result = await new Lib2700(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("txt fin [3] txt\nclose [0]\n", run.OutputText);
        Assert.AreEqual(WebSocketOptionBits.NoAutoPong, seen!.Protocols.WebSocket.Options);
        CollectionAssert.AreEqual(
            new[] { "", "", "", "tx", "t", "" },
            socket.Sent.Select(sent => sent.Payload).ToArray());
        Assert.AreEqual(CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, socket.Sent[1].Flags);
        Assert.AreEqual(3, socket.FragmentSizes[1]);
        Assert.AreEqual(CurlWebSocketFlags.Text, socket.Sent[3].Flags);
        Assert.AreEqual(CurlWebSocketFlags.Close | CurlWebSocketFlags.Offset, socket.Sent[5].Flags);
    }

    [TestMethod]
    public async Task Lib2700_ForCase2708_KeepsTheAutomaticPong()
    {
        var socket = new FakeEasyWebSocket();
        socket.FrameReceives.Enqueue((CurlCode.Ok, new CurlWebSocketFrame(0, CurlWebSocketFlags.Close, 0, 0, 0), string.Empty));
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket, Performing = o => { seen = o; return CurlCode.Ok; } }, Lib2700.AutoPongCase, "ws://x/2708");

        LibTestResult result = await new Lib2700(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(WebSocketOptionBits.None, seen!.Protocols.WebSocket.Options);
    }

    [TestMethod]
    public async Task Lib2700_WhenThePerformFails_ReportsItAndEndsWithItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 2700, "ws://x/2700");

        LibTestResult result = await new Lib2700(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), " curl_easy_perform() failed with code 7 (");
    }

    [TestMethod]
    [DataRow(0, "curl_ws_recv() failed with code 56", CurlCode.RecvError)]
    [DataRow(1, "curl_ws_send() failed with code 55", CurlCode.SendError)]
    [DataRow(2, "curl_ws_recv() failed with code 56", CurlCode.RecvError)]
    [DataRow(3, "curl_ws_send() failed with code 55", CurlCode.SendError)]
    public async Task Lib2700_WhenAReceiveOrSendFails_ReportsItAndEndsWithItsCode(int failingStep, string line, CurlCode expected)
    {
        var socket = new FakeEasyWebSocket();
        socket.FrameReceives.Enqueue(failingStep == 0
            ? (CurlCode.RecvError, default, string.Empty)
            : (CurlCode.Ok, new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 3, 3), string.Empty));
        socket.FrameReceives.Enqueue(failingStep == 2
            ? (CurlCode.RecvError, default, string.Empty)
            : (CurlCode.Ok, new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 0, 3), "bin"));
        socket.SendResults.Enqueue(failingStep == 1 ? CurlCode.SendError : CurlCode.Ok);
        socket.SendResults.Enqueue(CurlCode.SendError);
        var run = new PortRun(new FakeCurlEngine { WebSocket = socket }, 2700, "ws://x/2700");

        LibTestResult result = await new Lib2700(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)expected, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), line);
    }

    [TestMethod]
    public async Task Lib3010_WritesTheRetryAfterBeforeAndAfterTheReset()
    {
        var urls = new List<string?>();
        var engine = new FakeCurlEngine
        {
            Informing = () => new TransferInfo { RedirectUrl = "http://x/follow", RetryAfter = urls.Count < 2 ? TimeSpan.FromSeconds(2) : TimeSpan.Zero },
            Performing = o => { urls.Add(o.Network.Url); return CurlCode.Ok; },
        };
        var run = new PortRun(engine, 3010, "http://x/3010");

        LibTestResult result = await new Lib3010().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Retry-After 2\nRetry-After 0\n", run.OutputText);
        CollectionAssert.AreEqual(new[] { "http://x/3010", "http://x/follow" }, urls);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Lib3010_WhenATransferFails_ReportsItAndEndsWithItsCode(int failingTransfer)
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == failingTransfer ? CurlCode.CouldntConnect : CurlCode.Ok };
        var run = new PortRun(engine, 3010, "http://x/3010");

        LibTestResult result = await new Lib3010().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(failingTransfer, performs);
        StringAssert.Contains(run.Error.ToString(), "curl_easy_perform() failed with code 7 (");
    }

    [TestMethod]
    public async Task Lib3025_SetsTheIcyAliasAndHeaders()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 3025, "http://x/3025");

        LibTestResult result = await new Lib3025().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(new[] { Lib3025.Alias }, seen!.Http.Http200Aliases.ToArray());
        Assert.IsTrue(seen.Behaviour.Verbose);
    }

    [TestMethod]
    public async Task Lib3026_WithoutTheThreadSafeFeature_SaysSoAndEndsMajorBad()
    {
        int calls = 0;
        var run = new PortRun(new FakeCurlEngine(), 3026);

        LibTestResult result = await new Lib3026(() => { Interlocked.Increment(ref calls); return ValueTask.FromResult(CurlCode.Ok); }).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual(0, calls);
        StringAssert.Contains(run.Error.ToString(), "CURL_VERSION_THREADSAFE feature flag is not set\n");
    }

    [TestMethod]
    public async Task Lib3026_WithTheThreadSafeFeature_InitsOncePerThread()
    {
        int calls = 0;
        var run = new PortRun(new FakeCurlEngine { VersionInfo = new CurlVersionInfo { Features = ["threadsafe"] } }, 3026);

        LibTestResult result = await new Lib3026(() => { Interlocked.Increment(ref calls); return ValueTask.FromResult(CurlCode.Ok); }).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Lib3026.ThreadCount, calls);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib3026_WhenAThreadsInitFails_ReportsEachAndEndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { VersionInfo = new CurlVersionInfo { Features = ["threadsafe"] } }, 3026);

        LibTestResult result = await new Lib3026(() => ValueTask.FromResult(CurlCode.FailedInit)).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "thread[99]: curl_global_init() failed,with code 2 (");
    }

    [TestMethod]
    public async Task Lib3026_InitAndCleanUpAsync_InitsARealLibrary()
    {
        CurlCode code = await Lib3026.InitAndCleanUpAsync();

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("lib3026", new Lib3026().ProgramName);
    }

    [TestMethod]
    [DataRow(-1L, 0)]
    [DataRow(5L, (int)CurlCode.UnsupportedProtocol)]
    public async Task Lib3027_EndsByWhetherTheFileTimeIsUnknown(long fileTime, int expected)
    {
        var run = new PortRun(new FakeCurlEngine { Info = new TransferInfo { FileTime = fileTime } }, 3027, "ftp://x/3027");

        LibTestResult result = await new Lib3027().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expected, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib3027_WhenTheTransferFails_EndsWithItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.FtpCouldntRetrFile, Info = new TransferInfo { FileTime = 5 } }, 3027, "ftp://x/3027");

        LibTestResult result = await new Lib3027().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.FtpCouldntRetrFile, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib3033_ClearsTheConnectionsBeforeTheSecondTransfer()
    {
        var (engine, adds) = EngineCountingAdds(addNumber => addNumber == 3 ? 0 : 1);
        engine.Multi.EachAddedDoneResult = CurlCode.Ok;
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        var run = new PortRun(engine, 3033, "http://x/3033");

        LibTestResult result = await new Lib3033(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(3, adds());
        Assert.AreEqual("[0] no network change\n[1] signal network change\n[2] no network change\n", run.OutputText);
        Assert.AreEqual(CurlNetworkChange.ClearConnections, engine.Multi.Options.NetworkChanged);
    }

    [TestMethod]
    [DataRow(0L, "[1] signal network change\n[1] should not reuse connection in pool\n")]
    [DataRow(1L, "[1] signal network change\n[2] no network change\n[2] should have reused connection from [1]\n")]
    public async Task Lib3033_WhenTheConnectionCountIsWrong_SaysSoAndEndsMajorBad(long laterConnects, string outputEnd)
    {
        var (engine, _) = EngineCountingAdds(addNumber => addNumber == 1 ? 1 : laterConnects);
        engine.Multi.EachAddedDoneResult = CurlCode.Ok;
        var run = new PortRun(engine, 3033, "http://x/3033");

        LibTestResult result = await new Lib3033(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("[0] no network change\n" + outputEnd, run.OutputText);
    }

    [TestMethod]
    public async Task Lib3033_WhenTheWaitFails_ReportsItAndEndsMajorBad()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        var run = new PortRun(engine, 3033, "http://x/3033");

        LibTestResult result = await new Lib3033(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), $"curl_multi_wait() returned {(int)CurlMultiCode.BadHandle}\n");
    }

    [TestMethod]
    public async Task Lib3033_WhenATransferFails_ReportsItAndEndsWithItsCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.EachAddedDoneResult = CurlCode.RecvError;
        var run = new PortRun(engine, 3033, "http://x/3033");

        LibTestResult result = await new Lib3033(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.RecvError, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "curl_multi_info_read() returned 56\n");
    }

    [TestMethod]
    public async Task Lib3033_SkipsAMessageThatIsNotDone()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.DoneResult = CurlCode.Ok;
        engine.Multi.DoneMessageType = CurlMultiMessageType.None;
        var run = new PortRun(engine, 3033, "http://x/3033");

        LibTestResult result = await new Lib3033(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task Lib3034_ExpectsTheRewindFailureThenSucceedsAfterTheReset()
    {
        var seen = new List<EasyOptions>();
        var engine = new FakeCurlEngine { Performing = o => { seen.Add(o); return seen.Count == 1 ? CurlCode.SendFailRewind : CurlCode.Ok; } };
        var run = new PortRun(engine, 3034, "http://x/3034");

        LibTestResult result = await new Lib3034().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.HasCount(2, seen);
        Assert.IsNotNull(seen[0].Callbacks.Read);
        Assert.IsNull(seen[1].Callbacks.Read);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib3034_WhenTheFirstTransferDoesNotFailToRewind_ReportsItAndEndsWithItsCode()
    {
        var run = new PortRun(new FakeCurlEngine(), 3034, "http://x/3034");

        LibTestResult result = await new Lib3034().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), " curl_easy_perform() failed with code 0 (");
    }

    [TestMethod]
    [DataRow(10, "hello\0")]
    [DataRow(3, "hel")]
    public void Lib3034_Read_CopiesWhatFitsOfThePayload(int size, string expected)
    {
        byte[] buffer = new byte[size];

        long copied = Lib3034.Read(buffer);

        Assert.AreEqual(expected, System.Text.Encoding.ASCII.GetString(buffer, 0, (int)copied));
    }

    [TestMethod]
    public async Task Lib3100_DescribesWithAnyAuthentication()
    {
        EasyOptions? seen = null;
        var run = new PortRun(new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; } }, 3100, "rtsp://x/3100");

        LibTestResult result = await new Lib3100().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(RtspRequest.Describe, seen!.Protocols.Rtsp.Request);
        Assert.AreEqual("rtsp://x/3100", seen.Protocols.Rtsp.StreamUri);
        Assert.AreEqual("user", seen.Authentication.UserName);
    }

    [TestMethod]
    public async Task Lib3100_WhenTheDescribeFails_SaysSoAndEndsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.RtspCseqError }, 3100, "rtsp://x/3100");

        LibTestResult result = await new Lib3100().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual($"Failed to send DESCRIBE: {(int)CurlCode.RtspCseqError}\n", run.Error.ToString());
    }

    private static (FakeCurlEngine Engine, Func<int> Adds) EngineCountingAdds(Func<int, long> connectsForAdd)
    {
        int adds = 0;
        var engine = new FakeCurlEngine { Informing = () => new TransferInfo { NumConnects = connectsForAdd(adds) } };
        engine.Multi.Adding = () => adds++;
        return (engine, () => adds);
    }
}
