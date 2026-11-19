namespace Curl.Core;

/// <summary>Pins that the HTTPS-carried extension schemes read https_proxy and HTTPS_PROXY, and no other scheme but wss does (BL-5405, ADR-1297).</summary>
[TestClass]
public sealed class ProxySelectorHttpsExtensionSchemeTests
{
    private static string? Find(string scheme, params string[] set)
    {
        ProxySelector selector = new(name => Array.IndexOf(set, name) >= 0 ? "http://p.test:1" : null);
        return selector.TryFindProxyVariable(scheme, out string? name, out _) ? name : null;
    }

    [TestMethod]
    [DataRow("oci")]
    [DataRow("s3")]
    [DataRow("gs")]
    [DataRow("az")]
    [DataRow("abfs")]
    [DataRow("abfss")]
    [DataRow("mcps")]
    [DataRow("grpcs")]
    [DataRow("socketios")]
    [DataRow("signalrs")]
    public void TryFindProxyVariable_HttpsCarriedScheme_ReadsVariablesInOrder(string scheme)
    {
        string[] all = [scheme + "_proxy", (scheme + "_proxy").ToUpperInvariant(), "https_proxy", "HTTPS_PROXY", "all_proxy", "ALL_PROXY"];

        for (int i = 0; i < all.Length; i++)
        {
            Assert.AreEqual(all[i], Find(scheme, all[i..]));
        }
    }

    [TestMethod]
    [DataRow("oci")]
    [DataRow("signalrs")]
    public void TryFindProxyVariable_HttpsCarriedSchemeWithOnlyHttpProxy_FindsNothing(string scheme)
    {
        Assert.IsNull(Find(scheme, "http_proxy", "HTTP_PROXY"));
    }

    [TestMethod]
    [DataRow("ftp")]
    [DataRow("smtp")]
    [DataRow("imap")]
    [DataRow("http")]
    public void TryFindProxyVariable_NonHttpsScheme_DoesNotReadHttpsProxy(string scheme)
    {
        Assert.IsNull(Find(scheme, "https_proxy", "HTTPS_PROXY"));
    }

    [TestMethod]
    [DataRow("mcp")]
    [DataRow("grpc")]
    [DataRow("socketio")]
    [DataRow("signalr")]
    public void TryFindProxyVariable_CleartextExtensionScheme_FallsBackToHttpProxyLikeWs(string scheme)
    {
        Assert.AreEqual(scheme + "_proxy", Find(scheme, scheme + "_proxy", "http_proxy"));
        Assert.AreEqual("http_proxy", Find(scheme, "http_proxy", "all_proxy"));
        Assert.AreEqual("all_proxy", Find(scheme, "all_proxy"));
        Assert.IsNull(Find(scheme, "https_proxy"));
    }
}
