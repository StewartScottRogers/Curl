using System.Net;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="ResolveOverrides" /> parses and answers <c>--resolve</c> entries
/// against curl 8.21.0 (measured; the commands are in BL-214's Notes).
/// </summary>
[TestClass]
public sealed class ResolveOverridesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_WithNullEntries_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("entries", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ResolveOverrides.Parse(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "entries", exception.ParamName);

        Assert.AreEqual("entries", exception.ParamName);
    }

    [TestMethod]
    public void Find_WithNullHost_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("host, port", "null, 80");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ResolveOverrides.None.Find(null!, 80));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "host", exception.ParamName);

        Assert.AreEqual("host", exception.ParamName);
    }

    [TestMethod]
    public void None_HasNoEntriesAndNoParseError()
    {
        Diagnostics.Arrange("overrides, host, port", "None, example.com, 80");

        var parseError = ResolveOverrides.None.ParseError;
        var found = ResolveOverrides.None.Find("example.com", 80);

        Diagnostics.Act("parse error, found", $"{parseError ?? "null"}, {Describe(found)}");
        Diagnostics.Assert("parse error, found", "null, null", $"{parseError ?? "null"}, {Describe(found)}");

        Assert.IsNull(ResolveOverrides.None.ParseError);
        Assert.IsNull(ResolveOverrides.None.Find("example.com", 80));
    }

    [TestMethod]
    public void Find_ForTheEntrysHostAndPort_ReturnsItsAddressesInOrder()
    {
        // curl --resolve a:80:127.0.0.3,127.0.0.4 http://a/ -> Trying 127.0.0.3:80, then 127.0.0.4:80
        Diagnostics.Arrange("entries", "a:80:127.0.0.3,127.0.0.4");

        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3,127.0.0.4"]);

        Diagnostics.Act("parse error, a:80", $"{overrides.ParseError ?? "null"}, {Describe(overrides.Find("a", 80))}");
        Diagnostics.Assert("a:80", "127.0.0.3, 127.0.0.4", Describe(overrides.Find("a", 80)));

        Assert.IsNull(overrides.ParseError);
        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("127.0.0.3"), IPAddress.Parse("127.0.0.4") },
            overrides.Find("a", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_MatchesTheHostWithoutRegardToCase()
    {
        // curl --resolve a:80:127.0.0.1 http://A:80/ -> Trying 127.0.0.1:80
        Diagnostics.Arrange("entries, host", "a:80:127.0.0.1, A");

        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.1"]);

        Diagnostics.Act("A:80", Describe(overrides.Find("A", 80)));
        Diagnostics.Assert("A:80", "127.0.0.1", Describe(overrides.Find("A", 80)));

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("A", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_ForAnotherPort_ReturnsNull()
    {
        // curl --resolve a:81:127.0.0.1 http://a/ -> resolves a through the system resolver
        Diagnostics.Arrange("entries, port", "a:81:127.0.0.1, 80");

        var overrides = ResolveOverrides.Parse(["a:81:127.0.0.1"]);

        Diagnostics.Act("a:80", Describe(overrides.Find("a", 80)));
        Diagnostics.Assert("a:80", "null", Describe(overrides.Find("a", 80)));

        Assert.IsNull(overrides.Find("a", 80));
    }

    [TestMethod]
    public void Find_WithAWildcardEntry_AnswersForAnyHostOnThatPortAfterANamedEntry()
    {
        // curl --resolve *:80:127.0.0.2 http://zz/ -> RESOLVE *:80 using wildcard, Trying 127.0.0.2:80
        Diagnostics.Arrange("entries", "*:80:127.0.0.2, named:80:127.0.0.9");

        var overrides = ResolveOverrides.Parse(["*:80:127.0.0.2", "named:80:127.0.0.9"]);

        var answers = $"{Describe(overrides.Find("zz", 80))}; {Describe(overrides.Find("named", 80))}; {Describe(overrides.Find("zz", 81))}";
        Diagnostics.Act("zz:80; named:80; zz:81", answers);
        Diagnostics.Assert("zz:80; named:80; zz:81", "127.0.0.2; 127.0.0.9; null", answers);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.2") }, overrides.Find("zz", 80)!.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.9") }, overrides.Find("named", 80)!.ToArray());
        Assert.IsNull(overrides.Find("zz", 81));
    }

    [TestMethod]
    public void Parse_ALaterEntryForTheSameHostAndPort_ReplacesTheEarlierOne()
    {
        // curl --resolve a:80:127.0.0.3 --resolve a:80:127.0.0.4 http://a/ -> old addresses discarded, Trying 127.0.0.4:80
        Diagnostics.Arrange("entries", "a:80:127.0.0.3, a:80:127.0.0.4");

        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3", "a:80:127.0.0.4"]);

        Diagnostics.Act("a:80", Describe(overrides.Find("a", 80)));
        Diagnostics.Assert("a:80", "127.0.0.4", Describe(overrides.Find("a", 80)));

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.4") }, overrides.Find("a", 80)!.ToArray());
    }

    [TestMethod]
    public void Parse_ARemovalEntry_DropsTheEarlierEntry()
    {
        // curl --resolve a:80:127.0.0.3 --resolve -a:80 http://a/ -> resolves a through the system resolver
        Diagnostics.Arrange("entries", "a:80:127.0.0.3, -a:80");

        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3", "-a:80"]);

        Diagnostics.Act("parse error, a:80", $"{overrides.ParseError ?? "null"}, {Describe(overrides.Find("a", 80))}");
        Diagnostics.Assert("parse error, a:80", "null, null", $"{overrides.ParseError ?? "null"}, {Describe(overrides.Find("a", 80))}");

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
        Diagnostics.Arrange("entry", entry);

        var overrides = ResolveOverrides.Parse([entry]);

        Diagnostics.Act("parse error, a:80", $"{overrides.ParseError ?? "null"}, {Describe(overrides.Find("a", 80))}");
        Diagnostics.Assert("parse error, a:80", "null, null", $"{overrides.ParseError ?? "null"}, {Describe(overrides.Find("a", 80))}");

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
        Diagnostics.Arrange("entry", entry);

        var overrides = ResolveOverrides.Parse([entry]);

        Diagnostics.Act("parse error, a:80", $"{overrides.ParseError ?? "null"}, {Describe(overrides.Find("a", 80))}");
        Diagnostics.Assert("a:80", address, Describe(overrides.Find("a", 80)));

        Assert.IsNull(overrides.ParseError);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse(address) }, overrides.Find("a", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_ForABracketedIPv6Host_MatchesTheEntryEitherWay()
    {
        // curl --resolve [::1]:80:127.0.0.1 http://[::1]/ -> Added ::1:80:127.0.0.1, Trying 127.0.0.1:80
        Diagnostics.Arrange("entries", "[::1]:80:127.0.0.1");

        var overrides = ResolveOverrides.Parse(["[::1]:80:127.0.0.1"]);

        var answers = $"{Describe(overrides.Find("[::1]", 80))}; {Describe(overrides.Find("::1", 80))}";
        Diagnostics.Act("[::1]:80; ::1:80", answers);
        Diagnostics.Assert("[::1]:80; ::1:80", "127.0.0.1; 127.0.0.1", answers);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("[::1]", 80)!.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, overrides.Find("::1", 80)!.ToArray());
    }

    [TestMethod]
    public void Find_AcceptsPortZero()
    {
        // curl --resolve a:0:127.0.0.1 ... -> Added a:0:127.0.0.1 to DNS cache
        Diagnostics.Arrange("entries", "a:0:127.0.0.1");

        var overrides = ResolveOverrides.Parse(["a:0:127.0.0.1"]);

        Diagnostics.Act("a:0", Describe(overrides.Find("a", 0)));
        Diagnostics.Assert("a:0", "127.0.0.1", Describe(overrides.Find("a", 0)));

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
        Diagnostics.Arrange("entry", entry);

        var overrides = ResolveOverrides.Parse([entry]);

        Diagnostics.Act("parse error", overrides.ParseError);
        Diagnostics.Assert("parse error", $"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);

        Assert.AreEqual($"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);
    }

    [TestMethod]
    public void Parse_StopsAtTheFirstEntryThatDoesNotParseAndKeepsTheOnesBefore()
    {
        // curl --resolve a:80:127.0.0.3 --resolve a:80:127.0.0.4,xx http://a/ -> (49) ... 'a:80:127.0.0.4,xx'
        Diagnostics.Arrange("entries", "a:80:127.0.0.3, a:80:127.0.0.4,xx, b:80:garbage");

        var overrides = ResolveOverrides.Parse(["a:80:127.0.0.3", "a:80:127.0.0.4,xx", "b:80:garbage"]);

        Diagnostics.Act("parse error, a:80", $"{overrides.ParseError}, {Describe(overrides.Find("a", 80))}");
        Diagnostics.Assert(
            "parse error, a:80",
            "Could not parse CURLOPT_RESOLVE entry 'a:80:127.0.0.4,xx', 127.0.0.3",
            $"{overrides.ParseError}, {Describe(overrides.Find("a", 80))}");

        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'a:80:127.0.0.4,xx'", overrides.ParseError);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.3") }, overrides.Find("a", 80)!.ToArray());
    }

    private static string Describe(IEnumerable<IPAddress>? addresses) =>
        addresses is null ? "null" : string.Join(", ", addresses);
}
