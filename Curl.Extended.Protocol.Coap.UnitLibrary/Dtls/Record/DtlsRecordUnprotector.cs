using Curl.Extended.Protocol.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extended.Protocol.Coap.Dtls.Record;

/// <summary>
/// Unprotects the records the other side sends in one epoch with AES-128-GCM (RFC 5288, RFC
/// 6347 section 4.1.2). An epoch-0 record passes through unprotected; a record that does not
/// parse, belongs to another epoch, is a replay (section 4.1.2.6) or fails its tag is dropped
/// silently (section 4.1.2.7), and only a record whose tag verifies advances the replay window.
/// </summary>
/// <param name="keys">The sending side's write keys, from <see cref="DtlsKeyBlock.Derive" />.</param>
/// <param name="epoch">The epoch the keys belong to, 1 after the first ChangeCipherSpec.</param>
/// <param name="bulkCipher">The suite's bulk cipher, AES-128-GCM unless given.</param>
internal sealed class DtlsRecordUnprotector(Tls12WriteKeys keys, ushort epoch, Tls12BulkCipher bulkCipher = Tls12BulkCipher.Aes128Gcm) : IDisposable
{
    private readonly DtlsRecordAead aead = new(bulkCipher, keys.Key);

    private readonly byte[] writeIv = keys.Iv;

    private readonly DtlsReplayWindow replayWindow = new();

    private int Overhead => DtlsRecordNonce.ExplicitLength + aead.TagLength;

    /// <summary>
    /// Reads the record at the start of <paramref name="record" /> and gives its header and
    /// plaintext, or returns <c>false</c> when the record must be dropped.
    /// </summary>
    public bool TryUnprotect(ReadOnlySpan<byte> record, out DtlsRecordHeader header, out byte[] plaintext)
    {
        plaintext = [];
        if (!TryDecodeHeader(record, out header))
        {
            return false;
        }

        ReadOnlySpan<byte> fragment = record.Slice(DtlsRecordHeader.HeaderLength, header.Length);
        if (header.Epoch == 0)
        {
            plaintext = fragment.ToArray();
            return true;
        }

        if (IsNotAcceptable(header, fragment.Length))
        {
            return false;
        }

        byte[] decrypted = new byte[fragment.Length - Overhead];
        if (!aead.TryDecrypt(
                DtlsRecordNonce.Combine(writeIv, fragment[..DtlsRecordNonce.ExplicitLength]),
                fragment[DtlsRecordNonce.ExplicitLength..^aead.TagLength],
                fragment[^aead.TagLength..],
                decrypted,
                header.AdditionalData((ushort)decrypted.Length)))
        {
            return false;
        }

        replayWindow.MarkReceived(header.SequenceNumber);
        plaintext = decrypted;
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => aead.Dispose();

    private bool IsNotAcceptable(DtlsRecordHeader header, int fragmentLength) =>
        header.Epoch != epoch || fragmentLength < Overhead || replayWindow.IsReplay(header.SequenceNumber);

    private static bool TryDecodeHeader(ReadOnlySpan<byte> record, out DtlsRecordHeader header)
    {
        try
        {
            header = DtlsRecordHeader.Decode(record);
            return true;
        }
        catch (FormatException)
        {
            header = default;
            return false;
        }
    }
}
