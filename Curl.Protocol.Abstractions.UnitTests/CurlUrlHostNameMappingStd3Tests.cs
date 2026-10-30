namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// libidn2 2.3.8 passes a label that maps to ASCII as it is, even when the mapping holds a character
/// other than a letter, digit, hyphen or dot, but refuses such a mapping in a label that stays outside
/// ASCII, as measured with curl 8.18.0 (BL-4076, BL-4079).
/// </summary>
[TestClass]
public sealed class CurlUrlHostNameMappingStd3Tests
{
    [TestMethod]
    [DataRow("a℀b.test", "aa/cb.test", DisplayName = "account of, a/c")]
    [DataRow("a℁b.test", "aa/sb.test", DisplayName = "addressed to the subject, a/s")]
    [DataRow("a℅b.test", "ac/ob.test", DisplayName = "care of, c/o")]
    [DataRow("a℆b.test", "ac/ub.test", DisplayName = "cada una, c/u")]
    [DataRow("a︐b.test", "a,b.test", DisplayName = "vertical comma")]
    [DataRow("a︳b.test", "a_b.test", DisplayName = "vertical low line")]
    [DataRow("a℀.é.test", "aa/c.xn--9ca.test", DisplayName = "account of in a label of its own")]
    public void TryNormalizeReadingHost_MappingOutsideStd3RulesInAsciiLabel_IsKept(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }

    [TestMethod]
    [DataRow("ﷺ", DisplayName = "sallallahou alayhe wasallam ligature")]
    [DataRow("ﷻ", DisplayName = "jalla jalalouhou ligature")]
    [DataRow("ﹰ", DisplayName = "Arabic fathatan isolated form")]
    [DataRow("ﹲ", DisplayName = "Arabic dammatan isolated form")]
    [DataRow("ﹴ", DisplayName = "Arabic kasratan isolated form")]
    [DataRow("ﹶ", DisplayName = "Arabic fatha isolated form")]
    [DataRow("ﹸ", DisplayName = "Arabic damma isolated form")]
    [DataRow("ﹺ", DisplayName = "Arabic kasra isolated form")]
    [DataRow("ﹼ", DisplayName = "Arabic shadda isolated form")]
    [DataRow("ﹾ", DisplayName = "Arabic sukun isolated form")]
    [DataRow("ﱞ", DisplayName = "Arabic shadda with dammatan")]
    [DataRow("ﱟ", DisplayName = "Arabic shadda with kasratan")]
    [DataRow("ﱠ", DisplayName = "Arabic shadda with fatha")]
    [DataRow("ﱡ", DisplayName = "Arabic shadda with damma")]
    [DataRow("ﱢ", DisplayName = "Arabic shadda with kasra")]
    [DataRow("ﱣ", DisplayName = "Arabic superscript alef with shadda")]
    [DataRow("℀é", DisplayName = "account of beside a letter outside ASCII")]
    [DataRow("︐é", DisplayName = "vertical comma beside a letter outside ASCII")]
    [DataRow(" é", DisplayName = "no-break space beside a letter outside ASCII")]
    public void TryNormalizeReadingHost_MappingOutsideStd3RulesInLabelNotAscii_IsRefused(string character)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("a" + character + "b.invalid", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out _);

        Assert.IsFalse(accepted);
    }

    [TestMethod]
    public void TryNormalizeReadingHost_MappingOutsideStd3RulesInLastLabelNotAscii_IsRefused()
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost("example.a℀é", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out _);

        Assert.IsFalse(accepted);
    }
}
