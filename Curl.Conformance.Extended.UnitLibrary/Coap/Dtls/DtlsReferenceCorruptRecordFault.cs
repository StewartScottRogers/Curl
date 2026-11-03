namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Sends, before each answer, a protected record under the next epoch 1 sequence number carrying the answer as
/// <paramref name="forge" /> rewrites it, with the last byte of its authentication tag flipped. It fails
/// authentication, so curl must discard it silently (RFC 6347 section 4.1.2.7) and take the real answer that follows.
/// </summary>
/// <param name="forge">Rewrites the answer the corrupt record carries.</param>
public sealed class DtlsReferenceCorruptRecordFault(Func<byte[], byte[]> forge) : DtlsReferenceFault
{
    /// <inheritdoc />
    public override IReadOnlyList<DtlsReferenceRecord> SendApplicationAnswer(byte[] plaintext, DtlsReferenceServer server)
    {
        DtlsReferenceRecord sealedRecord = server.ProtectedRecord(DtlsReferenceRecord.ApplicationData, forge(plaintext));
        byte[] fragment = [.. sealedRecord.Fragment];
        fragment[^1] ^= 0xFF;
        return [sealedRecord with { Fragment = fragment }, .. base.SendApplicationAnswer(plaintext, server)];
    }
}
