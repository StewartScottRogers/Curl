using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// The ASN.1 pieces every Kerberos V5 message is built from (RFC 4120 section 5 and
/// Appendix A): explicit context-tagged fields, application-tagged messages,
/// <c>KerberosString</c> as a GeneralString, <c>KerberosTime</c> as a GeneralizedTime
/// without fractions, <c>KerberosFlags</c> as a 32-bit BIT STRING, and the small shared
/// types (<c>EncryptionKey</c>, <c>HostAddress</c>, <c>AuthorizationData</c>). Writing is
/// DER; reading accepts BER, as MIT does.
/// </summary>
/// <remarks>
/// <para>
/// An optional field is absent when its value is <see langword="null" />, and a list-valued
/// optional field is absent when its list is empty, which is how MIT writes them.
/// </para>
/// <para>
/// The readers and writers handed to the field helpers are static fields made with an
/// explicit <c>new</c>, never a method group or a lambda that captures nothing: the compiler
/// caches those behind a null check, a branch in every method that names one, which would
/// put the message readers over the complexity limit.
/// </para>
/// </remarks>
internal static class KerberosAsn1
{
    /// <summary>The protocol version number every Kerberos V5 message and ticket carries.</summary>
    public const int ProtocolVersion = 5;

    /// <summary>Reads an <c>Int32</c> or <c>Microseconds</c> (<see cref="ReadInt32" />).</summary>
    public static readonly Func<AsnReader, int> Int32Reader = new Func<AsnReader, int>(ReadInt32);

    /// <summary>Reads a <c>UInt32</c> (<see cref="ReadUInt32" />).</summary>
    public static readonly Func<AsnReader, uint> UInt32Reader = new Func<AsnReader, uint>(ReadUInt32);

    /// <summary>Reads a <c>KerberosString</c> (<see cref="ReadString" />).</summary>
    public static readonly Func<AsnReader, string> StringReader = new Func<AsnReader, string>(ReadString);

    /// <summary>Reads a <c>KerberosTime</c> (<see cref="ReadTime" />).</summary>
    public static readonly Func<AsnReader, DateTimeOffset> TimeReader = new Func<AsnReader, DateTimeOffset>(ReadTime);

    /// <summary>Reads an OCTET STRING (<see cref="ReadOctets" />).</summary>
    public static readonly Func<AsnReader, byte[]> OctetsReader = new Func<AsnReader, byte[]>(ReadOctets);

    /// <summary>Reads a <c>KerberosFlags</c> (<see cref="ReadFlags" />).</summary>
    public static readonly Func<AsnReader, uint> FlagsReader = new Func<AsnReader, uint>(ReadFlags);

    /// <summary>Reads an <c>EncryptionKey</c> (<see cref="ReadKey" />).</summary>
    public static readonly Func<AsnReader, KerberosKey> KeyReader = new Func<AsnReader, KerberosKey>(ReadKey);

    /// <summary>Reads a <c>HostAddress</c> (<see cref="ReadAddress" />).</summary>
    public static readonly Func<AsnReader, KerberosAddress> AddressReader = new Func<AsnReader, KerberosAddress>(ReadAddress);

    /// <summary>Reads one <c>AuthorizationData</c> element (<see cref="ReadAuthorizationData" />).</summary>
    public static readonly Func<AsnReader, KerberosAuthorizationData> AuthorizationDataReader = new Func<AsnReader, KerberosAuthorizationData>(ReadAuthorizationData);

    /// <summary>Writes an <c>Int32</c> or <c>Microseconds</c> (<see cref="WriteInt32" />).</summary>
    public static readonly Action<AsnWriter, int> Int32Writer = new Action<AsnWriter, int>(WriteInt32);

    /// <summary>Writes a <c>UInt32</c> (<see cref="WriteUInt32" />).</summary>
    public static readonly Action<AsnWriter, uint> UInt32Writer = new Action<AsnWriter, uint>(WriteUInt32);

    /// <summary>Writes a <c>KerberosString</c> (<see cref="WriteString" />).</summary>
    public static readonly Action<AsnWriter, string> StringWriter = new Action<AsnWriter, string>(WriteString);

    /// <summary>Writes a <c>KerberosTime</c> (<see cref="WriteTime" />).</summary>
    public static readonly Action<AsnWriter, DateTimeOffset> TimeWriter = new Action<AsnWriter, DateTimeOffset>(WriteTime);

