using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Extension.CertificateTransparency.UnitTests;

/// <summary>
/// Builds a server chain whose certificate embeds SCTs signed by a test log, written out byte by byte from RFC 6962 §3.2
/// and §3.3 independently of the library under test.
/// </summary>
internal sealed class CertificateTransparencyChain : IDisposable
{
    internal static readonly DateTimeOffset Logged = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ECDsa issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly X509Certificate2 issuer;

    internal CertificateTransparencyChain()
    {
        issuer = new CertificateRequest("CN=Test CA", issuerKey, HashAlgorithmName.SHA256).CreateSelfSigned(Logged.AddDays(-1), Logged.AddYears(1));
        LogKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    internal ECDsa LogKey { get; }

    internal byte[] IssuerDer => issuer.RawData;

    internal static byte[] LogIdOf(byte[] publicKey) => SHA256.HashData(publicKey);

    internal static byte[] LogListOf(params byte[][] publicKeys) =>
        System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":\"1\",\"operators\":[{\"name\":\"Test\",\"logs\":[" +
            string.Join(",", publicKeys.Select(key => $"{{\"description\":\"Test log\",\"key\":\"{Convert.ToBase64String(key)}\"}}")) +
            "]}]}");

    internal static byte[] TlsVector16(byte[] contents)
    {
        var vector = new byte[2 + contents.Length];
        BinaryPrimitives.WriteUInt16BigEndian(vector, (ushort)contents.Length);
        contents.CopyTo(vector, 2);
        return vector;
    }

    internal static byte[] ListOf(params byte[][] serializedTimestamps) => TlsVector16([.. serializedTimestamps.SelectMany(TlsVector16)]);

    /// <summary>Gives the leaf certificate's DER without any SCT extension.</summary>
    internal byte[] LeafWithout() => Leaf(null, false);

    /// <summary>Gives the leaf certificate's DER carrying <paramref name="list" /> in its SCT extension.</summary>
    internal byte[] LeafWith(byte[] list, bool critical = false) => Leaf(list, critical);

    /// <summary>Gives the leaf's TBSCertificate as a precertificate would have had it.</summary>
    internal byte[] PrecertificateTbs() => new AsnReader(LeafWithout(), AsnEncodingRules.DER).ReadSequence().ReadEncodedValue().ToArray();

    /// <summary>Signs a version 1 SCT over the precertificate entry (RFC 6962 §3.2) and serializes it.</summary>
    internal byte[] Timestamp(Func<byte[], byte[]> sign, byte[] logPublicKey, DateTimeOffset logged, byte signatureAlgorithm = 3, byte hashAlgorithm = 4, byte[]? tbs = null, byte version = 0)
    {
        var precertificateTbs = tbs ?? PrecertificateTbs();
        var extensions = new byte[] { 0xCA, 0xFE };
        var milliseconds = (ulong)logged.ToUnixTimeMilliseconds();
        var signed = new List<byte> { 0, 0 };
        signed.AddRange(BigEndian(milliseconds));
        signed.AddRange([0, 1]);
        signed.AddRange(SHA256.HashData(issuer.PublicKey.ExportSubjectPublicKeyInfo()));
        signed.AddRange([(byte)(precertificateTbs.Length >> 16), (byte)(precertificateTbs.Length >> 8), (byte)precertificateTbs.Length]);
        signed.AddRange(precertificateTbs);
        signed.AddRange(TlsVector16(extensions));
        var signature = sign([.. signed]);

        var serialized = new List<byte> { version };
        serialized.AddRange(LogIdOf(logPublicKey));
        serialized.AddRange(BigEndian(milliseconds));
        serialized.AddRange(TlsVector16(extensions));
        serialized.AddRange([hashAlgorithm, signatureAlgorithm]);
        serialized.AddRange(TlsVector16(signature));
        return [.. serialized];
    }

    internal byte[] EcdsaTimestamp(DateTimeOffset logged) =>
        Timestamp(data => LogKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence), LogKey.ExportSubjectPublicKeyInfo(), logged);

    public void Dispose()
    {
        issuer.Dispose();
        issuerKey.Dispose();
        leafKey.Dispose();
        LogKey.Dispose();
    }

    private static byte[] BigEndian(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private byte[] Leaf(byte[]? list, bool critical)
    {
        var request = new CertificateRequest("CN=example.com", leafKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        if (list is not null)
        {
            var value = new AsnWriter(AsnEncodingRules.DER);
            value.WriteOctetString(list);
            request.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.11129.2.4.2", value.Encode(), critical));
        }

        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

        using var certificate = request.Create(issuer.SubjectName, X509SignatureGenerator.CreateForECDsa(issuerKey), Logged.AddDays(-1), Logged.AddDays(90), [1, 2, 3, 4]);
        return certificate.RawData;
    }
}
