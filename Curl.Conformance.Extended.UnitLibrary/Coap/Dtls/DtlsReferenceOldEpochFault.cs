namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Sends, before each answer, an application data record from epoch 0, unprotected and carrying the answer as
/// <paramref name="forge" /> rewrites it. Once the handshake has moved curl to epoch 1, a record from the old epoch
/// must be dropped (RFC 6347 section 4.1); curl must take the real answer that follows.
/// </summary>
/// <param name="forge">Rewrites the answer the old-epoch record carries.</param>
public sealed class DtlsReferenceOldEpochFault(Func<byte[], byte[]> forge) : DtlsReferenceFault
{
    /// <inheritdoc />
    public override IReadOnlyList<DtlsReferenceRecord> SendApplicationAnswer(byte[] plaintext, DtlsReferenceServer server) =>
        [server.PlainRecord(DtlsReferenceRecord.ApplicationData, forge(plaintext)), .. base.SendApplicationAnswer(plaintext, server)];
}
