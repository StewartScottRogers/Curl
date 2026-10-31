using System.Buffers.Binary;

namespace Curl.Extension.CertificateTransparency;

/// <summary>One version 1 Signed Certificate Timestamp (RFC 6962 §3.2).</summary>
/// <param name="LogId">The SHA-256 hash of the log's public key.</param>
/// <param name="Timestamp">When the log saw the certificate, in milliseconds since the Unix epoch.</param>
/// <param name="Extensions">The SCT's <c>CtExtensions</c>, signed with it.</param>
/// <param name="HashAlgorithm">The TLS <c>HashAlgorithm</c> of its signature.</param>
/// <param name="SignatureAlgorithm">The TLS <c>SignatureAlgorithm</c> of its signature.</param>
/// <param name="Signature">The log's signature.</param>
internal sealed record SignedCertificateTimestamp(
    ReadOnlyMemory<byte> LogId,
    ulong Timestamp,
    ReadOnlyMemory<byte> Extensions,
    byte HashAlgorithm,
    byte SignatureAlgorithm,
    ReadOnlyMemory<byte> Signature)
{
    /// <summary>The <c>Version</c> value of a version 1 SCT.</summary>
    internal const byte Version1 = 0;

    /// <summary>The length of a <c>LogID</c>.</summary>
    internal const int LogIdLength = 32;

    private const byte CertificateTimestampSignatureType = 0;
    private const ushort PrecertificateEntryType = 1;

    /// <summary>Reads a <c>SignedCertificateTimestampList</c> (RFC 6962 §3.3), skipping SCTs of a version other than 1.</summary>
    /// <param name="list">The list's TLS encoding.</param>
    /// <returns>The version 1 SCTs, in list order.</returns>
    /// <exception cref="FormatException">The list is not well formed.</exception>
    internal static IReadOnlyList<SignedCertificateTimestamp> ParseList(ReadOnlyMemory<byte> list)
    {
        var outer = new TlsReader(list);
        var body = new TlsReader(outer.ReadVector16());
        outer.ExpectEnd();
        var timestamps = new List<SignedCertificateTimestamp>();
        while (!body.IsAtEnd)
        {
            var timestamp = Parse(body.ReadVector16());
            if (timestamp is not null)
            {
                timestamps.Add(timestamp);
            }
        }

        return timestamps;
    }

    /// <summary>Reads one <c>SerializedSCT</c>.</summary>
    /// <param name="serialized">Its TLS encoding.</param>
    /// <returns>The SCT, or <see langword="null" /> when its version is not 1, whose layout is unknown.</returns>
    /// <exception cref="FormatException">The SCT is not well formed.</exception>
    internal static SignedCertificateTimestamp? Parse(ReadOnlyMemory<byte> serialized)
    {
        var reader = new TlsReader(serialized);
        if (reader.ReadByte() != Version1)
        {
            return null;
        }

        var timestamp = new SignedCertificateTimestamp(reader.ReadBytes(LogIdLength), reader.ReadUInt64(), reader.ReadVector16(), reader.ReadByte(), reader.ReadByte(), reader.ReadVector16());
        reader.ExpectEnd();
        return timestamp;
    }

    /// <summary>Builds the data the log signed for a precertificate entry (RFC 6962 §3.2, <c>digitally-signed struct</c>).</summary>
    /// <param name="issuerKeyHash">The SHA-256 hash of the issuer's <c>SubjectPublicKeyInfo</c>.</param>
    /// <param name="tbsCertificate">The certificate's <c>TBSCertificate</c> without its SCT list extension.</param>
    /// <returns>The signed bytes.</returns>
    internal byte[] SignedDataFor(ReadOnlySpan<byte> issuerKeyHash, ReadOnlySpan<byte> tbsCertificate)
    {
        var tbsAt = 12 + issuerKeyHash.Length;
        var extensionsAt = tbsAt + 3 + tbsCertificate.Length;
        var data = new byte[extensionsAt + sizeof(ushort) + Extensions.Length];
        var span = data.AsSpan();
        span[0] = Version1;
        span[1] = CertificateTimestampSignatureType;
        BinaryPrimitives.WriteUInt64BigEndian(span[2..], Timestamp);
        BinaryPrimitives.WriteUInt16BigEndian(span[10..], PrecertificateEntryType);
        issuerKeyHash.CopyTo(span[12..]);
        span[tbsAt] = (byte)(tbsCertificate.Length >> 16);
        BinaryPrimitives.WriteUInt16BigEndian(span[(tbsAt + 1)..], (ushort)tbsCertificate.Length);
        tbsCertificate.CopyTo(span[(tbsAt + 3)..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[extensionsAt..], (ushort)Extensions.Length);
        Extensions.Span.CopyTo(span[(extensionsAt + sizeof(ushort))..]);
        return data;
    }
}
