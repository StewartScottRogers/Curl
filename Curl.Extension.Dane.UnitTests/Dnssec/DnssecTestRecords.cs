using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Builds the names, records, recorded responses, clocks and generated zone keys the DNSSEC tests share.
/// </summary>
internal static class DnssecTestRecords
{
    /// <summary>The inception of every generated signature: 2026-10-01 00:00:00 UTC.</summary>
    public const uint Inception = 1790812800;

    /// <summary>The expiration of every generated signature: 2026-12-01 00:00:00 UTC.</summary>
    public const uint Expiration = 1796083200;

    /// <summary>A moment between <see cref="Inception" /> and <see cref="Expiration" />.</summary>
    public static readonly DateTimeOffset GeneratedValidAt = DateTimeOffset.FromUnixTimeSeconds(1793500000);

    public static DnsName Name(string text)
    {
        List<byte> wire = [];
        string[] labels = text.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (string label in labels)
        {
            wire.Add((byte)label.Length);
            wire.AddRange(Encoding.ASCII.GetBytes(label));
        }

        wire.Add(0);
        return new DnsName([.. wire], labels.Length);
    }

    public static DnssecResponse Decode(string hex)
    {
        Assert.IsTrue(DnssecResponseDecoder.TryDecode(Convert.FromHexString(hex), out DnssecResponse? response));
        return response;
    }

    public static List<DnsResourceRecord> Answers(DnssecResponse response, DnssecRecordType type) =>
        [.. response.Answers.Where(record => record.Type == type)];

    public static DnsResourceRecord Rrsig(DnssecResponse response) =>
        response.Answers.Single(record => record.Type == DnssecRecordType.Rrsig);

    public static TimeProvider FixedTime(DateTimeOffset now) => new FixedTimeProvider(now);

    public static DnsResourceRecord Record(string owner, DnssecRecordType type, byte[] rdata, DnsRecordData? data = null) =>
        new(Name(owner), type, 1, 300, rdata, data);

    public static List<DnsResourceRecord> AddressRrset(string owner) =>
    [
        Record(owner, DnssecRecordType.A, [192, 0, 2, 2]),
        Record(owner, DnssecRecordType.A, [192, 0, 2, 1]),
    ];

    public static DnsResourceRecord DnskeyRecord(string owner, DnskeyRecordData key) =>
        Record(owner, DnssecRecordType.Dnskey, DnskeyKeyTag.EncodeRdata(key), key);

    public static DnsResourceRecord RrsigRecord(string owner, RrsigRecordData signature) =>
        Record(owner, DnssecRecordType.Rrsig, [], signature);

    /// <summary>
    /// Signs <paramref name="rrset" /> with <paramref name="zoneKey" /> as zone <paramref name="signer" />, the label
    /// count the owner's, valid from <see cref="Inception" /> to <see cref="Expiration" />.
    /// </summary>
    public static RrsigRecordData Sign(GeneratedZoneKey zoneKey, IReadOnlyList<DnsResourceRecord> rrset, string signer = "example.com.", byte? labels = null)
    {
        RrsigRecordData unsigned = new(
            rrset[0].Type,
            zoneKey.Key.Algorithm,
            labels ?? (byte)rrset[0].Owner.LabelCount,
            3600,
            Expiration,
            Inception,
            DnskeyKeyTag.Compute(zoneKey.Key),
            Name(signer),
            []);
        return unsigned with { Signature = zoneKey.Sign(DnssecSignedData.Encode(unsigned, rrset)) };
    }

    public static GeneratedZoneKey RsaSha512()
    {
        using RSA rsa = RSA.Create(2048);
        RSAParameters parameters = rsa.ExportParameters(true);
        byte[] publicKey = [(byte)parameters.Exponent!.Length, .. parameters.Exponent, .. parameters.Modulus!];
        return new GeneratedZoneKey(
            new DnskeyRecordData(256, 3, 10, publicKey),
            data =>
            {
                using RSA signer = RSA.Create(parameters);
                return signer.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            });
    }

    public static GeneratedZoneKey EcdsaP384()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        ECParameters parameters = ecdsa.ExportParameters(true);
        return new GeneratedZoneKey(
            new DnskeyRecordData(256, 3, 14, [.. parameters.Q.X!, .. parameters.Q.Y!]),
            data =>
            {
                using ECDsa signer = ECDsa.Create(parameters);
                return signer.SignData(data, HashAlgorithmName.SHA384);
            });
    }

    public static GeneratedZoneKey Ed25519Key()
    {
        byte[] privateKey = new byte[Ed25519.PrivateKeySize];
        byte[] publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.GeneratePrivateKey(privateKey);
        Ed25519.ComputePublicKey(privateKey, publicKey);
        return new GeneratedZoneKey(
            new DnskeyRecordData(256, 3, 15, publicKey),
            data =>
            {
                byte[] signature = new byte[Ed25519.SignatureSize];
                Ed25519.Sign(privateKey, data, signature);
                return signature;
            });
    }

    public static GeneratedZoneKey Ed448Key()
    {
        byte[] privateKey = new byte[Ed448.PrivateKeySize];
        byte[] publicKey = new byte[Ed448.PublicKeySize];
        Ed448.GeneratePrivateKey(privateKey);
        Ed448.ComputePublicKey(privateKey, publicKey);
        return new GeneratedZoneKey(
            new DnskeyRecordData(256, 3, 16, publicKey),
            data =>
            {
                byte[] signature = new byte[Ed448.SignatureSize];
                Ed448.Sign(privateKey, data, signature);
                return signature;
            });
    }

    /// <summary>A zone key made for a test and the function that signs with its private half.</summary>
    internal sealed record GeneratedZoneKey(DnskeyRecordData Key, Func<byte[], byte[]> Sign);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
