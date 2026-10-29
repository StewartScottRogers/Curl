namespace Curl.Core;

/// <summary>Pins which environment variable <see cref="ProxySelector.TryFindProxyVariable" /> names (BL-3740).</summary>
[TestClass]
public sealed class ProxySelectorVariableTests
{
    [TestMethod]
    [DataRow("http", "http_proxy", "http_proxy")]
    [DataRow("https", "HTTPS_PROXY", "HTTPS_PROXY")]
    [DataRow("ftp", "all_proxy", "all_proxy")]
    [DataRow("ftp", "ALL_PROXY", "ALL_PROXY")]
    [DataRow("ws", "http_proxy", "http_proxy")]
    [DataRow("wss", "https_proxy", "https_proxy")]
    [DataRow("wss", "HTTPS_PROXY", "HTTPS_PROXY")]
    public void TryFindProxyVariable_VariableSet_NamesIt(string scheme, string variable, string expected)
    {
        ProxySelector selector = new(name => name == variable ? "http://p.test:1" : null);

        bool found = selector.TryFindProxyVariable(scheme, out string? name, out string? value);

        Assert.IsTrue(found);
        Assert.AreEqual(expected, name);
        Assert.AreEqual("http://p.test:1", value);
    }

    [TestMethod]
    public void TryFindProxyVariable_NoneSetOrEmpty_FindsNothing()
    {
        ProxySelector selector = new(name => name == "http_proxy" ? string.Empty : null);

        bool found = selector.TryFindProxyVariable("http", out string? name, out string? value);

        Assert.IsFalse(found);
        Assert.IsNull(name);
        Assert.IsNull(value);
    }
}
