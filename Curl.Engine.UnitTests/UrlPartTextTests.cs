namespace Curl.Engine.UnitTests;

[TestClass]
public sealed class UrlPartTextTests
{
    [TestMethod]
    [DataRow("a%aab%aAc%Aa", "a%AAb%AAc%AA")]
    [DataRow("w%wf%xf%f", "w%wf%xf%f")]
    public void UrlEncodeString_PercentEscapes_UpperCasesOnlyValidLowerCaseOnes(string text, string expected)
    {
        string encoded = UrlPartText.UrlEncodeString(text, relative: true, UrlEncodeQuery.No);

        Assert.AreEqual(expected, encoded);
    }

    [TestMethod]
    [DataRow("http://le ave\x01/a b", "http://le ave\x01/a%20b")]
    [DataRow("http://host?a b", "http://host?a%20b")]
    [DataRow("host a", "host a")]
    public void UrlEncodeString_NotRelative_KeepsTextBeforeTheHostSeparator(string text, string expected)
    {
        string encoded = UrlPartText.UrlEncodeString(text, relative: false, UrlEncodeQuery.No);

        Assert.AreEqual(expected, encoded);
    }

    [TestMethod]
    [DataRow(0, "a%20b?c%20d")]
    [DataRow(1, "a%20b?c+d")]
    [DataRow(2, "a+b?c+d")]
    public void UrlEncodeString_Query_WritesSpacesAsUpstreamDoes(int query, string expected)
    {
        string encoded = UrlPartText.UrlEncodeString("a b?c d", relative: true, (UrlEncodeQuery)query);

        Assert.AreEqual(expected, encoded);
    }

    [TestMethod]
    public void Encode_NotAQuery_TurnsToPlusAfterQuestionMarkAndUpperCasesEscapes()
    {
        string encoded = UrlPartText.Encode("a b?c d%aa", query: false);

        Assert.AreEqual("a%20b?c+d%AA", encoded);
    }
}
