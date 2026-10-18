using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

[TestClass]
public sealed class UrlPortOverrideTests
{
    [TestMethod]
    [DataRow("ftp://127.0.0.1/521/", "ftp://127.0.0.1:2121/521/")]
    [DataRow("ftp://127.0.0.1/562;type=A", "ftp://127.0.0.1:2121/562;type=A")]
    [DataRow("http://h:999/x", "http://h:2121/x")]
    [DataRow("h/x", "http://h:2121/x")]
    public void Apply_PortSet_WritesItIntoTheUrl(string url, string expected)
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = url, Port = 2121 } };

        EasyOptions applied = UrlPortOverride.Apply(options);

        Assert.AreEqual(expected, applied.Network.Url);
    }

    [TestMethod]
    [DataRow(false, "http://h:81/b")]
    [DataRow(true, "http://h:81/a/../b")]
    public void Apply_PortSet_NormalizesThePathUnlessPathAsIs(bool pathAsIs, string expected)
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/a/../b", Port = 81, PathAsIs = pathAsIs } };

        EasyOptions applied = UrlPortOverride.Apply(options);

        Assert.AreEqual(expected, applied.Network.Url);
    }

    [TestMethod]
    [DataRow("http://h/x", 0, DisplayName = "No port set")]
    [DataRow(null, 8990, DisplayName = "No URL set")]
    [DataRow("http://[bad/x", 8990, DisplayName = "A URL that does not parse")]
    public void Apply_NothingToOverride_LeavesTheOptionsAsTheyAre(string? url, int port)
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = url, Port = port } };

        EasyOptions applied = UrlPortOverride.Apply(options);

        Assert.AreSame(options, applied);
    }
}
