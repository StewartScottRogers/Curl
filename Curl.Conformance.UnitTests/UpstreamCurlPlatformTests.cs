using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins the features and null device <see cref="UpstreamCurlPlatform"/> reports per platform.</summary>
[TestClass]
public sealed class UpstreamCurlPlatformTests
{
    private static readonly string[] CommonFeatures =
    [
        "dict", "file", "gopher", "gophers", "http", "https", "ipfs", "mqtt", "mqtts", "scp", "sftp", "telnet", "tftp", "ws", "wss",
        "alt-svc", "AsynchDNS", "brotli", "ECH", "GSS-API", "HSTS", "http/2", "h2c", "http/3", "HTTPS-proxy",
        "HTTPSRR", "IDN", "IPv6", "Kerberos", "Largefile", "libz", "NTLM", "PSL", "SPNEGO", "SSL",
        "UnixSockets", "zstd",
        "crypto", "cookies", "proxy", "Mime", "manual", "DoH", "digest", "aws", "netrc", "verbose-strings",
        "large-time", "large-size", "sha512-256", "SSLpinning",
        "large_file", "local-http", "httpsig",
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
    public void WithFeatures_AddsFeaturesAndLeavesTheBasisAndTheRestAsTheyWere()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamCurlPlatform basis = UpstreamCurlPlatform.Windows;
        diagnostics.Arrange("extra features", "Debug");

        UpstreamCurlPlatform extended = basis.WithFeatures(["Debug"]);

        diagnostics.Act("extended has Debug", extended.Features.Contains("Debug"));
        diagnostics.Assert("extended has Debug", true, extended.Features.Contains("Debug"));
        diagnostics.Assert("basis has Debug", false, basis.Features.Contains("Debug"));
        Assert.IsTrue(extended.Features.Contains("Debug"));
        Assert.IsTrue(extended.Features.IsSupersetOf(basis.Features));
        Assert.IsFalse(basis.Features.Contains("Debug"));
        Assert.AreEqual(basis.NullDevice, extended.NullDevice);
        Assert.AreEqual(basis.OperatingSystemName, extended.OperatingSystemName);
    }

    [TestMethod]
    public void WithFeatures_NullFeatures_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCurlPlatform.Unix.WithFeatures(null!));

        diagnostics.Act("exception type", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
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
    [DataRow("sftp")]
    [DataRow("scp")]
    public void AllPlatforms_ListTheSshProtocolsTheHarnessEmulates(string feature)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("feature", feature);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains(feature));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
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
