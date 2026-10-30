using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

[TestClass]
public sealed class TlsaLookupTests
{
    private const DnssecRecordType NameServer = (DnssecRecordType)2;

    private const DnssecRecordType StartOfAuthority = (DnssecRecordType)6;

    private static readonly byte[] LowestHash = new byte[20];

    private static readonly byte[] HighestHash = [.. Enumerable.Repeat((byte)0xFF, 20)];

    [TestMethod]
    public async Task LookupAsync_SignedTlsaRrset_IsSecure_Rfc4035Section5()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(SignedTree.Tlsa()), []));

        TlsaLookupResult result = await tree.LookupAsync();

        Assert.AreEqual(TlsaLookupStatus.Secure, result.Status);
        Assert.HasCount(1, result.Records);
        CollectionAssert.AreEqual(SignedTree.Association, result.Records[0].CertificateAssociationData);
        CollectionAssert.AreEqual(
            new[] { ". Dnskey", "test. Ds", "test. Dnskey", "example.test. Ds", "example.test. Dnskey", "_tcp.example.test. Ds", "_443._tcp.example.test. Tlsa" },
            tree.Server.UdpQuestions);
    }

    [TestMethod]
    public async Task LookupAsync_TamperedSignature_IsBogus_Rfc4035Section5_3()
    {
        SignedTree tree = new();
        List<DnsResourceRecord> answers = tree.Example.Signed(SignedTree.Tlsa());
        RrsigRecordData signature = (RrsigRecordData)answers[1].Data!;
        byte[] tampered = [.. signature.Signature];
        tampered[0] ^= 1;
        answers[1] = answers[1] with { Data = signature with { Signature = tampered } };
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, answers, []));

        TlsaLookupResult result = await tree.LookupAsync();

        Assert.AreEqual(TlsaLookupStatus.Bogus, result.Status);
        Assert.IsEmpty(result.Records);
    }

    [TestMethod]
    public async Task LookupAsync_UnsignedTlsaInSignedZone_IsBogus_Rfc4035Section5()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [SignedTree.Tlsa()], []));

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_NsecProvedUnsignedDelegation_IsInsecure_Rfc4035Section5_2()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec = SignedTree.Nsec("example.test.", "zzz.test.", NameServer, DnssecRecordType.Rrsig, DnssecRecordType.Nsec);
        tree.Server.Reply("example.test.", DnssecRecordType.Ds, new DnsReply(0, [], tree.Test.Signed(nsec)));
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [SignedTree.Tlsa()], []));

        TlsaLookupResult result = await tree.LookupAsync();

        Assert.AreEqual(TlsaLookupStatus.Insecure, result.Status);
        Assert.IsEmpty(result.Records);
        Assert.DoesNotContain(SignedTree.TlsaName + " Tlsa", tree.Server.UdpQuestions);
    }

    [TestMethod]
    public async Task LookupAsync_Nsec3OptOutCoversDelegation_IsInsecure_Rfc5155Section8_6()
    {
        SignedTree tree = new();
        DnsResourceRecord closestEncloser = SignedTree.Nsec3("test.", SignedTree.HashOf("test."), HighestHash, types: [StartOfAuthority, NameServer]);
        DnsResourceRecord optOut = SignedTree.Nsec3("test.", LowestHash, HighestHash, flags: 1, types: [NameServer]);
        tree.Server.Reply("example.test.", DnssecRecordType.Ds, new DnsReply(0, [], tree.Test.SignedEach(closestEncloser, optOut)));

        Assert.AreEqual(TlsaLookupStatus.Insecure, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_NsecProvedNoData_IsAbsent_Rfc4035Section5_4()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec = SignedTree.Nsec(SignedTree.TlsaName, "zzz.example.test.", DnssecRecordType.Rrsig, DnssecRecordType.Nsec);
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [], tree.Example.Signed(nsec)));

        Assert.AreEqual(TlsaLookupStatus.Absent, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_NsecProvedNameError_IsAbsent_Rfc4035Section5_4()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec = SignedTree.Nsec("example.test.", "zzz.example.test.", StartOfAuthority, DnssecRecordType.Rrsig, DnssecRecordType.Nsec);
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(3, [], tree.Example.Signed(nsec)));

        Assert.AreEqual(TlsaLookupStatus.Absent, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_NameErrorWithoutProof_IsBogus_Rfc4035Section5_4()
    {
        SignedTree tree = new();
        DnsResourceRecord unsignedNsec = SignedTree.Nsec("example.test.", "zzz.example.test.", StartOfAuthority);
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(3, [], [unsignedNsec]));

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_Nsec3ProvedNoData_IsAbsent_Rfc5155Section8_5()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec3 = SignedTree.Nsec3("example.test.", SignedTree.HashOf(SignedTree.TlsaName), HighestHash, types: [DnssecRecordType.Rrsig]);
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [], tree.Example.Signed(nsec3)));

        Assert.AreEqual(TlsaLookupStatus.Absent, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_Nsec3ProvedNameError_IsAbsent_Rfc5155Section8_4()
    {
        SignedTree tree = new();
        DnsResourceRecord closestEncloser = SignedTree.Nsec3("example.test.", SignedTree.HashOf("example.test."), HighestHash, types: [StartOfAuthority]);
        DnsResourceRecord covering = SignedTree.Nsec3("example.test.", LowestHash, LowestHash.Select(octet => (byte)1).ToArray());
        DnsResourceRecord wrapping = SignedTree.Nsec3("example.test.", HighestHash.Select(octet => (byte)0xFE).ToArray(), LowestHash);
        DnsResourceRecord middle = SignedTree.Nsec3("example.test.", LowestHash.Select(octet => (byte)1).ToArray(), HighestHash.Select(octet => (byte)0xFE).ToArray());
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(3, [], tree.Example.SignedEach(closestEncloser, covering, wrapping, middle)));

        Assert.AreEqual(TlsaLookupStatus.Absent, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_WildcardAnswerWithNoCloserMatchProved_IsSecure_Rfc4035Section5_3_4()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec = SignedTree.Nsec("example.test.", "zzz.example.test.", StartOfAuthority);
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(SignedTree.Tlsa(), labels: 2), tree.Example.Signed(nsec)));

        Assert.AreEqual(TlsaLookupStatus.Secure, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_WildcardAnswerWithoutProof_IsBogus_Rfc4035Section5_3_4()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(SignedTree.Tlsa(), labels: 2), []));

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_SignedAlias_FollowsCnameToSignedTlsa_Rfc7671Section7()
    {
        SignedTree tree = new();
        const string target = "_443._tcp.other.example.test.";
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(Alias(SignedTree.TlsaName, target)), []));
        tree.Server.Reply(target, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(SignedTree.Tlsa(target)), []));

        TlsaLookupResult result = await tree.LookupAsync();

        Assert.AreEqual(TlsaLookupStatus.Secure, result.Status);
        Assert.Contains(target + " Tlsa", tree.Server.UdpQuestions);
    }

    [TestMethod]
    public async Task LookupAsync_UnsignedAlias_IsBogus_Rfc7671Section7()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [Alias(SignedTree.TlsaName, "other.example.test.")], []));

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_AliasLoop_FailsAfterEightAliases()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(Alias(SignedTree.TlsaName, SignedTree.TlsaName)), []));

        TlsaLookupResult result = await tree.LookupAsync();

        Assert.AreEqual(TlsaLookupStatus.Failed, result.Status);
        Assert.AreEqual(9, tree.Server.UdpQuestions.Count(question => question == SignedTree.TlsaName + " Tlsa"));
    }

    [TestMethod]
    public async Task LookupAsync_TruncatedUdpReply_RetriesOverTcp_Rfc7766Section5()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(SignedTree.Tlsa()), []) { IsTruncatedOverUdp = true });

        TlsaLookupResult result = await tree.LookupAsync();

        Assert.AreEqual(TlsaLookupStatus.Secure, result.Status);
        CollectionAssert.AreEqual(new[] { SignedTree.TlsaName + " Tlsa" }, tree.Server.TcpQuestions);
    }

    [TestMethod]
    public async Task LookupAsync_ResolverNeverAnswers_FailsAtTimeout()
    {
        SignedTree tree = new();
        tree.Server.IsSilentOverUdp = true;

        Assert.AreEqual(TlsaLookupStatus.Failed, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    [DataRow(".", DnssecRecordType.Dnskey)]
    [DataRow("example.test.", DnssecRecordType.Ds)]
    [DataRow(SignedTree.TlsaName, DnssecRecordType.Tlsa)]
    public async Task LookupAsync_ServerFailure_Fails(string name, DnssecRecordType type)
    {
        SignedTree tree = new();
        tree.Server.Reply(name, type, new DnsReply(2, [], []));

        Assert.AreEqual(TlsaLookupStatus.Failed, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_UnsignedDelegationSigner_IsBogus_Rfc4035Section5_2()
    {
        SignedTree tree = new();
        tree.Server.Reply("example.test.", DnssecRecordType.Ds, new DnsReply(0, [tree.Example.DsRecord], []));

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_NoDnskeyRrset_IsBogus_Rfc4035Section5_2()
    {
        SignedTree tree = new();
        tree.Server.Reply("example.test.", DnssecRecordType.Dnskey, DnsReply.Empty);

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_DnskeyNotNamedByDelegationSigner_IsBogus_Rfc4035Section5_2()
    {
        SignedTree tree = new();
        SignedZone impostor = new("example.test.");
        tree.PublishKeys(impostor);

        Assert.AreEqual(TlsaLookupStatus.Bogus, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_DelegationSignerWithUnsupportedDigest_IsInsecure_Rfc4035Section5_2()
    {
        SignedTree tree = new();
        DsRecordData sha1 = tree.Example.DelegationSigner with { DigestType = 1 };
        DnsResourceRecord delegationSigner = DnssecTestRecords.Record("example.test.", DnssecRecordType.Ds, DnsMessageWriter.Ds(sha1), sha1);
        tree.Server.Reply("example.test.", DnssecRecordType.Ds, new DnsReply(0, tree.Test.Signed(delegationSigner), []));

        Assert.AreEqual(TlsaLookupStatus.Insecure, (await tree.LookupAsync()).Status);
    }

    [TestMethod]
    public async Task LookupAsync_LabelOverSixtyThreeOctets_ThrowsArgumentException()
    {
        TlsaLookup lookup = new SignedTree().Lookup();

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await lookup.LookupAsync(new string('a', 64) + ".test", 443, CancellationToken.None));
    }

    private static DnsResourceRecord Alias(string owner, string target) =>
        DnssecTestRecords.Record(owner, DnssecRecordType.Cname, DnssecTestRecords.Name(target).Wire.ToArray(), new CnameRecordData(DnssecTestRecords.Name(target)));
}
