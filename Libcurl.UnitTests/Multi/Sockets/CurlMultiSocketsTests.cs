using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Multi;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Multi.Sockets;

[TestClass]
public sealed class CurlMultiSocketsTests
{
    [TestMethod]
    public async Task SocketActionAsync_PassesTheSocketEventsAndTokenAndAnswersTheRunningHandles()
    {
        var engine = new FakeSocketMultiTransfer { SocketActionResult = (CurlMultiCode.Ok, 3) };
        await using var multi = new CurlMulti(engine);
        using var source = new CancellationTokenSource();

        var (code, running) = await multi.SocketActionAsync(new CurlSocket(42), CurlSocketSelect.In | CurlSocketSelect.Error, source.Token);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(3, running);
        Assert.AreEqual((new CurlSocket(42), CurlSocketSelect.In | CurlSocketSelect.Error, source.Token), engine.SocketAction);
    }

    [TestMethod]
    public async Task SocketActionAsync_TheTimeoutSocket_ReachesTheEngineAsCurlSocketBadAndAnswersItsCode()
    {
        var engine = new FakeSocketMultiTransfer { SocketActionResult = (CurlMultiCode.BadSocket, 0) };
        await using var multi = new CurlMulti(engine);

        var (code, running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None);

        Assert.AreEqual(CurlMultiCode.BadSocket, code);
        Assert.AreEqual(0, running);
        Assert.AreEqual((CurlSocket.Bad, CurlSocketSelect.None, CancellationToken.None), engine.SocketAction);
    }

    [TestMethod]
    public async Task Assign_PassesTheSocketAndStateAndAnswersTheEnginesCode()
    {
        var engine = new FakeSocketMultiTransfer { AssignResult = CurlMultiCode.BadSocket };
        await using var multi = new CurlMulti(engine);
        var state = new object();

        Assert.AreEqual(CurlMultiCode.BadSocket, multi.Assign(new CurlSocket(7), state));

        Assert.AreSame(state, engine.Assigned!.Value.State);
        Assert.AreEqual(new CurlSocket(7), engine.Assigned.Value.Socket);
    }

    [TestMethod]
    public async Task SetSocketFunction_TheEnginesCallback_CallsTheApplicationsWithTheAddedEasyHandle()
    {
        var engine = new FakeSocketMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        multi.AddHandle(easy);
        var state = new object();
        (CurlEasy Easy, CurlSocket Socket, CurlPoll What, object? State)? called = null;

        Assert.AreEqual(CurlMultiCode.Ok, multi.SetSocketFunction((handle, socket, what, socketState) =>
        {
            called = (handle, socket, what, socketState);
            return -1;
        }));

        Assert.AreEqual(-1, engine.Options.Socket!(transfer, new CurlSocket(9), CurlPoll.InOut, state));
        Assert.AreSame(easy, called!.Value.Easy);
        Assert.AreEqual(new CurlSocket(9), called.Value.Socket);
        Assert.AreEqual(CurlPoll.InOut, called.Value.What);
        Assert.AreSame(state, called.Value.State);
    }

    [TestMethod]
    public async Task SetTimerFunction_TheEnginesCallback_CallsTheApplicationsWithThisMultiHandle()
    {
        var engine = new FakeSocketMultiTransfer();
        await using var multi = new CurlMulti(engine);
        (CurlMulti Multi, long Timeout)? called = null;

        Assert.AreEqual(CurlMultiCode.Ok, multi.SetTimerFunction((handle, timeout) =>
        {
            called = (handle, timeout);
            return 0;
        }));

        Assert.AreEqual(0, engine.Options.Timer!(250));
        Assert.AreSame(multi, called!.Value.Multi);
        Assert.AreEqual(250, called.Value.Timeout);
    }

    [TestMethod]
    public async Task SetSocketAndTimerFunction_Null_ClearTheEnginesCallbacksAndKeepItsOtherOptions()
    {
        var engine = new FakeSocketMultiTransfer { Options = new MultiOptions { MaxConnects = 5 } };
        await using var multi = new CurlMulti(engine);
        multi.SetSocketFunction((_, _, _, _) => 0);
        multi.SetTimerFunction((_, _) => 0);

        Assert.AreEqual(CurlMultiCode.Ok, multi.SetSocketFunction(null));
        Assert.AreEqual(CurlMultiCode.Ok, multi.SetTimerFunction(null));

        Assert.IsNull(engine.Options.Socket);
        Assert.IsNull(engine.Options.Timer);
        Assert.AreEqual(5, engine.Options.MaxConnects);
    }

    [TestMethod]
    public async Task SetOption_LibcurlsSocketAndTimerCallbacks_ReachTheEngineWrappedAroundTheLibcurlHandles()
    {
        var engine = new FakeSocketMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        multi.AddHandle(easy);
        CurlEasy? socketEasy = null;
        CurlMulti? timerMulti = null;
        CurlMultiSocketCallback socketCallback = (handle, _, _, _) =>
        {
            socketEasy = handle;
            return 0;
        };
        CurlMultiTimerCallback timerCallback = (handle, _) =>
        {
            timerMulti = handle;
            return 0;
        };

        Assert.AreEqual(CurlMultiCode.Ok, multi.SetOption(CurlMultiOption.SocketFunction, socketCallback));
        Assert.AreEqual(CurlMultiCode.Ok, multi.SetOption(CurlMultiOption.TimerFunction, timerCallback));
        engine.Options.Socket!(transfer, new CurlSocket(3), CurlPoll.In, null);
        engine.Options.Timer!(10);

        Assert.AreSame(easy, socketEasy);
        Assert.AreSame(multi, timerMulti);
    }

    [TestMethod]
    public async Task SetOption_LibcurlsTimerCallbackForSocketFunction_AnswersBadFunctionArgument()
    {
        var engine = new FakeSocketMultiTransfer();
        await using var multi = new CurlMulti(engine);
        CurlMultiTimerCallback timerCallback = (_, _) => 0;

        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, multi.SetOption(CurlMultiOption.SocketFunction, timerCallback));

        Assert.IsNull(engine.Options.Socket);
    }

    [TestMethod]
    public async Task EveryMember_AfterCleanup_ThrowsObjectDisposedException()
    {
        var multi = new CurlMulti(new FakeSocketMultiTransfer());
        await multi.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.Assign(new CurlSocket(1), null));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.SetSocketFunction(null));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.SetTimerFunction(null));
    }
}
