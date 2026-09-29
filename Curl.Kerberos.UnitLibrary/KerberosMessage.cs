using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>What every Kerberos V5 message shares: the application tag that says which message it is.</summary>
public static class KerberosMessage
{
    /// <summary>
    /// Says which message <paramref name="bytes" /> hold, from its application tag, e.g. to
    /// tell a KDC's AS-REP from its KRB-ERROR before decoding either.
    /// </summary>
    /// <param name="bytes">One whole message.</param>
    /// <returns>The message type.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.UnexpectedMessage" /> for a tag that is not a
    /// <see cref="KerberosMessageType" />; <see cref="KerberosMessageError.Malformed" /> for
    /// bytes that are not one whole ASN.1 value.
    /// </exception>
    public static KerberosMessageType PeekType(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.DecodeValue(bytes, reader =>
        {
            Asn1Tag tag = reader.PeekTag();
            reader.ReadEncodedValue();
            KerberosMessageType type = (KerberosMessageType)tag.TagValue;
            return tag.TagClass == TagClass.Application && tag.IsConstructed && Enum.IsDefined(type)
                ? type
                : throw new KerberosMessageException(KerberosMessageError.UnexpectedMessage);
        });

    /// <summary>Gives the application tag a message of <paramref name="type" /> is encoded with, which must be one of two.</summary>
    /// <param name="type">The message type to encode.</param>
    /// <param name="first">One message type the encoder writes.</param>
    /// <param name="second">The other.</param>
    /// <returns>The application tag number.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="type" /> is neither.</exception>
    internal static int ApplicationTagOf(KerberosMessageType type, KerberosMessageType first, KerberosMessageType second) =>
        type == first || type == second
            ? (int)type
            : throw new InvalidOperationException($"This message is a {first} or a {second}, not {type}.");

    /// <summary>Decodes a message that may carry either of two application tags, e.g. an AS-REQ or a TGS-REQ.</summary>
    /// <typeparam name="T">What the message makes.</typeparam>
    /// <param name="bytes">The encoding.</param>
    /// <param name="first">One message type the decoder reads.</param>
    /// <param name="second">The other.</param>
    /// <param name="readContents">Reads the message's fields, given its type.</param>
    /// <returns>What <paramref name="readContents" /> made.</returns>
    internal static T DecodeEither<T>(ReadOnlyMemory<byte> bytes, KerberosMessageType first, KerberosMessageType second, Func<AsnReader, KerberosMessageType, T> readContents)
    {
        KerberosMessageType type = PeekType(bytes);
        return type == first || type == second
            ? KerberosAsn1.Decode(bytes, (int)type, reader => readContents(reader, type))
            : throw new KerberosMessageException(KerberosMessageError.UnexpectedMessage);
    }
}
