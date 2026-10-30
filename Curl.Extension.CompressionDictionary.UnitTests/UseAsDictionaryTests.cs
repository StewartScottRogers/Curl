namespace Curl.Extension.CompressionDictionary.UnitTests;

[TestClass]
public sealed class UseAsDictionaryTests
{
    // RFC 9842 section 2.1: match, match-dest, id and type.
    [TestMethod]
    public void Parse_AllMembers_ReadsEach()
    {
        var parsed = UseAsDictionary.Parse("match=\"/app/*\", match-dest=(\"document\" \"frame\"), id=\"v1 \\\"a\\\\b\\\"\", type=raw");

        Assert.IsNotNull(parsed);
        Assert.AreEqual("/app/*", parsed.Match);
        CollectionAssert.AreEqual(new[] { "document", "frame" }, parsed.MatchDestinations.ToArray());
        Assert.AreEqual("v1 \"a\\b\"", parsed.Id);
    }

    // RFC 9842 section 2.1: match-dest and id default to empty, type to raw.
    [TestMethod]
    public void Parse_OnlyMatch_DefaultsTheRest()
    {
        var parsed = UseAsDictionary.Parse("  match=\"/js/*.js\"\t");

        Assert.IsNotNull(parsed);
        Assert.AreEqual(0, parsed.MatchDestinations.Count);
        Assert.AreEqual(string.Empty, parsed.Id);
    }

    // RFC 8941 section 4.2.2: unknown members, parameters and every bare item type are parsed and ignored.
    [TestMethod]
    [DataRow("match=\"/a\";p=1, n=-1.5, b=?0, t=*x/y:z;q, s=:AAEC:, flag, flag2;w=\"x\", l=(1 ?1);p, e=(), z=1, z=2")]
    [DataRow("match=\"/a\", list=( \"x\" )")]
    [DataRow("match=\"/a\",flag")]
    public void Parse_UnknownMembers_AreIgnored(string fieldValue) =>
        Assert.AreEqual("/a", UseAsDictionary.Parse(fieldValue)?.Match);

    // RFC 9842 section 2.1: a header without a usable match, or with a type other than raw, is ignored.
    [TestMethod]
    [DataRow("id=\"x\"")]
    [DataRow("match=token")]
    [DataRow("match=\"/a\", type=other")]
    [DataRow("match=\"/a\", type=\"raw\"")]
    [DataRow("match=\"/a\", id=token")]
    [DataRow("match=\"/a\", match-dest=\"document\"")]
    [DataRow("match=\"/a\", match-dest=(document)")]
    public void Parse_IgnoredHeader_ReturnsNull(string fieldValue) => Assert.IsNull(UseAsDictionary.Parse(fieldValue));

    // RFC 9842 section 2.1.3: an id longer than 1024 characters is ignored.
    [TestMethod]
    public void Parse_IdLongerThan1024_ReturnsNull()
    {
        Assert.IsNotNull(UseAsDictionary.Parse("match=\"/a\", id=\"" + new string('x', 1024) + "\""));
        Assert.IsNull(UseAsDictionary.Parse("match=\"/a\", id=\"" + new string('x', 1025) + "\""));
    }

    // RFC 8941 section 4.2: a syntax error fails the whole field.
    [TestMethod]
    [DataRow("Match=\"/a\"")]
    [DataRow("match=\"/a\" x")]
    [DataRow("match=\"/a\",")]
    [DataRow("match=\"/a\";")]
    [DataRow("match=\"/a\";p=")]
    [DataRow("match=\"/a\";p=@")]
    [DataRow("match=\"/a\", f;")]
    [DataRow("match=")]
    [DataRow("match=@")]
    [DataRow("match=\"/a")]
    [DataRow("match=\"/a\\n\"")]
    [DataRow("match=\"/a\tb\"")]
    [DataRow("match=\"/a\\")]
    [DataRow("match=\"/é\"")]
    [DataRow("match=\"/a\", n=-")]
    [DataRow("match=\"/a\", n=1.")]
    [DataRow("match=\"/a\", n=1.2.3")]
    [DataRow("match=\"/a\", b=?2")]
    [DataRow("match=\"/a\", b=?")]
    [DataRow("match=\"/a\", s=:AAEC")]
    [DataRow("match=\"/a\", s=:A!:")]
    [DataRow("match=\"/a\", l=(1")]
    [DataRow("match=\"/a\", l=(1;")]
    [DataRow("match=\"/a\", l=(1 ")]
    [DataRow("match=\"/a\", l=(1,2)")]
    [DataRow("match=\"/a\", l=(@)")]
    [DataRow("match=\"/a\", l=(1);")]
    public void Parse_InvalidStructuredField_ReturnsNull(string fieldValue) => Assert.IsNull(UseAsDictionary.Parse(fieldValue));
}
