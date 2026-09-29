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
        AltSvcHeader header = AltSvcHeaderParser.Parse(value);

        Assert.IsTrue(header.IsClear);
        Assert.AreEqual(0, header.Alternatives.Count);
    }

    [TestMethod]
    public void Parse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AltSvcHeaderParser.Parse(null!));

    private static void AssertAlternatives(string value, params AltSvcAlternative[] expected)
    {
        AltSvcHeader header = AltSvcHeaderParser.Parse(value);

        Assert.IsFalse(header.IsClear);
        CollectionAssert.AreEqual(expected, header.Alternatives.ToArray());
    }
}
