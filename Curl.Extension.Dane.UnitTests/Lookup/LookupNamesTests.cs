using Curl.Extension.Dane.Dnssec;

namespace Curl.Extension.Dane.Lookup;

[TestClass]
public sealed class LookupNamesTests
{
    [TestMethod]
    [DataRow("_443._tcp.example.com")]
    [DataRow("_443._tcp.example.com.")]
    public void FromText_WithOrWithoutTrailingDot_GivesTheSameName_Rfc1035Section3_1(string text)
    {
        CollectionAssert.AreEqual(
            DnssecTestRecords.Name("_443._tcp.example.com.").Wire.ToArray(),
            LookupNames.FromText(text).Wire.ToArray());
    }

    [TestMethod]
    public void FromText_EmptyLabel_ThrowsArgumentException_Rfc1035Section3_1() =>
        Assert.ThrowsExactly<ArgumentException>(() => LookupNames.FromText("a..example"));

    [TestMethod]
    [DataRow("example.", "a.example.", -1)]
    [DataRow("Z.example.", "a.example.", 1)]
    [DataRow("A.Example.", "a.example.", 0)]
    [DataRow("z.example.", "a.z.example.", -1)]
    public void CompareCanonical_Rfc4034Section6_1_Example_SortsFromTheRightmostLabel(string first, string second, int sign)
    {
        Assert.AreEqual(sign, Math.Sign(LookupNames.CompareCanonical(DnssecTestRecords.Name(first), DnssecTestRecords.Name(second))));
    }
}
