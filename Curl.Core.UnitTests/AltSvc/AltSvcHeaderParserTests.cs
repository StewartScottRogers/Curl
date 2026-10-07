using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.AltSvc;

/// <summary>
/// Pins how <see cref="AltSvcHeaderParser" /> reads <c>Alt-Svc</c> values, each measured against
/// curl 8.21.0 (mingw, Schannel) on 2026-09-29 UTC by the alt-svc file it wrote; the commands and the
/// files are in BL-622's notes.
/// </summary>
[TestClass]
public sealed class AltSvcHeaderParserTests
{
    private const long Day = AltSvcHeaderParser.DefaultMaxAgeSeconds;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_TwoAlternatives_GivesEachItsOwnMaxAge() =>
        AssertAlternatives(
            "h2=\":8443\"; ma=60, h3=\":443\"",
            new AltSvcAlternative(AltSvcAlpn.H2, null, 8443, 60, false),
            new AltSvcAlternative(AltSvcAlpn.H3, null, 443, Day, false));

    [TestMethod]
    public void Parse_HostPersistAndUnknownAlpn_SkipsTheUnknownAlternative() =>
        AssertAlternatives(
            "h3=\"alt.example:8443\"; ma=120; persist=1, h2=\":9443\", foo=\":1\"",
            new AltSvcAlternative(AltSvcAlpn.H3, "alt.example", 8443, 120, true),
            new AltSvcAlternative(AltSvcAlpn.H2, null, 9443, Day, false));

    [TestMethod]
    public void Parse_BracketedHostAndQuotedMaxAge_ReadsTheIpv6Address() =>
        AssertAlternatives("h2=\"[::1]:8443\"; ma=\"30\"", new AltSvcAlternative(AltSvcAlpn.H2, "::1", 8443, 30, false));

    [TestMethod]
    public void Parse_ParameterNamesInAnyCaseWithBlanks_ReadsThem() =>
        AssertAlternatives(
            "h2=\":8443\";MA=5;PERSIST=1, h3=\":1\"; persist=2 ,h1=\":2\" ; MA = 7",
            new AltSvcAlternative(AltSvcAlpn.H2, null, 8443, 5, true),
            new AltSvcAlternative(AltSvcAlpn.H3, null, 1, Day, false),
            new AltSvcAlternative(AltSvcAlpn.H1, null, 2, 7, false));

    [TestMethod]
    public void Parse_HostCaseAndTrailingDot_KeepsThemAsSent() =>
        AssertAlternatives(
            "h2=\":8443\",h3=\"Alt.Example.:1\"",
            new AltSvcAlternative(AltSvcAlpn.H2, null, 8443, Day, false),
            new AltSvcAlternative(AltSvcAlpn.H3, "Alt.Example.", 1, Day, false));

    [TestMethod]
    public void Parse_UnreadableMaxAge_KeepsTheDefault() =>
        AssertAlternatives(
            "h2=\":1\";ma=abc, h3=\":2\";ma=0, h2=\":3\"; ma=99999999999999999999, h2=\":4\"; ma=\"3x",
            new AltSvcAlternative(AltSvcAlpn.H2, null, 1, Day, false),
            new AltSvcAlternative(AltSvcAlpn.H3, null, 2, 0, false),
            new AltSvcAlternative(AltSvcAlpn.H2, null, 3, Day, false),
            new AltSvcAlternative(AltSvcAlpn.H2, null, 4, 3, false));

    [TestMethod]
    public void Parse_PortAboveRange_StopsBeforeLaterAlternatives() =>
        AssertAlternatives("h2=\"a.example:99999\", h3=\":4\"");

    [TestMethod]
    public void Parse_HostWithoutPort_StopsKeepingEarlierAlternatives() =>
        AssertAlternatives("h3=\":5\", h2=\"a.example\"", new AltSvcAlternative(AltSvcAlpn.H3, null, 5, Day, false));

