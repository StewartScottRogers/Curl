namespace Curl.Conformance;

/// <summary>Pins the features and null device <see cref="UpstreamCurlPlatform"/> reports per platform.</summary>
[TestClass]
public sealed class UpstreamCurlPlatformTests
{
    [TestMethod]
    public void Windows_ReportsTheSchannelBuildAndWin32()
    {
        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Windows;

        Assert.IsTrue(platform.Features.SetEquals(["dict", "file", "gopher", "gophers", "http", "https", "mqtt", "telnet", "tftp", "SSL", "large_file", "local-http", "win32", "Schannel"]));
        Assert.AreEqual("NUL", platform.NullDevice);
    }

    [TestMethod]
    public void Unix_ReportsTheOpenSslBuild()
    {
        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Unix;

        Assert.IsTrue(platform.Features.SetEquals(["dict", "file", "gopher", "gophers", "http", "https", "mqtt", "telnet", "tftp", "SSL", "large_file", "local-http", "OpenSSL"]));
        Assert.AreEqual("/dev/null", platform.NullDevice);
    }
}