    /// <summary>Writes an OCTET STRING (<see cref="WriteOctets" />).</summary>
    public static readonly Action<AsnWriter, byte[]> OctetsWriter = new Action<AsnWriter, byte[]>(WriteOctets);

    /// <summary>Writes a <c>KerberosFlags</c> (<see cref="WriteFlags" />).</summary>
    public static readonly Action<AsnWriter, uint> FlagsWriter = new Action<AsnWriter, uint>(WriteFlags);

    /// <summary>Writes an <c>EncryptionKey</c> (<see cref="WriteKey" />).</summary>
    public static readonly Action<AsnWriter, KerberosKey> KeyWriter = new Action<AsnWriter, KerberosKey>(WriteKey);

    /// <summary>Writes a <c>HostAddress</c> (<see cref="WriteAddress" />).</summary>
    public static readonly Action<AsnWriter, KerberosAddress> AddressWriter = new Action<AsnWriter, KerberosAddress>(WriteAddress);

    /// <summary>Writes one <c>AuthorizationData</c> element (<see cref="WriteAuthorizationData" />).</summary>
    public static readonly Action<AsnWriter, KerberosAuthorizationData> AuthorizationDataWriter = new Action<AsnWriter, KerberosAuthorizationData>(WriteAuthorizationData);

    private const byte GeneralStringTagByte = 0x1B;

    private static readonly Asn1Tag GeneralStringTag = new(UniversalTagNumber.GeneralString);

    /// <summary>Gives the constructed <c>[APPLICATION n]</c> tag.</summary>
    /// <param name="number">The application tag number.</param>
    /// <returns>The tag.</returns>
    public static Asn1Tag Application(int number) => new(TagClass.Application, number, isConstructed: true);

    /// <summary>Writes <paramref name="writeContents" />'s SEQUENCE inside <c>[APPLICATION n]</c> and gives the DER bytes.</summary>
    /// <param name="applicationTag">The application tag number.</param>
    /// <param name="writeContents">Writes the SEQUENCE's fields.</param>
    /// <returns>The DER encoding.</returns>
    public static byte[] Encode(int applicationTag, Action<AsnWriter> writeContents) =>
        EncodeValue(writeContents, (writer, contents) => WriteApplication(writer, applicationTag, contents));

