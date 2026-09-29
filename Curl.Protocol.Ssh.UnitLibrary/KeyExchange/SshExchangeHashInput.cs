using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The bytes the exchange hash H is computed over. Every method starts them the same way -
/// <c>V_C</c>, <c>V_S</c>, <c>I_C</c>, <c>I_S</c> and <c>K_S</c> as <c>string</c>s (RFC 4253
/// section 8) - appends its own fields to <see cref="Fields" />, and ends them with the
/// shared secret K as an <c>mpint</c>.
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
    /// Appends K and hashes everything.
    /// </summary>
    /// <param name="sharedSecret">K, unsigned big-endian.</param>
    /// <param name="hashAlgorithm">The method's hash.</param>
    /// <returns>H.</returns>
    internal byte[] ComputeHash(ReadOnlySpan<byte> sharedSecret, HashAlgorithmName hashAlgorithm)
    {
        Fields.WriteMpint(sharedSecret);
        return CryptographicOperations.HashData(hashAlgorithm, Fields.ToArray());
    }
}
