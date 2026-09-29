using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.3 key schedule (RFC 8446 section 7) for one cipher suite hash: the early,
/// handshake and master secrets, every secret derived from them, traffic keys and IVs,
/// Finished and binder values, the resumption PSK and the key update. Every method is a
/// pure function of its arguments; the handshake holds the secrets between calls.
/// </summary>
public sealed class Tls13KeySchedule
{
    private const int IvLength = 12;

    private readonly byte[] emptyHash;

    /// <summary>Creates the key schedule for <paramref name="hashAlgorithm" />.</summary>
    /// <param name="hashAlgorithm">The cipher suite's hash, SHA-256 or SHA-384.</param>
    /// <exception cref="ArgumentException">The hash is neither SHA-256 nor SHA-384.</exception>
    public Tls13KeySchedule(HashAlgorithmName hashAlgorithm)
    {
        HashLength = Tls13HashLength.Of(hashAlgorithm);
        HashAlgorithm = hashAlgorithm;
        emptyHash = CryptographicOperations.HashData(hashAlgorithm, []);
    }

    /// <summary>Gets the key schedule of the SHA-256 suites (<c>TLS_AES_128_GCM_SHA256</c>, <c>TLS_CHACHA20_POLY1305_SHA256</c>).</summary>
    public static Tls13KeySchedule Sha256 { get; } = new(HashAlgorithmName.SHA256);

    /// <summary>Gets the key schedule of the SHA-384 suite (<c>TLS_AES_256_GCM_SHA384</c>).</summary>
    public static Tls13KeySchedule Sha384 { get; } = new(HashAlgorithmName.SHA384);

    /// <summary>Gets the hash every derivation uses.</summary>
    public HashAlgorithmName HashAlgorithm { get; }

    /// <summary>Gets the length in bytes of the hash, and so of every secret.</summary>
    public int HashLength { get; }

    /// <summary>Starts an empty transcript hashed with this schedule's hash.</summary>
    /// <returns>The transcript hash.</returns>
    public TranscriptHash CreateTranscriptHash() => new(HashAlgorithm);

    /// <summary>Returns HKDF-Extract(<paramref name="salt" />, <paramref name="inputKeyingMaterial" />).</summary>
    /// <param name="salt">The salt.</param>
    /// <param name="inputKeyingMaterial">The input keying material.</param>
    /// <returns>The pseudorandom key.</returns>
    public byte[] Extract(byte[] salt, byte[] inputKeyingMaterial) => HKDF.Extract(HashAlgorithm, inputKeyingMaterial, salt);

    /// <summary>Returns HKDF-Expand-Label(<paramref name="secret" />, <paramref name="label" />, <paramref name="context" />, <paramref name="length" />).</summary>
    /// <param name="secret">The secret to expand.</param>
    /// <param name="label">The label without its <c>tls13 </c> prefix.</param>
    /// <param name="context">The context.</param>
    /// <param name="length">The length in bytes of the output.</param>
    /// <returns>The expanded key material.</returns>
    public byte[] ExpandLabel(byte[] secret, string label, ReadOnlySpan<byte> context, int length) =>
        HkdfLabel.Expand(HashAlgorithm, secret, label, context, length);

    /// <summary>Returns Derive-Secret: HKDF-Expand-Label of <paramref name="secret" /> with the transcript hash as context, one hash long.</summary>
    /// <param name="secret">The secret to derive from.</param>
    /// <param name="label">The label without its <c>tls13 </c> prefix.</param>
    /// <param name="transcriptHash">The transcript hash of the messages the secret binds.</param>
    /// <returns>The derived secret.</returns>
    public byte[] DeriveSecret(byte[] secret, string label, ReadOnlySpan<byte> transcriptHash) =>
        ExpandLabel(secret, label, transcriptHash, HashLength);

    /// <summary>Returns the early secret: HKDF-Extract of the PSK, or of zeros when there is none, with a zero salt.</summary>
    /// <param name="preSharedKey">The PSK, or <see langword="null" /> for a full handshake.</param>
    /// <returns>The early secret.</returns>
    public byte[] ComputeEarlySecret(byte[]? preSharedKey) => Extract(new byte[HashLength], preSharedKey ?? new byte[HashLength]);

