using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The bytes the exchange hash H is computed over. Every method starts them the same way -
/// <c>V_C</c>, <c>V_S</c>, <c>I_C</c>, <c>I_S</c> and <c>K_S</c> as <c>string</c>s (RFC 4253
/// section 8) - appends its own fields to <see cref="Fields" />, and ends them with the
/// shared secret K in the method's encoding: an <c>mpint</c> for the Diffie-Hellman methods
/// (<see cref="EncodeMpint" />), a <c>string</c> for the hybrid post-quantum ones
/// (<see cref="EncodeString" />).
/// </summary>
internal sealed class SshExchangeHashInput
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SshExchangeHashInput" /> class with the
    /// fields every method shares.
    /// </summary>
    /// <param name="handshake">The identification strings and <c>KEXINIT</c> payloads.</param>
    /// <param name="hostKey">The server's host key blob, <c>K_S</c>.</param>
    internal SshExchangeHashInput(SshNegotiatedHandshake handshake, ReadOnlySpan<byte> hostKey)
    {
        Fields.WriteString(Encoding.Latin1.GetBytes(handshake.ClientIdentification));
        Fields.WriteString(Encoding.Latin1.GetBytes(handshake.ServerIdentification));
        Fields.WriteString(handshake.ClientKexInitPayload);
        Fields.WriteString(handshake.ServerKexInitPayload);
        Fields.WriteString(hostKey);
    }

    /// <summary>
    /// Gets the writer the method appends its own fields to, after <c>K_S</c>.
    /// </summary>
    internal SshWireWriter Fields { get; } = new();

    /// <summary>
    /// Encodes K, unsigned big-endian, as an <c>mpint</c> (RFC 4253 section 8).
    /// </summary>
    /// <param name="sharedSecret">K.</param>
    /// <returns>The encoding H and the key derivation hash.</returns>
    internal static byte[] EncodeMpint(ReadOnlySpan<byte> sharedSecret)
    {
        SshWireWriter writer = new();
        writer.WriteMpint(sharedSecret);
        return writer.ToArray();
    }

    /// <summary>
    /// Encodes K as a <c>string</c>, as the hybrid post-quantum methods do
    /// (draft-ietf-sshm-mlkem-hybrid-kex, draft-josefsson-ntruprime-ssh).
    /// </summary>
    /// <param name="sharedSecret">K.</param>
    /// <returns>The encoding H and the key derivation hash.</returns>
    internal static byte[] EncodeString(ReadOnlySpan<byte> sharedSecret)
    {
        SshWireWriter writer = new();
        writer.WriteString(sharedSecret);
        return writer.ToArray();
    }

    /// <summary>
    /// Appends K and hashes everything.
    /// </summary>
    /// <param name="encodedSharedSecret">K, already in the method's encoding.</param>
    /// <param name="hashAlgorithm">The method's hash.</param>
    /// <returns>H.</returns>
    internal byte[] ComputeHash(ReadOnlySpan<byte> encodedSharedSecret, HashAlgorithmName hashAlgorithm)
    {
        Fields.WriteBytes(encodedSharedSecret);
        return CryptographicOperations.HashData(hashAlgorithm, Fields.ToArray());
    }
}
