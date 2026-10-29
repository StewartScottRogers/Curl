namespace Curl.Protocol.Abstractions.UnitTests;

/// <summary>
/// <see cref="CurlUrlHostNameMapping" /> maps with the whole UTS #46 table and normalizes to NFC as
/// libidn2 2.3.8 does (BL-3829, ADR-0980). The punycode hosts follow the Unicode 15.1 IDNA
/// mapping table and NFC; measuring them against curl 8.22.0 with libidn2 on Linux is BL-4025.
/// </summary>
[TestClass]
public sealed class CurlUrlHostNameMappingTests
{
    [TestMethod]
    [DataRow("ſtraſſe.invalid", "strasse.invalid", DisplayName = "long s")]
    [DataRow("ℌello.invalid", "hello.invalid", DisplayName = "letterlike symbol")]
    [DataRow("①②.invalid", "12.invalid", DisplayName = "circled digits")]
    [DataRow("Ⅻ.invalid", "xii.invalid", DisplayName = "Roman numeral")]
    [DataRow("㎏.invalid", "kg.invalid", DisplayName = "square unit")]
    [DataRow("™.invalid", "tm.invalid", DisplayName = "trade mark")]
    [DataRow("ℓ.invalid", "l.invalid", DisplayName = "script l")]
    [DataRow("½.invalid", "xn--12-c6t.invalid", DisplayName = "vulgar fraction")]
    public void TryNormalize_CompatibilityCharacter_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("ｶﾞ.invalid", "xn--mck.invalid", DisplayName = "half-width ka and voicing mark")]
    [DataRow("ﾊﾟ.invalid", "xn--odk.invalid", DisplayName = "half-width ha and semi-voicing mark")]
    [DataRow("㋐.invalid", "xn--cck.invalid", DisplayName = "circled katakana")]
    [DataRow("㍿.invalid", "xn--6oqv20b1zgzxr.invalid", DisplayName = "square corporation")]
    [DataRow("㌀.invalid", "xn--cckzd0a3n.invalid", DisplayName = "square apaato")]
    public void TryNormalize_EastAsianCompatibilityCharacter_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("ﻼ.invalid", "xn--mgb1d.invalid", DisplayName = "lam-alef ligature")]
    [DataRow("ﺏﺑ.invalid", "xn--ngba.invalid", DisplayName = "beh presentation forms")]
    [DataRow("ﷲ.invalid", "xn--mgb2dam.invalid", DisplayName = "Allah ligature")]
    [DataRow("ﬓ.invalid", "xn--ibbe.invalid", DisplayName = "Armenian ligature")]
    public void TryNormalize_PresentationForm_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("ꭰ.invalid", "xn--58d.invalid", DisplayName = "Cherokee small letter")]
    [DataRow("ẞa.invalid", "xn--a-pfa.invalid", DisplayName = "capital sharp s")]
    [DataRow("ǅ.invalid", "xn--d-toa.invalid", DisplayName = "title-case digraph")]
    [DataRow("Ω.invalid", "xn--bya.invalid", DisplayName = "ohm sign")]
    [DataRow("Å.invalid", "xn--5ca.invalid", DisplayName = "angstrom sign")]
    [DataRow("Ꞵ.invalid", "xn--968a.invalid", DisplayName = "Latin capital beta")]
    [DataRow("ᾼ.invalid", "xn--mxaq.invalid", DisplayName = "Greek alpha with prosgegrammeni")]
    [DataRow("ΣΑΣ.invalid", "xn--mxa9ab.invalid", DisplayName = "Greek capitals")]
    public void TryNormalize_CaseMapping_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("𝐀𝔥.invalid", "ah.invalid", DisplayName = "mathematical letters")]
    [DataRow("🄰.invalid", "a.invalid", DisplayName = "squared letter")]
    [DataRow("Ⓐ.invalid", "a.invalid", DisplayName = "circled letter")]
    [DataRow("𐐀.invalid", "xn--hj8c.invalid", DisplayName = "Deseret capital")]
    [DataRow("𝐀𐐀.invalid", "xn--a-ps2i.invalid", DisplayName = "mathematical letter and Deseret capital")]
    public void TryNormalize_CharacterOutsideTheBmp_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("가각.invalid", "xn--o39ac.invalid", DisplayName = "precomposed syllables")]
    [DataRow("가각.invalid", "xn--o39ac.invalid", DisplayName = "jamo")]
    [DataRow("ᄒ한.invalid", "xn--hqd4161h.invalid", DisplayName = "leading jamo before a syllable")]
    public void TryNormalize_Hangul_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("ऩ.invalid", "xn--m2b.invalid", DisplayName = "precomposed nnna")]
    [DataRow("nऩ.invalid", "xn--n-zvd.invalid", DisplayName = "na and nukta compose")]
    [DataRow("क़.invalid", "xn--11b2f.invalid", DisplayName = "excluded qa decomposes")]
    [DataRow("क़.invalid", "xn--11b2f.invalid", DisplayName = "ka and nukta stay apart")]
    public void TryNormalize_IndicNukta_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("ậ.invalid", "xn--zkg.invalid", DisplayName = "below then above")]
    [DataRow("ậ.invalid", "xn--zkg.invalid", DisplayName = "above then below")]
    [DataRow("ậ.invalid", "xn--zkg.invalid", DisplayName = "precomposed")]
    [DataRow("q̣̇.invalid", "xn--q-9bb7f.invalid", DisplayName = "no composite")]
    [DataRow("ḍ̇.invalid", "xn--rsa949k.invalid", DisplayName = "dot below composes first")]
    [DataRow("é́.invalid", "xn--9ca68h.invalid", DisplayName = "second acute stays")]
    [DataRow("Ą̊.invalid", "xn--2da84h.invalid", DisplayName = "ring and ogonek")]
    public void TryNormalize_CombiningMarks_ConvertsAsCurl(string host, string expected) =>
        AssertConvertsTo(host, expected);

    [TestMethod]
    [DataRow("각ᆨ", "각ᆨ", DisplayName = "second trailing jamo")]
    [DataRow("가a", "가a", DisplayName = "syllable before a letter")]
    [DataRow("ᄒa", "ᄒa", DisplayName = "leading jamo before a letter")]
    [DataRow("q́́", "q́́", DisplayName = "blocked mark")]
    [DataRow("́é", "́é", DisplayName = "leading mark")]
    [DataRow("𑠎᠎", "𑠎", DisplayName = "supplementary character beside a dropped one")]
    [DataRow("｟", "⦅", DisplayName = "full-width parenthesis")]
    public void Map_Name_MapsAndComposes(string name, string expected) =>
        Assert.AreEqual(expected, CurlUrlHostNameMapping.Map(name));

    private static void AssertConvertsTo(string host, string expected)
    {
        bool accepted = CurlUrlHost.TryNormalizeReadingHost(host, refuseUnconvertibleName: true, hostAsWindows1252: false, out _, out string idnHost, out _, out _);

        Assert.IsTrue(accepted);
        Assert.AreEqual(expected, idnHost);
    }
}
