using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>Pins that an <c>oci://</c> transfer with only HTTPS_PROXY set goes through that proxy unless no-proxy matches (BL-5405, ADR-1296).</summary>
[TestClass]
public sealed class TransferProxySelectionHttpsExtensionTests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("oci://reg.test/lib/app:v1");

    private static ProxySelector OnlyHttpsProxy() =>
        new(name => name == "HTTPS_PROXY" ? "http://127.0.0.1:43009" : null);

    [TestMethod]
    public void TrySelect_OciUrlWithOnlyHttpsProxySet_SelectsThatProxy()
    {
        bool selected = TransferProxySelection.TrySelect(OnlyHttpsProxy(), new EasyProxyOptions(), false, null, Url, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsTrue(selected);
        Assert.IsNull(failure);
        Assert.IsNotNull(proxy);
        Assert.AreEqual(43009, proxy.Port);
    }

    [TestMethod]
    public void TrySelect_OciUrlMatchingNoProxyOption_GoesDirect()
    {
        EasyProxyOptions options = new() { NoProxy = "reg.test" };

        TransferProxySelection.TrySelect(OnlyHttpsProxy(), options, false, null, Url, out ProxyEndpoint? proxy, out _);

        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_OciUrlMatchingNoProxyVariable_GoesDirect()
    {
        ProxySelector selector = new(name => name switch { "HTTPS_PROXY" => "http://127.0.0.1:43009", "NO_PROXY" => "reg.test", _ => null });

        TransferProxySelection.TrySelect(selector, new EasyProxyOptions(), false, null, Url, out ProxyEndpoint? proxy, out _);

        Assert.IsNull(proxy);
    }
}
