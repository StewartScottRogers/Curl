using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Multi;

namespace Libcurl.UnitTests.Multi.Settings;

[TestClass]
public sealed class MultiOptionSettersTests
{
    private static readonly MultiSocketCallback SocketCallback = static (_, _, _, _) => 0;
    private static readonly MultiTimerCallback TimerCallback = static _ => 0;
    private static readonly MultiPushCallback PushCallback = static (_, _, _) => CurlPushResult.Ok;
    private static readonly MultiNotifyCallback NotifyCallback = static (_, _) => { };

    [TestMethod]
    public void SetOption_SocketFunction_SetsTheCallbackOrClearsItAndRefusesAnotherDelegate()
    {
        var set = Set(CurlMultiOption.SocketFunction, SocketCallback);
        var cleared = Set(CurlMultiOption.SocketFunction, null, set.Options);
        var refused = Set(CurlMultiOption.SocketFunction, TimerCallback);

        Assert.AreEqual(CurlMultiCode.Ok, set.Code);
        Assert.AreSame(SocketCallback, set.Options.Socket);
        Assert.IsNull(cleared.Options.Socket);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
        Assert.IsNull(refused.Options.Socket);
    }

    [TestMethod]
    public void SetOption_TimerFunction_SetsTheCallbackAndRefusesAnotherDelegate()
    {
        var set = Set(CurlMultiOption.TimerFunction, TimerCallback);
        var refused = Set(CurlMultiOption.TimerFunction, SocketCallback);

        Assert.AreSame(TimerCallback, set.Options.Timer);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    public void SetOption_PushFunction_SetsTheCallbackAndRefusesAnotherDelegate()
    {
        var set = Set(CurlMultiOption.PushFunction, PushCallback);
        var refused = Set(CurlMultiOption.PushFunction, NotifyCallback);

        Assert.AreSame(PushCallback, set.Options.Push);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    public void SetOption_NotifyFunction_SetsTheCallbackAndRefusesAnotherDelegate()
    {
        var set = Set(CurlMultiOption.NotifyFunction, NotifyCallback);
        var refused = Set(CurlMultiOption.NotifyFunction, PushCallback);

        Assert.AreSame(NotifyCallback, set.Options.Notify);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    public void SetOption_AFunctionGivenANonDelegate_AnswersBadFunctionArgument()
    {
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, Set(CurlMultiOption.SocketFunction, 1L).Code);
    }

    [TestMethod]
    [DataRow(CurlMultiOption.SocketData)]
    [DataRow(CurlMultiOption.TimerData)]
    [DataRow(CurlMultiOption.PushData)]
    [DataRow(CurlMultiOption.NotifyData)]
    public void SetOption_AUserDataOption_TakesAnyValueAndChangesNothing(CurlMultiOption option)
    {
        var set = Set(option, new object());
        var refused = Set(option, SocketCallback);

        Assert.AreEqual(CurlMultiCode.Ok, set.Code);
        Assert.AreEqual(new MultiOptions(), set.Options);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    public void SetOption_Pipelining_KeepsTheBitmaskFromALongOrTheEnum()
    {
        var none = Set(CurlMultiOption.Pipelining, 0L);
        var multiplex = Set(CurlMultiOption.Pipelining, CurlPipelining.Multiplex, none.Options);

        Assert.AreEqual(CurlMultiCode.Ok, none.Code);
        Assert.AreEqual(CurlPipelining.Nothing, none.Options.Pipelining);
        Assert.AreEqual(CurlPipelining.Multiplex, multiplex.Options.Pipelining);
    }

    [TestMethod]
    public void SetOption_PipeliningGivenAString_AnswersBadFunctionArgument()
    {
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, Set(CurlMultiOption.Pipelining, "2").Code);
    }

    [TestMethod]
    [DataRow(0L, CurlMultiCode.Ok, 0L)]
    [DataRow(4_294_967_295L, CurlMultiCode.Ok, 4_294_967_295L)]
    [DataRow(4_294_967_296L, CurlMultiCode.BadFunctionArgument, 0L)]
    [DataRow(-1L, CurlMultiCode.BadFunctionArgument, 0L)]
    public void SetOption_MaxConnects_TakesAnUnsignedIntAndRefusesTheRest(long value, CurlMultiCode expected, long kept)
    {
        var result = Set(CurlMultiOption.MaxConnects, value);

        Assert.AreEqual(expected, result.Code);
        Assert.AreEqual(kept, result.Options.MaxConnects);
    }

    [TestMethod]
    public void SetOption_MaxHostConnections_TakesACountAndRefusesANegativeOne()
    {
        var set = Set(CurlMultiOption.MaxHostConnections, 3);
        var refused = Set(CurlMultiOption.MaxHostConnections, -1);

        Assert.AreEqual(3, set.Options.MaxHostConnections);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    public void SetOption_MaxTotalConnections_TakesACountAndRefusesANegativeOne()
    {
        var set = Set(CurlMultiOption.MaxTotalConnections, long.MaxValue);
        var refused = Set(CurlMultiOption.MaxTotalConnections, -5L);

        Assert.AreEqual(long.MaxValue, set.Options.MaxTotalConnections);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    [DataRow(1L, 1L)]
    [DataRow(2_147_483_647L, 2_147_483_647L)]
    [DataRow(0L, 100L)]
    [DataRow(2_147_483_648L, 100L)]
    public void SetOption_MaxConcurrentStreams_KeepsOneToIntMaxElseOneHundred(long value, long kept)
    {
        var result = Set(CurlMultiOption.MaxConcurrentStreams, value, new MultiOptions { MaxConcurrentStreams = 7 });

        Assert.AreEqual(CurlMultiCode.Ok, result.Code);
        Assert.AreEqual(kept, result.Options.MaxConcurrentStreams);
    }

    [TestMethod]
    public void SetOption_NetworkChanged_KeepsTheBitmask()
    {
        var result = Set(CurlMultiOption.NetworkChanged, CurlNetworkChange.ClearConnections | CurlNetworkChange.ClearDns);

        Assert.AreEqual(CurlMultiCode.Ok, result.Code);
        Assert.AreEqual(CurlNetworkChange.ClearConnections | CurlNetworkChange.ClearDns, result.Options.NetworkChanged);
    }

    [TestMethod]
    public void SetOption_MaxPipelineLength_IsTakenAndIgnored()
    {
        var result = Set(CurlMultiOption.MaxPipelineLength, 5L);

        Assert.AreEqual(CurlMultiCode.Ok, result.Code);
        Assert.AreEqual(new MultiOptions(), result.Options);
    }

    [TestMethod]
    [DataRow(CurlMultiOption.ContentLengthPenaltySize)]
    [DataRow(CurlMultiOption.ChunkLengthPenaltySize)]
    public void SetOption_APenaltySize_TakesAnOffTAndIgnoresItButRefusesAnEnum(CurlMultiOption option)
    {
        var set = Set(option, 1024L);
        var refused = Set(option, CurlPipelining.Multiplex);

        Assert.AreEqual(CurlMultiCode.Ok, set.Code);
        Assert.AreEqual(new MultiOptions(), set.Options);
        Assert.AreEqual(CurlMultiCode.BadFunctionArgument, refused.Code);
    }

    [TestMethod]
    [DataRow(CurlMultiOption.PipeliningSiteBlocklist)]
    [DataRow(CurlMultiOption.PipeliningServerBlocklist)]
    public void SetOption_APipeliningBlocklist_IsTakenAndIgnored(CurlMultiOption option)
    {
        var result = Set(option, new[] { "example.com" });

        Assert.AreEqual(CurlMultiCode.Ok, result.Code);
        Assert.AreEqual(new MultiOptions(), result.Options);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(20)]
    [DataRow(10020)]
    public void SetOption_AnOptionMultiHLacks_AnswersUnknownOption(int option)
    {
        Assert.AreEqual(CurlMultiCode.UnknownOption, Set((CurlMultiOption)option, 1L).Code);
    }

    private static (CurlMultiCode Code, MultiOptions Options) Set(CurlMultiOption option, object? value, MultiOptions? start = null)
    {
        var options = start ?? new MultiOptions();
        var code = MultiOptionSetters.SetOption(ref options, option, value);
        return (code, options);
    }
}
