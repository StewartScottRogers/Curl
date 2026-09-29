using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// One direction's record protection for TLS 1.2 and below: turns a record's content into
/// its protected fragment and back. The caller owns the sequence number and the record
/// header; <see cref="Create" /> picks the layout the parameters name.
/// </summary>
internal abstract class Tls12RecordCipher : IDisposable
{
    /// <summary>The length of the MAC and AEAD header: sequence number, type, version and length.</summary>
    public const int AdditionalDataLength = 13;

    /// <summary>Creates the record protection <paramref name="parameters" /> names, keyed by <paramref name="keys" />.</summary>
    /// <param name="parameters">The negotiated record protection, already validated.</param>
    /// <param name="keys">This direction's keys.</param>
    /// <param name="randomSource">The source of explicit CBC IVs; <see langword="null" /> for a direction that only opens records.</param>
    public static Tls12RecordCipher Create(Tls12RecordProtectionParameters parameters, Tls12WriteKeys keys, ITlsRandomSource? randomSource) =>
        parameters.Mode switch
        {
            Tls12CipherMode.Null => new Tls12NullRecordCipher(CreateMac(parameters, keys)),
            Tls12CipherMode.Cbc => new Tls12CbcRecordCipher(
                CreateCbcBlockCipher(parameters.BulkCipher, keys.Key),
                CreateMac(parameters, keys)!,
                keys.Iv,
                parameters.EncryptThenMac,
                randomSource),
            Tls12CipherMode.Stream => new Tls12StreamRecordCipher(new Rc4(keys.Key), CreateMac(parameters, keys)!),
            _ => new Tls12AeadRecordCipher(
                CreateAead(parameters.BulkCipher, keys.Key),
                keys.Iv,
                parameters.Mode == Tls12CipherMode.ExplicitNonceAead,
                parameters.TagLength),
        };

    /// <summary>
    /// Writes the header both the record MAC and the AEAD additional data cover (RFC 5246
    /// sections 6.2.3.1 and 6.2.3.3): <c>seq_num</c>, type, version and length.
    /// </summary>
    public static void WriteAdditionalData(Span<byte> destination, ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, int length)
    {
        BinaryPrimitives.WriteUInt64BigEndian(destination, sequenceNumber);
        destination[8] = (byte)contentType;
        BinaryPrimitives.WriteUInt16BigEndian(destination[9..], (ushort)version);
        BinaryPrimitives.WriteUInt16BigEndian(destination[11..], (ushort)length);
    }

    /// <summary>Protects one record's content and returns the fragment that follows the record header.</summary>
    public abstract byte[] Seal(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> content);

    /// <summary>Checks and removes one record's protection, or names the alert its failure calls for.</summary>
    public abstract TlsDecodeResult<byte[]> Open(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment);

    /// <inheritdoc />
    public abstract void Dispose();

    private static Tls12RecordMac? CreateMac(Tls12RecordProtectionParameters parameters, Tls12WriteKeys keys) =>
        parameters.MacAlgorithm == Tls12MacAlgorithm.None ? null : new Tls12RecordMac(parameters.MacHash, keys.MacKey);

    private static ITls12CbcBlockCipher CreateCbcBlockCipher(Tls12BulkCipher bulkCipher, byte[] key)
    {
        if (bulkCipher is Tls12BulkCipher.Camellia128Cbc or Tls12BulkCipher.Camellia256Cbc)
        {
            return new CamelliaCbcBlockCipher(new Camellia(key));
        }

        SymmetricAlgorithm algorithm = bulkCipher == Tls12BulkCipher.TripleDesEdeCbc ? TripleDES.Create() : Aes.Create();
        algorithm.Key = key;
        return new SymmetricAlgorithmCbcBlockCipher(algorithm);
    }

    private static ITlsAead CreateAead(Tls12BulkCipher bulkCipher, byte[] key) => bulkCipher switch
    {
        Tls12BulkCipher.Aria128Gcm or Tls12BulkCipher.Aria256Gcm => new AriaGcmTlsAead(key),
        Tls12BulkCipher.ChaCha20Poly1305 => new ChaCha20Poly1305TlsAead(key),
        Tls12BulkCipher.Aes128Ccm or Tls12BulkCipher.Aes256Ccm or Tls12BulkCipher.Aes128Ccm8 or Tls12BulkCipher.Aes256Ccm8 => new AesCcmTlsAead(key),
        _ => new AesGcmTlsAead(key),
    };
}
