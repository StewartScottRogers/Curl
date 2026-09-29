using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>A checksum and its type (RFC 4120's <c>Checksum</c>).</summary>
/// <param name="ChecksumType">The checksum type, e.g. 16 for <c>hmac-sha1-96-aes256</c>, or 0x8003 for RFC 4121's GSS-API checksum.</param>
/// <param name="Value">The checksum bytes.</param>
public sealed record KerberosChecksum(int ChecksumType, byte[] Value)
{
    /// <summary>Reads a <c>Checksum</c>.</summary>
    internal static readonly Func<AsnReader, KerberosChecksum> Reader = new Func<AsnReader, KerberosChecksum>(Read);

    /// <summary>Writes a <c>Checksum</c>.</summary>
    internal static readonly Action<AsnWriter, KerberosChecksum> Writer = new Action<AsnWriter, KerberosChecksum>(Write);

    private static KerberosChecksum Read(AsnReader reader) =>
        KerberosAsn1.ReadSequence(reader, inner => new KerberosChecksum(
            KerberosAsn1.ReadField(inner, 0, KerberosAsn1.Int32Reader),
            KerberosAsn1.ReadField(inner, 1, KerberosAsn1.OctetsReader)));

    private static void Write(AsnWriter writer, KerberosChecksum checksum) =>
        KerberosAsn1.WriteSequence(writer, inner =>
        {
            KerberosAsn1.WriteField(inner, 0, checksum.ChecksumType, KerberosAsn1.Int32Writer);
            KerberosAsn1.WriteField(inner, 1, checksum.Value, KerberosAsn1.OctetsWriter);
        });
}