    [TestMethod]
    [DataRow("H2=\":8443\" ; MA = 5")]
    [DataRow("")]
    [DataRow("=\":1\"")]
    [DataRow("h2=")]
    [DataRow("h2= \":1\"")]
    [DataRow("h2=\"[::1\"")]
    [DataRow("h2=\"[]:1\"")]
    [DataRow("h2=\"[::1]8443\"")]
    [DataRow("h2=\":8443")]
    [DataRow("h2=\":\"")]
    [DataRow("h2=\"a:1:2\"")]
    [DataRow("clear, h2=\":1\"")]
    public void Parse_NoReadableKnownAlternative_GivesNone(string value) =>
        AssertAlternatives(value);

    [TestMethod]
    public void Parse_ParameterWithoutEquals_StopsTheParametersAndKeepsTheAlternative() =>
        AssertAlternatives("h2=\":1\"; ma", new AltSvcAlternative(AltSvcAlpn.H2, null, 1, Day, false));

    [TestMethod]
    public void Parse_TextAfterAnAlternativeThatIsNotAComma_Stops() =>
        AssertAlternatives("h2=\":1\" h3=\":2\"", new AltSvcAlternative(AltSvcAlpn.H2, null, 1, Day, false));

    [TestMethod]
    public void Parse_TrailingComma_KeepsTheAlternativesBeforeIt() =>
        AssertAlternatives("h2=\":1\",", new AltSvcAlternative(AltSvcAlpn.H2, null, 1, Day, false));

    [TestMethod]
    public void Parse_ParameterWithoutValueBeforeAComma_LosesTheNextAlternative() =>
        AssertAlternatives("h2=\":8443\"; foo, h3=\":443\"", new AltSvcAlternative(AltSvcAlpn.H2, null, 8443, Day, false));

    [TestMethod]
    public void Parse_HostOfTheLongestLength_ReadsIt()
    {
        string host = new('a', AltSvcEntry.MaxHostLength);

        AssertAlternatives(
            $"h2=\"{host}:1\", h3=\":2\"",
            new AltSvcAlternative(AltSvcAlpn.H2, host, 1, Day, false),
            new AltSvcAlternative(AltSvcAlpn.H3, null, 2, Day, false));
    }

    [TestMethod]
    public void Parse_HostLongerThanTheLongest_Stops() =>
        AssertAlternatives($"h2=\"{new string('a', AltSvcEntry.MaxHostLength + 1)}:1\", h3=\":2\"");

    [TestMethod]
    public void Parse_BracketedHostOf46Characters_ReadsIt() =>
        AssertAlternatives($"h2=\"[{new string('1', 46)}]:1\"", new AltSvcAlternative(AltSvcAlpn.H2, new string('1', 46), 1, Day, false));

    [TestMethod]
    public void Parse_BracketedHostOf47Characters_Stops() =>
        AssertAlternatives($"h2=\"[{new string('1', 47)}]:1\"");


