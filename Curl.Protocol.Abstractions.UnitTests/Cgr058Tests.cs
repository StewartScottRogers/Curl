namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-058: curl 8.22.0 prints a bracketed IPv6 address back in its normalised form only
/// when that is no longer than the address as written, zone id aside, so an IPv4-mapped
/// address written in hex keeps its spelling in the Host header and url_effective.
/// </summary>
[TestClass]
public sealed class Cgr058Tests
{
    [TestMethod]
    [DataRow("[::ffff:7f00:1]", "[::ffff:7f00:1]")]
    [DataRow("[::FFFF:7F00:1]", "[::FFFF:7F00:1]")]
    [DataRow("[::FFFF:1]", "[::FFFF:1]")]
    [DataRow("[::0:ffff:1:2]", "[::0:ffff:1:2]")]
    [DataRow("[::ffff:0:1:2]", "[::ffff:0:1:2]")]
    [DataRow("[::ffff:7f00:1%25eth0]", "[::ffff:7f00:1]")]
    [DataRow("[::ABCD]", "[::abcd]")]
    [DataRow("[::FFFF:102:304]", "[::ffff:1.2.3.4]")]
    [DataRow("[::102:304]", "[::1.2.3.4]")]
    [DataRow("[0:0:0:0:0:0:0:1]", "[::1]")]
    [DataRow("[fe80::00ab]", "[fe80::ab]")]
    public void TryNormalize_IPv6Address_UsesTheNormalisedFormOnlyWhenNoLonger(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalize(host, refuseUnconvertibleName: true, out string normalized, out _, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, normalized);
    }
}
