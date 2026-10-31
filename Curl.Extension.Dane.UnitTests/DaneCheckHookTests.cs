using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Lookup;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane;

[TestClass]
public sealed class DaneCheckHookTests
{
    private const DnssecRecordType NameServer = (DnssecRecordType)2;

    private static readonly (byte[] Der, byte[] SpkiSha256) LeafCertificate = CreateLeaf();

    private static byte[] Leaf => LeafCertificate.Der;

    private static byte[] LeafSpkiSha256 => LeafCertificate.SpkiSha256;

    [TestMethod]
    public async Task RefusalOfAsync_SecureRecordMatchesTheChain_LetsItStand_Rfc6698Section2_1()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(TlsaFor(3, LeafSpkiSha256)), []));

        Assert.IsNull(await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_SecureRecordsMatchNothing_Refuses_Rfc6698Section4_1()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(SignedTree.Tlsa()), []));

        Assert.AreEqual("DANE: no TLSA record of _443._tcp.example.test matches the server's certificate chain", await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_SecureRecordsNoneUsable_LetsItStand_Rfc7671Section4_1()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, tree.Example.Signed(TlsaFor(9, LeafSpkiSha256)), []));

        Assert.IsNull(await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_UnsignedRecordsInSignedZone_RefusesAsBogus_Rfc4035Section5_5()
    {
        SignedTree tree = new();
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [SignedTree.Tlsa()], []));

        Assert.AreEqual("DANE: the DNSSEC validation of _443._tcp.example.test's TLSA records failed (bogus)", await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_ProvedInsecureDelegation_LetsItStand_Rfc6698Section4_1()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec = SignedTree.Nsec("example.test.", "zzz.test.", NameServer, DnssecRecordType.Rrsig, DnssecRecordType.Nsec);
        tree.Server.Reply("example.test.", DnssecRecordType.Ds, new DnsReply(0, [], tree.Test.Signed(nsec)));

        Assert.IsNull(await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_NsecProvedNoTlsaRecords_LetsItStand_Rfc6698Section4_1()
    {
        SignedTree tree = new();
        DnsResourceRecord nsec = SignedTree.Nsec(SignedTree.TlsaName, "zzz.example.test.", DnssecRecordType.Rrsig, DnssecRecordType.Nsec);
        tree.Server.Reply(SignedTree.TlsaName, DnssecRecordType.Tlsa, new DnsReply(0, [], tree.Example.Signed(nsec)));

        Assert.IsNull(await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_ResolverUnreachable_Refuses_Rfc6698Section4_1()
    {
        SignedTree tree = new();
        tree.Server.OpenFails = true;

        Assert.AreEqual("DANE: the resolver 192.0.2.53 gave no usable answer for _443._tcp.example.test's TLSA records", await RefusalAsync(tree));
    }

    [TestMethod]
    public async Task RefusalOfAsync_ResolverPassedToTheLookup_IsTheParsedValue()
    {
        DaneResolverAddress? given = null;
        SignedTree tree = new();
        var hook = new DaneCheckHook("[2001:db8::53]:5353", address =>
        {
            given = address;
            return tree.Lookup();
        });

        await hook.RefusalOfAsync([Leaf], "example.test", 443, CancellationToken.None);

        Assert.AreEqual(new DaneResolverAddress("2001:db8::53", 5353), given);
    }

    [TestMethod]
    public async Task RefusalOfAsync_ResolverNotAnAddress_RefusesWithoutLookingUp()
    {
        var hook = new DaneCheckHook("resolver/path", _ => throw new AssertFailedException("no lookup expected"));

        string? refusal = await hook.RefusalOfAsync([Leaf], "example.test", 443, CancellationToken.None);

        Assert.AreEqual("DANE: the resolver resolver/path is not a host, host:port or [IPv6]:port", refusal);
    }

    [TestMethod]
    public async Task RefusalOfAsync_ServerNamedByAddress_LetsItStandWithoutLookingUp()
    {
        var hook = new DaneCheckHook("192.0.2.53", _ => throw new AssertFailedException("no lookup expected"));

        Assert.IsNull(await hook.RefusalOfAsync([Leaf], "192.0.2.1", 443, CancellationToken.None));
        Assert.IsNull(await hook.RefusalOfAsync([Leaf], "2001:db8::1", 443, CancellationToken.None));
    }

    [TestMethod]
    public async Task RefusalOfAsync_PortUnknown_Refuses_Rfc6698Section3()
    {
        var hook = new DaneCheckHook("192.0.2.53", _ => throw new AssertFailedException("no lookup expected"));

        string? refusal = await hook.RefusalOfAsync([Leaf], "example.test", null, CancellationToken.None);

        Assert.AreEqual("DANE: the port of the connection to example.test is unknown, so its TLSA records cannot be looked up", refusal);
    }

    [TestMethod]
    public async Task RefusalOfAsync_HostNotADnsName_Refuses()
    {
        SignedTree tree = new();
        string host = new('a', 64);
        var hook = new DaneCheckHook("192.0.2.53", _ => tree.Lookup());

        string? refusal = await hook.RefusalOfAsync([Leaf], host, 443, CancellationToken.None);

        Assert.AreEqual($"DANE: _443._tcp.{host} is not a DNS name, so its TLSA records cannot be looked up", refusal);
    }

    [TestMethod]
    public async Task RefusalOfAsync_NullArguments_ThrowArgumentNullException()
    {
        var hook = new DaneCheckHook("192.0.2.53", _ => throw new AssertFailedException("no lookup expected"));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => hook.RefusalOfAsync(null!, "example.test", 443, CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => hook.RefusalOfAsync([Leaf], null!, 443, CancellationToken.None).AsTask());
        Assert.AreSame(DaneExtension.Option, hook.Option);
    }

    private static Task<string?> RefusalAsync(SignedTree tree) =>
        new DaneCheckHook("192.0.2.53", _ => tree.Lookup()).RefusalOfAsync([Leaf], "example.test", 443, CancellationToken.None).AsTask();

    private static DnsResourceRecord TlsaFor(byte usage, byte[] association) =>
        DnssecTestRecords.Record(SignedTree.TlsaName, DnssecRecordType.Tlsa, [usage, 1, 1, .. association], new TlsaRecordData(usage, 1, 1, association));

    private static (byte[] Der, byte[] SpkiSha256) CreateLeaf()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=example.test", key, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return (certificate.RawData, SHA256.HashData(key.ExportSubjectPublicKeyInfo()));
    }
}
