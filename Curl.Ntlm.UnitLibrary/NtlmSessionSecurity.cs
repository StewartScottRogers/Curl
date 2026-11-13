using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.Ntlm;

/// <summary>
/// NTLM session security (MS-NLMP section 3.4) for one end of a connection, with extended
/// session security and 128-bit keys: it seals and signs the messages this end sends and
/// unseals and verifies the ones it receives, each direction with its own RC4 stream and
/// sequence number. A signature is 16 bytes: version 1, the checksum (the first 8 bytes of
/// <c>HMAC_MD5(SignKey, sequence number + message)</c>, RC4-encrypted under
/// <see cref="NtlmNegotiateFlags.NegotiateKeyExchange" />) and the sequence number.
/// </summary>
public sealed class NtlmSessionSecurity : IDisposable
{
    /// <summary>The length of a signature.</summary>
    public const int SignatureLength = 16;

    private const string ClientToServerSigning = "session key to client-to-server signing key magic constant\0";

    private const string ServerToClientSigning = "session key to server-to-client signing key magic constant\0";

    private const string ClientToServerSealing = "session key to client-to-server sealing key magic constant\0";

    private const string ServerToClientSealing = "session key to server-to-client sealing key magic constant\0";

    private readonly bool encryptChecksum;

    private readonly Rc4 sendHandle;

    private readonly Rc4 receiveHandle;

    private uint sendSequence;

    private uint receiveSequence;

    private NtlmSessionSecurity(NtlmNegotiateFlags flags, ReadOnlySpan<byte> exportedSessionKey, bool client)
    {
        Validate(flags, exportedSessionKey);
        encryptChecksum = flags.HasFlag(NtlmNegotiateFlags.NegotiateKeyExchange);
        SendSignKey = Derive(exportedSessionKey, client ? ClientToServerSigning : ServerToClientSigning);
        ReceiveSignKey = Derive(exportedSessionKey, client ? ServerToClientSigning : ClientToServerSigning);
        SendSealKey = Derive(exportedSessionKey, client ? ClientToServerSealing : ServerToClientSealing);
        ReceiveSealKey = Derive(exportedSessionKey, client ? ServerToClientSealing : ClientToServerSealing);
        sendHandle = new Rc4(SendSealKey);
        receiveHandle = new Rc4(ReceiveSealKey);
    }

    private static void Validate(NtlmNegotiateFlags flags, ReadOnlySpan<byte> exportedSessionKey)
    {
        if (exportedSessionKey.Length != NtlmResponseComputation.SessionKeyLength)
        {
            throw new ArgumentException($"The exported session key must be {NtlmResponseComputation.SessionKeyLength} bytes; this is {exportedSessionKey.Length}.", nameof(exportedSessionKey));
        }

        if (!flags.HasFlag(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity) || !flags.HasFlag(NtlmNegotiateFlags.Negotiate128))
        {
            throw new ArgumentException("Session security here needs extended session security and 128-bit keys.", nameof(flags));
        }
    }

    /// <summary>The key this end signs the messages it sends with.</summary>
    public byte[] SendSignKey { get; }

    /// <summary>The key this end verifies the messages it receives with.</summary>
    public byte[] ReceiveSignKey { get; }

    /// <summary>The key of the RC4 stream this end seals with.</summary>
    public byte[] SendSealKey { get; }

    /// <summary>The key of the RC4 stream this end unseals with.</summary>
    public byte[] ReceiveSealKey { get; }

    /// <summary>The session security of the client, which sends client-to-server.</summary>
    /// <exception cref="ArgumentException">
    /// The key is not 16 bytes, or <paramref name="flags" /> lacks extended session security or 128-bit keys.
    /// </exception>
    public static NtlmSessionSecurity CreateClient(NtlmNegotiateFlags flags, ReadOnlySpan<byte> exportedSessionKey) => new(flags, exportedSessionKey, true);

    /// <summary>The session security of the server, which sends server-to-client.</summary>
    /// <exception cref="ArgumentException">
    /// The key is not 16 bytes, or <paramref name="flags" /> lacks extended session security or 128-bit keys.
    /// </exception>
    public static NtlmSessionSecurity CreateServer(NtlmNegotiateFlags flags, ReadOnlySpan<byte> exportedSessionKey) => new(flags, exportedSessionKey, false);

    /// <summary>
    /// Encrypts <paramref name="message" /> and signs it (MS-NLMP 3.4.3 and 3.4.4.2), taking
    /// the next send sequence number.
    /// </summary>
    /// <param name="message">The plaintext.</param>
    /// <param name="signature">The 16-byte signature of the plaintext.</param>
    /// <returns>The ciphertext, as long as <paramref name="message" />.</returns>
    public byte[] Seal(ReadOnlySpan<byte> message, out byte[] signature)
    {
        byte[] checksum = Checksum(SendSignKey, sendSequence, message);
        byte[] sealedMessage = new byte[message.Length];
        sendHandle.ApplyKeyStream(message, sealedMessage);
        signature = BuildSignature(sendHandle, checksum, sendSequence);
        sendSequence++;
        return sealedMessage;
    }

    /// <summary>
    /// Decrypts <paramref name="sealedMessage" /> and verifies <paramref name="signature" />
    /// (MS-NLMP 3.4.3 and 3.4.4.2) against the next receive sequence number.
    /// </summary>
    /// <param name="sealedMessage">The ciphertext.</param>
    /// <param name="signature">The 16-byte signature received with it.</param>
    /// <param name="message">The plaintext when the signature verifies, otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the signature verifies; the sequence number then advances.</returns>
    public bool TryUnseal(ReadOnlySpan<byte> sealedMessage, ReadOnlySpan<byte> signature, out byte[]? message)
    {
        message = null;
        byte[] plain = new byte[sealedMessage.Length];
        receiveHandle.ApplyKeyStream(sealedMessage, plain);
        byte[] expected = BuildSignature(receiveHandle, Checksum(ReceiveSignKey, receiveSequence, plain), receiveSequence);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
        {
            return false;
        }

        receiveSequence++;
        message = plain;
        return true;
    }

    /// <summary>Zeroes the keys and the RC4 states.</summary>
    public void Dispose()
    {
        sendHandle.Dispose();
        receiveHandle.Dispose();
        CryptographicOperations.ZeroMemory(SendSignKey);
        CryptographicOperations.ZeroMemory(ReceiveSignKey);
        CryptographicOperations.ZeroMemory(SendSealKey);
        CryptographicOperations.ZeroMemory(ReceiveSealKey);
    }

    private static byte[] Derive(ReadOnlySpan<byte> key, string magic)
    {
        byte[] input = new byte[key.Length + magic.Length];
        key.CopyTo(input);
        Encoding.ASCII.GetBytes(magic, input.AsSpan(key.Length));
        return MD5.HashData(input);
    }

    private static byte[] Checksum(byte[] signKey, uint sequence, ReadOnlySpan<byte> message)
    {
        byte[] input = new byte[4 + message.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(input, sequence);
        message.CopyTo(input.AsSpan(4));
        return HMACMD5.HashData(signKey, input);
    }

    private byte[] BuildSignature(Rc4 handle, byte[] hmac, uint sequence)
    {
        byte[] signature = new byte[SignatureLength];
        BinaryPrimitives.WriteUInt32LittleEndian(signature, 1);
        if (encryptChecksum)
        {
            handle.ApplyKeyStream(hmac.AsSpan(0, 8), signature.AsSpan(4, 8));
        }
        else
        {
            hmac.AsSpan(0, 8).CopyTo(signature.AsSpan(4));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(signature.AsSpan(12), sequence);
        return signature;
    }
}
