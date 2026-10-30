using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Extension.ObliviousDoh.Wire;

namespace Curl.Extension.ObliviousDoh.Encryption;

/// <summary>
/// One encrypted query and the HPKE context that opens its response: the client side of RFC 9230 sections 6.2
/// (<c>encrypt_query_body</c>) and 6.4 (<c>decrypt_response_body</c>).
/// </summary>
public sealed class ObliviousDohQuery : IDisposable
{
    private readonly ObliviousDohConfig config;
    private readonly HpkeContext context;
    private readonly byte[] plaintext;

    private ObliviousDohQuery(ObliviousDohConfig config, HpkeContext context, byte[] plaintext, byte[] message)
    {
        this.config = config;
        this.context = context;
        this.plaintext = plaintext;
        Message = message;
    }

    /// <summary>Gets the <c>ObliviousDoHMessage</c> of type query, the body of the POST to the proxy.</summary>
    public byte[] Message { get; }

    /// <summary>Encrypts <paramref name="dnsMessage" /> to <paramref name="config" />'s key with a fresh ephemeral key.</summary>
    /// <param name="config">The target's config.</param>
    /// <param name="dnsMessage">The DNS query.</param>
    /// <returns>The query, or <see langword="null" /> when HPKE cannot use the config's public key.</returns>
    public static ObliviousDohQuery? Seal(ObliviousDohConfig config, ReadOnlySpan<byte> dnsMessage)
    {
        ArgumentNullException.ThrowIfNull(config);
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(config.Kem)];
        bool setUp = Hpke.TrySetupBaseSender(config.Kem, config.Kdf, config.Aead, config.PublicKey, "odoh query"u8, encapsulatedKey, out HpkeContext? context);
        return Sealed(config, dnsMessage, setUp ? context : null, encapsulatedKey);
    }

    /// <summary>Encrypts <paramref name="dnsMessage" /> with a given ephemeral private key, so a test can fix the bytes.</summary>
    /// <param name="config">The target's config.</param>
    /// <param name="dnsMessage">The DNS query.</param>
    /// <param name="ephemeralPrivateKey">The ephemeral private key.</param>
    /// <returns>The query, or <see langword="null" /> when HPKE cannot use the config's public key.</returns>
    internal static ObliviousDohQuery? Seal(ObliviousDohConfig config, ReadOnlySpan<byte> dnsMessage, ReadOnlySpan<byte> ephemeralPrivateKey)
    {
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(config.Kem)];
        bool setUp = Hpke.TrySetupBaseSender(config.Kem, config.Kdf, config.Aead, config.PublicKey, ephemeralPrivateKey, "odoh query"u8, encapsulatedKey, out HpkeContext? context);
        return Sealed(config, dnsMessage, setUp ? context : null, encapsulatedKey);
    }

    /// <summary>
    /// Opens a response message: checks it is of type response, derives the key and nonce from the exported
    /// secret, the query plaintext and the response nonce in its <c>key_id</c> field (section 6.4), and opens it.
    /// </summary>
    /// <param name="responseMessage">The body of the proxy's answer.</param>
    /// <returns>The DNS answer, or <see langword="null" /> when the message is malformed or does not open.</returns>
    public byte[]? OpenResponse(ReadOnlySpan<byte> responseMessage)
    {
        if (ObliviousDohMessage.Decode(responseMessage) is not { Type: ObliviousDohMessageType.Response } response
            || response.EncryptedMessage.Length < ResponseAead.TagSize)
        {
            return null;
        }

        int keySize = ResponseAead.KeySize(config.Aead);
        byte[] secret = new byte[keySize];
        context.Export("odoh response"u8, secret);
        byte[] responseNonce = response.KeyId;
        byte[] pseudorandomKey = HKDF.Extract(HashAlgorithmName.SHA256, secret, [.. plaintext, .. LengthPrefix.Prepend(responseNonce)]);
        byte[] key = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, keySize, "odoh key"u8.ToArray());
        byte[] nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, ResponseAead.NonceSize, "odoh nonce"u8.ToArray());
        byte[] associatedData = [(byte)ObliviousDohMessageType.Response, .. LengthPrefix.Prepend(responseNonce)];
        byte[] opened = new byte[response.EncryptedMessage.Length - ResponseAead.TagSize];
        return ResponseAead.TryOpen(config.Aead, key, nonce, associatedData, response.EncryptedMessage, opened)
            ? ObliviousDohPlaintext.DecodeDnsMessage(opened)
            : null;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    // Seals the padded plaintext with the query's associated data, 0x01 then the key ID as a vector (section 6.2).
    private static ObliviousDohQuery? Sealed(ObliviousDohConfig config, ReadOnlySpan<byte> dnsMessage, HpkeContext? context, byte[] encapsulatedKey)
    {
        if (context is null)
        {
            return null;
        }

        byte[] plaintext = ObliviousDohPlaintext.Encode(dnsMessage);
        byte[] keyId = config.KeyId();
        byte[] associatedData = [(byte)ObliviousDohMessageType.Query, .. LengthPrefix.Prepend(keyId)];
        byte[] ciphertext = new byte[plaintext.Length + HpkeContext.TagSize];
        context.Seal(associatedData, plaintext, ciphertext);
        byte[] message = new ObliviousDohMessage(ObliviousDohMessageType.Query, keyId, [.. encapsulatedKey, .. ciphertext]).Encode();
        return new ObliviousDohQuery(config, context, plaintext, message);
    }
}
