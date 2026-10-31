using Curl.Extension.Dane.Wire;
using static Curl.Extension.Dane.Dnssec.DnssecTestRecords;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Checks <see cref="DnskeyKeyTag" /> against RFC 4034 appendix B with RFC 6605 section 6's keys and the recorded
/// root keys.
/// </summary>
[TestClass]
public sealed class DnskeyKeyTagTests
{
    [TestMethod]
    public void Compute_Rfc6605Section6_1P256Key_Is55648()
    {
        DnskeyRecordData key = new(257, 3, 13, Convert.FromBase64String("GojIhhXUN/u4v54ZQqGSnyhWJwaubCvTmeexv7bR6edbkrSqQpF64cYbcB7wNcP+e+MAnLr+Wi9xMWyQLc8NAA=="));

        Assert.AreEqual(55648, DnskeyKeyTag.Compute(key));
    }

    [TestMethod]
    public void Compute_RfcAppendixBRecordedRootKeys_IncludeKsks20326And38696()
    {
        List<DnsResourceRecord> rootKeys = Answers(Decode(RecordedDnssecResponses.RootDnskey), DnssecRecordType.Dnskey);

        ushort[] tags = [.. rootKeys.Select(record => DnskeyKeyTag.Compute((DnskeyRecordData)record.Data!))];

        CollectionAssert.IsSubsetOf(new ushort[] { 20326, 38696 }, tags);
    }
}
