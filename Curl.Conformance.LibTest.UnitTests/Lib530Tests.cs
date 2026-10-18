using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib530Tests
{
    private static readonly CurlSocket First = new(5);
    private static readonly CurlSocket Second = new(7);

    [TestMethod]
    public async Task RunAsync_FiveRuns_OnlyTheFailingCallbacksAbortAndTheProgramSucceeds()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        int actions = 0;
        multi.SocketActing = (socket, _) => socket == CurlMultiSockets.Timeout
            ? Code(Timer(multi, 0) | Socket(multi, First, CurlPoll.In), ++actions)
            : Finish(multi, Code(Socket(multi, First, CurlPoll.Remove) | Timer(multi, -1), ++actions));
        var run = new PortRun(engine, 530, "http://127.0.0.1/file530");

        LibTestResult result = await new Lib530(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

        string error = run.Error.ToString();
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib530", new Lib530(TimeProvider.System).ProgramName);
        Assert.Contains("[T530-0-0] [0/0] start\n", error);
        Assert.Contains("[T530-0-1] [0/1] <- CURLMOPT_TIMERFUNCTION returns error\n", error);
        Assert.Contains("[T530-0-1] [1/1] curl error on timeout (11) ", error);
        Assert.Contains("[T530-0-2] [2/2] curl error on read (11)", error);
        Assert.Contains("[T530-2-0] [2/1] <- CURLMOPT_SOCKETFUNCTION returns error\n", error);
        Assert.Contains("[T530-3-0] [2/2] FAILED: 0\n", error);
        Assert.DoesNotContain("[T530-0-0] [2/2] FAILED", error);
        Assert.Contains("[T530-0-0] [1/1] add socket fd 5 for read\n", error);
        Assert.Contains("[T530-0-0] [2/1] remove socket fd 5\n", error);
        Assert.Contains("[T530-3-0] [2/2] done\n", error);
    }

    [TestMethod]
    public async Task TestAsync_FirstRunFails_WritesFailed()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.SocketActing = (_, _) => (CurlMultiCode.BadSocket, 0);
        var run = new PortRun(engine, 530, "http://127.0.0.1/file530");

        await Lib530.TestAsync(run.Context, CurlGlobal.Init(engine), new ManualTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None);

        Assert.Contains("[T530-0-0] [0/0] FAILED: 20\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task TestOneAsync_TimerElapsesAndTheTransferFails_ErrorGettingFile()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        int waits = 0;
        multi.Waiting = () => clock.Advance(TimeSpan.FromMilliseconds(++waits == 2 ? 50 : 0));
        int timeouts = 0;
        multi.SocketActing = (socket, _) => socket != CurlMultiSockets.Timeout
            ? (CurlMultiCode.Ok, 1)
            : ++timeouts == 1 ? Code(Timer(multi, 50) | Socket(multi, First, CurlPoll.InOut) | Socket(multi, Second, CurlPoll.In), 1) : Finish(multi, CurlCode.CouldntConnect);
        var (lib530, run) = Create(engine, clock);

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, result);
        Assert.Contains("Error getting file.\n", run.Error.ToString());
        Assert.Contains("add socket fd 5 for write\n", run.Error.ToString());
        Assert.HasCount(2, lib530.ReadSockets);
        Assert.HasCount(1, lib530.WriteSockets);
    }

    [TestMethod]
    public async Task TestOneAsync_TimeoutActionFails_BadTimeout()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        int timeouts = 0;
        multi.SocketActing = (socket, _) => socket != CurlMultiSockets.Timeout
            ? (CurlMultiCode.Ok, 1)
            : ++timeouts == 1 ? Code(Timer(multi, 0) | Socket(multi, First, CurlPoll.In), 0) : (CurlMultiCode.BadSocket, 0);
        var (lib530, _) = Create(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.BadTimeout, result);
    }

    [TestMethod]
    public async Task TestOneAsync_FirstOfTwoReadSocketsFails_MajorBadWithoutTheSecond()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        var acted = new List<CurlSocket>();
        multi.SocketActing = (socket, _) =>
        {
            acted.Add(socket);
            return socket == CurlMultiSockets.Timeout ? Code(Socket(multi, First, CurlPoll.In) | Socket(multi, Second, CurlPoll.In), 0) : (CurlMultiCode.BadSocket, 0);
        };
        var (lib530, run) = Create(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, result);
        CollectionAssert.AreEqual(new[] { CurlMultiSockets.Timeout, First }, acted);
        Assert.Contains("curl error on read (5)", run.Error.ToString());
    }

    [TestMethod]
    public async Task TestOneAsync_WriteSocketFails_MajorBad()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        multi.SocketActing = (socket, events) => socket == CurlMultiSockets.Timeout
            ? Code(Socket(multi, First, CurlPoll.Out), 0)
            : (events == CurlSocketSelect.Out ? CurlMultiCode.BadSocket : CurlMultiCode.Ok, 1);
        var (lib530, run) = Create(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, result);
        Assert.Contains("curl error on write (5)", run.Error.ToString());
    }

    [TestMethod]
    public async Task TestOneAsync_NoSocketToWaitOn_MaxFdZero()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.SocketActing = (_, _) => (CurlMultiCode.Ok, 1);
        var (lib530, run) = Create(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, result);
        Assert.Contains("[T530-0-0] [0/0] maxFd == 0\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task TestOneAsync_UnexpectedMessage_IsReportedAndFails()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        multi.DoneMessageType = CurlMultiMessageType.None;
        multi.SocketActing = (_, _) => Finish(multi, (CurlMultiCode.Ok, 0));
        var (lib530, run) = Create(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, result);
        Assert.Contains("got an unexpected message from curl: 0\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task TestOneAsync_SelectFails_SelectCode()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        multi.WaitResult = (CurlMultiCode.InternalError, 0);
        multi.SocketActing = (_, _) => Code(Socket(multi, First, CurlPoll.In), 0);
        var (lib530, _) = Create(engine, new ManualTimeProvider(DateTimeOffset.UnixEpoch));

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Select, result);
    }

    [TestMethod]
    public async Task TestOneAsync_TransferHangs_RunsForever()
    {
        var engine = new FakeCurlEngine();
        var multi = engine.Multi;
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        multi.Waiting = () => clock.Advance(TimeSpan.FromMinutes(2));
        multi.SocketActing = (socket, _) => socket == CurlMultiSockets.Timeout ? Code(Socket(multi, First, CurlPoll.In), 0) : (CurlMultiCode.Ok, 1);
        var (lib530, _) = Create(engine, clock);

        CurlCode result = await lib530.TestOneAsync(CurlGlobal.Init(engine), CancellationToken.None);

        Assert.AreNotEqual(CurlCode.Ok, result);
    }

    [TestMethod]
    public async Task TestOneAsync_HandleStepsFail_CleanUpWithTheirCodes()
    {
        var addFails = new FakeCurlEngine();
        addFails.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var holder = new PortRun.LibraryHolder();
        var multiFails = PortRun.CleaningUpOnEasy(holder, 1);
        holder.Library = CurlGlobal.Init(multiFails);
        var (easyRun, easyPort) = Create(new FakeCurlEngine(), TimeProvider.System);

        CurlCode added = await Create(addFails, TimeProvider.System).Run.TestOneAsync(CurlGlobal.Init(addFails), CancellationToken.None);
        CurlCode multiInit = await Create(multiFails, TimeProvider.System).Run.TestOneAsync(holder.Library, CancellationToken.None);
        CurlCode easyInit = await easyRun.TestOneAsync(await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Multi, added);
        Assert.AreEqual(LibTestErrorCodes.Multi, multiInit);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, easyInit);
        Assert.Contains("[T530-0-0] [0/0] cleanup\n[T530-0-0] [0/0] done\n", easyPort.Error.ToString());
    }

    [TestMethod]
    public void Constructors_NullArguments_Throw()
    {
        var run = new PortRun(new FakeCurlEngine(), 530, "u");

        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib530(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib530Run(null!, TimeProvider.System, 0, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib530Run(run.Context, null!, 0, 0));
    }

    private static (Lib530Run Run, PortRun Port) Create(FakeCurlEngine engine, TimeProvider clock)
    {
        var port = new PortRun(engine, 530, "http://127.0.0.1/file530");
        return (new Lib530Run(port.Context, clock, 0, 0), port);
    }

    private static int Timer(FakeMultiTransfer multi, long milliseconds) => multi.Options.Timer!(milliseconds);

    private static int Socket(FakeMultiTransfer multi, CurlSocket socket, CurlPoll what) => multi.Options.Socket!(multi.Added!, socket, what, null);

    private static (CurlMultiCode Code, int RunningTransfers) Code(int callbacks, int running) =>
        (callbacks == 0 ? CurlMultiCode.Ok : CurlMultiCode.AbortedByCallback, running);

    private static (CurlMultiCode Code, int RunningTransfers) Finish(FakeMultiTransfer multi, CurlCode result)
    {
        multi.DoneResult = result;
        return (CurlMultiCode.Ok, 0);
    }

    private static (CurlMultiCode Code, int RunningTransfers) Finish(FakeMultiTransfer multi, (CurlMultiCode Code, int RunningTransfers) answer)
    {
        if (answer.Code == CurlMultiCode.Ok)
        {
            multi.DoneResult = CurlCode.Ok;
        }

        return answer;
    }
}