    [TestMethod]
    [DataRow("clear")]
    [DataRow(" CLEAR\t")]
    [DataRow("Clear; ma=5")]
    [DataRow("clear\r\n")]
    public void Parse_Clear_IsClear(string value)
    {
        AltSvcHeader header = Parse(value, out var diagnostics);

        diagnostics.Assert("is clear", true, header.IsClear);
        diagnostics.Assert("alternative count", 0, header.Alternatives.Count);
        Assert.IsTrue(header.IsClear);
        Assert.AreEqual(0, header.Alternatives.Count);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("call", "Parse(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => AltSvcHeaderParser.Parse(null!));

        diagnostics.Act("exception", exception.GetType().Name + " (" + exception.ParamName + ")");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow("h2=\":abc\"")]
    [DataRow("h2=\"[::1]:99999\"")]
    [DataRow("h2=\"host:\"")]
    [DataRow("h2=\"[::1]443\"")]
    [DataRow("h2=\"host\"")]
    public void Parse_EmptyBadOrMissingPort_SaysUnknownPortNumber(string value) =>
        Assert.AreEqual(AltSvcSkipReason.UnknownPortNumber, SkipReason(value, AltSvcSkipReason.UnknownPortNumber));

    [TestMethod]
    public void Parse_UnclosedIpv6Literal_SaysBadIpv6Hostname() =>
        Assert.AreEqual(AltSvcSkipReason.BadIpv6Hostname, SkipReason("h2=\"[::1:443\"", AltSvcSkipReason.BadIpv6Hostname));

    [TestMethod]
    public void Parse_HostOneCharacterOverTheLimit_SaysBadHostname() =>
        Assert.AreEqual(
            AltSvcSkipReason.BadHostname,
            SkipReason($"h2=\"{new string('a', AltSvcEntry.MaxHostLength + 1)}:443\"", AltSvcSkipReason.BadHostname));

    [TestMethod]
    public void Parse_HostAtTheLimit_ReadsItWithNoSkipReason()
    {
        string host = new('a', AltSvcEntry.MaxHostLength);
        var expected = new[] { new AltSvcAlternative(AltSvcAlpn.H2, host, 443, Day, false) };

        AltSvcHeader header = Parse($"h2=\"{host}:443\"", out var diagnostics);

        diagnostics.Assert("skip reason", "(none)", header.SkipReason?.ToString() ?? "(none)");
        diagnostics.Assert("alternatives", Describe(expected), Describe(header.Alternatives));
        Assert.IsNull(header.SkipReason);
        CollectionAssert.AreEqual(expected, header.Alternatives.ToArray());
    }

    [TestMethod]
    [DataRow("h2=\":443\"")]
    [DataRow("h2=\":443")]
    [DataRow("h2=:443")]
    public void Parse_GoodAlternativeOrStopWithoutACurlLine_HasNoSkipReason(string value) =>
        Assert.IsNull(SkipReason(value, null));

    [TestMethod]
    public void Parse_BadPortAfterAGoodAlternative_KeepsTheGoodOneAndSaysWhy()
    {
        var expected = new[] { new AltSvcAlternative(AltSvcAlpn.H2, "a.test", 443, Day, false) };

        AltSvcHeader header = Parse("h2=\"a.test:443\", h2=\":abc\"", out var diagnostics);

        diagnostics.Assert("skip reason", AltSvcSkipReason.UnknownPortNumber, header.SkipReason);
        diagnostics.Assert("alternatives", Describe(expected), Describe(header.Alternatives));
        Assert.AreEqual(AltSvcSkipReason.UnknownPortNumber, header.SkipReason);
        CollectionAssert.AreEqual(expected, header.Alternatives.ToArray());
    }

    private void AssertAlternatives(string value, params AltSvcAlternative[] expected)
    {
        AltSvcHeader header = Parse(value, out var diagnostics);

        diagnostics.Assert("is clear", false, header.IsClear);
        diagnostics.Assert("alternatives", Describe(expected), Describe(header.Alternatives));
        Assert.IsFalse(header.IsClear);
        CollectionAssert.AreEqual(expected, header.Alternatives.ToArray());
    }

    private AltSvcSkipReason? SkipReason(string value, AltSvcSkipReason? expected)
    {
        AltSvcHeader header = Parse(value, out var diagnostics);

        diagnostics.Assert("skip reason", expected?.ToString() ?? "(none)", header.SkipReason?.ToString() ?? "(none)");
        return header.SkipReason;
    }

    private AltSvcHeader Parse(string value, out TestDiagnostics diagnostics)
    {
        diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value length", value.Length);
        diagnostics.Arrange("value", Visible(value));

        AltSvcHeader header = AltSvcHeaderParser.Parse(value);

        diagnostics.Act("is clear", header.IsClear);
        diagnostics.Act("skip reason", header.SkipReason?.ToString() ?? "(none)");
        diagnostics.Act("alternatives", Describe(header.Alternatives));
        return header;
    }

    // Hosts at the length limit run to hundreds of characters; the length line above carries
    // their size, so the text shows only the start.
    private static string Visible(string text)
    {
        var shown = text.Length > 80 ? text[..80] + "..." : text;
        return "\"" + shown.Replace("\r", "\r", StringComparison.Ordinal).Replace("\n", "\n", StringComparison.Ordinal).Replace("\t", "\t", StringComparison.Ordinal) + "\"";
    }

    private static string Describe(IEnumerable<AltSvcAlternative> alternatives) =>
        "[" + string.Join("; ", alternatives.Select(a => $"{a.Alpn} {ShortHost(a.Host)}:{a.Port} ma={a.MaxAgeSeconds} persist={a.Persist}")) + "]";

    private static string ShortHost(string? host) => host switch
    {
        null => "(origin host)",
        { Length: > 40 } => $"{host[..8]}... ({host.Length} characters)",
        _ => host,
    };
}
