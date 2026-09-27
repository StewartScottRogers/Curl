using System.Net;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="ResolveOverrides" /> parses and answers <c>--resolve</c> entries
/// against curl 8.21.0 (measured; the commands are in BL-214's Notes).
/// </summary>
[TestClass]
public sealed class ResolveOverridesTests
{
    [TestMethod]
    public void Parse_WithNullEntries_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ResolveOverrides.Parse(null!));

        Assert.AreEqual("entries", exception.ParamName);
    }

    [TestMethod]
    public void Find_WithNullHost_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ResolveOverrides.None.Find(null!, 80));

        Assert.AreEqual("host", exception.ParamName);
    }

    [TestMethod]
    public void None_HasNoEntriesAndNoParseError()
    {
        Assert.IsNull(ResolveOverrides.None.ParseError);
        Assert.IsNull(ResolveOverrides.None.Find("example.com", 80));
    }

    [TestMethod]
    public void Find_ForTheEntrysHostAndPort_ReturnsItsAddressesInOrder()
    {
        // curl --resolve a:80:127.0.0.3,127.0.0.4 http://a/ -> Trying 127.0.0.3:80, then 127.0.0.4:80
        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3,127.0.0.4"]);

        Assert.IsNull(overrides.ParseError);
        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("127.0.0.3"), IPAddress.Parse("127.0.0.4") },
            overrides.Find("a", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_MatchesTheHostWithoutRegardToCase()
    {
        // curl --resolve a:80:127.0.0.1 http://A:80/ -> Trying 127.0.0.1:80
        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.1"]);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("A", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_ForAnotherPort_ReturnsNull()
    {
        // curl --resolve a:81:127.0.0.1 http://a/ -> resolves a through the system resolver
        var overrides = ResolveOverrides.Parse(["a:81:127.0.0.1"]);

        Assert.IsNull(overrides.Find("a", 80));
    }

    [TestMethod]
    public void Find_WithAWildcardEntry_AnswersForAnyHostOnThatPortAfterANamedEntry()
    {
        // curl --resolve *:80:127.0.0.2 http://zz/ -> RESOLVE *:80 using wildcard, Trying 127.0.0.2:80
        var overrides = ResolveOverrides.Parse(["*:80:127.0.0.2", "named:80:127.0.0.9"]);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.2") }, overrides.Find("zz", 80)!.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.9") }, overrides.Find("named", 80)!.ToArray());
        Assert.IsNull(overrides.Find("zz", 81));
    }

    [TestMethod]
    public void Parse_ALaterEntryForTheSameHostAndPort_ReplacesTheEarlierOne()
    {
        // curl --resolve a:80:127.0.0.3 --resolve a:80:127.0.0.4 http://a/ -> old addresses discarded, Trying 127.0.0.4:80
        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3", "a:80:127.0.0.4"]);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.4") }, overrides.Find("a", 80)!.ToArray());
    }

    [TestMethod]
    public void Parse_ARemovalEntry_DropsTheEarlierEntry()
    {
        // curl --resolve a:80:127.0.0.3 --resolve -a:80 http://a/ -> resolves a through the system resolver
        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3", "-a:80"]);

        Assert.IsNull(overrides.ParseError);
        Assert.IsNull(overrides.Find("a", 80));
    }

    [TestMethod]
    [DataRow("-a")]
    [DataRow("-a:x")]
    [DataRow("")]
    [DataRow(":80:127.0.0.1")]
    [DataRow("+")]
    [DataRow("[::1:80:127.0.0.1")]
    [DataRow("-[::1:80")]
    public void Parse_AnEntryCurlIgnores_AddsNothingAndReportsNothing(string entry)
    {
        // curl --resolve <entry> http://a/ -> resolves a through the system resolver, no exit 49
        var overrides = ResolveOverrides.Parse([entry]);

        Assert.IsNull(overrides.ParseError);
        Assert.IsNull(overrides.Find("a", 80));
    }

    [TestMethod]
    [DataRow("+a:80:127.0.0.1", "127.0.0.1")]
    [DataRow("a:080:127.0.0.1", "127.0.0.1")]
    [DataRow("a:80:127.0.0.1,", "127.0.0.1")]
    [DataRow("a:80:,127.0.0.1", "127.0.0.1")]
    [DataRow("a:80:[::1]", "::1")]
    [DataRow("a:80:[127.0.0.1]", "127.0.0.1")]
    [DataRow("a:80:[::1]x", "::1")]
    [DataRow("a:80:::1", "::1")]
    [DataRow("a:80:255.0.0.0", "255.0.0.0")]
    public void Parse_AnAcceptedEntry_AnswersWithItsAddress(string entry, string address)
    {
        var overrides = ResolveOverrides.Parse([entry]);

        Assert.IsNull(overrides.ParseError);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse(address) }, overrides.Find("a", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_ForABracketedIPv6Host_MatchesTheEntryEitherWay()
    {
        // curl --resolve [::1]:80:127.0.0.1 http://[::1]/ -> Added ::1:80:127.0.0.1, Trying 127.0.0.1:80
        var overrides = ResolveOverrides.Parse(["[::1]:80:127.0.0.1"]);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("[::1]", 80)!.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("::1", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_AcceptsPortZero()
    {
        // curl --resolve a:0:127.0.0.1 ... -> Added a:0:127.0.0.1 to DNS cache
        var overrides = ResolveOverrides.Parse(["a:0:127.0.0.1"]);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("a", 0)!.ToArray());
    }

    [TestMethod]
    [DataRow("garbage")]
    [DataRow("a:x:1.2.3.4")]
    [DataRow("a:80:")]
    [DataRow("a:80")]
    [DataRow("a:80:,")]
    [DataRow("a:80:notanip")]
    [DataRow("a:99999:127.0.0.1")]
    [DataRow("a:123456:127.0.0.1")]
    [DataRow("a:-1:127.0.0.1")]
    [DataRow("a:+80:127.0.0.1")]
    [DataRow("a:80x:1.2.3.4")]
    [DataRow("a::127.0.0.1")]
    [DataRow("a:80:127.0.0.1:81")]
    [DataRow("a:80:[::1")]
    [DataRow("a:80:+127.0.0.1")]
    [DataRow("a:80:01.2.3.4")]
    [DataRow("a:80:1.2.3")]
    [DataRow("a:80:1..2.3")]
    [DataRow("a:80:1.2.3.1234")]
    [DataRow("a:80:1.2.3.256")]
    [DataRow("a:80:fe80::1%1")]
    [DataRow("a:80:::g")]
    public void Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message(string entry)
    {
        // curl --resolve <entry> http://a/ -> curl: (49) Could not parse CURLOPT_RESOLVE entry '<entry>'
        var overrides = ResolveOverrides.Parse([entry]);

        Assert.AreEqual($"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);
    }

    [TestMethod]
    public void Parse_StopsAtTheFirstEntryThatDoesNotParseAndKeepsTheOnesBefore()
    {
        // curl --resolve a:80:127.0.0.3 --resolve a:80:127.0.0.4,xx http://a/ -> (49) ... 'a:80:127.0.0.4,xx'
        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3", "a:80:127.0.0.4,xx", "b:80:garbage"]);

        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'a:80:127.0.0.4,xx'", overrides.ParseError);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.3") }, overrides.Find("a", 80)!.ToArray());
    }
}
