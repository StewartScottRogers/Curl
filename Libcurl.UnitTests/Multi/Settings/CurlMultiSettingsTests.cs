using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Multi;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Multi.Settings;

[TestClass]
public sealed class CurlMultiSettingsTests
{
    [TestMethod]
    public async Task SetOption_AnAcceptedValue_WritesTheChangedOptionsToTheEngine()
    {
        var engine = new SettingsFakeMultiTransfer();
        await using var multi = new CurlMulti(engine);

        var code = multi.SetOption(CurlMultiOption.MaxHostConnections, 6L);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(new MultiOptions { MaxHostConnections = 6 }, engine.Options);
    }

    [TestMethod]
    public async Task SetOption_ARefusedValue_LeavesTheEnginesOptionsAsTheyWere()
    {
        var before = new MultiOptions { MaxConnects = 9 };
        var engine = new SettingsFakeMultiTransfer { Options = before };
        await using var multi = new CurlMulti(engine);

        var code = multi.SetOption(CurlMultiOption.MaxConnects, -1L);

        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, code);
        Assert.AreSame(before, engine.Options);
    }

    [TestMethod]
    public async Task SetOption_LibcurlsSocketCallbackForTimerFunction_AnswersBadFunctionArgument()
    {
        var before = new MultiOptions { MaxConnects = 9 };
        var engine = new SettingsFakeMultiTransfer { Options = before };
        await using var multi = new CurlMulti(engine);
        CurlMultiSocketCallback socketCallback = (_, _, _, _) => 0;

        var code = multi.SetOption(CurlMultiOption.TimerFunction, socketCallback);

        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, code);
        Assert.AreSame(before, engine.Options);
    }

    [TestMethod]
    public async Task GetHandles_AnswersTheAddedEasyHandlesAndLeavesOutTransfersAddedElsewhere()
    {
        var engine = new SettingsFakeMultiTransfer();
        engine.Extra.Add(new FakeEasyTransfer());
        await using var multi = new CurlMulti(engine);
        using var first = NewEasy();
        using var second = NewEasy();
        using var removed = NewEasy();
        multi.AddHandle(first);
        multi.AddHandle(removed);
        multi.AddHandle(second);
        multi.RemoveHandle(removed);

        var handles = multi.GetHandles();

        CollectionAssert.AreEqual(new[] { first, second }, handles.ToArray());
    }

    [TestMethod]
    public async Task WaitFds_FillsTheSocketsAndCountFromTheEngine()
    {
        var socket = new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, CurlWaitEvents.None);
        var engine = new SettingsFakeMultiTransfer { WaitSockets = [socket] };
        await using var multi = new CurlMulti(engine);
        var sockets = new CurlWaitSocket[2];

        var code = multi.WaitFds(sockets, out var count);
        var shortCode = multi.WaitFds([], out var countOnly);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(1, count);
        Assert.AreEqual(socket, sockets[0]);
        Assert.AreEqual(CurlMultiCode.OutOfMemory, shortCode);
        Assert.AreEqual(1, countOnly);
    }

    [TestMethod]
    [DataRow(CurlMultiInfo.TransfersCurrent, 3L)]
    [DataRow(CurlMultiInfo.TransfersRunning, 2L)]
    [DataRow(CurlMultiInfo.TransfersPending, 1L)]
    [DataRow(CurlMultiInfo.TransfersDone, 4L)]
    [DataRow(CurlMultiInfo.TransfersAdded, 10L)]
    public async Task GetInfo_ACountMultiHDefines_AnswersTheEnginesCount(CurlMultiInfo info, long expected)
    {
        var engine = new SettingsFakeMultiTransfer();
        engine.Counts[info] = expected;
        await using var multi = new CurlMulti(engine);

        var code = multi.GetInfo(info, out var value);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow(CurlMultiInfo.None)]
    [DataRow((CurlMultiInfo)6)]
    [DataRow((CurlMultiInfo)(-1))]
    public async Task GetInfo_NoneOrACountMultiHLacks_AnswersUnknownOptionAndMinusOneWithoutAskingTheEngine(CurlMultiInfo info)
    {
        var engine = new SettingsFakeMultiTransfer();
        await using var multi = new CurlMulti(engine);

        var code = multi.GetInfo(info, out var value);

        Assert.AreEqual(CurlMultiCode.UnknownOption, code);
        Assert.AreEqual(-1, value);
        Assert.IsEmpty(engine.InfoAsked);
    }

    [TestMethod]
    public async Task NotifyEnableAndDisable_PassEachNotificationToTheEngineAndAnswerItsCode()
    {
        var engine = new SettingsFakeMultiTransfer { NotifyResult = CurlMultiCode.BadFunctionArgument };
        await using var multi = new CurlMulti(engine);

        var enabled = multi.NotifyEnable(CurlMultiNotification.InfoRead);
        var disabled = multi.NotifyDisable(CurlMultiNotification.EasyDone);

        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, enabled);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, disabled);
        CollectionAssert.AreEqual(
            new[] { (CurlMultiNotification.InfoRead, true), (CurlMultiNotification.EasyDone, false) },
            engine.Notifications.ToArray());
    }

    [TestMethod]
    public async Task NotifyEnableAndDisable_ANotificationMultiHLacks_AnswersUnknownOptionWithoutAskingTheEngine()
    {
        var engine = new SettingsFakeMultiTransfer();
        await using var multi = new CurlMulti(engine);

        Assert.AreEqual(CurlMultiCode.UnknownOption, multi.NotifyEnable((CurlMultiNotification)2));
        Assert.AreEqual(CurlMultiCode.UnknownOption, multi.NotifyDisable((CurlMultiNotification)(-1)));

        Assert.IsEmpty(engine.Notifications);
    }

    [TestMethod]
    public async Task SettingsMembers_AfterCleanup_ThrowObjectDisposedException()
    {
        var multi = new CurlMulti(new SettingsFakeMultiTransfer());
        await multi.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.SetOption(CurlMultiOption.MaxConnects, 1L));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.GetHandles());
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.WaitFds([], out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.GetInfo(CurlMultiInfo.TransfersAdded, out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.NotifyEnable(CurlMultiNotification.InfoRead));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.NotifyDisable(CurlMultiNotification.InfoRead));
    }

    private static CurlEasy NewEasy() => new(new FakeEasyTransfer(), EasyOptionDispatch.Default);
}