    /// <summary>
    /// Writes one value with <paramref name="writeValue" /> and gives the DER bytes. The
    /// writer's buffer is cleared afterwards, because the value may hold a key.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="writeValue">Writes it.</param>
    /// <returns>The DER encoding.</returns>
    public static byte[] EncodeValue<T>(T value, Action<AsnWriter, T> writeValue)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        try
        {
            writeValue(writer, value);
            return writer.Encode();
        }
        finally
        {
            writer.Reset();
        }
    }

    /// <summary>
    /// Reads the rest of a structure that already holds <paramref name="key" />, disposing
    /// the key if the rest fails, so a malformed message leaves no key bytes behind.
    /// </summary>
    /// <typeparam name="T">What the structure makes.</typeparam>
    /// <param name="key">The key read so far, or <see langword="null" /> when there is none.</param>
    /// <param name="readRest">Reads the rest and makes the structure.</param>
    /// <returns>What <paramref name="readRest" /> made.</returns>
    public static T DisposeKeyOnFailure<T>(KerberosKey? key, Func<T> readRest)
    {
        try
        {
            return readRest();
        }
        catch
        {
            key?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads one <c>[APPLICATION n] SEQUENCE</c> that must fill <paramref name="bytes" />
    /// and hands its fields to <paramref name="readContents" />.
    /// </summary>
    /// <typeparam name="T">What the fields make.</typeparam>
    /// <param name="bytes">The encoding.</param>
    /// <param name="applicationTag">The application tag number the message must carry.</param>
    /// <param name="readContents">Reads the SEQUENCE's fields.</param>
    /// <returns>What <paramref name="readContents" /> made.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.UnexpectedMessage" /> for another tag;
    /// <see cref="KerberosMessageError.Malformed" /> for bytes that are not the structure.
    /// </exception>
    public static T Decode<T>(ReadOnlyMemory<byte> bytes, int applicationTag, Func<AsnReader, T> readContents) =>
        DecodeValue(bytes, reader => ReadApplication(reader, applicationTag, readContents));

    /// <summary>
    /// Gives the first value of decrypted <paramref name="plaintext" /> without the bytes after
    /// it: <c>des3-cbc-sha1</c> pads its plaintext with zeros to a whole block, and MIT's
    /// <c>k5_asn1_full_decode</c> ignores what follows the value for that reason. The
    /// plaintext is zeroed when a shorter copy is given; one that is not a value is given back
    /// as it is, for its decoding to refuse.
    /// </summary>
    /// <param name="plaintext">The decrypted bytes; the caller zeroes what comes back.</param>
    /// <returns>The value's bytes.</returns>
    public static byte[] WithoutPadding(byte[] plaintext)
    {
        if (!AsnDecoder.TryReadEncodedValue(plaintext, AsnEncodingRules.BER, out _, out _, out _, out int valueLength) || valueLength == plaintext.Length)
        {
            return plaintext;
        }

        byte[] value = plaintext[..valueLength];
        CryptographicOperations.ZeroMemory(plaintext);
        return value;
    }

    /// <summary>
    /// Reads one value that must fill <paramref name="bytes" />, turning every ASN.1 failure
    /// into <see cref="KerberosMessageError.Malformed" />.
    /// </summary>
    /// <typeparam name="T">What the value makes.</typeparam>
    /// <param name="bytes">The encoding.</param>
    /// <param name="readValue">Reads the value.</param>
    /// <returns>What <paramref name="readValue" /> made.</returns>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.Malformed" />.</exception>
    public static T DecodeValue<T>(ReadOnlyMemory<byte> bytes, Func<AsnReader, T> readValue)
    {
        try
        {
            AsnReader reader = new(bytes, AsnEncodingRules.BER);
            T value = readValue(reader);
            ThrowIfNotEmpty(reader, value);
            return value;
        }
        catch (AsnContentException)
        {
            throw new KerberosMessageException(KerberosMessageError.Malformed);
        }
    }

    /// <summary>Reads one <c>[APPLICATION n] SEQUENCE</c> and hands its fields to <paramref name="readContents" />.</summary>
    /// <typeparam name="T">What the fields make.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="applicationTag">The application tag number the value must carry.</param>
    /// <param name="readContents">Reads the SEQUENCE's fields.</param>
    /// <returns>What <paramref name="readContents" /> made.</returns>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.UnexpectedMessage" /> for another tag.</exception>
    public static T ReadApplication<T>(AsnReader reader, int applicationTag, Func<AsnReader, T> readContents)
    {
        if (!reader.PeekTag().HasSameClassAndValue(Application(applicationTag)))
        {
            throw new KerberosMessageException(KerberosMessageError.UnexpectedMessage);
        }

        return ReadExplicit(reader, Application(applicationTag), inner => ReadSequence(inner, readContents));
    }

    /// <summary>Writes <paramref name="writeContents" />'s SEQUENCE inside <c>[APPLICATION n]</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="applicationTag">The application tag number.</param>
    /// <param name="writeContents">Writes the SEQUENCE's fields.</param>
    public static void WriteApplication(AsnWriter writer, int applicationTag, Action<AsnWriter> writeContents)
    {
        using (writer.PushSequence(Application(applicationTag)))
        {
            WriteSequence(writer, writeContents);
        }
    }

    /// <summary>Writes a SEQUENCE whose fields <paramref name="writeContents" /> writes.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="writeContents">Writes the fields.</param>
    public static void WriteSequence(AsnWriter writer, Action<AsnWriter> writeContents)
    {
        using (writer.PushSequence())
        {
            writeContents(writer);
        }
    }

    /// <summary>Reads a SEQUENCE and hands its fields to <paramref name="readContents" />, which must read them all.</summary>
    /// <typeparam name="T">What the fields make.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="readContents">Reads the fields.</param>
    /// <returns>What <paramref name="readContents" /> made.</returns>
    public static T ReadSequence<T>(AsnReader reader, Func<AsnReader, T> readContents) =>
        ReadExplicit(reader, Asn1Tag.Sequence, readContents);

    /// <summary>Writes the explicit field <c>[n]</c> holding <paramref name="value" />.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="value">The value.</param>
    /// <param name="writeValue">Writes the value.</param>
    public static void WriteField<T>(AsnWriter writer, int number, T value, Action<AsnWriter, T> writeValue)
    {
        using (writer.PushSequence(Context(number)))
        {
            writeValue(writer, value);
        }
    }

    /// <summary>Writes the explicit field <c>[n]</c> when <paramref name="value" /> is not <see langword="null" />.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="value">The value, or <see langword="null" /> to leave the field out.</param>
    /// <param name="writeValue">Writes the value.</param>
    public static void WriteOptionalField<T>(AsnWriter writer, int number, T? value, Action<AsnWriter, T> writeValue)
        where T : class
    {
        if (value is not null)
        {
            WriteField(writer, number, value, writeValue);
        }
    }

    /// <summary>Writes the explicit field <c>[n]</c> when <paramref name="value" /> has a value.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="value">The value, or <see langword="null" /> to leave the field out.</param>
    /// <param name="writeValue">Writes the value.</param>
    public static void WriteOptionalValueField<T>(AsnWriter writer, int number, T? value, Action<AsnWriter, T> writeValue)
        where T : struct
    {
        if (value is T present)
        {
            WriteField(writer, number, present, writeValue);
        }
    }

    /// <summary>Writes the explicit field <c>[n]</c> as a SEQUENCE OF <paramref name="values" />.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="values">The elements.</param>
    /// <param name="writeElement">Writes one element.</param>
    public static void WriteSequenceOfField<T>(AsnWriter writer, int number, IReadOnlyList<T> values, Action<AsnWriter, T> writeElement)
    {
        using (writer.PushSequence(Context(number)))
        {
            WriteSequenceOf(writer, values, writeElement);
        }
    }

    /// <summary>Writes the explicit field <c>[n]</c> as a SEQUENCE OF <paramref name="values" />, leaving it out when there are none.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="values">The elements.</param>
    /// <param name="writeElement">Writes one element.</param>
    public static void WriteOptionalSequenceOfField<T>(AsnWriter writer, int number, IReadOnlyList<T> values, Action<AsnWriter, T> writeElement)
    {
        if (values.Count > 0)
        {
            WriteSequenceOfField(writer, number, values, writeElement);
        }
    }

    /// <summary>Writes a SEQUENCE OF <paramref name="values" />.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="values">The elements.</param>
    /// <param name="writeElement">Writes one element.</param>
    public static void WriteSequenceOf<T>(AsnWriter writer, IReadOnlyList<T> values, Action<AsnWriter, T> writeElement)
    {
        using (writer.PushSequence())
        {
            foreach (T value in values)
            {
                writeElement(writer, value);
            }
        }
    }

    /// <summary>Reads the explicit field <c>[n]</c>, which must be next.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="readValue">Reads the value.</param>
    /// <returns>The value.</returns>
    public static T ReadField<T>(AsnReader reader, int number, Func<AsnReader, T> readValue) =>
        ReadExplicit(reader, Context(number), readValue);

    /// <summary>Reads the explicit field <c>[n]</c> when it is next.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="readValue">Reads the value.</param>
    /// <returns>The value, or <see langword="null" /> when the field is absent.</returns>
    public static T? ReadOptionalField<T>(AsnReader reader, int number, Func<AsnReader, T> readValue)
        where T : class =>
        IsNext(reader, number) ? ReadField(reader, number, readValue) : null;

    /// <summary>Reads the explicit field <c>[n]</c> when it is next.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="readValue">Reads the value.</param>
    /// <returns>The value, or <see langword="null" /> when the field is absent.</returns>
    public static T? ReadOptionalValueField<T>(AsnReader reader, int number, Func<AsnReader, T> readValue)
        where T : struct =>
        IsNext(reader, number) ? ReadField(reader, number, readValue) : null;

    /// <summary>Reads the explicit field <c>[n]</c> as a SEQUENCE OF, which must be next.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="readElement">Reads one element.</param>
    /// <returns>The elements.</returns>
    public static IReadOnlyList<T> ReadSequenceOfField<T>(AsnReader reader, int number, Func<AsnReader, T> readElement) =>
        ReadField(reader, number, inner => ReadSequenceOf(inner, readElement));

    /// <summary>Reads the explicit field <c>[n]</c> as a SEQUENCE OF when it is next.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The context tag number.</param>
    /// <param name="readElement">Reads one element.</param>
    /// <returns>The elements; empty when the field is absent.</returns>
    public static IReadOnlyList<T> ReadOptionalSequenceOfField<T>(AsnReader reader, int number, Func<AsnReader, T> readElement) =>
        IsNext(reader, number) ? ReadSequenceOfField(reader, number, readElement) : [];

    /// <summary>Reads a SEQUENCE OF.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="readElement">Reads one element.</param>
    /// <returns>The elements.</returns>
    public static IReadOnlyList<T> ReadSequenceOf<T>(AsnReader reader, Func<AsnReader, T> readElement) =>
        ReadSequence(reader, inner =>
        {
            List<T> values = [];
            while (inner.HasData)
            {
                values.Add(readElement(inner));
            }

            return values;
        });

    /// <summary>Writes an <c>Int32</c> or <c>Microseconds</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void WriteInt32(AsnWriter writer, int value) => writer.WriteInteger(value);

    /// <summary>Writes a <c>UInt32</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void WriteUInt32(AsnWriter writer, uint value) => writer.WriteInteger(value);

    /// <summary>Reads an <c>Int32</c> or <c>Microseconds</c>.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The value.</returns>
    /// <exception cref="AsnContentException">The integer does not fit 32 signed bits.</exception>
    public static int ReadInt32(AsnReader reader) =>
        reader.TryReadInt32(out int value) ? value : throw new AsnContentException();

    /// <summary>
    /// Reads a <c>UInt32</c>. A negative 32-bit value is taken as its two's complement,
    /// because some implementations write nonces and sequence numbers as signed integers.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The value.</returns>
    /// <exception cref="AsnContentException">The integer fits neither 32 unsigned nor 32 signed bits.</exception>
    public static uint ReadUInt32(AsnReader reader) =>
        reader.TryReadUInt32(out uint value) ? value : unchecked((uint)ReadInt32(reader));

    /// <summary>Writes an OCTET STRING.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The bytes.</param>
    public static void WriteOctets(AsnWriter writer, byte[] value) => writer.WriteOctetString(value);

    /// <summary>Reads an OCTET STRING.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The bytes.</returns>
    public static byte[] ReadOctets(AsnReader reader) => reader.ReadOctetString();

    /// <summary>
    /// Writes a <c>KerberosString</c>: a GeneralString of the UTF-8 bytes. <see cref="AsnWriter" />
    /// has no GeneralString writer, so the bytes are written as a primitive OCTET STRING
    /// under the one-byte tag <c>[0]</c>, and that tag byte is then replaced with
    /// GeneralString's, <c>0x1B</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The string.</param>
    public static void WriteString(AsnWriter writer, string value)
    {
        AsnWriter scratch = new(AsnEncodingRules.DER);
        scratch.WriteOctetString(Encoding.UTF8.GetBytes(value), new Asn1Tag(TagClass.ContextSpecific, 0));
        byte[] encoded = scratch.Encode();
        encoded[0] = GeneralStringTagByte;
        writer.WriteEncodedValue(encoded);
    }

    /// <summary>Reads a <c>KerberosString</c>: a primitive GeneralString, taken as UTF-8.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The string.</returns>
    /// <exception cref="AsnContentException">The GeneralString uses the constructed encoding.</exception>
    public static string ReadString(AsnReader reader) =>
        reader.TryReadPrimitiveCharacterStringBytes(GeneralStringTag, out ReadOnlyMemory<byte> contents)
            ? Encoding.UTF8.GetString(contents.Span)
            : throw new AsnContentException();

    /// <summary>Writes a <c>KerberosTime</c>: a GeneralizedTime in UTC, to the second.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The time; anything below a second is dropped.</param>
    public static void WriteTime(AsnWriter writer, DateTimeOffset value) =>
        writer.WriteGeneralizedTime(value, omitFractionalSeconds: true);

    /// <summary>Reads a <c>KerberosTime</c>.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The time.</returns>
    public static DateTimeOffset ReadTime(AsnReader reader) => reader.ReadGeneralizedTime();

    /// <summary>Writes a <c>KerberosFlags</c>: 32 bits, bit 0 the most significant.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The flags.</param>
    public static void WriteFlags(AsnWriter writer, uint value)
    {
        Span<byte> bits = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bits, value);
        writer.WriteBitString(bits);
    }

    /// <summary>
    /// Reads a <c>KerberosFlags</c>. A BIT STRING shorter than 32 bits reads as if padded with
    /// zeros and bits past the 32nd are ignored, as MIT does.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The flags, bit 0 the most significant.</returns>
    public static uint ReadFlags(AsnReader reader)
    {
        byte[] bits = reader.ReadBitString(out _);
        Span<byte> padded = stackalloc byte[sizeof(uint)];
        bits.AsSpan(0, Math.Min(bits.Length, sizeof(uint))).CopyTo(padded);
        return BinaryPrimitives.ReadUInt32BigEndian(padded);
    }

    /// <summary>Writes an <c>EncryptionKey</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="key">The key.</param>
    public static void WriteKey(AsnWriter writer, KerberosKey key)
    {
        using (writer.PushSequence())
        {
            WriteField(writer, 0, key.EncryptionType, Int32Writer);
            using (writer.PushSequence(Context(1)))
            {
                writer.WriteOctetString(key.Value);
            }
        }
    }

    /// <summary>Reads an <c>EncryptionKey</c>.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The key; the caller disposes it.</returns>
    public static KerberosKey ReadKey(AsnReader reader) =>
        ReadSequence(reader, inner => new KerberosKey(ReadField(inner, 0, Int32Reader), ReadField(inner, 1, OctetsReader)));

    /// <summary>Writes a <c>HostAddress</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="address">The address.</param>
    public static void WriteAddress(AsnWriter writer, KerberosAddress address)
    {
        using (writer.PushSequence())
        {
            WriteField(writer, 0, address.AddressType, Int32Writer);
            WriteField(writer, 1, address.Address, OctetsWriter);
        }
    }

    /// <summary>Reads a <c>HostAddress</c>.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The address.</returns>
    public static KerberosAddress ReadAddress(AsnReader reader) =>
        ReadSequence(reader, inner => new KerberosAddress(ReadField(inner, 0, Int32Reader), ReadField(inner, 1, OctetsReader)));

    /// <summary>Writes one <c>AuthorizationData</c> element.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="element">The element.</param>
    public static void WriteAuthorizationData(AsnWriter writer, KerberosAuthorizationData element)
    {
        using (writer.PushSequence())
        {
            WriteField(writer, 0, element.DataType, Int32Writer);
            WriteField(writer, 1, element.Data, OctetsWriter);
        }
    }

    /// <summary>Reads one <c>AuthorizationData</c> element.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The element.</returns>
    public static KerberosAuthorizationData ReadAuthorizationData(AsnReader reader) =>
        ReadSequence(reader, inner => new KerberosAuthorizationData(ReadField(inner, 0, Int32Reader), ReadField(inner, 1, OctetsReader)));

    /// <summary>Checks a message's <c>pvno</c> and <c>msg-type</c> fields.</summary>
    /// <param name="reader">The reader, at the <c>pvno</c> field.</param>
    /// <param name="versionField">The <c>pvno</c> field's context tag number; <c>msg-type</c> follows it.</param>
    /// <param name="messageType">The message type the application tag named.</param>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.UnsupportedVersion" /> or <see cref="KerberosMessageError.UnexpectedMessage" />.
    /// </exception>
    public static void ReadMessageHeader(AsnReader reader, int versionField, KerberosMessageType messageType)
    {
        ReadVersion(reader, versionField);
        if (ReadField(reader, versionField + 1, Int32Reader) != (int)messageType)
        {
            throw new KerberosMessageException(KerberosMessageError.UnexpectedMessage);
        }
    }

    /// <summary>Writes a message's <c>pvno</c> and <c>msg-type</c> fields.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="versionField">The <c>pvno</c> field's context tag number; <c>msg-type</c> follows it.</param>
    /// <param name="messageType">The message type.</param>
    public static void WriteMessageHeader(AsnWriter writer, int versionField, KerberosMessageType messageType)
    {
        WriteVersion(writer, versionField);
        WriteField(writer, versionField + 1, (int)messageType, Int32Writer);
    }

    /// <summary>Reads a version field that must say 5.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="number">The field's context tag number.</param>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.UnsupportedVersion" />.</exception>
    public static void ReadVersion(AsnReader reader, int number)
    {
        if (ReadField(reader, number, Int32Reader) != ProtocolVersion)
        {
            throw new KerberosMessageException(KerberosMessageError.UnsupportedVersion);
        }
    }

    /// <summary>Writes a version field saying 5.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The field's context tag number.</param>
    public static void WriteVersion(AsnWriter writer, int number) => WriteField(writer, number, ProtocolVersion, Int32Writer);

    private static Asn1Tag Context(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    private static bool IsNext(AsnReader reader, int number) =>
        reader.HasData && reader.PeekTag().HasSameClassAndValue(Context(number));

    private static T ReadExplicit<T>(AsnReader reader, Asn1Tag tag, Func<AsnReader, T> readContents)
    {
        AsnReader inner = reader.ReadSequence(tag);
        T value = readContents(inner);
        ThrowIfNotEmpty(inner, value);
        return value;
    }

    /// <summary>
    /// Fails when bytes are left after <paramref name="value" />, first disposing it when it
    /// holds a key, so a malformed message leaves no key bytes behind.
    /// </summary>
    private static void ThrowIfNotEmpty(AsnReader reader, object? value)
    {
        if (reader.HasData)
        {
            (value as IDisposable)?.Dispose();
            throw new AsnContentException();
        }
    }
}
