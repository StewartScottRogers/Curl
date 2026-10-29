namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// libidn2 2.3.8 applies the STD3 rules to what a character maps to, so curl refuses a host holding
/// a character that maps to text with a character other than a letter, digit, hyphen or dot (BL-4076).
/// </summary>
[TestClass]
public sealed class CurlUrlHostNameMappingStd3Tests
{
    [TestMethod]
    [DataRow("℀", DisplayName = "account of, a/c")]
    [DataRow("℁", DisplayName = "addressed to the subject, a/s")]
    [DataRow("℅", DisplayName = "care of, c/o")]
    [DataRow("℆", DisplayName = "cada una, c/u")]
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
    [DataRow("︐", DisplayName = "vertical comma")]
    [DataRow("︳", DisplayName = "vertical low line")]
    public void TryNormalizeReadingHost_CharacterMappingOutsideStd3Rules_IsRefused(string character)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(character + ".invalid", refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out _, out _, out _);

        Assert.IsFalse(accepted);
    }
}
