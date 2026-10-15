using Curl.Api.EngineContracts.Multi;

namespace Curl.Api.UnitTests.EngineContracts.Multi;

[TestClass]
public sealed class MultiOptionsTests
{
    private static readonly MultiOptions Defaults = new();

    [TestMethod]
    public void New_ConnectionLimits_AreLibcurlsDefaults()
    {
        Assert.AreEqual(0, Defaults.MaxConnects);
        Assert.AreEqual(0, Defaults.MaxHostConnections);
        Assert.AreEqual(0, Defaults.MaxTotalConnections);
        Assert.AreEqual(100, Defaults.MaxConcurrentStreams);
    }

    [TestMethod]
    public void New_Pipelining_IsMultiplex()
    {
        Assert.AreEqual(CurlPipelining.Multiplex, Defaults.Pipelining);
    }

    [TestMethod]
    public void New_NetworkChanged_IsNone()
    {
        Assert.AreEqual(CurlNetworkChange.None, Defaults.NetworkChanged);
    }

    [TestMethod]
    public void New_Callbacks_AreUnset()
    {
        Assert.IsNull(Defaults.Socket);
        Assert.IsNull(Defaults.Timer);
        Assert.IsNull(Defaults.Push);
        Assert.IsNull(Defaults.Notify);
    }

    [TestMethod]
    public void New_TwoInstances_AreEqual()
    {
        Assert.AreEqual(new MultiOptions(), Defaults);
    }

    [TestMethod]
    public void Init_EveryProperty_KeepsItsValue()
    {
        MultiSocketCallback socket = (_, _, _, _) => 0;
        MultiTimerCallback timer = _ => 0;
        MultiPushCallback push = (_, _, _) => CurlPushResult.Deny;
        MultiNotifyCallback notify = (_, _) => { };

        var options = Defaults with
        {
            MaxConnects = 8,
            MaxHostConnections = 2,
            MaxTotalConnections = 16,
            MaxConcurrentStreams = 50,
            Pipelining = CurlPipelining.Nothing,
            NetworkChanged = CurlNetworkChange.ClearConnections | CurlNetworkChange.ClearDns,
            Socket = socket,
            Timer = timer,
            Push = push,
            Notify = notify,
        };

        Assert.AreEqual(8, options.MaxConnects);
        Assert.AreEqual(2, options.MaxHostConnections);
        Assert.AreEqual(16, options.MaxTotalConnections);
        Assert.AreEqual(50, options.MaxConcurrentStreams);
        Assert.AreEqual(CurlPipelining.Nothing, options.Pipelining);
        Assert.AreEqual(CurlNetworkChange.ClearConnections | CurlNetworkChange.ClearDns, options.NetworkChanged);
        Assert.AreSame(socket, options.Socket);
        Assert.AreSame(timer, options.Timer);
        Assert.AreSame(push, options.Push);
        Assert.AreSame(notify, options.Notify);
    }
}
