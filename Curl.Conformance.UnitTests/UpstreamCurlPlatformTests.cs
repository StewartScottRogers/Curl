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

    [TestMethod]
    public void Windows_ReportsTheSchannelBuildAndWin32()
    {
        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Windows;

        Assert.IsTrue(platform.Features.SetEquals([.. CommonFeatures, "win32", "Schannel"]));
        Assert.AreEqual("NUL", platform.NullDevice);
    }

    [TestMethod]
    public void Unix_ReportsTheOpenSslBuildAndExtendedAttributes()
    {
        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Unix;

        Assert.IsTrue(platform.Features.SetEquals([.. CommonFeatures, "OpenSSL", "xattr"]));
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
        Assert.IsFalse(UpstreamCurlPlatform.Windows.Features.Contains(feature));
        Assert.IsFalse(UpstreamCurlPlatform.Unix.Features.Contains(feature));
    }
}