    /// <summary>Returns the handshake secret: HKDF-Extract of the (EC)DHE shared secret, salted with the early secret's <c>derived</c> secret.</summary>
    /// <param name="earlySecret">The early secret.</param>
    /// <param name="sharedSecret">The (EC)DHE shared secret.</param>
    /// <returns>The handshake secret.</returns>
    public byte[] ComputeHandshakeSecret(byte[] earlySecret, byte[] sharedSecret) =>
        Extract(DeriveSecret(earlySecret, "derived", emptyHash), sharedSecret);

    /// <summary>Returns the master secret: HKDF-Extract of zeros, salted with the handshake secret's <c>derived</c> secret.</summary>
    /// <param name="handshakeSecret">The handshake secret.</param>
    /// <returns>The master secret.</returns>
    public byte[] ComputeMasterSecret(byte[] handshakeSecret) =>
        Extract(DeriveSecret(handshakeSecret, "derived", emptyHash), new byte[HashLength]);

    /// <summary>Returns the binder key of an external PSK (<c>ext binder</c>).</summary>
    /// <param name="earlySecret">The early secret of the PSK.</param>
    /// <returns>The binder key.</returns>
    public byte[] DeriveExternalBinderKey(byte[] earlySecret) => DeriveSecret(earlySecret, "ext binder", emptyHash);

    /// <summary>Returns the binder key of a resumption PSK (<c>res binder</c>).</summary>
    /// <param name="earlySecret">The early secret of the PSK.</param>
    /// <returns>The binder key.</returns>
    public byte[] DeriveResumptionBinderKey(byte[] earlySecret) => DeriveSecret(earlySecret, "res binder", emptyHash);

    /// <summary>Returns <c>client_early_traffic_secret</c> (<c>c e traffic</c>).</summary>
    /// <param name="earlySecret">The early secret.</param>
    /// <param name="clientHelloHash">The transcript hash through the ClientHello.</param>
    /// <returns>The secret that protects 0-RTT data.</returns>
    public byte[] DeriveClientEarlyTrafficSecret(byte[] earlySecret, ReadOnlySpan<byte> clientHelloHash) =>
        DeriveSecret(earlySecret, "c e traffic", clientHelloHash);

    /// <summary>Returns <c>early_exporter_master_secret</c> (<c>e exp master</c>).</summary>
    /// <param name="earlySecret">The early secret.</param>
    /// <param name="clientHelloHash">The transcript hash through the ClientHello.</param>
    /// <returns>The early exporter master secret.</returns>
    public byte[] DeriveEarlyExporterMasterSecret(byte[] earlySecret, ReadOnlySpan<byte> clientHelloHash) =>
        DeriveSecret(earlySecret, "e exp master", clientHelloHash);

    /// <summary>Returns <c>client_handshake_traffic_secret</c> (<c>c hs traffic</c>).</summary>
    /// <param name="handshakeSecret">The handshake secret.</param>
    /// <param name="serverHelloHash">The transcript hash through the ServerHello.</param>
    /// <returns>The secret that protects the client's handshake messages.</returns>
    public byte[] DeriveClientHandshakeTrafficSecret(byte[] handshakeSecret, ReadOnlySpan<byte> serverHelloHash) =>
        DeriveSecret(handshakeSecret, "c hs traffic", serverHelloHash);

    /// <summary>Returns <c>server_handshake_traffic_secret</c> (<c>s hs traffic</c>).</summary>
    /// <param name="handshakeSecret">The handshake secret.</param>
    /// <param name="serverHelloHash">The transcript hash through the ServerHello.</param>
    /// <returns>The secret that protects the server's handshake messages.</returns>
    public byte[] DeriveServerHandshakeTrafficSecret(byte[] handshakeSecret, ReadOnlySpan<byte> serverHelloHash) =>
        DeriveSecret(handshakeSecret, "s hs traffic", serverHelloHash);

    /// <summary>Returns <c>client_application_traffic_secret_0</c> (<c>c ap traffic</c>).</summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="serverFinishedHash">The transcript hash through the server's Finished.</param>
    /// <returns>The secret that protects the client's first application data.</returns>
    public byte[] DeriveClientApplicationTrafficSecret(byte[] masterSecret, ReadOnlySpan<byte> serverFinishedHash) =>
        DeriveSecret(masterSecret, "c ap traffic", serverFinishedHash);

