using Curl.Testing;

namespace Curl.Core.AltSvc;

/// <summary>
/// Pins which alt-svc file lines <see cref="AltSvcFileLineParser" /> reads an entry from, each
/// measured against curl 8.21.0 (mingw, Schannel) on 2026-09-29 UTC by whether curl wrote the line
/// back; the seeded files are in BL-622's notes.
/// </summary>
[TestClass]
public sealed class AltSvcFileLineParserTests
{
    private static readonly DateTimeOffset Expiry = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_CurlsOwnLine_ReadsEveryField()
    {
        var expected = new AltSvcEntry(AltSvcAlpn.H1, "localhost", 18443, AltSvcAlpn.H2, "localhost", 8443, new DateTimeOffset(2026, 9, 29, 5, 21, 5, TimeSpan.Zero), false);

        var entry = Parse("h1 localhost 18443 h2 localhost 8443 \"20260929 05:21:05\" 0 0", expected);

        Assert.AreEqual(expected, entry);
    }

    [TestMethod]
    [DataRow("h1 a.example 443 h3 b.example 443 \"20300101 00:00:00\" 1 0\r")]
    [DataRow("\th1 a.example 443 h3 b.example 443 \"20300101 00:00:00\" 1 0")]
    [DataRow("  h1 a.example 443 h3 b.example 443 \"20300101 00:00:00\" 1 0\rafter")]
    public void Parse_LeadingBlanksOrCarriageReturn_ReadsTheEntry(string line)
    {
        var expected = new AltSvcEntry(AltSvcAlpn.H1, "a.example", 443, AltSvcAlpn.H3, "b.example", 443, Expiry, true);

        var entry = Parse(line, expected);

        Assert.AreEqual(expected, entry);
    }

    [TestMethod]
    [DataRow("h1 ::1 443 h2 ::1 443", "::1", "::1")]
    [DataRow("h1 [::2] 443 h2 [::3] 443", "::2", "::3")]
    [DataRow("h1 f.example. 443 h2 b.example. 443", "f.example", "b.example.")]
    [DataRow("h1 a 443 h2 b 443", "a", "b")]
    public void Parse_Hosts_StripsBracketsAndTheSourcesTrailingDot(string origins, string sourceHost, string destinationHost)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", Visible(origins + " \"20300101 00:00:00\" 0 0"));

        AltSvcEntry? entry = AltSvcFileLineParser.Parse(origins + " \"20300101 00:00:00\" 0 0");

        diagnostics.Act("entry", entry?.ToString() ?? "(null)");
        diagnostics.Assert("source host", sourceHost, entry?.SourceHost);
        diagnostics.Assert("destination host", destinationHost, entry?.DestinationHost);
        Assert.AreEqual(sourceHost, entry?.SourceHost);
        Assert.AreEqual(destinationHost, entry?.DestinationHost);
    }

    [TestMethod]
    [DataRow("# comment")]
    [DataRow("")]
    [DataRow("bogus line")]
    [DataRow("h1")]
    [DataRow("h1 a.example")]
    [DataRow("h1 a.example 443")]
    [DataRow("h1 a.example 443 ")]
    [DataRow("h9 a 1 h2 b 2 \"20300101 00:00:00\" 0 0")]
    [DataRow("H1 c.example 443 H2 b.example 443 \"20300101 00:00:00\" 0 0")]
    [DataRow("h1 c.example 443 H2 b.example 443 \"20300101 00:00:00\" 0 0")]
    [DataRow("h1  e.example 443 h2 b.example 443 \"20300101 00:00:00\" 0 0")]
    [DataRow("h1 e.example 65536 h2 b.example 443 \"20300101 00:00:00\" 0 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00\" 5 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00\" 1 7")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00\" 0 0 ")]
    [DataRow("h1 c.example 443 h2 b.example 443 \"20300101 00:00:00\" 1 0 extra")]
    [DataRow("h1 d.example 443 h2 b.example 443 20300101 0 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"garbage\" 0 0")]
    [DataRow("h1 d.example 443 h2 b.example 443 \"Tue, 01 Jan 2030 00:00:00 GMT\" 0 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00")]
    [DataRow("h1 a.example 443 h2 b.example 443 ")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00\"")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00\" 0")]
    [DataRow("h1 . 443 h2 b.example 443 \"20300101 00:00:00\" 0 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 20300101 00:00:00 0 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20300101 00:00:00x 0 0")]
    [DataRow("h1 a.example 443 h2 b.example 443 \"20301301 00:00:00\" 0 0")]
    [DataRow("h1  443 h2 b.example 443 \"20300101 00:00:00\" 0 0")]
    [DataRow("h1 a\t.example 443 h2 b.example 443 \"20300101 00:00:00\" 0 0")]
    public void Parse_NotAnEntry_GivesNull(string line) =>
        Assert.IsNull(Parse(line, null));

    [TestMethod]
    public void Parse_HostOfTheLongestLength_ReadsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string host = new('a', AltSvcEntry.MaxHostLength);
        diagnostics.Arrange("host length", host.Length);

        AltSvcEntry? entry = AltSvcFileLineParser.Parse($"h1 {host} 443 h2 {host} 443 \"20300101 00:00:00\" 0 0");

        diagnostics.Act("source host length", entry?.SourceHost.Length);
        diagnostics.Act("destination host length", entry?.DestinationHost.Length);
        diagnostics.Assert("source host length", host.Length, entry?.SourceHost.Length);
        diagnostics.Assert("destination host length", host.Length, entry?.DestinationHost.Length);
        Assert.AreEqual(host, entry?.SourceHost);
        Assert.AreEqual(host, entry?.DestinationHost);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Parse_HostLongerThanTheLongest_GivesNull(bool source)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string host = new('a', AltSvcEntry.MaxHostLength + 1);
        diagnostics.Arrange("host length", host.Length);
        diagnostics.Arrange("long host is", source ? "source" : "destination");

        var entry = AltSvcFileLineParser.Parse(source
            ? $"h1 {host} 443 h2 b 443 \"20300101 00:00:00\" 0 0"
            : $"h1 a 443 h2 {host} 443 \"20300101 00:00:00\" 0 0");

        diagnostics.Act("entry", entry?.ToString() ?? "(null)");
        diagnostics.Assert("entry", "(null)", entry?.ToString() ?? "(null)");
        Assert.IsNull(entry);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("call", "Parse(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => AltSvcFileLineParser.Parse(null!));

        diagnostics.Act("exception", exception.GetType().Name + " (" + exception.ParamName + ")");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private AltSvcEntry? Parse(string line, AltSvcEntry? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", Visible(line));

        var entry = AltSvcFileLineParser.Parse(line);

        diagnostics.Act("entry", entry?.ToString() ?? "(null)");
        diagnostics.Assert("entry", expected?.ToString() ?? "(null)", entry?.ToString() ?? "(null)");
        return entry;
    }

    private static string Visible(string text) =>
        "\"" + text.Replace("\r", "\r", StringComparison.Ordinal).Replace("\t", "\t", StringComparison.Ordinal) + "\"";
}
