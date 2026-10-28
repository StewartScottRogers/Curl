namespace Curl.Networking;

/// <summary>
/// Pins CGR-180: a <c>--resolve</c> entry with an empty host is ignored, not a parse error (measured against curl 8.22.0).
/// </summary>
[TestClass]
public sealed class Cgr180Tests
{
    [TestMethod]
    [DataRow(":")]
    [DataRow("::")]
    [DataRow(":::")]
    [DataRow(":80")]
    [DataRow(":x")]
    [DataRow("+:")]
    public void Parse_EntryWithEmptyHost_IsIgnoredWithoutError(string entry)
    {
        var overrides = ResolveOverrides.Parse([entry, "h:80:1.2.3.4"]);

        Assert.IsNull(overrides.ParseError);
        Assert.HasCount(1, overrides.Entries);
        Assert.AreEqual("h", overrides.Entries[0].Host);
    }

    [TestMethod]
    [DataRow("a:")]
    [DataRow("a:80")]
    [DataRow("a::")]
    public void Parse_EntryWithHostButNoAddress_StillFailsToParse(string entry)
    {
        var overrides = ResolveOverrides.Parse([entry]);

        Assert.AreEqual($"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);
    }
}
