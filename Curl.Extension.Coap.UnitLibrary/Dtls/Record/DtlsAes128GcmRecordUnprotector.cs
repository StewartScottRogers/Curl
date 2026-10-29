using System.Security.Cryptography;
using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Record;

/// <summary>
/// Unprotects the records the other side sends in one epoch with AES-128-GCM (RFC 5288, RFC
/// 6347 section 4.1.2). An epoch-0 record passes through unprotected; a record that does not
/// parse, belongs to another epoch, is a replay (section 4.1.2.6) or fails its tag is dropped
/// silently (section 4.1.2.7), and only a record whose tag verifies advances the replay window.
/// </summary>
/// <param name="keys">The sending side's write keys, from <see cref="DtlsAes128GcmKeyBlock.Derive" />.</param>
/// <param name="epoch">The epoch the keys belong to, 1 after the first ChangeCipherSpec.</param>
internal sealed class DtlsAes128GcmRecordUnprotector(Tls12WriteKeys keys, ushort epoch) : IDisposable
{
    private const int Overhead = DtlsAes128GcmRecordNonce.ExplicitLength + DtlsAes128GcmRecordNonce.TagLength;

    private readonly AesGcm aesGcm = new(keys.Key, DtlsAes128GcmRecordNonce.TagLength);

    private readonly byte[] writeIv = keys.Iv;

    private readonly DtlsReplayWindow replayWindow = new();

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

        if (header.Epoch != epoch || fragment.Length < Overhead || replayWindow.IsReplay(header.SequenceNumber))
        {
            return false;
        }

        byte[] decrypted = new byte[fragment.Length - Overhead];
        try
        {
            aesGcm.Decrypt(
                DtlsAes128GcmRecordNonce.Combine(writeIv, fragment[..DtlsAes128GcmRecordNonce.ExplicitLength]),
                fragment[DtlsAes128GcmRecordNonce.ExplicitLength..^DtlsAes128GcmRecordNonce.TagLength],
                fragment[^DtlsAes128GcmRecordNonce.TagLength..],
                decrypted,
                header.AdditionalData((ushort)decrypted.Length));
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }

        replayWindow.MarkReceived(header.SequenceNumber);
        plaintext = decrypted;
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => aesGcm.Dispose();

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