    /// <summary>Returns <c>server_application_traffic_secret_0</c> (<c>s ap traffic</c>).</summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="serverFinishedHash">The transcript hash through the server's Finished.</param>
    /// <returns>The secret that protects the server's first application data.</returns>
    public byte[] DeriveServerApplicationTrafficSecret(byte[] masterSecret, ReadOnlySpan<byte> serverFinishedHash) =>
        DeriveSecret(masterSecret, "s ap traffic", serverFinishedHash);

    /// <summary>Returns <c>exporter_master_secret</c> (<c>exp master</c>).</summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="serverFinishedHash">The transcript hash through the server's Finished.</param>
    /// <returns>The exporter master secret.</returns>
    public byte[] DeriveExporterMasterSecret(byte[] masterSecret, ReadOnlySpan<byte> serverFinishedHash) =>
        DeriveSecret(masterSecret, "exp master", serverFinishedHash);

    /// <summary>Returns <c>resumption_master_secret</c> (<c>res master</c>).</summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="clientFinishedHash">The transcript hash through the client's Finished.</param>
    /// <returns>The resumption master secret.</returns>
    public byte[] DeriveResumptionMasterSecret(byte[] masterSecret, ReadOnlySpan<byte> clientFinishedHash) =>
        DeriveSecret(masterSecret, "res master", clientFinishedHash);

    /// <summary>Returns the PSK a NewSessionTicket carries (<c>resumption</c>, RFC 8446 section 4.6.1).</summary>
    /// <param name="resumptionMasterSecret">The resumption master secret of the connection that received the ticket.</param>
    /// <param name="ticketNonce">The ticket's nonce.</param>
    /// <returns>The PSK to resume with.</returns>
    public byte[] DeriveResumptionPreSharedKey(byte[] resumptionMasterSecret, ReadOnlySpan<byte> ticketNonce) =>
        ExpandLabel(resumptionMasterSecret, "resumption", ticketNonce, HashLength);

    /// <summary>Returns the write key (<c>key</c>) and IV (<c>iv</c>) of a traffic secret.</summary>
    /// <param name="trafficSecret">The traffic secret.</param>
    /// <param name="keyLength">The AEAD key length: 16 for AES-128-GCM, 32 for AES-256-GCM and ChaCha20-Poly1305.</param>
    /// <returns>The key and IV.</returns>
    public Tls13TrafficKeys DeriveTrafficKeys(byte[] trafficSecret, int keyLength) =>
        new(ExpandLabel(trafficSecret, "key", [], keyLength), ExpandLabel(trafficSecret, "iv", [], IvLength));

    /// <summary>Returns the Finished key (<c>finished</c>) of a base key: a handshake traffic secret or a binder key.</summary>
    /// <param name="baseKey">The handshake traffic secret or binder key.</param>
    /// <returns>The Finished key.</returns>
    public byte[] DeriveFinishedKey(byte[] baseKey) => ExpandLabel(baseKey, "finished", [], HashLength);

    /// <summary>Returns the Finished verify data: the HMAC of the transcript hash under the base key's Finished key.</summary>
    /// <param name="baseKey">The handshake traffic secret of the side sending the Finished.</param>
    /// <param name="transcriptHash">The transcript hash through the message before the Finished.</param>
    /// <returns>The verify data.</returns>
    public byte[] ComputeFinishedVerifyData(byte[] baseKey, ReadOnlySpan<byte> transcriptHash) =>
        CryptographicOperations.HmacData(HashAlgorithm, DeriveFinishedKey(baseKey), transcriptHash);

    /// <summary>Returns a PSK binder (RFC 8446 section 4.2.11.2): Finished verify data under the binder key over the truncated ClientHello.</summary>
    /// <param name="binderKey">The binder key.</param>
    /// <param name="truncatedClientHelloHash">The transcript hash through the ClientHello without its binders list.</param>
    /// <returns>The binder.</returns>
    public byte[] ComputePskBinder(byte[] binderKey, ReadOnlySpan<byte> truncatedClientHelloHash) =>
        ComputeFinishedVerifyData(binderKey, truncatedClientHelloHash);

    /// <summary>Returns the next application traffic secret after a KeyUpdate (<c>traffic upd</c>, RFC 8446 section 7.2).</summary>
    /// <param name="applicationTrafficSecret">The current application traffic secret.</param>
    /// <returns>The next application traffic secret.</returns>
    public byte[] DeriveNextApplicationTrafficSecret(byte[] applicationTrafficSecret) =>
        ExpandLabel(applicationTrafficSecret, "traffic upd", [], HashLength);
}
