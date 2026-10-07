using Curl.Testing;

namespace Curl.Core.Hsts;

/// <summary>
/// Pins which HSTS file lines <see cref="HstsFileLineParser" /> reads, each measured against curl
/// 8.21.0 (mingw, Schannel) on 2026-09-29 UTC by whether curl wrote the line back; the seeded
/// files are in BL-620's notes.
/// </summary>
[TestClass]
public sealed class HstsFileLineParserTests
{
    public TestContext TestContext { get; set; } = null!;

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
    public void Parse_LineCurlReads_ReadsItsFields(string line, string host, string expiryText)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", line);
        HstsFileLine expected = new(host, expiryText);

        HstsFileLine? parsed = HstsFileLineParser.Parse(line);

        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("parsed line", expected, parsed);
        Assert.AreEqual(expected, parsed);
    }

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
    public void Parse_LineCurlSkips_ReadsNothing(string line)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", line);

        HstsFileLine? parsed = HstsFileLineParser.Parse(line);

        diagnostics.Act("parsed", parsed?.ToString() ?? "null");
        diagnostics.Assert("parsed line", "null", parsed?.ToString() ?? "null");
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void Parse_HostOfTheLongestLength_ReadsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string host = new('h', HstsFileLineParser.MaxHostLength);
        diagnostics.Arrange("host length", host.Length);

        string? parsedHost = HstsFileLineParser.Parse(host + " \"unlimited\"")?.Host;

        diagnostics.Act("parsed host length", parsedHost?.Length ?? -1);
        diagnostics.Assert("parsed host", host, parsedHost);
        Assert.AreEqual(host, parsedHost);
    }

    [TestMethod]
    public void Parse_HostLongerThanCurlReads_ReadsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string host = new('h', HstsFileLineParser.MaxHostLength + 1);
        diagnostics.Arrange("host length", host.Length);

        HstsFileLine? parsed = HstsFileLineParser.Parse(host + " \"unlimited\"");

        diagnostics.Act("parsed", parsed is null ? "null" : "a line");
        diagnostics.Assert("parsed line", "null", parsed is null ? "null" : "a line");
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => HstsFileLineParser.Parse(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
