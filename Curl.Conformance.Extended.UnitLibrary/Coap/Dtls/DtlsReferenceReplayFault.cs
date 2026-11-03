namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Sends, before each answer, a protected record under epoch 1 sequence number 0, which curl has already received
/// (the server's Finished), carrying the answer as <paramref name="forge" /> rewrites it. It authenticates, so only
/// the replay window (RFC 6347 section 4.1.2.6) can drop it; curl must take the real answer that follows.
/// </summary>
/// <param name="forge">Rewrites the answer the replayed record carries.</param>
public sealed class DtlsReferenceReplayFault(Func<byte[], byte[]> forge) : DtlsReferenceFault
{
    /// <inheritdoc />
    public override IReadOnlyList<DtlsReferenceRecord> SendApplicationAnswer(byte[] plaintext, DtlsReferenceServer server) =>
        [server.ProtectedRecordAt(DtlsReferenceRecord.ApplicationData, 0, forge(plaintext)), .. base.SendApplicationAnswer(plaintext, server)];
}
