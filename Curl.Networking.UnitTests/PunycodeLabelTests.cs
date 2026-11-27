namespace Curl.Networking;

[TestClass]
public class PunycodeLabelTests
{
    [TestMethod]
    [DataRow("bcher-kva", "bücher")]
    [DataRow("bcher-KVA", "bücher")]
    [DataRow("tdaa", "üü")]
    [DataRow("abc-", "abc")]
    [DataRow("", "")]
    public void Decode_PunycodeLabel_IsTheText(string encoded, string expected) =>
        Assert.AreEqual(expected, PunycodeLabel.Decode(encoded));

    [TestMethod]
    [DataRow("!!")] // no digit
    [DataRow("-abc")] // a leading hyphen ends no basic part, so it is read as a digit
    [DataRow("z")] // cut short
    [DataRow("999999999999")] // the delta overflows
    [DataRow("99999a")] // past the last code point
    [DataRow("bücher-")] // not ASCII
    public void Decode_WhatIsNotPunycode_IsNull(string encoded) => Assert.IsNull(PunycodeLabel.Decode(encoded));

    [TestMethod]
    public void Decode_ALabelOfMoreCodePointsThanFitAConstraint_IsNull() =>
        Assert.IsNull(PunycodeLabel.Decode(new string('a', 256) + "-"));
}
