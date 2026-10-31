using Curl.Tls;

namespace Curl.Extended.Protocol.Coap.Dtls.Record;

/// <summary>
/// Derives the DTLS 1.2 write keys of an AES-128-GCM or AES-128-CCM_8 suite (RFC 5288 section 3,
/// RFC 6655, RFC 6347 section 4.2.6 keeping RFC 5246 section 6.3): the key block
/// <c>PRF(master_secret, "key expansion", server_random + client_random)</c> divided into
/// each side's 16-byte key and 4-byte write IV.
/// </summary>
internal static class DtlsKeyBlock
{
    /// <summary>Gets the record protection of <paramref name="bulkCipher" />: no MAC key, a 16-byte key and a 4-byte implicit IV.</summary>
    public static Tls12RecordProtectionParameters ParametersOf(Tls12BulkCipher bulkCipher) =>
        new(TlsProtocolVersion.Tls12, bulkCipher, Tls12MacAlgorithm.None);

    /// <summary>Computes the key block with TLS 1.2's P_SHA256 and divides it into both sides' write keys.</summary>
    /// <param name="masterSecret">The 48-byte master secret.</param>
    /// <param name="clientRandom">The ClientHello random.</param>
    /// <param name="serverRandom">The ServerHello random.</param>
    /// <param name="bulkCipher">The negotiated suite's bulk cipher, AES-128-GCM unless given.</param>
    public static Tls12KeyBlock Derive(ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom, Tls12BulkCipher bulkCipher = Tls12BulkCipher.Aes128Gcm)
    {
        Tls12RecordProtectionParameters parameters = ParametersOf(bulkCipher);
        return Tls12KeyBlock.Partition(parameters, TlsPrf.Sha256.ComputeKeyBlock(masterSecret, serverRandom, clientRandom, parameters.KeyBlockLength));
    }
}
