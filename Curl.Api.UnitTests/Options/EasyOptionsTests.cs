using Curl.Api.Callbacks;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Api.UnitTests.Options;

[TestClass]
public sealed class EasyOptionsTests
{
    [TestMethod]
    public void New_EqualsOneBuiltFromEveryGroupsDefaultRecord()
    {
        var builtFromDefaults = new EasyOptions
        {
            Behaviour = new EasyBehaviourOptions(),
            Transfer = new EasyTransferOptions(),
            Network = new EasyNetworkOptions(),
            Tls = new EasyTlsOptions(),
            Authentication = new EasyAuthenticationOptions(),
            Proxy = new EasyProxyOptions(),
            Http = new EasyHttpOptions(),
            Protocols = new EasyProtocolOptions(),
            Callbacks = new EasyCallbackOptions(),
        };

        Assert.AreEqual(builtFromDefaults, new EasyOptions());
    }

    [TestMethod]
    public void New_EveryGroup_EqualsItsDefaultRecord()
    {
        var options = new EasyOptions();

        Assert.AreEqual(new EasyBehaviourOptions(), options.Behaviour);
        Assert.AreEqual(new EasyTransferOptions(), options.Transfer);
        Assert.AreEqual(new EasyNetworkOptions(), options.Network);
        Assert.AreEqual(new EasyTlsOptions(), options.Tls);
        Assert.AreEqual(new EasyAuthenticationOptions(), options.Authentication);
        Assert.AreEqual(new EasyProxyOptions(), options.Proxy);
        Assert.AreEqual(new EasyHttpOptions(), options.Http);
        Assert.AreEqual(new EasyProtocolOptions(), options.Protocols);
        Assert.AreEqual(new EasyCallbackOptions(), options.Callbacks);
    }

    [TestMethod]
    public void With_OneGroupChanged_NoLongerEqualsTheDefault()
    {
        var changed = new EasyOptions() with { Network = new EasyNetworkOptions { Url = "http://example.com/" } };

        Assert.AreNotEqual(new EasyOptions(), changed);
        Assert.AreEqual("http://example.com/", changed.Network.Url);
    }
}
