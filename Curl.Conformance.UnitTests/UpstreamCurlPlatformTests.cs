using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins the features and null device <see cref="UpstreamCurlPlatform"/> reports per platform.</summary>
[TestClass]
public sealed partial class UpstreamCurlPlatformTests
{
    private static readonly string[] CommonFeatures =
    [
        "dict", "file", "ftp", "gopher", "gophers", "http", "https", "imap", "ipfs", "mqtt", "mqtts", "pop3", "scp", "sftp", "smtp", "telnet", "tftp", "ws", "wss",
        "alt-svc", "AsynchDNS", "brotli", "ECH", "GSS-API", "HSTS", "HTTPS-proxy",
        "IDN", "IPv6", "Kerberos", "Largefile", "libz", "NTLM", "PSL", "SPNEGO", "SSL",
        "UnixSockets", "zstd",
        "crypto", "cookies", "proxy", "Mime", "manual", "DoH", "digest", "aws", "netrc", "verbose-strings",
        "large-time", "large-size", "sha512-256", "SSLpinning",
        "large_file", "local-http", "httpsig", "headers-api", "form-api", "wakeup",
        "threadsafe", "threaded-resolver",
    ];

    // Each feature added later gets its own UpstreamCurlPlatformTests.<Feature>.cs, so this baseline is a floor, not the whole set.
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Windows_ReportsTheSchannelBuildAndWin32()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "Windows");

        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Windows;

        bool featuresMatch = platform.Features.IsSupersetOf([.. CommonFeatures, "win32", "Schannel"]);
        diagnostics.Act("feature count", platform.Features.Count);
        diagnostics.Act("null device", platform.NullDevice);
        diagnostics.Assert("features include common + win32 + Schannel", true, featuresMatch);
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

        bool featuresMatch = platform.Features.IsSupersetOf([.. CommonFeatures, "OpenSSL", "SSLS-EXPORT", "xattr", "HTTPSRR"]);
        diagnostics.Act("feature count", platform.Features.Count);
        diagnostics.Act("null device", platform.NullDevice);
        diagnostics.Assert("features include common + OpenSSL + xattr", true, featuresMatch);
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
    [DataRow("SSPI")]
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
