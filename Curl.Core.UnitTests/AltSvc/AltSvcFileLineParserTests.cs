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

    [TestMethod]
    public void Parse_CurlsOwnLine_ReadsEveryField() =>
        Assert.AreEqual(
            new AltSvcEntry(AltSvcAlpn.H1, "localhost", 18443, AltSvcAlpn.H2, "localhost", 8443, new DateTimeOffset(2026, 9, 29, 5, 21, 5, TimeSpan.Zero), false),
            AltSvcFileLineParser.Parse("h1 localhost 18443 h2 localhost 8443 \"20260929 05:21:05\" 0 0"));

    [TestMethod]
    [DataRow("h1 a.example 443 h3 b.example 443 \"20300101 00:00:00\" 1 0\r")]
    [DataRow("\th1 a.example 443 h3 b.example 443 \"20300101 00:00:00\" 1 0")]
    [DataRow("  h1 a.example 443 h3 b.example 443 \"20300101 00:00:00\" 1 0\rafter")]
    public void Parse_LeadingBlanksOrCarriageReturn_ReadsTheEntry(string line) =>
        Assert.AreEqual(
            new AltSvcEntry(AltSvcAlpn.H1, "a.example", 443, AltSvcAlpn.H3, "b.example", 443, Expiry, true),
            AltSvcFileLineParser.Parse(line));

    [TestMethod]
    [DataRow("h1 ::1 443 h2 ::1 443", "::1", "::1")]
    [DataRow("h1 [::2] 443 h2 [::3] 443", "::2", "::3")]
    [DataRow("h1 f.example. 443 h2 b.example. 443", "f.example", "b.example.")]
    [DataRow("h1 a 443 h2 b 443", "a", "b")]
    public void Parse_Hosts_StripsBracketsAndTheSourcesTrailingDot(string origins, string sourceHost, string destinationHost)
    {
        AltSvcEntry? entry = AltSvcFileLineParser.Parse(origins + " \"20300101 00:00:00\" 0 0");

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
        Assert.IsNull(AltSvcFileLineParser.Parse(line));

    [TestMethod]
    public void Parse_HostOfTheLongestLength_ReadsIt()
    {
        string host = new('a', AltSvcEntry.MaxHostLength);

        AltSvcEntry? entry = AltSvcFileLineParser.Parse($"h1 {host} 443 h2 {host} 443 \"20300101 00:00:00\" 0 0");

        Assert.AreEqual(host, entry?.SourceHost);
        Assert.AreEqual(host, entry?.DestinationHost);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Parse_HostLongerThanTheLongest_GivesNull(bool source)
    {
        string host = new('a', AltSvcEntry.MaxHostLength + 1);

        Assert.IsNull(AltSvcFileLineParser.Parse(source
            ? $"h1 {host} 443 h2 b 443 \"20300101 00:00:00\" 0 0"
            : $"h1 a 443 h2 {host} 443 \"20300101 00:00:00\" 0 0"));
    }

    [TestMethod]
    public void Parse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AltSvcFileLineParser.Parse(null!));
}
