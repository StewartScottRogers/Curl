using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

[TestClass]
public sealed class DenialOfExistenceTests
{
    private const DnssecRecordType NameServer = (DnssecRecordType)2;

    private const DnssecRecordType StartOfAuthority = (DnssecRecordType)6;

    private static readonly byte[] LowestHash = new byte[20];

    private static readonly byte[] HighestHash = [.. Enumerable.Repeat((byte)0xFF, 20)];

    private static DnsName Name(string text) => DnssecTestRecords.Name(text);

    [TestMethod]
    [DataRow(DnssecRecordType.Tlsa)]
    [DataRow(DnssecRecordType.Cname)]
    public void ProvesNoData_NsecListsTheTypeOrCname_IsFalse_Rfc4035Section5_4(DnssecRecordType listed)
    {
        DnsResourceRecord nsec = SignedTree.Nsec("a.example.", "b.example.", listed);

        Assert.IsFalse(DenialOfExistence.ProvesNoData(Name("a.example."), DnssecRecordType.Tlsa, [nsec]));
    }

    [TestMethod]
    public void ProvesNoData_NoRecordForTheName_IsFalse_Rfc4035Section5_4()
    {
        DnsResourceRecord nsec = SignedTree.Nsec("a.example.", "c.example.", DnssecRecordType.Rrsig);

        Assert.IsFalse(DenialOfExistence.ProvesNoData(Name("b.example."), DnssecRecordType.Tlsa, [nsec]));
    }

    [TestMethod]
    [DataRow(new[] { NameServer, DnssecRecordType.Ds })]
    [DataRow(new[] { NameServer, StartOfAuthority })]
    [DataRow(new[] { DnssecRecordType.Rrsig })]
    public void ProvesInsecureDelegation_NsecWithoutAnUnsignedDelegation_IsFalse_Rfc4035Section5_2(DnssecRecordType[] types)
    {
        DnsResourceRecord nsec = SignedTree.Nsec("a.example.", "b.example.", types);

        Assert.IsFalse(DenialOfExistence.ProvesInsecureDelegation(Name("a.example."), [nsec]));
    }

    [TestMethod]
    public void ProvesInsecureDelegation_Nsec3WithoutOptOut_IsFalse_Rfc5155Section8_6()
    {
        DnsResourceRecord closestEncloser = SignedTree.Nsec3("example.", SignedTree.HashOf("example."), HighestHash, types: [StartOfAuthority]);
        DnsResourceRecord covering = SignedTree.Nsec3("example.", LowestHash, HighestHash);

        Assert.IsFalse(DenialOfExistence.ProvesInsecureDelegation(Name("a.example."), [closestEncloser, covering]));
    }

    [TestMethod]
    public void ProvesInsecureDelegation_NoClosestEncloser_IsFalse_Rfc5155Section8_3()
    {
        DnsResourceRecord nsec = SignedTree.Nsec("z.example.", "zz.example.", NameServer);

        Assert.IsFalse(DenialOfExistence.ProvesInsecureDelegation(Name("a.example."), [nsec]));
    }

    [TestMethod]
    public void ProvesNameError_NsecNextNameIsTheCloserEncloser_CoversItsWildcard_Rfc4035Section5_4()
    {
        DnsResourceRecord nsec = SignedTree.Nsec("example.", "c.a.example.", StartOfAuthority);

        Assert.IsTrue(DenialOfExistence.ProvesNameError(Name("b.a.example."), [nsec]));
    }

    [TestMethod]
    public void ProvesNameError_WildcardNotCovered_IsFalse_Rfc4035Section5_4()
    {
        DnsResourceRecord nsec = SignedTree.Nsec("a.example.", "c.example.", DnssecRecordType.Rrsig);

        Assert.IsFalse(DenialOfExistence.ProvesNameError(Name("b.example."), [nsec]));
    }

    [TestMethod]
    [DataRow("0.example.")]
    [DataRow("zzz.example.")]
    public void ProvesNameError_NameOutsideTheNsecSpan_IsFalse_Rfc4035Section5_4(string name)
    {
        DnsResourceRecord nsec = SignedTree.Nsec("a.example.", "c.example.", DnssecRecordType.Rrsig);

        Assert.IsFalse(DenialOfExistence.ProvesNameError(Name(name), [nsec]));
    }

    [TestMethod]
    public void ProvesNameError_LastNsecWrapsToTheApex_CoversNamesAfterIt_Rfc4035Section4_1_1()
    {
        DnsResourceRecord last = SignedTree.Nsec("y.example.", "example.", DnssecRecordType.Rrsig);
        DnsResourceRecord apex = SignedTree.Nsec("example.", "a.example.", StartOfAuthority);

        Assert.IsTrue(DenialOfExistence.ProvesNameError(Name("z.example."), [last, apex]));
    }

    [TestMethod]
    public void ProvesNameError_Nsec3WithoutNextCloserCovered_IsFalse_Rfc5155Section8_4()
    {
        byte[] apexHash = SignedTree.HashOf("example.");
        byte[] nextHash = [.. apexHash[..^1], (byte)(apexHash[^1] + 1)];
        DnsResourceRecord closestEncloser = SignedTree.Nsec3("example.", apexHash, nextHash, types: [StartOfAuthority]);

        Assert.IsFalse(DenialOfExistence.ProvesNameError(Name("a.example."), [closestEncloser]));
    }

    [TestMethod]
    [DataRow((byte)2, (ushort)1)]
    [DataRow(Nsec3Hash.Sha1Algorithm, (ushort)151)]
    public void ProvesNoData_Nsec3WithUnknownHashOrTooManyIterations_IsIgnored_Rfc9276Section3_2(byte hashAlgorithm, ushort iterations)
    {
        DnsResourceRecord nsec3 = SignedTree.Nsec3("example.", SignedTree.HashOf("a.example."), HighestHash, iterations: iterations);
        nsec3 = nsec3 with { Data = ((Nsec3RecordData)nsec3.Data!) with { HashAlgorithm = hashAlgorithm } };

        Assert.IsFalse(DenialOfExistence.ProvesNoData(Name("a.example."), DnssecRecordType.Tlsa, [nsec3]));
    }

    [TestMethod]
    public void ProvesNoData_Nsec3OwnerNotBase32Hex_IsIgnored_Rfc5155Section3()
    {
        Nsec3RecordData data = new(Nsec3Hash.Sha1Algorithm, 0, 1, [], HighestHash, []);
        DnsResourceRecord nsec3 = DnssecTestRecords.Record("not-base32.example.", DnssecRecordType.Nsec3, DnsMessageWriter.Nsec3(data), data);

        Assert.IsFalse(DenialOfExistence.ProvesNoData(Name("a.example."), DnssecRecordType.Tlsa, [nsec3]));
    }

    [TestMethod]
    public void ProvesNoCloserMatch_Nsec3CoversTheNextCloserName_IsTrue_Rfc5155Section8_8()
    {
        DnsResourceRecord nsec = SignedTree.Nsec("z.example.", "zz.example.", DnssecRecordType.Rrsig);
        DnsResourceRecord covering = SignedTree.Nsec3("example.", LowestHash, HighestHash);

        Assert.IsTrue(DenialOfExistence.ProvesNoCloserMatch(Name("a.b.example."), 1, [nsec, covering]));
    }
}
