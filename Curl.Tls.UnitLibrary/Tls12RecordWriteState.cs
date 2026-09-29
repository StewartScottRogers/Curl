using System.Buffers;
using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// The current write state of the TLS 1.2, 1.1 and 1.0 record layer (RFC 5246 section
/// 6.1): it fragments content into records of at most 2^14 bytes, protects each with the
/// negotiated cipher and its own sequence number, and writes the record headers.
/// </summary>
/// <remarks>
/// With a CBC cipher in TLS 1.0, each call that writes application data first writes an
/// empty application data record, the BEAST countermeasure OpenSSL applies unless
/// <c>SSL_OP_DONT_INSERT_EMPTY_FRAGMENTS</c> is set, which curl sets for
/// <c>--ssl-allow-beast</c> (ADR-0148).
/// </remarks>
public sealed class Tls12RecordWriteState : IDisposable
{
    /// <summary>The longest content one record carries (RFC 5246 section 6.2.1).</summary>
    public const int MaximumFragmentLength = 1 << 14;

    /// <summary>The length of a record header: type, version and length.</summary>
    public const int RecordHeaderLength = 5;

    private readonly TlsProtocolVersion version;

    private readonly Tls12RecordCipher cipher;

    private readonly bool insertsEmptyFragment;

    private Tls12RecordWriteState(TlsProtocolVersion version, Tls12RecordCipher cipher, bool insertsEmptyFragment)
    {
        this.version = version;
        this.cipher = cipher;
        this.insertsEmptyFragment = insertsEmptyFragment;
    }

    /// <summary>Gets the sequence number the next record is protected with.</summary>
    public ulong SequenceNumber { get; private set; }

    /// <summary>
    /// Gets a value indicating whether each write of application data starts with an
    /// empty record: TLS 1.0 with a CBC cipher, unless the caller turned it off.
    /// </summary>
    public bool InsertsEmptyFragment => insertsEmptyFragment;

    /// <summary>Creates the initial write state: no encryption and no MAC.</summary>
    /// <param name="version">The version the record headers carry.</param>
    /// <returns>The write state.</returns>
    public static Tls12RecordWriteState CreatePlaintext(TlsProtocolVersion version) =>
        Create(new Tls12RecordProtectionParameters(version, Tls12BulkCipher.Null, Tls12MacAlgorithm.None), Tls12WriteKeys.None, SystemTlsRandomSource.Instance);

    /// <summary>Creates the write state a ChangeCipherSpec switches to.</summary>
    /// <param name="parameters">The negotiated record protection.</param>
    /// <param name="keys">This side's write keys from <see cref="Tls12KeyBlock" />.</param>
    /// <param name="randomSource">The source of TLS 1.1 and 1.2 CBC records' explicit IVs.</param>
    /// <param name="insertEmptyFragment">
    /// <see langword="false" /> for <c>--ssl-allow-beast</c>; it matters only for TLS 1.0 with a CBC cipher.
    /// </param>
    /// <returns>The write state.</returns>
    /// <exception cref="ArgumentException">The parameters name no suite, or a key has the wrong length.</exception>
    public static Tls12RecordWriteState Create(
        Tls12RecordProtectionParameters parameters,
        Tls12WriteKeys keys,
        ITlsRandomSource randomSource,
        bool insertEmptyFragment = true)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(randomSource);
        parameters.Validate(keys);
        bool isTls10Cbc = parameters.Version == TlsProtocolVersion.Tls10 && parameters.Mode == Tls12CipherMode.Cbc;
        return new Tls12RecordWriteState(
            parameters.Version,
            Tls12RecordCipher.Create(parameters, keys, randomSource),
            insertEmptyFragment && isTls10Cbc);
    }

    /// <summary>
    /// Protects <paramref name="content" /> as one or more records of
    /// <paramref name="contentType" />, headers included, ready to send. Empty content is
    /// one empty record.
    /// </summary>
    /// <param name="contentType">The records' content type.</param>
    /// <param name="content">The bytes to send.</param>
    /// <returns>The records, back to back.</returns>
    public byte[] Protect(TlsContentType contentType, ReadOnlySpan<byte> content)
    {
        ArrayBufferWriter<byte> records = new();
        if (insertsEmptyFragment && contentType == TlsContentType.ApplicationData && !content.IsEmpty)
        {
            WriteRecord(records, contentType, []);
        }

        int offset = 0;
        do
        {
            int length = Math.Min(MaximumFragmentLength, content.Length - offset);
            WriteRecord(records, contentType, content.Slice(offset, length));
            offset += length;
        }
        while (offset < content.Length);

        return records.WrittenSpan.ToArray();
    }

    /// <summary>Zeroes and releases the keys.</summary>
    public void Dispose() => cipher.Dispose();

    private void WriteRecord(ArrayBufferWriter<byte> records, TlsContentType contentType, ReadOnlySpan<byte> fragment)
    {
        byte[] protectedFragment = cipher.Seal(SequenceNumber, contentType, version, fragment);
        SequenceNumber++;
        Span<byte> header = records.GetSpan(RecordHeaderLength);
        header[0] = (byte)contentType;
        BinaryPrimitives.WriteUInt16BigEndian(header[1..], (ushort)version);
        BinaryPrimitives.WriteUInt16BigEndian(header[3..], (ushort)protectedFragment.Length);
        records.Advance(RecordHeaderLength);
        records.Write(protectedFragment);
    }
}
