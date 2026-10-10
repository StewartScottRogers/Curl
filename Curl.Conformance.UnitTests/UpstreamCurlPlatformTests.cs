using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins the features and null device <see cref="UpstreamCurlPlatform"/> reports per platform.</summary>
[TestClass]
public sealed class UpstreamCurlPlatformTests
{
    private static readonly string[] CommonFeatures =
    [
        "dict", "file", "gopher", "gophers", "http", "https", "ipfs", "mqtt", "telnet", "tftp", "ws", "wss",
        "alt-svc", "AsynchDNS", "brotli", "ECH", "GSS-API", "HSTS", "http/2", "h2c", "http/3", "HTTPS-proxy",
        "HTTPSRR", "IDN", "IPv6", "Kerberos", "Largefile", "libz", "NTLM", "PSL", "SPNEGO", "SSL", "TLS-SRP",
        "UnixSockets", "zstd",
        "crypto", "cookies", "proxy", "Mime", "manual", "DoH", "digest", "aws", "netrc", "verbose-strings",
        "large-time", "large-size", "sha512-256", "SSLpinning",
        "large_file", "local-http",
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Windows_ReportsTheSchannelBuildAndWin32()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "Windows");

        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Windows;

        bool featuresMatch = platform.Features.SetEquals([.. CommonFeatures, "win32", "Schannel"]);
        diagnostics.Act("feature count", platform.Features.Count);
        diagnostics.Act("null device", platform.NullDevice);
        diagnostics.Assert("features equal common + win32 + Schannel", true, featuresMatch);
        diagnostics.Assert("null device", "NUL", platform.NullDevice);
        Assert.IsTrue(featuresMatch);
        Assert.AreEqual("NUL", platform.NullDevice);
    }

    [TestMethod]
    public void Unix_ReportsTheOpenSslBuildAndExtendedAttributes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "Unix");

        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Unix;

        bool featuresMatch = platform.Features.SetEquals([.. CommonFeatures, "OpenSSL", "xattr"]);
        diagnostics.Act("feature count", platform.Features.Count);
        diagnostics.Act("null device", platform.NullDevice);
        diagnostics.Assert("features equal common + OpenSSL + xattr", true, featuresMatch);
        diagnostics.Assert("null device", "/dev/null", platform.NullDevice);
        Assert.IsTrue(featuresMatch);
        Assert.AreEqual("/dev/null", platform.NullDevice);
    }

    [TestMethod]
    [DataRow("Debug")]
    [DataRow("TrackMemory")]
    [DataRow("unittest")]
    [DataRow("headers-api")]
    [DataRow("ftp")]
    [DataRow("SSPI")]
    [DataRow("codeset-utf8")]
    public void BothPlatforms_LeaveOffWhatCurlLacks(string feature)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("feature", feature);

        bool onWindows = UpstreamCurlPlatform.Windows.Features.Contains(feature);
        bool onUnix = UpstreamCurlPlatform.Unix.Features.Contains(feature);

        diagnostics.Act("listed on Windows", onWindows);
        diagnostics.Act("listed on Unix", onUnix);
        diagnostics.Assert("listed on Windows", false, onWindows);
        diagnostics.Assert("listed on Unix", false, onUnix);
        Assert.IsFalse(UpstreamCurlPlatform.Windows.Features.Contains(feature));
        Assert.IsFalse(UpstreamCurlPlatform.Unix.Features.Contains(feature));
    }

    [TestMethod]
    public void OperatingSystemName_IsPerlsNameForEachPlatform()
    {
        Assert.AreEqual("MSWin32", UpstreamCurlPlatform.Windows.OperatingSystemName);
        Assert.AreEqual("linux", UpstreamCurlPlatform.Unix.OperatingSystemName);
        Assert.AreEqual("darwin", UpstreamCurlPlatform.MacOS.OperatingSystemName);
        Assert.IsTrue(UpstreamCurlPlatform.MacOS.Features.SetEquals(UpstreamCurlPlatform.Unix.Features));
        Assert.AreEqual("/dev/null", UpstreamCurlPlatform.MacOS.NullDevice);
    }
}
