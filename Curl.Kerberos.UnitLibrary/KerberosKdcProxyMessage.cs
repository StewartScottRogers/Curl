using System.Buffers.Binary;

namespace Curl.Kerberos;

/// <summary>
/// MS-KKDCP's <c>KDC-PROXY-MESSAGE</c>, the body of a KDC proxy's HTTPS request and reply:
/// <c>kerb-message [0] OCTET STRING</c> holding the Kerberos message framed as over TCP
/// (its length in four big-endian bytes, then the message), <c>target-domain [1]</c> as a
/// GeneralString and <c>dclocator-hint [2] INTEGER</c>, both optional. It is encoded as MIT's
/// <c>encode_krb5_kkdcp_message</c> does for <c>sendto_kdc.c</c>: the realm as
/// <c>target-domain</c> and no <c>dclocator-hint</c>.
/// </summary>
/// <param name="KerberosMessage">The Kerberos message, without its length prefix.</param>
/// <param name="TargetDomain">The realm whose KDC is to answer, or <see langword="null" /> when not given.</param>
public sealed record KerberosKdcProxyMessage(byte[] KerberosMessage, string? TargetDomain)
{
    private const int LengthPrefixSize = sizeof(uint);

    /// <summary>Encodes the <c>KDC-PROXY-MESSAGE</c>.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.EncodeValue(this, (writer, message) => KerberosAsn1.WriteSequence(writer, inner =>
    {
        KerberosAsn1.WriteField(inner, 0, Frame(message.KerberosMessage), KerberosAsn1.OctetsWriter);
        KerberosAsn1.WriteOptionalField(inner, 1, message.TargetDomain, KerberosAsn1.StringWriter);
    }));

    /// <summary>Decodes a <c>KDC-PROXY-MESSAGE</c>, ignoring any <c>dclocator-hint</c>.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The message.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, also for a <c>kerb-message</c> whose
    /// length prefix is missing or does not match the bytes after it.
    /// </exception>
    public static KerberosKdcProxyMessage Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.DecodeValue(bytes, reader => KerberosAsn1.ReadSequence(reader, inner =>
        {
            byte[] framed = KerberosAsn1.ReadField(inner, 0, KerberosAsn1.OctetsReader);
            string? targetDomain = KerberosAsn1.ReadOptionalField(inner, 1, KerberosAsn1.StringReader);
            KerberosAsn1.ReadOptionalValueField(inner, 2, KerberosAsn1.Int32Reader);
            return new KerberosKdcProxyMessage(Unframe(framed), targetDomain);
        }));

    private static byte[] Frame(byte[] message)
    {
        byte[] framed = new byte[LengthPrefixSize + message.Length];
        BinaryPrimitives.WriteInt32BigEndian(framed, message.Length);
        message.CopyTo(framed, LengthPrefixSize);
        return framed;
    }

    private static byte[] Unframe(byte[] framed) =>
        framed.Length >= LengthPrefixSize && BinaryPrimitives.ReadUInt32BigEndian(framed) == (uint)(framed.Length - LengthPrefixSize)
            ? framed[LengthPrefixSize..]
            : throw new KerberosMessageException(KerberosMessageError.Malformed);
}
