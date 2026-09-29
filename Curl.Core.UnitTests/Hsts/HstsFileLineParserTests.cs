namespace Curl.Core.Hsts;

/// <summary>
/// Pins which HSTS file lines <see cref="HstsFileLineParser" /> reads, each measured against curl
/// 8.21.0 (mingw, Schannel) on 2026-09-29 UTC by whether curl wrote the line back; the seeded
/// files are in BL-620's notes.
/// </summary>
[TestClass]
public sealed class HstsFileLineParserTests
{
    [TestMethod]
    [DataRow("localhost \"20300101 00:00:00\"", "localhost", "20300101 00:00:00")]
    [DataRow(".sub.test \"unlimited\"", ".sub.test", "unlimited")]
    [DataRow("  lead.test \"20300101 00:00:00\"", "lead.test", "20300101 00:00:00")]
    [DataRow("\ttrail.test. \"20300101 00:00:00\"", "trail.test.", "20300101 00:00:00")]
    [DataRow("cr.test \"20300101 00:00:00\"\r", "cr.test", "20300101 00:00:00")]
    [DataRow("cr.test \"20300101 00:00:00\"\rafter", "cr.test", "20300101 00:00:00")]
    [DataRow("e \"a\\\"b\"", "e", "a\\\"b")]
    [DataRow("e \"\"", "e", "")]
    [DataRow("e \"12345678901234567\"", "e", "12345678901234567")]
    public void Parse_LineCurlReads_ReadsItsFields(string line, string host, string expiryText) =>
        Assert.AreEqual(new HstsFileLine(host, expiryText), HstsFileLineParser.Parse(line));

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("# localhost \"20300101 00:00:00\"")]
    [DataRow("   # localhost \"20300101 00:00:00\"")]
    [DataRow("bad.test 20300101")]
    [DataRow("two.test  \"20300101 00:00:00\"")]
    [DataRow("tab.test\t\"20300101 00:00:00\"")]
    [DataRow("x.test \"20300101 00:00:00\" y")]
    [DataRow("x.test \"20300101 00:00:00")]
    [DataRow("x.test \"20300101 00:00:00\\\"")]
    [DataRow("x.test \"20300101 00:00:00\\")]
    [DataRow("x.test ")]
    [DataRow("e \"123456789012345678\"")]
    [DataRow("e \"1234567890123456\\78\"")]
    public void Parse_LineCurlSkips_ReadsNothing(string line) =>
        Assert.IsNull(HstsFileLineParser.Parse(line));

    [TestMethod]
    public void Parse_HostOfTheLongestLength_ReadsIt() =>
        Assert.AreEqual(
            new string('h', HstsFileLineParser.MaxHostLength),
            HstsFileLineParser.Parse(new string('h', HstsFileLineParser.MaxHostLength) + " \"unlimited\"")?.Host);

    [TestMethod]
    public void Parse_HostLongerThanCurlReads_ReadsNothing() =>
        Assert.IsNull(HstsFileLineParser.Parse(new string('h', HstsFileLineParser.MaxHostLength + 1) + " \"unlimited\""));

    [TestMethod]
    public void Parse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HstsFileLineParser.Parse(null!));
}
