namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// CGR-019: a host name that is not ASCII is mapped as UTS #46 does before punycode, so the
/// host contacted is the one curl 8.22.0 (libidn2) contacts.
/// </summary>
[TestClass]
public sealed class Cgr019Tests
{
    [TestMethod]
    [DataRow("BÜCHER.example", "xn--bcher-kva.example")]
    [DataRow("École.example", "xn--cole-9oa.example")]
    [DataRow("ΣΣ", "xn--4xaa")]
    [DataRow("İstanbul", "xn--istanbul-o0e")]
    [DataRow("Ａb", "ab")]
    [DataRow("ﬁle", "file")]
    [DataRow("a­b", "ab")]
    [DataRow("a‍b", "ab")]
    [DataRow("école", "xn--cole-9oa")]
    [DataRow("straße", "xn--strae-oqa")]
    public void TryNormalize_HostThatIsNotAscii_ConvertsTheMappedName(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalize(host, refuseUnconvertibleName: true, out string normalized, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(host, normalized);
        Assert.AreEqual(expected, idnHost);
    }

    [TestMethod]
    [DataRow("BÜCHER", "bücher")]
    [DataRow("İ", "i̇")]
    [DataRow("Ａｚ０", "az0")]
    [DataRow("ﬀﬃﬆ", "ffffist")]
    [DataRow("﫿﬇＀｟į", "﫿﬇＀｟į")]
    [DataRow("a­͏​‌‍⁠️﻿b", "ab")]
    [DataRow("é", "é")]
    [DataRow("ǖ", "ǖ")]
    [DataRow("́e", "́e")]
    [DataRow("x́", "x́")]
    [DataRow("straßeς", "straßeς")]
    public void Map_Character_MapsAsUts46(string name, string expected)
    {
        Assert.AreEqual(expected, CurlUrlHostNameMapping.Map(name));
    }
}
