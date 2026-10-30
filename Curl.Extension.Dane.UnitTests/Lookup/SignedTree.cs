using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// A signed hierarchy ". &gt; test. &gt; example.test." with a generated Ed25519 key per zone, the root's DS as the
/// trust anchor, and each zone's DNSKEY RRset and each delegation's DS RRset published on a <see cref="FakeDnsServer" />.
/// </summary>
internal sealed class SignedTree
{
    public const string TlsaName = "_443._tcp.example.test.";

    public static readonly byte[] Association = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    public SignedTree()
    {
        Server = new FakeDnsServer(Clock);
        PublishKeys(Root);
        PublishDelegation(Root, Test);
        PublishKeys(Test);
        PublishDelegation(Test, Example);
        PublishKeys(Example);
    }

    public ManualTimeProvider Clock { get; } = new();

    public FakeDnsServer Server { get; }

    public SignedZone Root { get; } = new(".");

    public SignedZone Test { get; } = new("test.");

    public SignedZone Example { get; } = new("example.test.");

    public static DnsResourceRecord Tlsa(string owner = TlsaName) =>
        DnssecTestRecords.Record(owner, DnssecRecordType.Tlsa, [3, 1, 1, .. Association], new TlsaRecordData(3, 1, 1, Association));

    public static DnsResourceRecord Nsec(string owner, string next, params DnssecRecordType[] types) =>
        DnssecTestRecords.Record(owner, DnssecRecordType.Nsec, DnsMessageWriter.Nsec(DnssecTestRecords.Name(next), types), new NsecRecordData(DnssecTestRecords.Name(next), types));

    public static DnsResourceRecord Nsec3(string zone, byte[] ownerHash, byte[] nextHash, byte flags = 0, ushort iterations = 1, params DnssecRecordType[] types)
    {
        Nsec3RecordData data = new(Nsec3Hash.Sha1Algorithm, flags, iterations, [], nextHash, types);
        return DnssecTestRecords.Record($"{EncodeBase32Hex(ownerHash)}.{zone}", DnssecRecordType.Nsec3, DnsMessageWriter.Nsec3(data), data);
    }

    public static byte[] HashOf(string name) => Nsec3Hash.Compute(DnssecTestRecords.Name(name), new Nsec3RecordData(1, 0, 1, [], [], []));

    public static string EncodeBase32Hex(byte[] hash)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuv";
        System.Text.StringBuilder text = new();
        int buffer = 0;
        int bits = 0;
        foreach (byte octet in hash)
        {
            buffer = (buffer << 8) | octet;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                text.Append(alphabet[(buffer >> bits) & 31]);
            }
        }

        return text.ToString();
    }

    public void PublishKeys(SignedZone zone) =>
        Server.Reply(zone.Name, DnssecRecordType.Dnskey, new DnsReply(0, zone.Signed(zone.KeyRecord), []));

    public void PublishDelegation(SignedZone parent, SignedZone child) =>
        Server.Reply(child.Name, DnssecRecordType.Ds, new DnsReply(0, parent.Signed(child.DsRecord), []));

    public TlsaLookup Lookup() =>
        new(
            new DnssecQueryTransport(Server, Server, "resolver.test", 53, Clock, TimeSpan.FromSeconds(5)),
            new RrsigVerifier(Clock),
            [Root.DelegationSigner]);

    public Task<TlsaLookupResult> LookupAsync() => Lookup().LookupAsync("example.test", 443, CancellationToken.None).AsTask();
}

/// <summary>One zone of a <see cref="SignedTree" />: its name, its generated key and what signing with it gives.</summary>
internal sealed class SignedZone(string name)
{
    public string Name => name;

    public DnssecTestRecords.GeneratedZoneKey Key { get; } = DnssecTestRecords.Ed25519Key();

    public DnsResourceRecord KeyRecord => DnssecTestRecords.DnskeyRecord(name, Key.Key);

    public DsRecordData DelegationSigner =>
        new(DnskeyKeyTag.Compute(Key.Key), Key.Key.Algorithm, 2, DelegationVerifier.ComputeDigest(2, DnssecTestRecords.Name(name), Key.Key));

    public DnsResourceRecord DsRecord => DnssecTestRecords.Record(name, DnssecRecordType.Ds, DnsMessageWriter.Ds(DelegationSigner), DelegationSigner);

    /// <summary>Each record as an RRset of its own followed by its RRSIG; <paramref name="labels" /> overrides the RRSIG's label count.</summary>
    public List<DnsResourceRecord> Signed(DnsResourceRecord record, byte? labels = null) =>
        [record, DnssecTestRecords.RrsigRecord(record.Owner.ToString(), DnssecTestRecords.Sign(Key, [record], name, labels))];

    public List<DnsResourceRecord> SignedEach(params DnsResourceRecord[] records) => [.. records.SelectMany(record => Signed(record))];
}
