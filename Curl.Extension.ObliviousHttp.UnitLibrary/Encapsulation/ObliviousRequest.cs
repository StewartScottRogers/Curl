using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Extension.ObliviousHttp.Hpke;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>
/// A client's Oblivious HTTP request: encapsulates a binary HTTP request to a gateway's key
/// configuration (RFC 9458 section 4.3) and keeps the HPKE context that decapsulates the
/// gateway's response (section 4.4).
/// </summary>
public sealed class ObliviousRequest : IDisposable
{
    private const int HeaderSize = 7;

    private readonly HpkeSender sender;
    private readonly HpkeAead aead;
    private readonly byte[] encapsulatedRequest;

    private ObliviousRequest(HpkeSender sender, HpkeAead aead, byte[] encapsulatedRequest)
    {
        this.sender = sender;
        this.aead = aead;
        this.encapsulatedRequest = encapsulatedRequest;
    }

    /// <summary>The <c>message/ohttp-req</c> body: the header, the HPKE encapsulated key, and the sealed request.</summary>
    public ReadOnlySpan<byte> EncapsulatedRequest => encapsulatedRequest;

    /// <summary>Encapsulates <paramref name="binaryRequest" /> with a fresh ephemeral key.</summary>
    /// <exception cref="HpkeSetupException">The configuration's public key cannot be used.</exception>
    public static ObliviousRequest Encapsulate(KeyConfigurationChoice choice, ReadOnlySpan<byte> binaryRequest)
    {
        byte[] header = Header(choice);
        return Seal(choice, HpkeSender.SetUp(choice.Suite, choice.Configuration.PublicKey, Info(header)), header, binaryRequest);
    }

    /// <summary>
    /// Encapsulates <paramref name="binaryRequest" /> with <paramref name="ephemeralPrivateKey" />
    /// as skE, so a published example can be reproduced.
    /// </summary>
    /// <exception cref="HpkeSetupException">The configuration's public key cannot be used.</exception>
    /// <exception cref="ArgumentException"><paramref name="ephemeralPrivateKey" /> is not 32 bytes.</exception>
    public static ObliviousRequest Encapsulate(KeyConfigurationChoice choice, ReadOnlySpan<byte> binaryRequest, ReadOnlySpan<byte> ephemeralPrivateKey)
    {
        byte[] header = Header(choice);
        return Seal(choice, HpkeSender.SetUp(choice.Suite, choice.Configuration.PublicKey, ephemeralPrivateKey, Info(header)), header, binaryRequest);
    }

    /// <summary>The binary HTTP response a <c>message/ohttp-res</c> body carries.</summary>
    /// <exception cref="EncapsulatedResponseException"><paramref name="encapsulatedResponse" /> is too short or does not authenticate.</exception>
    /// <exception cref="ObjectDisposedException">The request has been disposed.</exception>
    public byte[] DecapsulateResponse(ReadOnlySpan<byte> encapsulatedResponse)
    {
        int keySize = ResponseAead.KeySize(aead);
        int responseNonceSize = Math.Max(ResponseAead.NonceSize, keySize);
        if (encapsulatedResponse.Length < responseNonceSize + ResponseAead.TagSize)
        {
            throw new EncapsulatedResponseException("The Oblivious HTTP response is shorter than its nonce and tag.");
        }

        byte[] secret = sender.Export("message/bhttp response"u8, responseNonceSize);
        byte[] salt = [.. sender.EncapsulatedKey, .. encapsulatedResponse[..responseNonceSize]];
        byte[] pseudorandomKey = HKDF.Extract(HashAlgorithmName.SHA256, secret, salt);
        byte[] key = new byte[keySize];
        byte[] nonce = new byte[ResponseAead.NonceSize];
        HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, key, "key"u8);
        HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, nonce, "nonce"u8);
        ReadOnlySpan<byte> ciphertext = encapsulatedResponse[responseNonceSize..];
        byte[] binaryResponse = new byte[ciphertext.Length - ResponseAead.TagSize];
        return ResponseAead.TryOpen(aead, key, nonce, ciphertext, binaryResponse)
            ? binaryResponse
            : throw new EncapsulatedResponseException("The Oblivious HTTP response does not authenticate.");
    }

    /// <summary>Zeroes the HPKE context.</summary>
    public void Dispose() => sender.Dispose();

    private static byte[] Header(KeyConfigurationChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        byte[] header = new byte[HeaderSize];
        header[0] = choice.Configuration.KeyId;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(1), (ushort)choice.Suite.Kem);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(3), (ushort)choice.Suite.Kdf);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(5), (ushort)choice.Suite.Aead);
        return header;
    }

    private static byte[] Info(byte[] header) => [.. "message/bhttp request"u8, 0, .. header];

    private static ObliviousRequest Seal(KeyConfigurationChoice choice, HpkeSender sender, byte[] header, ReadOnlySpan<byte> binaryRequest)
    {
        byte[] sealedRequest = sender.Seal([], binaryRequest);
        return new ObliviousRequest(sender, choice.Suite.Aead, [.. header, .. sender.EncapsulatedKey, .. sealedRequest]);
    }
}
